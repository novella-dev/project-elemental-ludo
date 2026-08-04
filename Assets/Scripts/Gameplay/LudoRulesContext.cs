namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Optional-rule toggles threaded through LudoRulesEngine. A single
    /// struct instead of a growing parameter list, so later phases can add
    /// more flags without changing every method's signature again.
    /// </summary>
    public readonly struct LudoRulesContext
    {
        public bool ElementalModeEnabled { get; }

        /// <summary>
        /// Captured tokens are gone for good and a single token reaching the
        /// goal wins, instead of needing all four.
        /// </summary>
        public bool PermadeathEnabled { get; }

        /// <summary>
        /// The active player has a Barrera firme armed, so a 6 does not oblige
        /// them to break their own barrier this turn.
        ///
        /// A per-turn flag rather than a match rule: it comes from an armed
        /// upgrade the player switches on and spends, so it can be true on one
        /// turn and false on the next for the same match.
        /// </summary>
        public bool BarrierBreakExempt { get; }

        public LudoRulesContext(
            bool elementalModeEnabled,
            bool permadeathEnabled = false,
            bool barrierBreakExempt = false)
        {
            ElementalModeEnabled = elementalModeEnabled;
            PermadeathEnabled = permadeathEnabled;
            BarrierBreakExempt = barrierBreakExempt;
        }
    }
}
