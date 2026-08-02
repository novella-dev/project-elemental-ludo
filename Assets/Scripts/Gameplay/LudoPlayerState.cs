using System.Collections.Generic;
using ElementalLudo.Tokens;
using UnityEngine;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Runtime data for a single player: which tokens it owns and the route
    /// those tokens follow. Plain data, no MonoBehaviour dependency, so it
    /// can be built, copied or simulated outside of the live scene (AI
    /// look-ahead, save/load, tests, etc.).
    /// </summary>
    public sealed class LudoPlayerState
    {
        public PlayerStyle Style { get; }
        public List<Token> Tokens { get; }
        public Vector2Int[] Route { get; }
        public LudoElement Element => Style.Element;

        public LudoPlayerState(
            PlayerStyle style,
            List<Token> tokens,
            Vector2Int[] route)
        {
            Style = style;
            Tokens = tokens;
            Route = route;
        }
    }
}
