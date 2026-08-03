using UnityEngine;

namespace ElementalLudo.Board
{
    /// <summary>
    /// Shared cartoon palette for every procedural board layer. Keeping the
    /// surface, raised geometry and biome rims here prevents the three board
    /// views from drifting into slightly different versions of each colour.
    /// </summary>
    public static class LudoBoardVisualStyle
    {
        public static readonly Color Ink = new Color(0.01f, 0.015f, 0.02f, 1f);
        public static readonly Color Paper = new Color(1f, 0.975f, 0.90f, 1f);

        public static readonly Color Red = new Color(0.95f, 0.075f, 0.20f, 1f);
        public static readonly Color Blue = new Color(0.02f, 0.52f, 0.96f, 1f);
        public static readonly Color Green = new Color(0.08f, 0.72f, 0.12f, 1f);
        public static readonly Color Yellow = new Color(1f, 0.80f, 0.08f, 1f);

        public static readonly Color SafeCell = new Color(0.65f, 0.72f, 0.74f, 1f);
        public static readonly Color SafeCellOverlay =
            new Color(0.65f, 0.72f, 0.74f, 0.68f);
        public static readonly Color SandDark = new Color(0.76f, 0.55f, 0.28f, 1f);
        public static readonly Color SandMid = new Color(0.91f, 0.72f, 0.39f, 1f);
        public static readonly Color SandLight = new Color(1f, 0.86f, 0.52f, 1f);

        // Two neighbouring token outlines visually add up to roughly this
        // width, so cell borders feel related without overwhelming a tile.
        public const float GridLineWidth = 0.036f;

        public static Color Lighten(Color color, float amount = 0.22f)
        {
            return Color.Lerp(color, Color.white, amount);
        }

        public static Color Shade(Color color, float amount)
        {
            return Color.Lerp(color, Ink, amount);
        }
    }
}
