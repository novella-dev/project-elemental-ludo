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

        public static bool TryGetDestination(
            TokenState state,
            int currentRouteIndex,
            int rolledValue,
            int routeLength,
            out int destinationRouteIndex)
        {
            destinationRouteIndex = -1;
            if (state != TokenState.Track ||
                currentRouteIndex < 0 ||
                rolledValue < 1 ||
                rolledValue > 6 ||
                routeLength <= 0)
            {
                return false;
            }

            int candidate = currentRouteIndex + rolledValue;
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
