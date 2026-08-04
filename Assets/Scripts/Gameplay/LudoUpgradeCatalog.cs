using System.Collections.Generic;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// The upgrades that exist, with their default strength and charge count.
    ///
    /// Scope is worked out from the kind rather than stored on the upgrade, so
    /// a granted upgrade cannot end up claiming a scope its effect does not
    /// actually have — there is one answer per kind and this is where it lives.
    ///
    /// Balance numbers sit here rather than in the effects themselves so they
    /// can all be read, and retuned, in one place. Nothing reads this at
    /// runtime except whatever hands upgrades out; the fold in
    /// <see cref="LudoUpgradeInventory"/> works off the granted upgrade.
    /// </summary>
    public static class LudoUpgradeCatalog
    {
        public static LudoUpgradeScope ScopeOf(LudoUpgradeKind kind)
        {
            return kind switch
            {
                LudoUpgradeKind.BarrierExemption => LudoUpgradeScope.Turn,
                LudoUpgradeKind.MovementRethrow => LudoUpgradeScope.Turn,
                _ => LudoUpgradeScope.Duel
            };
        }

        /// <summary>
        /// The standard version of each upgrade. A run that wants a stronger or
        /// weaker copy builds its own <see cref="LudoUpgrade"/> instead — these
        /// are the defaults, not the only allowed values.
        /// </summary>
        public static LudoUpgrade Default(LudoUpgradeKind kind)
        {
            return kind switch
            {
                // A sixth die is worth far more than a sixth of the score: it
                // raises the pip floor and makes every set easier to fill.
                LudoUpgradeKind.ExtraDie =>
                    new LudoUpgrade(kind, 1f, 2),

                LudoUpgradeKind.ExtraReroll =>
                    new LudoUpgrade(kind, 1f, 3),

                // On top of the base +5, so an armed edge is worth +10 — only
                // when the elemental matchup already favours you.
                LudoUpgradeKind.ElementalEdge =>
                    new LudoUpgrade(kind, 5f, 2),

                // Before the multiplier, so a good hand compounds it.
                LudoUpgradeKind.FlatPips =>
                    new LudoUpgrade(kind, 3f, 3),

                // Trío is the default target: common enough to be worth
                // carrying, not so common it always fires.
                LudoUpgradeKind.HandMastery =>
                    new LudoUpgrade(kind, 0.4f, 2, LudoDiceHand.ThreeOfAKind),

                LudoUpgradeKind.BarrierExemption =>
                    new LudoUpgrade(kind, 1f, 2),

                LudoUpgradeKind.MovementRethrow =>
                    new LudoUpgrade(kind, 1f, 2),

                _ => new LudoUpgrade(kind, 1f, 1)
            };
        }

        /// <summary>Every kind, in the order they should be offered or listed.</summary>
        public static IReadOnlyList<LudoUpgradeKind> AllKinds => Kinds;

        private static readonly LudoUpgradeKind[] Kinds =
        {
            LudoUpgradeKind.ExtraDie,
            LudoUpgradeKind.ExtraReroll,
            LudoUpgradeKind.ElementalEdge,
            LudoUpgradeKind.FlatPips,
            LudoUpgradeKind.HandMastery,
            LudoUpgradeKind.BarrierExemption,
            LudoUpgradeKind.MovementRethrow
        };
    }
}
