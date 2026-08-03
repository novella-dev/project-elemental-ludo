namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Player-facing name and pitch for each mode, kept out of the menu so
    /// the wording lives next to the modes it describes.
    /// </summary>
    public static class LudoGameModeInfo
    {
        public static string DisplayName(LudoGameMode mode)
        {
            return mode switch
            {
                LudoGameMode.Classic => "Clásico",
                LudoGameMode.Adventure => "Aventura",
                LudoGameMode.Hardcore => "Hardcore",
                LudoGameMode.Multiplayer => "Multijugador",
                _ => mode.ToString()
            };
        }

        public static string Summary(LudoGameMode mode)
        {
            return mode switch
            {
                LudoGameMode.Classic =>
                    "El parchís de toda la vida contra la IA. Tablero tradicional, " +
                    "sin biomas ni capas extra.",
                LudoGameMode.Adventure =>
                    "El tablero elemental actual contra la IA. Aquí es donde " +
                    "crecerá la estructura roguelike.",
                LudoGameMode.Hardcore =>
                    "Las fichas capturadas NO vuelven. Basta con llevar una a la " +
                    "meta para ganar, pero quedarte sin fichas es perder.",
                LudoGameMode.Multiplayer =>
                    "Local por turnos: los cuatro colores los jugáis personas en " +
                    "el mismo dispositivo.",
                _ => string.Empty
            };
        }
    }
}
