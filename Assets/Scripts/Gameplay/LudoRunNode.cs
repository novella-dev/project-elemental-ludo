using System.Collections.Generic;

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

        /// <summary>A full game of Ludo against the AI.</summary>
        Match,

        /// <summary>A harder match, for a better reward.</summary>
        Elite,

        /// <summary>Closes the run.</summary>
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
            IReadOnlyList<int> nextLanes)
        {
            Kind = kind;
            Stage = stage;
            Lane = lane;
            NextLanes = nextLanes;
        }

        public LudoRunNodeKind Kind { get; }
        public int Stage { get; }
        public int Lane { get; }

        /// <summary>Lanes in the next stage this one leads to.</summary>
        public IReadOnlyList<int> NextLanes { get; }

        /// <summary>Whether reaching it means playing something.</summary>
        public bool IsFight => Kind != LudoRunNodeKind.Reward;
    }
}
