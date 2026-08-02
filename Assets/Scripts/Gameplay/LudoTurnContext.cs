using System.Collections.Generic;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Everything a player controller needs to decide what to do, handed to
    /// it when its turn reaches a decision point.
    ///
    /// <see cref="Board"/> is the live board, not a copy: a controller that
    /// wants to try hypothetical moves must <see cref="BoardState.Clone"/>
    /// it first and reason about the clone. Nothing here should be mutated.
    /// </summary>
    public readonly struct LudoTurnContext
    {
        public BoardState Board { get; }
        public LudoPlayerState Player { get; }
        public IReadOnlyList<LudoPlayerState> AllPlayers { get; }
        public int RolledValue { get; }
        public IReadOnlyList<LudoLegalAction> LegalActions { get; }
        public LudoRulesContext Rules { get; }

        public LudoTurnContext(
            BoardState board,
            LudoPlayerState player,
            IReadOnlyList<LudoPlayerState> allPlayers,
            int rolledValue,
            IReadOnlyList<LudoLegalAction> legalActions,
            LudoRulesContext rules)
        {
            Board = board;
            Player = player;
            AllPlayers = allPlayers;
            RolledValue = rolledValue;
            LegalActions = legalActions;
            Rules = rules;
        }
    }
}
