using ElementalLudo.Tokens;

namespace ElementalLudo.Gameplay
{
    public enum LudoTurnPhase
    {
        AwaitingRoll,
        AwaitingAction,
        Resolving,
        GameOver
    }

    public enum LudoActionType
    {
        LeaveHome,
        Move
    }

    public enum LudoGameMode
    {
        /// <summary>Plain parchís against the AI: classic board, no extras.</summary>
        Classic,

        /// <summary>Current board and tokens; roguelike structure comes later.</summary>
        Adventure,

        /// <summary>
        /// Captured tokens never come back, and a single token reaching the
        /// goal is enough to win. Run out of tokens and you're out.
        /// </summary>
        Hardcore,

        /// <summary>Local hot-seat: every colour is played by a person.</summary>
        Multiplayer
    }

    public readonly struct LudoLegalAction
    {
        public Token Token { get; }
        public LudoActionType Type { get; }
        public int DestinationRouteIndex { get; }
        public int MoveDistance { get; }

        public LudoLegalAction(
            Token token,
            LudoActionType type,
            int destinationRouteIndex,
            int moveDistance)
        {
            Token = token;
            Type = type;
            DestinationRouteIndex = destinationRouteIndex;
            MoveDistance = moveDistance;
        }
    }
}
