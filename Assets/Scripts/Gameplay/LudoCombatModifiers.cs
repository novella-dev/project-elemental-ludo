using UnityEngine;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Everything that shifts one side's numbers in a duel, folded flat.
    ///
    /// This is the seam the whole upgrade system leans on. Rather than the
    /// resolver knowing what an upgrade is, upgrades are collapsed into the
    /// handful of levers scoring already has — how many dice, how many
    /// rerolls, pips added before multiplying, the multiplier itself, and the
    /// flat bonus added after. A new upgrade that moves any of those needs no
    /// change here and none in the resolver; it only needs folding in
    /// <see cref="LudoUpgradeInventory.BuildCombatModifiers"/>.
    ///
    /// Built once per hand and then only read, so the per-frame Evaluate the UI
    /// does costs nothing.
    /// </summary>
    public sealed class LudoCombatModifiers
    {
        /// <summary>No upgrades and no elemental edge: the plain rules.</summary>
        public static readonly LudoCombatModifiers None = new LudoCombatModifiers(0, 0, 0, 0, null);

        // Indexed by (int)LudoDiceHand. Null when nothing boosts a hand, which
        // is the common case and saves the allocation entirely.
        private readonly float[] handBoosts;

        public LudoCombatModifiers(
            int extraDice,
            int extraRerolls,
            int flatPips,
            int elementBonus,
            float[] handBoosts)
        {
            ExtraDice = Mathf.Max(0, extraDice);
            ExtraRerolls = Mathf.Max(0, extraRerolls);
            FlatPips = flatPips;
            ElementBonus = elementBonus;
            this.handBoosts = handBoosts;
        }

        /// <summary>Dice beyond the standard five.</summary>
        public int ExtraDice { get; }

        /// <summary>Rerolls beyond the standard three.</summary>
        public int ExtraRerolls { get; }

        /// <summary>Added to the pip sum, before the multiplier.</summary>
        public int FlatPips { get; }

        /// <summary>Added to the score, after the multiplier.</summary>
        public int ElementBonus { get; }

        public bool HasAny =>
            ExtraDice != 0 ||
            ExtraRerolls != 0 ||
            FlatPips != 0 ||
            ElementBonus != 0 ||
            handBoosts != null;

        /// <summary>Extra multiplier for one combination, or zero.</summary>
        public float MultiplierBoostFor(LudoDiceHand hand)
        {
            if (handBoosts == null)
            {
                return 0f;
            }

            int index = (int)hand;
            return index >= 0 && index < handBoosts.Length ? handBoosts[index] : 0f;
        }

        /// <summary>
        /// The same upgrades with a different elemental edge. The two sides of a
        /// duel carry different edges but only the player carries upgrades, so
        /// the rival's modifiers are this applied to nothing but their bonus.
        /// </summary>
        public LudoCombatModifiers WithElementBonus(int elementBonus)
        {
            return new LudoCombatModifiers(
                ExtraDice,
                ExtraRerolls,
                FlatPips,
                elementBonus,
                handBoosts);
        }
    }
}
