using System.Collections.Generic;
using ElementalLudo.Tokens;

namespace ElementalLudo.Gameplay
{
    public static class LudoMovementRules
    {
        public const int HomeExitRoll = 5;
        public const int TokensRequiredToWin = 4;

        public static bool CanLeaveHome(TokenState state, int rolledValue)
        {
            return state == TokenState.Home && rolledValue == HomeExitRoll;
        }

        /// <summary>
        /// <paramref name="moveDistance"/> is normally a 1-6 dice roll, but
        /// callers may pass up to 7 to account for the Lightning element's
        /// +1 effective distance (Fase 1) — this function just validates a
        /// distance and applies it, it doesn't know why it's 7.
        /// </summary>
        public static bool TryGetDestination(
            TokenState state,
            int currentRouteIndex,
            int moveDistance,
            int routeLength,
            out int destinationRouteIndex)
        {
            destinationRouteIndex = -1;
            if (state != TokenState.Track ||
                currentRouteIndex < 0 ||
                moveDistance < 1 ||
                moveDistance > 7 ||
                routeLength <= 0)
            {
                return false;
            }

            int candidate = currentRouteIndex + moveDistance;
            if (candidate >= routeLength)
            {
                return false;
            }

            destinationRouteIndex = candidate;
            return true;
        }

        public static bool HasWon(BoardState boardState, IReadOnlyList<Token> tokens)
        {
            if (tokens == null || tokens.Count != TokensRequiredToWin)
            {
                return false;
            }

            foreach (Token token in tokens)
            {
                if (token == null || boardState.GetState(token) != TokenState.Finished)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
