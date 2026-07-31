using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElementalLudo.Gameplay
{
    public static class LudoBoardRoutes
    {
        public const int SharedPathLength = 52;
        public const int PlayerMainPathLength = 49;
        public const int RouteLength = 55;

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
            new Vector2Int(-7, 0),
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
            new Vector2Int(0, -7),
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
            new Vector2Int(7, 0),
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
            new Vector2Int(0, 7),
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
                        0,
                        new Vector2Int(0, 6),
                        new Vector2Int(0, 5),
                        new Vector2Int(0, 4),
                        new Vector2Int(0, 3),
                        new Vector2Int(0, 2))
                },
                {
                    "blue",
                    BuildRoute(
                        39,
                        new Vector2Int(6, 0),
                        new Vector2Int(5, 0),
                        new Vector2Int(4, 0),
                        new Vector2Int(3, 0),
                        new Vector2Int(2, 0))
                },
                {
                    "yellow",
                    BuildRoute(
                        26,
                        new Vector2Int(0, -6),
                        new Vector2Int(0, -5),
                        new Vector2Int(0, -4),
                        new Vector2Int(0, -3),
                        new Vector2Int(0, -2))
                },
                {
                    "green",
                    BuildRoute(
                        13,
                        new Vector2Int(-6, 0),
                        new Vector2Int(-5, 0),
                        new Vector2Int(-4, 0),
                        new Vector2Int(-3, 0),
                        new Vector2Int(-2, 0))
                }
            };

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
