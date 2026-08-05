using ElementalLudo.Tokens;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Player-facing names for the dice combinations, kept out of the
    /// resolver so the scoring stays free of presentation.
    /// </summary>
    public static class LudoCombatInfo
    {
        /// <summary>The elemental circle, written out for the duel screen.</summary>
        public const string AdvantageRule =
            "Fuego › Planta › Rayo › Agua › Fuego";
        public static string HandName(LudoDiceHand hand)
        {
            return hand switch
            {
                LudoDiceHand.Pair => "Pareja",
                LudoDiceHand.TwoPair => "Doble pareja",
                LudoDiceHand.ThreeOfAKind => "Trío",
                LudoDiceHand.Straight => "Escalera",
                LudoDiceHand.FullHouse => "Full",
                LudoDiceHand.FourOfAKind => "Póker",
                LudoDiceHand.FiveOfAKind => "¡REPÓKER!",
                _ => "Nada"
            };
        }

        /// <summary>e.g. "Trío ×1,8 +5 → 50".</summary>
        public static string Describe(LudoCombatRoll roll)
        {
            string bonus = roll.ElementBonus > 0
                ? $" +{roll.ElementBonus}"
                : string.Empty;
            return $"{HandName(roll.Hand)} ×{roll.Multiplier:0.#}{bonus} → {roll.Score}";
        }

        /// <summary>
        /// The whole sum, step by step: the faces added up, anything joined to
        /// them, the multiplier, and the flat bonus after it.
        ///
        /// Written out because the order is the rule and it is not guessable
        /// from a total — pips join before the multiplier and so get multiplied
        /// with everything else, while the elemental edge lands after and is
        /// worth the same five points whatever the dice did.
        /// </summary>
        public static string Breakdown(LudoCombatRoll roll)
        {
            if (roll.Dice == null || roll.Dice.Count == 0)
            {
                return string.Empty;
            }

            System.Text.StringBuilder text = new System.Text.StringBuilder(64);

            for (int index = 0; index < roll.Dice.Count; index++)
            {
                if (index > 0)
                {
                    text.Append('+');
                }

                text.Append(roll.Dice[index]);
            }

            text.Append(" = ").Append(roll.DiceTotal);

            if (roll.FlatPips != 0)
            {
                text.Append("   ")
                    .Append(roll.FlatPips > 0 ? "+" : string.Empty)
                    .Append(roll.FlatPips)
                    .Append(" runa = ")
                    .Append(roll.Pips);
            }

            text.Append("   ×")
                .Append(roll.Multiplier.ToString("0.#"))
                .Append(' ')
                .Append(HandName(roll.Hand))
                .Append(" = ")
                .Append(roll.MultipliedScore);

            if (roll.ElementBonus != 0)
            {
                text.Append("   +")
                    .Append(roll.ElementBonus)
                    .Append(" elemental = ")
                    .Append(roll.Score);
            }

            return text.ToString();
        }

        /// <summary>
        /// Which side the elemental edge favours and why, or null when neither
        /// has one.
        ///
        /// Reads the bonus off the rolls rather than recomputing it from the
        /// elements, so it can only ever say what the scoring actually did — if
        /// the elemental layer is switched off, the rolls carry no bonus and
        /// this stays quiet on its own.
        /// </summary>
        public static string AdvantageLine(
            LudoCombatRoll attackerRoll,
            LudoCombatRoll defenderRoll,
            Token attackerToken,
            Token defenderToken)
        {
            if (attackerRoll.ElementBonus > 0)
            {
                return Advantage(attackerToken, defenderToken, "atacante");
            }

            if (defenderRoll.ElementBonus > 0)
            {
                return Advantage(defenderToken, attackerToken, "defensor");
            }

            return null;
        }

        private static string Advantage(Token winner, Token loser, string role)
        {
            if (winner?.OwnerStyle == null || loser?.OwnerStyle == null)
            {
                return null;
            }

            return $"{LudoElementInfo.DisplayName(winner.OwnerStyle.Element)} supera a " +
                   $"{LudoElementInfo.DisplayName(loser.OwnerStyle.Element)}: " +
                   $"+{LudoCombatResolver.ElementAdvantageBonus} al {role}, " +
                   $"tras multiplicar.";
        }
    }
}
