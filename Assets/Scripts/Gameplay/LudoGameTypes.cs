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
        /// <summary>One human plays all four colors.</summary>
        HotSeat,

        /// <summary>Human picks an element; the other three seats are AI.</summary>
        SinglePlayer
    }

    public readonly struct LudoLegalAction
    {
        public Token Token { get; }
        public LudoActionType Type { get; }
        public int DestinationRouteIndex { get; }

        public LudoLegalAction(
            Token token,
            LudoActionType type,
            int destinationRouteIndex)
        {
            Token = token;
            Type = type;
            DestinationRouteIndex = destinationRouteIndex;
        }
    }
}
