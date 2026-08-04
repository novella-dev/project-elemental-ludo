using System.Collections.Generic;
using ElementalLudo.Tokens;
using UnityEngine;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Pure Parchís/Ludo rules. Every method here is a function of its
    /// inputs (including the BoardState and LudoRulesContext it's handed):
    /// no scene state, no side effects, nothing mutated except the
    /// caller-supplied output collections. This is what lets the rules be
    /// unit-tested, run inside an AI's look-ahead against a cloned
    /// BoardState, or reused by a future combat/roguelike layer without
    /// dragging Unity along.
    /// </summary>
    public static class LudoRulesEngine
    {
        /// <summary>
        /// House rule: rolling a 6 while one of your own barriers (two
        /// same-colour tokens sharing a square) is on the board obliges you
        /// to break it — <paramref name="barrierBreakForced"/> comes back
        /// true whenever that narrowed the choice, so the caller can tell
        /// the player why their options just shrank. Only ever removes
        /// options, never adds any: if neither barrier token has a legal
        /// move with this roll, every other option stays open and the flag
        /// stays false.
        /// </summary>
        public static void CalculateLegalActions(
            BoardState boardState,
            LudoPlayerState activePlayer,
            IReadOnlyList<LudoPlayerState> allPlayers,
            int rolledValue,
            LudoRulesContext context,
            List<LudoLegalAction> results,
            out bool barrierBreakForced)
        {
            results.Clear();

            bool isLightning = context.ElementalModeEnabled &&
                                activePlayer.Element == LudoElement.Lightning;

            foreach (Token token in activePlayer.Tokens)
            {
                TokenState state = boardState.GetState(token);

                // Leaving home always needs an exact roll of 5 — Lightning's
                // +1 only applies to on-track moves, per design.
                if (LudoMovementRules.CanLeaveHome(state, rolledValue))
                {
                    Vector2Int startCell = activePlayer.Route[0];
                    if (!IsBarrier(boardState, allPlayers, startCell, context, activePlayer.Element) &&
                        !IsCellFull(boardState, allPlayers, startCell))
                    {
                        results.Add(new LudoLegalAction(
                            token,
                            LudoActionType.LeaveHome,
                            0,
                            0));
                    }

                    continue;
                }

                int routeIndex = boardState.GetRouteIndex(token);
                int moveDistance = isLightning ? rolledValue + 1 : rolledValue;
                if (LudoMovementRules.TryGetDestination(
                        state,
                        routeIndex,
                        moveDistance,
                        activePlayer.Route.Length,
                        out int destination))
                {
                    Vector2Int destinationCell = activePlayer.Route[destination];
                    bool destinationIsCenter =
                        destination == activePlayer.Route.Length - 1;
                    if (!IsPathBlocked(
                            boardState,
                            allPlayers,
                            activePlayer.Route,
                            routeIndex + 1,
                            destination,
                            context,
                            activePlayer.Element) &&
                        (destinationIsCenter || !IsCellFull(boardState, allPlayers, destinationCell)))
                    {
                        results.Add(new LudoLegalAction(
                            token,
                            LudoActionType.Move,
                            destination,
                            moveDistance));
                    }
                }
            }

            barrierBreakForced = rolledValue == 6 &&
                !context.BarrierBreakExempt &&
                RestrictToBarrierBreaksIfAny(boardState, activePlayer, results);
        }

        /// <summary>
        /// Narrows <paramref name="results"/> to moves of the active
        /// player's own barrier tokens, if at least one such move is
        /// present. Returns whether it did.
        /// </summary>
        private static bool RestrictToBarrierBreaksIfAny(
            BoardState boardState,
            LudoPlayerState activePlayer,
            List<LudoLegalAction> results)
        {
            bool anyBarrierBreak = false;
            foreach (LudoLegalAction action in results)
            {
                if (action.Type == LudoActionType.Move &&
                    IsOwnBarrierToken(boardState, activePlayer, action.Token))
                {
                    anyBarrierBreak = true;
                    break;
                }
            }

            if (!anyBarrierBreak)
            {
                return false;
            }

            for (int index = results.Count - 1; index >= 0; index--)
            {
                if (results[index].Type != LudoActionType.Move ||
                    !IsOwnBarrierToken(boardState, activePlayer, results[index].Token))
                {
                    results.RemoveAt(index);
                }
            }

            return true;
        }

        /// <summary>Whether <paramref name="token"/> shares its square with another of the same colour.</summary>
        private static bool IsOwnBarrierToken(
            BoardState boardState,
            LudoPlayerState player,
            Token token)
        {
            if (boardState.GetState(token) != TokenState.Track)
            {
                return false;
            }

            Vector2Int cell = player.Route[boardState.GetRouteIndex(token)];
            return CountSameColorTokensOnCell(boardState, player, cell) >= 2;
        }

        /// <summary>
        /// Calculates the legal choices for an indivisible capture or goal
        /// bonus. Only tokens already on the track are candidates: a bonus
        /// can never take a token out of Home, and Finished tokens cannot
        /// move again. Elemental distance and barrier-bypass abilities do
        /// not alter a counted 10/20-space reward.
        /// </summary>
        public static void CalculateBonusActions(
            BoardState boardState,
            LudoPlayerState activePlayer,
            IReadOnlyList<LudoPlayerState> allPlayers,
            int moveDistance,
            LudoRulesContext context,
            List<LudoLegalAction> results)
        {
            results.Clear();

            // A counted bonus must respect every barrier, including when the
            // moving element is Water. Keep permadeath only for consistency;
            // barrier validation itself does not use that flag.
            LudoRulesContext barrierContext =
                new LudoRulesContext(false, context.PermadeathEnabled);

            foreach (Token token in activePlayer.Tokens)
            {
                TokenState state = boardState.GetState(token);
                int routeIndex = boardState.GetRouteIndex(token);
                if (!LudoMovementRules.TryGetDestination(
                        state,
                        routeIndex,
                        moveDistance,
                        activePlayer.Route.Length,
                        out int destination))
                {
                    continue;
                }

                Vector2Int destinationCell = activePlayer.Route[destination];
                bool destinationIsCenter =
                    destination == activePlayer.Route.Length - 1;
                if (!IsPathBlocked(
                        boardState,
                        allPlayers,
                        activePlayer.Route,
                        routeIndex + 1,
                        destination,
                        barrierContext,
                        activePlayer.Element) &&
                    (destinationIsCenter ||
                     !IsCellFull(boardState, allPlayers, destinationCell)))
                {
                    results.Add(new LudoLegalAction(
                        token,
                        LudoActionType.Move,
                        destination,
                        moveDistance));
                }
            }
        }

        /// <summary>
        /// Whether <paramref name="cell"/> is a barrier (2+ same-color
        /// tokens) for <paramref name="movingElement"/>'s own movement.
        /// Water ignores every barrier, of any color, on its own moves —
        /// but a water-formed barrier still blocks everyone else normally,
        /// since this only short-circuits when the *mover* is Water.
        /// </summary>
        public static bool IsBarrier(
            BoardState boardState,
            IReadOnlyList<LudoPlayerState> allPlayers,
            Vector2Int cell,
            LudoRulesContext context,
            LudoElement movingElement)
        {
            if (context.ElementalModeEnabled && movingElement == LudoElement.Water)
            {
                return false;
            }

            foreach (LudoPlayerState player in allPlayers)
            {
                if (CountSameColorTokensOnCell(boardState, player, cell) >= 2)
                {
                    return true;
                }
            }

            return false;
        }

        public static int CountSameColorTokensOnCell(
            BoardState boardState,
            LudoPlayerState player,
            Vector2Int cell)
        {
            int count = 0;
            foreach (Token token in player.Tokens)
            {
                if (boardState.GetState(token) == TokenState.Track &&
                    player.Route[boardState.GetRouteIndex(token)] == cell)
                {
                    count++;
                }
            }

            return count;
        }

        public static int CountTokensOnCell(
            BoardState boardState,
            IReadOnlyList<LudoPlayerState> allPlayers,
            Vector2Int cell)
        {
            int count = 0;
            foreach (LudoPlayerState player in allPlayers)
            {
                foreach (Token token in player.Tokens)
                {
                    if (boardState.GetState(token) != TokenState.Track)
                    {
                        continue;
                    }

                    if (player.Route[boardState.GetRouteIndex(token)] == cell)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public static bool IsCellFull(
            BoardState boardState,
            IReadOnlyList<LudoPlayerState> allPlayers,
            Vector2Int cell)
        {
            return CountTokensOnCell(boardState, allPlayers, cell) >= LudoBoardRoutes.MaxTokensPerCell;
        }

        public static bool IsPathBlocked(
            BoardState boardState,
            IReadOnlyList<LudoPlayerState> allPlayers,
            Vector2Int[] route,
            int startIndex,
            int endIndex,
            LudoRulesContext context,
            LudoElement movingElement)
        {
            for (int i = startIndex; i <= endIndex; i++)
            {
                if (IsBarrier(boardState, allPlayers, route[i], context, movingElement))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the tokens that would be captured if <paramref name="movingToken"/>
        /// lands on its current cell. Pure query: it does NOT send anything
        /// home. That side effect stays with the caller, which is exactly
        /// the seam a future combat system hooks into ("before applying
        /// this capture, resolve a fight instead").
        ///
        /// Elemental Fire ignores safe cells when capturing. Elemental
        /// Water never captures Plant tokens, regardless of cell.
        /// </summary>
        public static List<Token> GetCapturedTokens(
            BoardState boardState,
            LudoPlayerState movingPlayer,
            IReadOnlyList<LudoPlayerState> allPlayers,
            Token movingToken,
            LudoRulesContext context)
        {
            List<Token> captured = new List<Token>();
            Vector2Int movingCell = movingPlayer.Route[boardState.GetRouteIndex(movingToken)];

            bool ignoresSafeCells = context.ElementalModeEnabled &&
                                     movingPlayer.Element == LudoElement.Fire;
            if (!ignoresSafeCells && LudoBoardRoutes.IsSafeCell(movingCell))
            {
                return captured;
            }

            bool sparesPlant = context.ElementalModeEnabled &&
                                movingPlayer.Element == LudoElement.Water;

            foreach (LudoPlayerState player in allPlayers)
            {
                if (player == movingPlayer)
                {
                    continue;
                }

                if (sparesPlant && player.Element == LudoElement.Plant)
                {
                    continue;
                }

                foreach (Token token in player.Tokens)
                {
                    if (boardState.GetState(token) != TokenState.Track)
                    {
                        continue;
                    }

                    if (player.Route[boardState.GetRouteIndex(token)] == movingCell)
                    {
                        captured.Add(token);
                    }
                }
            }

            return captured;
        }
    }
}
