using System.Collections.Generic;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// One granted upgrade and what is left of it.
    ///
    /// Mutable, unlike the <see cref="LudoUpgrade"/> it holds: granting the
    /// same upgrade twice has to give two independent charge counters, which is
    /// exactly what keeping the spent state out here buys.
    /// </summary>
    public sealed class LudoUpgradeSlot
    {
        public LudoUpgradeSlot(LudoUpgrade upgrade)
        {
            Upgrade = upgrade;
            ChargesLeft = upgrade.Charges;
        }

        public LudoUpgrade Upgrade { get; }
        public int ChargesLeft { get; internal set; }
        public bool Armed { get; internal set; }

        public bool CanArm => !Armed && ChargesLeft > 0;
    }

    /// <summary>
    /// What the player is carrying and what they have switched on.
    ///
    /// Upgrades are never passively in effect: one is armed, and the next event
    /// of its scope spends a charge and switches it back off. That is the
    /// design constraint the whole system is built around, so there is
    /// deliberately no way to ask this for a permanently-applied total — only
    /// for what is armed right now.
    /// </summary>
    public sealed class LudoUpgradeInventory
    {
        private readonly List<LudoUpgradeSlot> slots = new List<LudoUpgradeSlot>();

        public IReadOnlyList<LudoUpgradeSlot> Slots => slots;

        public void Grant(LudoUpgrade upgrade)
        {
            slots.Add(new LudoUpgradeSlot(upgrade));
        }

        public void Grant(LudoUpgradeKind kind)
        {
            Grant(LudoUpgradeCatalog.Default(kind));
        }

        public void Clear()
        {
            slots.Clear();
        }

        public bool TryArm(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                return false;
            }

            LudoUpgradeSlot slot = slots[slotIndex];
            if (!slot.CanArm)
            {
                return false;
            }

            slot.Armed = true;
            return true;
        }

        public bool TryDisarm(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                return false;
            }

            LudoUpgradeSlot slot = slots[slotIndex];
            if (!slot.Armed)
            {
                return false;
            }

            slot.Armed = false;
            return true;
        }

        /// <summary>Whether an armed, still-charged upgrade of this kind is ready.</summary>
        public bool IsArmed(LudoUpgradeKind kind)
        {
            foreach (LudoUpgradeSlot slot in slots)
            {
                if (slot.Armed && slot.ChargesLeft > 0 && slot.Upgrade.Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Spends the first armed charge of this kind, returning whether there
        /// was one. Used by the turn-scoped upgrades, which fire at a single
        /// identifiable moment rather than colouring a whole duel.
        /// </summary>
        public bool TryConsume(LudoUpgradeKind kind)
        {
            foreach (LudoUpgradeSlot slot in slots)
            {
                if (!slot.Armed || slot.ChargesLeft <= 0 || slot.Upgrade.Kind != kind)
                {
                    continue;
                }

                slot.ChargesLeft--;
                slot.Armed = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Folds every armed duel upgrade into the numbers the resolver reads.
        ///
        /// This switch is the one place a new combat upgrade has to be taught
        /// about — everything downstream already works in terms of
        /// <see cref="LudoCombatModifiers"/>.
        /// </summary>
        public LudoCombatModifiers BuildCombatModifiers(int elementBonus)
        {
            int extraDice = 0;
            int extraRerolls = 0;
            int flatPips = 0;
            int elementalEdge = 0;
            float[] handBoosts = null;

            foreach (LudoUpgradeSlot slot in slots)
            {
                if (!slot.Armed || slot.ChargesLeft <= 0 ||
                    slot.Upgrade.Scope != LudoUpgradeScope.Duel)
                {
                    continue;
                }

                LudoUpgrade upgrade = slot.Upgrade;
                switch (upgrade.Kind)
                {
                    case LudoUpgradeKind.ExtraDie:
                        extraDice += upgrade.WholeMagnitude;
                        break;

                    case LudoUpgradeKind.ExtraReroll:
                        extraRerolls += upgrade.WholeMagnitude;
                        break;

                    case LudoUpgradeKind.FlatPips:
                        flatPips += upgrade.WholeMagnitude;
                        break;

                    case LudoUpgradeKind.ElementalEdge:
                        elementalEdge += upgrade.WholeMagnitude;
                        break;

                    case LudoUpgradeKind.HandMastery:
                        handBoosts ??= new float[System.Enum.GetValues(typeof(LudoDiceHand)).Length];
                        int index = (int)upgrade.TargetHand;
                        if (index >= 0 && index < handBoosts.Length)
                        {
                            handBoosts[index] += upgrade.Magnitude;
                        }

                        break;
                }
            }

            // The edge only sharpens an advantage that already exists — it
            // never invents one out of a neutral or losing matchup.
            int totalElementBonus = elementBonus > 0
                ? elementBonus + elementalEdge
                : 0;

            return new LudoCombatModifiers(
                extraDice,
                extraRerolls,
                flatPips,
                totalElementBonus,
                handBoosts);
        }

        /// <summary>
        /// Spends a charge on every armed duel upgrade and switches them off.
        /// Called once the duel they were armed for has actually begun, so an
        /// armed upgrade is never burned by a duel that never happened.
        /// </summary>
        public void ConsumeArmedDuelUpgrades()
        {
            foreach (LudoUpgradeSlot slot in slots)
            {
                if (!slot.Armed || slot.ChargesLeft <= 0 ||
                    slot.Upgrade.Scope != LudoUpgradeScope.Duel)
                {
                    continue;
                }

                slot.ChargesLeft--;
                slot.Armed = false;
            }
        }
    }
}
