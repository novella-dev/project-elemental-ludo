using UnityEngine;

namespace ElementalLudo.Board
{
    public static class LudoBoardLayout
    {
        public const float CenterHalfExtent = 1.5f;

        /// <summary>
        /// Half-width of a raised track tile, in logical units. Slightly less
        /// than the 0.5 half-cell, and that difference is exactly the grid gap
        /// left between neighbouring tiles.
        /// </summary>
        public const float TrackCellHalfExtent = 0.455f;

        public const float CrossRouteCellLength = 2f;
        public const float RouteCellLength = 1f;
        public const float HomeLogicalCenter = 5.5f;
        public const float HomeWorldCenter = 7f;
        public const float HomeSizeScale = 4f / 3f;

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

        /// <summary>
        /// Inverse of <see cref="ToWorldCoordinate"/>, for turning a point
        /// picked on the board back into board coordinates.
        /// </summary>
        public static float ToLogicalCoordinate(float worldCoordinate)
        {
            float magnitude = Mathf.Abs(worldCoordinate);
            float centerWorldExtent = CenterHalfExtent * CrossRouteCellLength;
            if (magnitude <= centerWorldExtent)
            {
                return worldCoordinate / CrossRouteCellLength;
            }

            float logicalMagnitude = CenterHalfExtent +
                (magnitude - centerWorldExtent) / RouteCellLength;
            return Mathf.Sign(worldCoordinate) * logicalMagnitude;
        }
    }
}
