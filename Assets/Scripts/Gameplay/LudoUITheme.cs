using ElementalLudo.Board;
using ElementalLudo.Tokens;
using UnityEngine;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Shared palette for the adventure-style interface. These colours mirror
    /// the supplied stage-map references: charcoal backdrops, steel panels,
    /// cyan actions and saturated elemental rewards.
    /// </summary>
    internal static class LudoUITheme
    {
        public static readonly Color Ink = new Color(0.015f, 0.02f, 0.03f, 1f);
        public static readonly Color Backdrop = new Color(0.105f, 0.135f, 0.17f, 0.97f);
        public static readonly Color Header = new Color(0.29f, 0.32f, 0.42f, 0.99f);
        public static readonly Color HeaderHighlight = new Color(0.43f, 0.47f, 0.60f, 1f);
        public static readonly Color Panel = new Color(0.13f, 0.17f, 0.22f, 0.97f);
        public static readonly Color PanelRaised = new Color(0.20f, 0.25f, 0.32f, 0.99f);
        public static readonly Color Card = new Color(0.17f, 0.22f, 0.29f, 0.99f);
        public static readonly Color CardHover = new Color(0.24f, 0.31f, 0.40f, 1f);
        public static readonly Color Disabled = new Color(0.23f, 0.25f, 0.29f, 0.96f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.58f);
        public static readonly Color TextPrimary = new Color(0.97f, 0.98f, 1f, 1f);
        public static readonly Color TextSecondary = new Color(0.82f, 0.87f, 0.93f, 0.92f);
        public static readonly Color TextMuted = new Color(0.63f, 0.69f, 0.76f, 0.88f);

        public static readonly Color Cyan = new Color(0.10f, 0.69f, 0.88f, 1f);
        public static readonly Color CyanHighlight = new Color(0.38f, 0.91f, 0.96f, 1f);
        public static readonly Color CyanDark = new Color(0.035f, 0.30f, 0.48f, 1f);

        public static readonly Color Fire = LudoBoardVisualStyle.Red;
        public static readonly Color Water = LudoBoardVisualStyle.Blue;
        public static readonly Color Plant = LudoBoardVisualStyle.Green;
        public static readonly Color Lightning = LudoBoardVisualStyle.Yellow;
        public static readonly Color Sand = LudoBoardVisualStyle.SandMid;
        public static readonly Color Reward = new Color(1f, 0.67f, 0.08f, 1f);
        public static readonly Color Danger = new Color(0.92f, 0.18f, 0.25f, 1f);

        public static Color SoftTint(Color accent, float amount = 0.28f)
        {
            Color tint = Color.Lerp(Card, accent, Mathf.Clamp01(amount));
            tint.a = 0.99f;
            return tint;
        }

        public static Color ElementColor(LudoElement element)
        {
            return element switch
            {
                LudoElement.Fire => Fire,
                LudoElement.Water => Water,
                LudoElement.Plant => Plant,
                LudoElement.Lightning => Lightning,
                _ => Sand
            };
        }

        public static Color ModeColor(LudoGameMode mode)
        {
            return mode switch
            {
                LudoGameMode.Classic => Sand,
                LudoGameMode.Adventure => Plant,
                LudoGameMode.Hardcore => Fire,
                LudoGameMode.Multiplayer => Water,
                _ => Lightning
            };
        }
    }
}
