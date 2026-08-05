using System.Collections.Generic;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// The upgrades that exist, what a level of each is worth, and which ones a
    /// run may offer.
    ///
    /// Balance numbers all live here so they can be read and retuned in one
    /// place. Every figure below was measured at 200-300k simulated duels
    /// against an unupgraded rival, where an unarmed attacker wins 48.4%.
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
        /// What one level is worth. A level 3 is three times this, so the ramp
        /// is deliberately gentle: these stack, and stacking is the point.
        /// </summary>
        public static float MagnitudePerLevel(LudoUpgradeKind kind)
        {
            return kind switch
            {
                // 67.7% at a single extra die, the strongest thing measured, so
                // one level at a time and few charges to spend it on.
                LudoUpgradeKind.ExtraDie => 1f,

                // 54.2% at one, 58.4% at two — mild alone, worth levelling.
                LudoUpgradeKind.ExtraReroll => 1f,

                // 64.2% at +3 on top of the base +5, but only when the matchup
                // already favours you, which is about half of duels.
                LudoUpgradeKind.ElementalEdge => 3f,

                // 60.1% at +3 pips, applied before the multiplier.
                LudoUpgradeKind.FlatPips => 3f,

                // Depends on which hand — see MasteryPerLevel.
                LudoUpgradeKind.HandMastery => 0.8f,

                // A level raises the floor by one face: Lvl 1 means no ones.
                LudoUpgradeKind.LoadedDice => 1f,

                _ => 1f
            };
        }

        /// <summary>
        /// Mastery scales against how often its hand actually turns up, so a
        /// rarer target is worth more per level. Measured at 200k duels each:
        /// a full house lands 39.9% of the time and +0.8 puts it at 55.5%,
        /// while three of a kind lands 13.4% and needs +1.8 to reach 54.8%.
        ///
        /// Póker and escalera are deliberately absent. Boosting them barely
        /// moved the needle at any magnitude — +6.0 on a straight was worth
        /// half a point — because the reroll strategy has no way to steer
        /// toward them, so the hand simply never arrives to be rewarded.
        /// </summary>
        public static float MasteryPerLevel(LudoDiceHand hand)
        {
            return hand switch
            {
                LudoDiceHand.FullHouse => 0.8f,
                LudoDiceHand.TwoPair => 0.7f,
                LudoDiceHand.ThreeOfAKind => 1.8f,
                _ => 0.8f
            };
        }

        /// <summary>
        /// Uses granted. Levelling adds one, so a levelled upgrade is both
        /// stronger and available more often.
        /// </summary>
        public static int ChargesFor(LudoUpgradeKind kind, int level)
        {
            int baseCharges = kind switch
            {
                LudoUpgradeKind.ExtraDie => 1,
                LudoUpgradeKind.ElementalEdge => 2,
                LudoUpgradeKind.HandMastery => 2,
                LudoUpgradeKind.LoadedDice => 2,
                _ => 3
            };

            return baseCharges + (level - 1);
        }

        public static LudoUpgrade Default(LudoUpgradeKind kind)
        {
            return kind == LudoUpgradeKind.HandMastery
                ? new LudoUpgrade(kind, 1, LudoDiceHand.FullHouse)
                : new LudoUpgrade(kind);
        }

        /// <summary>
        /// Everything a run may offer as a reward, at level one.
        ///
        /// Only duel-scoped upgrades appear. A run is almost entirely arena
        /// fights now — the board game happens once, at the very end — so the
        /// movement upgrades were being handed out for something the player
        /// would meet at most once and often never. They remain in the model
        /// for the final game and for whatever A4 and A5 bring.
        ///
        /// Mastery appears once per combination rather than once overall: they
        /// level separately, and choosing which hand to specialise in is a
        /// different decision each time.
        /// </summary>
        public static IReadOnlyList<LudoUpgrade> RewardPool => Pool;

        private static readonly LudoUpgrade[] Pool =
        {
            new LudoUpgrade(LudoUpgradeKind.ExtraDie),
            new LudoUpgrade(LudoUpgradeKind.ExtraReroll),
            new LudoUpgrade(LudoUpgradeKind.ElementalEdge),
            new LudoUpgrade(LudoUpgradeKind.FlatPips),
            new LudoUpgrade(LudoUpgradeKind.HandMastery, 1, LudoDiceHand.FullHouse),
            new LudoUpgrade(LudoUpgradeKind.HandMastery, 1, LudoDiceHand.TwoPair),
            new LudoUpgrade(LudoUpgradeKind.HandMastery, 1, LudoDiceHand.ThreeOfAKind),
            new LudoUpgrade(LudoUpgradeKind.LoadedDice)
        };

        /// <summary>Every kind, in the order they should be listed.</summary>
        public static IReadOnlyList<LudoUpgradeKind> AllKinds => Kinds;

        private static readonly LudoUpgradeKind[] Kinds =
        {
            LudoUpgradeKind.ExtraDie,
            LudoUpgradeKind.ExtraReroll,
            LudoUpgradeKind.ElementalEdge,
            LudoUpgradeKind.FlatPips,
            LudoUpgradeKind.HandMastery,
            LudoUpgradeKind.LoadedDice,
            LudoUpgradeKind.BarrierExemption,
            LudoUpgradeKind.MovementRethrow
        };
    }
}
