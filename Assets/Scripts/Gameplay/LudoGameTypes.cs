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
