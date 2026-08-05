using System.Collections.Generic;
using ElementalLudo.Tokens;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// What a stop on the run's map asks of the player.
    ///
    /// The split between <see cref="Duel"/> and <see cref="Match"/> is the
    /// point of having node kinds at all: a duel is the dice arena on its own,
    /// over in a minute, while a match is a whole game of Ludo. Alternating
    /// them is what keeps a run from being five long games in a row.
    /// </summary>
    public enum LudoRunNodeKind
    {
        /// <summary>No fight. Pick one upgrade from a few offered.</summary>
        Reward,

        /// <summary>A loose dice duel in the arena, with no board.</summary>
        Duel,

        /// <summary>
        /// A full game of Ludo against the AI. Only the last stage is one:
        /// every other fight is a duel, so the run stays quick and the board
        /// game is the thing it builds up to.
        /// </summary>
        Match,

        /// <summary>A longer duel against a tougher rival, for a better reward.</summary>
        Elite,

        /// <summary>No fight. Gives lives back, up to the cap.</summary>
        Heal,

        /// <summary>Closes the run: the full game of Ludo it has been leading to.</summary>
        Boss
    }

    /// <summary>
    /// One stop on the map.
    ///
    /// Links point at lane indices in the following stage rather than at node
    /// objects. That keeps the whole map a graph of plain numbers, which is
    /// what will let A7 save a run — the lesson BoardState taught, where keying
    /// off live Token references made it impossible to serialise.
    /// </summary>
    public sealed class LudoRunNode
    {
        public LudoRunNode(
            LudoRunNodeKind kind,
            int stage,
            int lane,
            IReadOnlyList<int> nextLanes,
            LudoElement rivalElement)
        {
            Kind = kind;
            Stage = stage;
            Lane = lane;
            NextLanes = nextLanes;
            RivalElement = rivalElement;
        }

        public LudoRunNodeKind Kind { get; }
        public int Stage { get; }
        public int Lane { get; }

        /// <summary>
        /// Who waits here. Decided when the map is built rather than when the
        /// fight starts, so the map can show it and the player can weigh the
        /// elemental matchup before choosing a path — which is the whole point
        /// of there being a choice.
        /// </summary>
        public LudoElement RivalElement { get; }

        /// <summary>Lanes in the next stage this one leads to.</summary>
        public IReadOnlyList<int> NextLanes { get; }

        /// <summary>Whether reaching it means playing something.</summary>
        public bool IsFight =>
            Kind != LudoRunNodeKind.Reward && Kind != LudoRunNodeKind.Heal;
    }
}
