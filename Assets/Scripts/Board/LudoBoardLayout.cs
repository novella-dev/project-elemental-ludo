using UnityEngine;

namespace ElementalLudo.Board
{
    public static class LudoBoardLayout
    {
        public const float CenterHalfExtent = 1.5f;
        public const float CrossRouteCellLength = 2f;
        public const float RouteCellLength = 1f;

        public static Vector2 ToWorld(Vector2 logicalPosition)
        {
            return new Vector2(
                ToWorldCoordinate(logicalPosition.x),
                ToWorldCoordinate(logicalPosition.y));
        }

        public static Vector3 ToWorld(Vector2 logicalPosition, float depth)
        {
            Vector2 worldPosition = ToWorld(logicalPosition);
            return new Vector3(worldPosition.x, worldPosition.y, depth);
        }

        public static float ToWorldCoordinate(float logicalCoordinate)
        {
            float magnitude = Mathf.Abs(logicalCoordinate);
            if (magnitude <= CenterHalfExtent)
            {
                return logicalCoordinate * CrossRouteCellLength;
            }

            float stretchedMagnitude = CenterHalfExtent * CrossRouteCellLength +
                (magnitude - CenterHalfExtent) * RouteCellLength;
            return Mathf.Sign(logicalCoordinate) * stretchedMagnitude;
        }
    }
}
