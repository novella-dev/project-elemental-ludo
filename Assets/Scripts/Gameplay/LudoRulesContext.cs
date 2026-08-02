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

        public LudoRulesContext(bool elementalModeEnabled)
        {
            ElementalModeEnabled = elementalModeEnabled;
        }
    }
}
