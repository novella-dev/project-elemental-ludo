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

        public LudoRulesContext(bool elementalModeEnabled, bool permadeathEnabled = false)
        {
            ElementalModeEnabled = elementalModeEnabled;
            PermadeathEnabled = permadeathEnabled;
        }
    }
}
