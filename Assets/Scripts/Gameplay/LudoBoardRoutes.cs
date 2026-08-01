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

        public static Vector2Int GetSharedPathCell(int index)
        {
            return MainPath[(index % SharedPathLength + SharedPathLength) % SharedPathLength];
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
