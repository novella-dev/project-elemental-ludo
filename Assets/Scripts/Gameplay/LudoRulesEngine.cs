using System.Collections.Generic;
using ElementalLudo.Tokens;
using UnityEngine;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Pure Parchís/Ludo rules. Every method here is a function of its
    /// inputs (including the BoardState it's handed): no scene state, no
    /// side effects, nothing mutated except the caller-supplied output
    /// collections. This is what lets the rules be unit-tested, run inside
    /// an AI's look-ahead against a cloned BoardState, or reused by a
    /// future combat/roguelike layer without dragging Unity along.
    /// </summary>
    public static class LudoRulesEngine
    {
        public static void CalculateLegalActions(
            BoardState boardState,
            LudoPlayerState activePlayer,
            IReadOnlyList<LudoPlayerState> allPlayers,
            int rolledValue,
            List<LudoLegalAction> results)
        {
            results.Clear();

            foreach (Token token in activePlayer.Tokens)
            {
                TokenState state = boardState.GetState(token);
                if (LudoMovementRules.CanLeaveHome(state, rolledValue))
                {
                    Vector2Int startCell = activePlayer.Route[0];
                    if (!IsBarrier(boardState, allPlayers, startCell) &&
                        !IsCellFull(boardState, allPlayers, startCell))
                    {
                        results.Add(new LudoLegalAction(
                            token,
                            LudoActionType.LeaveHome,
                            0));
                    }

                    continue;
                }

                int routeIndex = boardState.GetRouteIndex(token);
                if (LudoMovementRules.TryGetDestination(
                        state,
                        routeIndex,
                        rolledValue,
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
                            destination) &&
                        (destinationIsCenter || !IsCellFull(boardState, allPlayers, destinationCell)))
                    {
                        results.Add(new LudoLegalAction(
                            token,
                            LudoActionType.Move,
                            destination));
                    }
                }
            }
        }

        public static bool IsBarrier(
            BoardState boardState,
            IReadOnlyList<LudoPlayerState> allPlayers,
            Vector2Int cell)
        {
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
            int endIndex)
        {
            for (int i = startIndex; i <= endIndex; i++)
            {
                if (IsBarrier(boardState, allPlayers, route[i]))
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
        /// </summary>
        public static List<Token> GetCapturedTokens(
            BoardState boardState,
            LudoPlayerState movingPlayer,
            IReadOnlyList<LudoPlayerState> allPlayers,
            Token movingToken)
        {
            List<Token> captured = new List<Token>();
            Vector2Int movingCell = movingPlayer.Route[boardState.GetRouteIndex(movingToken)];

            if (LudoBoardRoutes.IsSafeCell(movingCell))
            {
                return captured;
            }

            foreach (LudoPlayerState player in allPlayers)
            {
                if (player == movingPlayer)
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
