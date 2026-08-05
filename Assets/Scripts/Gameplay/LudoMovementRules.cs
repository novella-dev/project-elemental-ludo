using System.Collections.Generic;
using ElementalLudo.Tokens;

namespace ElementalLudo.Gameplay
{
    public static class LudoMovementRules
    {
        public const int HomeExitRoll = 5;
        public const int TokensRequiredToWin = 4;
        public const int CaptureBonusDistance = 20;
        public const int GoalBonusDistance = 10;

        public static bool CanLeaveHome(TokenState state, int rolledValue)
        {
            return state == TokenState.Home && rolledValue == HomeExitRoll;
        }

        /// <summary>
        /// Validates one indivisible forward movement. The distance can come
        /// from the die, an elemental modifier, or a capture/goal bonus; this
        /// function deliberately does not care where it came from.
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

        /// <summary>
        /// Normally every token has to be home. Under Hardcore a single one
        /// is enough — losing the rest is the price of the rule set.
        /// </summary>
        public static bool HasWon(
            BoardState boardState,
            IReadOnlyList<Token> tokens,
            LudoRulesContext context)
        {
            if (tokens == null || tokens.Count != TokensRequiredToWin)
            {
                return false;
            }

            int finished = 0;
            int playable = 0;
            foreach (Token token in tokens)
            {
                if (token == null)
                {
                    return false;
                }

                TokenState state = boardState.GetState(token);
                if (state == TokenState.Eliminated)
                {
                    continue;
                }

                playable++;
                if (state == TokenState.Finished)
                {
                    finished++;
                }
            }

            // Counted against the tokens still in play rather than a flat four,
            // so a seat that starts short — as the final game of an Adventure
            // run does, one token per life lost — can still win by bringing
            // everything it has home. With nothing eliminated this is exactly
            // the old rule, so no other mode changes.
            return context.PermadeathEnabled
                ? finished >= 1
                : playable > 0 && finished == playable;
        }

        /// <summary>
        /// A player with nothing left on or off the board. Only reachable
        /// under Hardcore, where captures are permanent.
        /// </summary>
        public static bool IsEliminated(BoardState boardState, IReadOnlyList<Token> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return false;
            }

            foreach (Token token in tokens)
            {
                if (token == null || boardState.GetState(token) != TokenState.Eliminated)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
