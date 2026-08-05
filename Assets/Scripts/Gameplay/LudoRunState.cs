using System.Collections.Generic;
using ElementalLudo.Tokens;

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

        public LudoRunState(LudoElement element, LudoRunMap map)
        {
            Element = element;
            Map = map;
            Upgrades = new LudoUpgradeInventory();
            Stage = 0;
            Lane = 0;
            Status = LudoRunStatus.AtNode;
            path.Add(map.Node(0, 0));
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
        /// Settles the current node. Losing anywhere ends the run; winning the
        /// last stage wins it, and winning anywhere else opens up the choice of
        /// where to go next.
        /// </summary>
        public void ResolveCurrentNode(bool won)
        {
            if (IsOver || Status != LudoRunStatus.AtNode)
            {
                return;
            }

            if (!won)
            {
                Status = LudoRunStatus.Lost;
                choices.Clear();
                return;
            }

            if (Stage >= Map.StageCount - 1)
            {
                Status = LudoRunStatus.Won;
                choices.Clear();
                return;
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
