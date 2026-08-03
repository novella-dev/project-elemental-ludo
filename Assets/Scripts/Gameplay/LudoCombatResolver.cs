using System.Collections.Generic;
using UnityEngine;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Poker-style ranking of a handful of dice, best to worst. Ordered so a
    /// plain comparison works, and matching poker's own ranking (a straight
    /// beats three of a kind, a full house beats a straight) rather than
    /// Yahtzee's, since that's the frame of reference here.
    /// </summary>
    public enum LudoDiceHand
    {
        Nothing,
        Pair,
        TwoPair,
        ThreeOfAKind,
        Straight,
        FullHouse,
        FourOfAKind,
        FiveOfAKind
    }

    /// <summary>One side's throw: the dice, what they add up to, and what it's worth.</summary>
    public readonly struct LudoCombatRoll
    {
        public IReadOnlyList<int> Dice { get; }
        public LudoDiceHand Hand { get; }
        public int Pips { get; }
        public float Multiplier { get; }
        public int Score { get; }

        public LudoCombatRoll(
            IReadOnlyList<int> dice,
            LudoDiceHand hand,
            int pips,
            float multiplier,
            int score)
        {
            Dice = dice;
            Hand = hand;
            Pips = pips;
            Multiplier = multiplier;
            Score = score;
        }
    }

    /// <summary>
    /// A resolved duel. Ties go to the defender: the attacker has to beat the
    /// score, not match it.
    /// </summary>
    public readonly struct LudoCombatOutcome
    {
        public LudoCombatRoll Attacker { get; }
        public LudoCombatRoll Defender { get; }

        public LudoCombatOutcome(LudoCombatRoll attacker, LudoCombatRoll defender)
        {
            Attacker = attacker;
            Defender = defender;
        }

        public bool AttackerWins => Attacker.Score > Defender.Score;
    }

    /// <summary>
    /// Adventure-mode capture duels: both sides throw a handful of dice, the
    /// pips are multiplied by how good the combination is, and the higher
    /// score takes it.
    ///
    /// Scoring is a pure function of the dice, deliberately: an AI-versus-AI
    /// capture resolves through exactly this code with nothing drawn, so a
    /// silent duel can never disagree with one the player watched. Only
    /// <see cref="Throw"/> is random.
    ///
    /// Dice count is a parameter rather than a constant because upgrading a
    /// character's dice is planned; the straight check is written against the
    /// actual count for the same reason.
    /// </summary>
    public static class LudoCombatResolver
    {
        public const int DefaultDiceCount = 5;

        /// <summary>
        /// Multipliers are tuned by feel, not by probability — players expect
        /// poker's ranking, and with 5d6 the true rarities disagree with it
        /// (an all-different hand is rarer than a pair). Expectation wins.
        /// </summary>
        public static float MultiplierFor(LudoDiceHand hand)
        {
            return hand switch
            {
                LudoDiceHand.Pair => 1.5f,
                LudoDiceHand.TwoPair => 2f,
                LudoDiceHand.ThreeOfAKind => 2.5f,
                LudoDiceHand.Straight => 3f,
                LudoDiceHand.FullHouse => 4f,
                LudoDiceHand.FourOfAKind => 5f,
                LudoDiceHand.FiveOfAKind => 7f,
                _ => 1f
            };
        }

        public static LudoDiceHand Classify(IReadOnlyList<int> dice)
        {
            if (dice == null || dice.Count == 0)
            {
                return LudoDiceHand.Nothing;
            }

            int[] counts = new int[7];
            int distinct = 0;
            int highest = int.MinValue;
            int lowest = int.MaxValue;

            foreach (int die in dice)
            {
                int value = Mathf.Clamp(die, 1, 6);
                if (counts[value] == 0)
                {
                    distinct++;
                }

                counts[value]++;
                highest = Mathf.Max(highest, value);
                lowest = Mathf.Min(lowest, value);
            }

            int bestOfAKind = 0;
            int pairCount = 0;
            bool hasThree = false;
            for (int face = 1; face <= 6; face++)
            {
                int count = counts[face];
                bestOfAKind = Mathf.Max(bestOfAKind, count);
                if (count == 2)
                {
                    pairCount++;
                }

                if (count == 3)
                {
                    hasThree = true;
                }
            }

            if (bestOfAKind >= 5)
            {
                return LudoDiceHand.FiveOfAKind;
            }

            if (bestOfAKind == 4)
            {
                return LudoDiceHand.FourOfAKind;
            }

            if (hasThree && pairCount >= 1)
            {
                return LudoDiceHand.FullHouse;
            }

            // Every die a different, consecutive face — checked against the
            // hand size so a 6-dice upgrade still needs 6 in a row.
            if (distinct == dice.Count && highest - lowest == dice.Count - 1)
            {
                return LudoDiceHand.Straight;
            }

            if (bestOfAKind == 3)
            {
                return LudoDiceHand.ThreeOfAKind;
            }

            if (pairCount >= 2)
            {
                return LudoDiceHand.TwoPair;
            }

            return pairCount == 1 ? LudoDiceHand.Pair : LudoDiceHand.Nothing;
        }

        /// <summary>Scores an already-thrown hand. Pure.</summary>
        public static LudoCombatRoll Evaluate(IReadOnlyList<int> dice)
        {
            int pips = 0;
            if (dice != null)
            {
                foreach (int die in dice)
                {
                    pips += Mathf.Clamp(die, 1, 6);
                }
            }

            LudoDiceHand hand = Classify(dice);
            float multiplier = MultiplierFor(hand);
            return new LudoCombatRoll(
                dice,
                hand,
                pips,
                multiplier,
                Mathf.RoundToInt(pips * multiplier));
        }

        /// <summary>Throws a hand and scores it. The only random part.</summary>
        public static LudoCombatRoll Throw(int diceCount = DefaultDiceCount)
        {
            int count = Mathf.Max(1, diceCount);
            int[] dice = new int[count];
            for (int index = 0; index < count; index++)
            {
                dice[index] = UnityEngine.Random.Range(1, 7);
            }

            return Evaluate(dice);
        }

        /// <summary>
        /// Throws for both sides. Used for duels the player watches and for
        /// AI-versus-AI ones resolved silently — same code either way.
        /// </summary>
        public static LudoCombatOutcome Resolve(
            int attackerDiceCount = DefaultDiceCount,
            int defenderDiceCount = DefaultDiceCount)
        {
            return new LudoCombatOutcome(
                Throw(attackerDiceCount),
                Throw(defenderDiceCount));
        }
    }
}
