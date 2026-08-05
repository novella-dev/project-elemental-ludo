using System.Collections.Generic;
using UnityEngine;

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

            // Armed on arrival. Something just won and chose to carry it, so
            // the useful default is ready — leaving it off meant a player
            // could collect rewards all run and never notice they had to be
            // switched on.
            Armed = true;
        }

        /// <summary>Rebuilds a slot exactly as a save left it, bypassing the fresh-grant defaults above.</summary>
        internal LudoUpgradeSlot(LudoUpgrade upgrade, int chargesLeft, bool armed)
        {
            Upgrade = upgrade;
            ChargesLeft = chargesLeft;
            Armed = armed;
        }

        public LudoUpgrade Upgrade { get; private set; }

        /// <summary>
        /// Raises the level and tops the charges back up, so taking the same
        /// reward twice deepens it rather than filling the panel with copies.
        /// </summary>
        internal void LevelUp()
        {
            LudoUpgrade next = Upgrade.AtLevel(Upgrade.Level + 1);
            int spent = Upgrade.Charges - ChargesLeft;
            Upgrade = next;
            ChargesLeft = Mathf.Max(1, next.Charges - spent);
            Armed = true;
        }
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
        /// <summary>Ceilings on the two effects that otherwise decide the duel outright.</summary>
        public const int MaxExtraDice = 2;
        public const int MaxMinimumFace = 3;

        private readonly List<LudoUpgradeSlot> slots = new List<LudoUpgradeSlot>();

        public IReadOnlyList<LudoUpgradeSlot> Slots => slots;

        /// <summary>Whether anything is left that can still be used.</summary>
        public bool HasUsable
        {
            get
            {
                foreach (LudoUpgradeSlot slot in slots)
                {
                    if (slot.ChargesLeft > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Drops everything spent. Called once a fight is over rather than the
        /// moment a charge runs out, so an upgrade does not vanish from the
        /// panel in the middle of the duel it is being used in.
        /// </summary>
        public void RemoveSpent()
        {
            for (int index = slots.Count - 1; index >= 0; index--)
            {
                if (slots[index].ChargesLeft <= 0)
                {
                    slots.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// Adds an upgrade, or deepens the one already held. Returns the slot
        /// either way, so the caller can say which happened.
        /// </summary>
        public LudoUpgradeSlot Grant(LudoUpgrade upgrade)
        {
            LudoUpgradeSlot existing = Find(upgrade);
            if (existing != null)
            {
                existing.LevelUp();
                return existing;
            }

            LudoUpgradeSlot slot = new LudoUpgradeSlot(upgrade);
            slots.Add(slot);
            return slot;
        }

        /// <summary>The slot already holding this upgrade, or null.</summary>
        public LudoUpgradeSlot Find(LudoUpgrade upgrade)
        {
            foreach (LudoUpgradeSlot slot in slots)
            {
                if (slot.Upgrade.SameAs(upgrade))
                {
                    return slot;
                }
            }

            return null;
        }

        public void Grant(LudoUpgradeKind kind)
        {
            Grant(LudoUpgradeCatalog.Default(kind));
        }

        /// <summary>Adds a slot with saved charge/armed state, for <see cref="LudoSaveService"/> alone.</summary>
        internal void RestoreSlot(LudoUpgrade upgrade, int chargesLeft, bool armed)
        {
            slots.Add(new LudoUpgradeSlot(upgrade, chargesLeft, armed));
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
            int minimumFace = 1;
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

                    case LudoUpgradeKind.LoadedDice:
                        // The highest floor wins rather than adding up, since
                        // two floors are just the stricter of the two.
                        minimumFace = Mathf.Max(
                            minimumFace,
                            1 + upgrade.WholeMagnitude);
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

            // Two upgrades run away with the duel if left to scale freely.
            // Measured across levels, a third extra die reaches 90% and a floor
            // of four reaches 92% — past that a fight stops being one. Both are
            // held at their level-two strength, so further levels still buy
            // charges but no longer buy certainty.
            extraDice = Mathf.Min(extraDice, MaxExtraDice);
            minimumFace = Mathf.Min(minimumFace, MaxMinimumFace);

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
                handBoosts,
                minimumFace);
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
