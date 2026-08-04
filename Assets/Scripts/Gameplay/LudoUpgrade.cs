namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// One upgrade as the run granted it: what it does, how strongly, and how
    /// many times it can be used.
    ///
    /// Immutable. How much of it is left lives on the inventory slot holding
    /// it, not here, so the same definition can be granted twice without the
    /// two copies sharing a charge counter.
    /// </summary>
    public readonly struct LudoUpgrade
    {
        public LudoUpgradeKind Kind { get; }

        /// <summary>
        /// How much of the effect. Whole numbers for dice, rerolls, pips and
        /// the elemental edge; a fraction for a multiplier boost, where 0.4
        /// means a Trío goes from ×1,8 to ×2,2.
        /// </summary>
        public float Magnitude { get; }

        /// <summary>
        /// Which combination <see cref="LudoUpgradeKind.HandMastery"/> improves.
        /// Ignored by every other kind.
        /// </summary>
        public LudoDiceHand TargetHand { get; }

        /// <summary>How many times it can be armed before it is used up.</summary>
        public int Charges { get; }

        public LudoUpgrade(
            LudoUpgradeKind kind,
            float magnitude,
            int charges,
            LudoDiceHand targetHand = LudoDiceHand.Nothing)
        {
            Kind = kind;
            Magnitude = magnitude;
            Charges = charges;
            TargetHand = targetHand;
        }

        public LudoUpgradeScope Scope => LudoUpgradeCatalog.ScopeOf(Kind);

        /// <summary>Rounded magnitude, for the kinds that only make sense whole.</summary>
        public int WholeMagnitude => UnityEngine.Mathf.RoundToInt(Magnitude);
    }
}
