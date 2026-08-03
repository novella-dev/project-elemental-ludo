namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Player-facing names for the dice combinations, kept out of the
    /// resolver so the scoring stays free of presentation.
    /// </summary>
    public static class LudoCombatInfo
    {
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

        /// <summary>e.g. "Trío ×2,5 → 45".</summary>
        public static string Describe(LudoCombatRoll roll)
        {
            return $"{HandName(roll.Hand)} ×{roll.Multiplier:0.#} → {roll.Score}";
        }
    }
}
