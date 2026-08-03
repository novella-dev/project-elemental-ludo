using ElementalLudo.Tokens;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Everything the start menu decides about a match, handed to the game
    /// controller in one piece so starting a game is a single call.
    /// </summary>
    public readonly struct LudoMatchSettings
    {
        public LudoGameMode Mode { get; }

        /// <summary>
        /// Which seat the human takes, identified by that seat's element
        /// (each PlayerStyle carries a distinct one). Ignored in Multiplayer,
        /// where every seat is human.
        /// </summary>
        public LudoElement PlayerSeat { get; }

        public LudoAIDifficulty Difficulty { get; }

        /// <summary>
        /// Elemental rules are an opt-in extra on top of a mode, except in
        /// Classic where they're the thing being opted out of.
        /// </summary>
        public bool ElementalRules { get; }

        public LudoMatchSettings(
            LudoGameMode mode,
            LudoElement playerSeat,
            LudoAIDifficulty difficulty,
            bool elementalRules)
        {
            Mode = mode;
            PlayerSeat = playerSeat;
            Difficulty = difficulty;
            ElementalRules = elementalRules && SupportsElementalRules(mode);
        }

        /// <summary>Hot-seat has no AI to configure.</summary>
        public bool HasAIOpponents => Mode != LudoGameMode.Multiplayer;

        /// <summary>Captures remove tokens for good and one token home wins.</summary>
        public bool Permadeath => Mode == LudoGameMode.Hardcore;

        /// <summary>Classic hides the biomes and shows the plain home discs.</summary>
        public bool UsesClassicBoard => Mode == LudoGameMode.Classic;

        public static bool SupportsElementalRules(LudoGameMode mode)
        {
            return mode != LudoGameMode.Classic;
        }

        public LudoRulesContext ToRulesContext()
        {
            return new LudoRulesContext(ElementalRules, Permadeath);
        }
    }
}
