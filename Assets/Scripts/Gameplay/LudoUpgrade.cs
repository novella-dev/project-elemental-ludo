namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// One upgrade the run granted, at the level it has reached.
    ///
    /// Strength is not stored — it is worked out from the kind and the level,
    /// so a level 2 is exactly a level 1 twice over and there is no way to end
    /// up with two copies of the same upgrade disagreeing about what it does.
    /// Picking the same reward twice raises the level rather than adding a
    /// second entry, which is why this had to stop being a free-form number.
    /// </summary>
    public readonly struct LudoUpgrade
    {
        public LudoUpgrade(
            LudoUpgradeKind kind,
            int level = 1,
            LudoDiceHand targetHand = LudoDiceHand.Nothing)
        {
            Kind = kind;
            Level = level < 1 ? 1 : level;
            TargetHand = targetHand;
        }

        public LudoUpgradeKind Kind { get; }

        /// <summary>How many times it has been taken. Drives everything else.</summary>
        public int Level { get; }

        /// <summary>
        /// Which combination <see cref="LudoUpgradeKind.HandMastery"/> improves.
        /// Also part of its identity: mastery of a full house and mastery of a
        /// straight are different upgrades and level up separately.
        /// </summary>
        public LudoDiceHand TargetHand { get; }

        public LudoUpgradeScope Scope => LudoUpgradeCatalog.ScopeOf(Kind);

        public float Magnitude =>
            (Kind == LudoUpgradeKind.HandMastery
                ? LudoUpgradeCatalog.MasteryPerLevel(TargetHand)
                : LudoUpgradeCatalog.MagnitudePerLevel(Kind)) * Level;

        public int Charges => LudoUpgradeCatalog.ChargesFor(Kind, Level);

        /// <summary>Rounded magnitude, for the kinds that only make sense whole.</summary>
        public int WholeMagnitude => UnityEngine.Mathf.RoundToInt(Magnitude);

        /// <summary>
        /// Whether two upgrades are the same thing and should merge rather than
        /// sit side by side.
        /// </summary>
        public bool SameAs(LudoUpgrade other)
        {
            return Kind == other.Kind &&
                   (Kind != LudoUpgradeKind.HandMastery ||
                    TargetHand == other.TargetHand);
        }

        public LudoUpgrade AtLevel(int level)
        {
            return new LudoUpgrade(Kind, level, TargetHand);
        }
    }
}
