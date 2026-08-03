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
