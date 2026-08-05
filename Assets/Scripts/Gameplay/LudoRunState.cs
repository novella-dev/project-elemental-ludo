using System.Collections.Generic;
using ElementalLudo.Tokens;
using UnityEngine;

namespace ElementalLudo.Gameplay
{
    public enum LudoRunStatus
    {
        /// <summary>Standing on a node that has not been settled yet.</summary>
        AtNode,

        /// <summary>Node settled; the player is choosing where to go next.</summary>
        Choosing,

        /// <summary>The boss went down. Run complete.</summary>
        Won,

        /// <summary>A fight was lost. Nothing carries over.</summary>
        Lost
    }

    /// <summary>
    /// A run in progress: the map, where the player stands on it, the element
    /// they took in, and what they are carrying.
    ///
    /// Owns the inventory, because upgrades are a property of the run rather
    /// than of any one match — that is the whole point of a run. Plain C# with
    /// no MonoBehaviour and no Unity references, so it survives a match ending
    /// and can be handed to A7 to save.
    ///
    /// Losing ends it outright, as chosen: there are no lives and nothing is
    /// kept, which is what makes each choice on the map cost something.
    /// </summary>
    public sealed class LudoRunState
    {
        private readonly List<LudoRunNode> choices = new List<LudoRunNode>(3);
        private readonly List<LudoRunNode> path = new List<LudoRunNode>(8);

        /// <summary>
        /// Lives, which are also the tokens the final game is played with. Four
        /// is the full board; every duel lost costs one, so arriving at the
        /// boss intact is worth as much as any upgrade.
        /// </summary>
        public const int MaxLives = 4;

        /// <summary>How many lives a heal node gives back.</summary>
        public const int HealAmount = 2;

        /// <summary>
        /// Chances to throw a reward offer back and draw three others. One per
        /// run: enough to rescue a build from an offer that fits nothing, not
        /// enough to shop until the right thing turns up.
        /// </summary>
        public const int RewardRefreshes = 1;

        public LudoRunState(LudoElement element, LudoRunMap map)
        {
            Element = element;
            Map = map;
            Upgrades = new LudoUpgradeInventory();
            Lives = MaxLives;
            RefreshesLeft = RewardRefreshes;
            Stage = 0;
            Lane = 0;
            Status = LudoRunStatus.AtNode;
            path.Add(map.Node(0, 0));
        }

        /// <summary>
        /// Lives left, and so the number of tokens the final game starts with.
        /// At zero the run is over.
        /// </summary>
        public int Lives { get; private set; }

        /// <summary>Reward refreshes still available this run.</summary>
        public int RefreshesLeft { get; private set; }

        public bool TrySpendRefresh()
        {
            if (RefreshesLeft <= 0)
            {
                return false;
            }

            RefreshesLeft--;
            return true;
        }

        /// <summary>Tops up to the cap, returning how many were actually given back.</summary>
        public int Heal()
        {
            int before = Lives;
            Lives = Mathf.Min(MaxLives, Lives + HealAmount);
            return Lives - before;
        }

        /// <summary>
        /// Every node walked, in order, starting with the one the run opened
        /// on. Kept so the map can draw the road behind the player rather than
        /// only the choice in front of them.
        /// </summary>
        public IReadOnlyList<LudoRunNode> Path => path;

        /// <summary>Whether the step between two nodes has been walked.</summary>
        public bool HasWalked(LudoRunNode from, LudoRunNode to)
        {
            for (int index = 1; index < path.Count; index++)
            {
                if (path[index - 1] == from && path[index] == to)
                {
                    return true;
                }
            }

            return false;
        }

        public LudoElement Element { get; }
        public LudoRunMap Map { get; }
        public LudoUpgradeInventory Upgrades { get; }
        public int Stage { get; private set; }
        public int Lane { get; private set; }
        public LudoRunStatus Status { get; private set; }

        public LudoRunNode CurrentNode => Map.Node(Stage, Lane);

        public bool IsOver =>
            Status == LudoRunStatus.Won || Status == LudoRunStatus.Lost;

        /// <summary>How far along the map the player is, for a progress bar.</summary>
        public float Progress => Map.StageCount <= 1
            ? 1f
            : (float)Stage / (Map.StageCount - 1);

        /// <summary>
        /// The nodes reachable from where the player stands. Empty until the
        /// current node has been settled, so the map cannot be walked past an
        /// unfought battle.
        /// </summary>
        public IReadOnlyList<LudoRunNode> Choices => choices;

        /// <summary>
        /// Whether a node is one the player may walk to now. Offered as a
        /// method because the UI asks it once per node every frame, and going
        /// through the list interface would mean LINQ and an allocation each
        /// time.
        /// </summary>
        public bool IsChoice(LudoRunNode node) => choices.Contains(node);

        /// <summary>Whether the player is standing on this node.</summary>
        public bool IsCurrent(LudoRunNode node) =>
            node != null && node.Stage == Stage && node.Lane == Lane;

        /// <summary>
        /// Settles the current node.
        ///
        /// Losing a duel costs a life rather than the run: the player carries
        /// on wounded, and only running out of lives ends it. The final game is
        /// different — it is the last stage, so losing it ends the run whatever
        /// lives are left, and winning it wins the run.
        /// </summary>
        public void ResolveCurrentNode(bool won)
        {
            if (IsOver || Status != LudoRunStatus.AtNode)
            {
                return;
            }

            bool isFinalStage = Stage >= Map.StageCount - 1;

            if (won && isFinalStage)
            {
                Status = LudoRunStatus.Won;
                choices.Clear();
                return;
            }

            if (!won)
            {
                Lives--;
                if (Lives <= 0 || isFinalStage)
                {
                    Lives = Mathf.Max(0, Lives);
                    Status = LudoRunStatus.Lost;
                    choices.Clear();
                    return;
                }
            }

            Status = LudoRunStatus.Choosing;
            RebuildChoices();
        }

        /// <summary>
        /// Walks to one of the offered nodes. Refuses anything not in
        /// <see cref="Choices"/>, so a stray click cannot skip a stage or jump
        /// across the map.
        /// </summary>
        public bool TryMoveTo(LudoRunNode node)
        {
            if (Status != LudoRunStatus.Choosing || node == null)
            {
                return false;
            }

            if (!choices.Contains(node))
            {
                return false;
            }

            Stage = node.Stage;
            Lane = node.Lane;
            Status = LudoRunStatus.AtNode;
            path.Add(node);
            choices.Clear();
            return true;
        }

        private void RebuildChoices()
        {
            choices.Clear();
            LudoRunNode node = CurrentNode;
            if (node.Stage >= Map.StageCount - 1)
            {
                return;
            }

            foreach (int lane in node.NextLanes)
            {
                choices.Add(Map.Node(node.Stage + 1, lane));
            }
        }
    }
}
