namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Every upgrade an Adventure run can hand out.
    ///
    /// Adding one should mean touching three places and no more: this enum, the
    /// entry in <see cref="LudoUpgradeCatalog"/> that gives it a scope, a
    /// default magnitude and a charge count, and its Spanish text in
    /// <see cref="LudoUpgradeInfo"/>. Anything that only moves one of the
    /// numbers combat already works with — dice, rerolls, pips, multipliers,
    /// the elemental edge — needs no new plumbing at all, because
    /// <see cref="LudoCombatModifiers"/> already carries those levers and the
    /// resolver already reads them.
    /// </summary>
    public enum LudoUpgradeKind
    {
        /// <summary>One more die in the duel, six instead of five.</summary>
        ExtraDie,

        /// <summary>One more reroll to spend during the duel.</summary>
        ExtraReroll,

        /// <summary>A bigger elemental advantage, when you already have one.</summary>
        ElementalEdge,

        /// <summary>Flat pips added to the throw before the multiplier.</summary>
        FlatPips,

        /// <summary>A better multiplier for one named combination.</summary>
        HandMastery,

        /// <summary>Ignore the obligation to break your own barrier on a 6.</summary>
        BarrierExemption,

        /// <summary>Throw the movement die again and keep the second result.</summary>
        MovementRethrow
    }

    /// <summary>
    /// When an upgrade can be armed and what spends its charge.
    ///
    /// Upgrades are never passively on — the player arms one and it is consumed
    /// by the next thing of its scope that happens. The distinction matters
    /// because a duel's dice count has to be settled before any dice exist,
    /// so a duel upgrade cannot be armed halfway through one.
    /// </summary>
    public enum LudoUpgradeScope
    {
        /// <summary>Armed ahead of time, spent by the next duel.</summary>
        Duel,

        /// <summary>Armed and spent during your own turn on the board.</summary>
        Turn
    }
}
