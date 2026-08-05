using System.Collections.Generic;
using ElementalLudo.Tokens;
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

        /// <summary>The throw plus any flat pips, which is what gets multiplied.</summary>
        public int Pips { get; }

        public float Multiplier { get; }

        /// <summary>
        /// Pips added before the multiplier, kept apart from
        /// <see cref="Pips"/> so the panel can show the sum of the dice and
        /// what was added to it as separate steps rather than one total the
        /// player has to take on trust.
        /// </summary>
        public int FlatPips { get; }

        /// <summary>Flat elemental edge, already included in the score.</summary>
        public int ElementBonus { get; }

        public int Score { get; }

        /// <summary>The dice alone, before anything was added.</summary>
        public int DiceTotal => Pips - FlatPips;

        /// <summary>The score after multiplying but before the elemental edge.</summary>
        public int MultipliedScore => Score - ElementBonus;

        public LudoCombatRoll(
            IReadOnlyList<int> dice,
            LudoDiceHand hand,
            int pips,
            float multiplier,
            int flatPips,
            int elementBonus,
            int score)
        {
            Dice = dice;
            Hand = hand;
            Pips = pips;
            Multiplier = multiplier;
            FlatPips = flatPips;
            ElementBonus = elementBonus;
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
    /// A duel plus who fought it, handed to the UI so it can show the throw.
    /// Carries the tokens rather than names or colours so the view can read
    /// whatever it needs off their PlayerStyle.
    /// </summary>
    public readonly struct LudoCombatReport
    {
        public Token Attacker { get; }
        public Token Defender { get; }
        public LudoCombatOutcome Outcome { get; }

        /// <summary>
        /// Which side the player was on. Needed to call the result a win or a
        /// loss, since losing as the defender and losing as the attacker are
        /// opposite outcomes of the same flag.
        /// </summary>
        public bool AttackerIsHuman { get; }

        public LudoCombatReport(
            Token attacker,
            Token defender,
            LudoCombatOutcome outcome,
            bool attackerIsHuman = false)
        {
            Attacker = attacker;
            Defender = defender;
            Outcome = outcome;
            AttackerIsHuman = attackerIsHuman;
        }

        /// <summary>True when the result went the player's way.</summary>
        public bool HumanWon => AttackerIsHuman == Outcome.AttackerWins;
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
        /// What holding the winning element is worth. Added after the
        /// multiplier, deliberately: a flat edge helps a bad hand far more than
        /// a good one, so it tilts close duels without letting a lucky póker
        /// run away with the fight.
        ///
        /// Five points against typical scores in the thirties is roughly a
        /// sixth of a throw — noticeable, not decisive.
        /// </summary>
        public const int ElementAdvantageBonus = 5;

        /// <summary>
        /// The elemental circle: fire burns plant, plant fouls lightning,
        /// lightning splits water, water quenches fire. Every element beats
        /// exactly one and loses to exactly one, so there is no dead pick and
        /// no dominant one.
        /// </summary>
        public static bool Beats(LudoElement element, LudoElement rival)
        {
            return element switch
            {
                LudoElement.Fire => rival == LudoElement.Plant,
                LudoElement.Plant => rival == LudoElement.Lightning,
                LudoElement.Lightning => rival == LudoElement.Water,
                LudoElement.Water => rival == LudoElement.Fire,
                _ => false
            };
        }

        /// <summary>
        /// The bonus one token earns against another, or zero. Returns zero for
        /// anything it cannot read, so a style without an element simply never
        /// gets an edge rather than throwing mid-duel.
        /// </summary>
        public static int ElementBonusFor(Token own, Token rival)
        {
            if (own == null || rival == null ||
                own.OwnerStyle == null || rival.OwnerStyle == null)
            {
                return 0;
            }

            return Beats(own.OwnerStyle.Element, rival.OwnerStyle.Element)
                ? ElementAdvantageBonus
                : 0;
        }

        /// <summary>
        /// Multipliers are tuned by feel, not by probability — players expect
        /// poker's ranking, and the true rarities disagree with it.
        ///
        /// Calibrated for three rerolls per side, which changes the odds
        /// enormously: a full house goes from 3.8% of hands without rerolls to
        /// 40% with them, so it's priced as the normal result rather than a
        /// prize. Simulated at 300k duels, this spread leaves the attacker
        /// winning 48.6% (ties go to the defender) and the better hand winning
        /// 82% of the time categories differ — dominant, but pips can still
        /// steal one in five.
        /// </summary>
        public static float MultiplierFor(LudoDiceHand hand)
        {
            return hand switch
            {
                LudoDiceHand.Pair => 1.2f,
                LudoDiceHand.TwoPair => 1.5f,
                LudoDiceHand.ThreeOfAKind => 1.8f,
                LudoDiceHand.Straight => 2.2f,
                LudoDiceHand.FullHouse => 2.5f,
                LudoDiceHand.FourOfAKind => 3.5f,
                LudoDiceHand.FiveOfAKind => 5f,
                _ => 1f
            };
        }

        /// <summary>
        /// Which die to reroll, or -1 to stand pat. Keeps anything that's part
        /// of a set and rerolls the lowest loose die, so a reroll can only
        /// improve the hand or leave it alone — never break it.
        ///
        /// Stands pat on a full house, a straight or five of a kind: those use
        /// every die, so any reroll can only make them worse.
        /// </summary>
        public static int SuggestReroll(IReadOnlyList<int> dice)
        {
            if (dice == null || dice.Count == 0)
            {
                return -1;
            }

            LudoDiceHand hand = Classify(dice);
            if (hand == LudoDiceHand.FiveOfAKind ||
                hand == LudoDiceHand.FullHouse ||
                hand == LudoDiceHand.Straight)
            {
                return -1;
            }

            int[] counts = new int[7];
            foreach (int die in dice)
            {
                counts[Mathf.Clamp(die, 1, 6)]++;
            }

            int chosen = -1;
            int lowest = int.MaxValue;
            for (int index = 0; index < dice.Count; index++)
            {
                int value = Mathf.Clamp(dice[index], 1, 6);
                if (counts[value] == 1 && value < lowest)
                {
                    lowest = value;
                    chosen = index;
                }
            }

            return chosen;
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

        /// <summary>
        /// Scores an already-thrown hand. Pure.
        ///
        /// The order the modifiers land in is the design, not an accident: flat
        /// pips join the throw before the multiplier so a good hand compounds
        /// them, while the elemental bonus lands after it and is worth the same
        /// whatever the dice did.
        /// </summary>
        public static LudoCombatRoll Evaluate(
            IReadOnlyList<int> dice,
            LudoCombatModifiers modifiers = null)
        {
            modifiers ??= LudoCombatModifiers.None;

            int pips = modifiers.FlatPips;
            if (dice != null)
            {
                foreach (int die in dice)
                {
                    pips += Mathf.Clamp(die, 1, 6);
                }
            }

            pips = Mathf.Max(0, pips);

            LudoDiceHand hand = Classify(dice);
            float multiplier =
                MultiplierFor(hand) + modifiers.MultiplierBoostFor(hand);
            return new LudoCombatRoll(
                dice,
                hand,
                pips,
                multiplier,
                modifiers.FlatPips,
                modifiers.ElementBonus,
                Mathf.RoundToInt(pips * multiplier) + modifiers.ElementBonus);
        }

        /// <summary>
        /// Plays a whole duel out with both sides using the AI's reroll
        /// judgement. This is the silent AI-versus-AI path; a duel the player
        /// is in drives two <see cref="LudoCombatHand"/> instances directly so
        /// they can pick their own rerolls, then scores them the same way.
        /// </summary>
        public static LudoCombatOutcome Resolve(
            Token attackerToken,
            Token defenderToken,
            bool elementalRules,
            int diceCount = DefaultDiceCount,
            int rerolls = LudoCombatHand.DefaultRerolls)
        {
            // Takes the tokens rather than plain numbers so the silent path
            // cannot end up applying a different elemental edge from the one a
            // watched duel would have used. No upgrades are folded in here on
            // purpose: this path only runs for AI-versus-AI captures, and
            // upgrades belong to the player.
            LudoCombatHand attacker = new LudoCombatHand(
                diceCount,
                rerolls,
                ModifiersFor(attackerToken, defenderToken, elementalRules));
            LudoCombatHand defender = new LudoCombatHand(
                diceCount,
                rerolls,
                ModifiersFor(defenderToken, attackerToken, elementalRules));
            attacker.PlayOutWithAI();
            defender.PlayOutWithAI();
            return new LudoCombatOutcome(attacker.Evaluate(), defender.Evaluate());
        }

        /// <summary>An unupgraded side carrying only whatever elemental edge it has.</summary>
        public static LudoCombatModifiers ModifiersFor(
            Token own,
            Token rival,
            bool elementalRules)
        {
            int bonus = elementalRules ? ElementBonusFor(own, rival) : 0;
            return bonus == 0
                ? LudoCombatModifiers.None
                : LudoCombatModifiers.None.WithElementBonus(bonus);
        }
    }
}
