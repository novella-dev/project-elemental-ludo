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
        ///
        /// Every magnitude below was measured at 200k simulated duels against
        /// an unupgraded rival, where an unarmed attacker wins 48.4%. The set
        /// lands between 57% and 68%, with the strongest effect given the
        /// fewest charges so power and availability trade off against each
        /// other rather than stacking.
        /// </summary>
        public static LudoUpgrade Default(LudoUpgradeKind kind)
        {
            return kind switch
            {
                // 67.9%, far and away the strongest, so it gets a single
                // charge. A sixth die is worth much more than a sixth of the
                // score: it lifts the pip floor and fills sets faster. It does
                // make a straight rarer, not commoner — six dice need all six
                // faces, where five have two ways to run — but the multiplier
                // it loses there is dwarfed by what the extra pips gain.
                LudoUpgradeKind.ExtraDie =>
                    new LudoUpgrade(kind, 1f, 1),

                // 58.4% at two extra. One alone was only 54.2%, barely worth
                // the slot, because the AI's own reroll policy already stops
                // early on a hand it cannot improve.
                LudoUpgradeKind.ExtraReroll =>
                    new LudoUpgrade(kind, 2f, 3),

                // 64.2% on top of the base +5, for +8 total. Held below the
                // others' headline because it is the only conditional one: it
                // does nothing at all unless the matchup already favours you,
                // which is about half of duels.
                LudoUpgradeKind.ElementalEdge =>
                    new LudoUpgrade(kind, 3f, 2),

                // 60.0%. Before the multiplier, so a good hand compounds it.
                LudoUpgradeKind.FlatPips =>
                    new LudoUpgrade(kind, 3f, 3),

                // 56.7% on Full. Full is the default target because rerolls
                // make it the most common hand by far at 39.9% — the same boost
                // on Trío managed 50.7%, near enough to nothing, because Trío
                // only lands 13.4% of the time. Runs wanting a riskier
                // specialisation build their own with a scarcer target hand.
                LudoUpgradeKind.HandMastery =>
                    new LudoUpgrade(kind, 1f, 2, LudoDiceHand.FullHouse),

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
