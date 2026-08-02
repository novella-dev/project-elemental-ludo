using ElementalLudo.Tokens;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Everything BoardState tracks about a single token: is it home, on
    /// the track, or finished, and at which route index. Plain, immutable,
    /// trivially copyable — the payload BoardState clones for AI
    /// look-ahead or would serialize for save/load.
    /// </summary>
    public readonly struct LudoTokenState
    {
        public static readonly LudoTokenState Home =
            new LudoTokenState(TokenState.Home, -1);

        public TokenState State { get; }
        public int RouteIndex { get; }

        public LudoTokenState(TokenState state, int routeIndex)
        {
            State = state;
            RouteIndex = routeIndex;
        }
    }
}
