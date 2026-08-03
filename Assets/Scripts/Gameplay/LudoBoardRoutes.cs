using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElementalLudo.Gameplay
{
    public static class LudoBoardRoutes
    {
        public const int SharedPathLength = 68;
        public const int PlayerMainPathLength = 64;
        public const int RouteLength = 70;
        public const int MaxTokensPerCell = 2;

        private static readonly HashSet<Vector2Int> SafeCells =
            new HashSet<Vector2Int>
            {
                new Vector2Int(0, 9),
                new Vector2Int(-1, 5),
                new Vector2Int(1, 5),
                new Vector2Int(-5, 1),
                new Vector2Int(5, 1),
                new Vector2Int(-9, 0),
                new Vector2Int(9, 0),
                new Vector2Int(-5, -1),
                new Vector2Int(5, -1),
                new Vector2Int(-1, -5),
                new Vector2Int(1, -5),
                new Vector2Int(0, -9)
            };

        private static readonly Vector2Int[] MainPath =
        {
            new Vector2Int(-1, 4),
            new Vector2Int(-1, 3),
            new Vector2Int(-1, 2),
            new Vector2Int(-2, 1),
            new Vector2Int(-3, 1),
            new Vector2Int(-4, 1),
            new Vector2Int(-5, 1),
            new Vector2Int(-6, 1),
            new Vector2Int(-7, 1),
            new Vector2Int(-8, 1),
            new Vector2Int(-9, 1),
            new Vector2Int(-9, 0),
            new Vector2Int(-9, -1),
            new Vector2Int(-8, -1),
            new Vector2Int(-7, -1),
            new Vector2Int(-6, -1),
            new Vector2Int(-5, -1),
            new Vector2Int(-4, -1),
            new Vector2Int(-3, -1),
            new Vector2Int(-2, -1),
            new Vector2Int(-1, -2),
            new Vector2Int(-1, -3),
            new Vector2Int(-1, -4),
            new Vector2Int(-1, -5),
            new Vector2Int(-1, -6),
            new Vector2Int(-1, -7),
            new Vector2Int(-1, -8),
            new Vector2Int(-1, -9),
            new Vector2Int(0, -9),
            new Vector2Int(1, -9),
            new Vector2Int(1, -8),
            new Vector2Int(1, -7),
            new Vector2Int(1, -6),
            new Vector2Int(1, -5),
            new Vector2Int(1, -4),
            new Vector2Int(1, -3),
            new Vector2Int(1, -2),
            new Vector2Int(2, -1),
            new Vector2Int(3, -1),
            new Vector2Int(4, -1),
            new Vector2Int(5, -1),
            new Vector2Int(6, -1),
            new Vector2Int(7, -1),
            new Vector2Int(8, -1),
            new Vector2Int(9, -1),
            new Vector2Int(9, 0),
            new Vector2Int(9, 1),
            new Vector2Int(8, 1),
            new Vector2Int(7, 1),
            new Vector2Int(6, 1),
            new Vector2Int(5, 1),
            new Vector2Int(4, 1),
            new Vector2Int(3, 1),
            new Vector2Int(2, 1),
            new Vector2Int(1, 2),
            new Vector2Int(1, 3),
            new Vector2Int(1, 4),
            new Vector2Int(1, 5),
            new Vector2Int(1, 6),
            new Vector2Int(1, 7),
            new Vector2Int(1, 8),
            new Vector2Int(1, 9),
            new Vector2Int(0, 9),
            new Vector2Int(-1, 9),
            new Vector2Int(-1, 8),
            new Vector2Int(-1, 7),
            new Vector2Int(-1, 6),
            new Vector2Int(-1, 5)
        };

        private static readonly Dictionary<string, Vector2Int[]> Routes =
            new Dictionary<string, Vector2Int[]>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "red",
                    BuildRoute(
                        67,
                        new Vector2Int(0, 8),
                        new Vector2Int(0, 7),
                        new Vector2Int(0, 6),
                        new Vector2Int(0, 5),
                        new Vector2Int(0, 4))
                },
                {
                    "blue",
                    BuildRoute(
                        50,
                        new Vector2Int(8, 0),
                        new Vector2Int(7, 0),
                        new Vector2Int(6, 0),
                        new Vector2Int(5, 0),
                        new Vector2Int(4, 0))
                },
                {
                    "yellow",
                    BuildRoute(
                        33,
                        new Vector2Int(0, -8),
                        new Vector2Int(0, -7),
                        new Vector2Int(0, -6),
                        new Vector2Int(0, -5),
                        new Vector2Int(0, -4))
                },
                {
                    "green",
                    BuildRoute(
                        16,
                        new Vector2Int(-8, 0),
                        new Vector2Int(-7, 0),
                        new Vector2Int(-6, 0),
                        new Vector2Int(-5, 0),
                        new Vector2Int(-4, 0))
                }
            };

        /// <summary>
        /// Shared-path index carrying printed number 1. The numbers painted on
        /// the board and anything that reports a square to the player have to
        /// read from here, or they'd quote different numbers for the same cell.
        /// </summary>
        public const int CellLabelStartIndex = 29;

        public static Vector2Int GetSharedPathCell(int index)
        {
            return MainPath[(index % SharedPathLength + SharedPathLength) % SharedPathLength];
        }

        /// <summary>
        /// The number printed on <paramref name="cell"/>. False for squares
        /// that carry no number: a player's own final lane and the goal.
        /// </summary>
        public static bool TryGetCellLabel(Vector2Int cell, out int label)
        {
            for (int index = 0; index < SharedPathLength; index++)
            {
                if (MainPath[index] != cell)
                {
                    continue;
                }

                // Inverse of the placement rule: number N sits at index
                // (CellLabelStartIndex + N - 1) % SharedPathLength.
                int offset = index - CellLabelStartIndex;
                label = ((offset % SharedPathLength) + SharedPathLength) % SharedPathLength + 1;
                return true;
            }

            label = 0;
            return false;
        }

        public static bool TryGetRoute(string playerId, out Vector2Int[] route)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                route = null;
                return false;
            }

            return Routes.TryGetValue(playerId, out route);
        }

        public static bool TryGetRouteStartCell(string playerId, out Vector2Int startCell)
        {
            startCell = default;
            if (!TryGetRoute(playerId, out Vector2Int[] route) || route.Length == 0)
            {
                return false;
            }

            startCell = route[0];
            return true;
        }

        public static bool IsSafeCell(Vector2Int cell)
        {
            return SafeCells.Contains(cell);
        }

        public static IEnumerable<Vector2Int> GetSafeCells()
        {
            return SafeCells;
        }

        private static Vector2Int[] BuildRoute(
            int startOffset,
            params Vector2Int[] finalLane)
        {
            Vector2Int[] route = new Vector2Int[RouteLength];
            for (int index = 0; index < PlayerMainPathLength; index++)
            {
                route[index] = MainPath[(startOffset + index) % SharedPathLength];
            }

            for (int index = 0; index < finalLane.Length; index++)
            {
                route[PlayerMainPathLength + index] = finalLane[index];
            }

            route[RouteLength - 1] = Vector2Int.zero;
            return route;
        }
    }
}
