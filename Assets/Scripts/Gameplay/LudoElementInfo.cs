using ElementalLudo.Tokens;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Player-facing names and rule blurbs for each element, in one place so
    /// the element picker and the in-game rules panel can't drift apart.
    /// Colors deliberately aren't here — those live on each PlayerStyle
    /// asset, which is the real source of truth for how a player looks.
    /// </summary>
    public static class LudoElementInfo
    {
        public static string DisplayName(LudoElement element)
        {
            return element switch
            {
                LudoElement.Fire => "Fuego",
                LudoElement.Water => "Agua",
                LudoElement.Lightning => "Rayo",
                LudoElement.Plant => "Planta",
                _ => element.ToString()
            };
        }

        public static string RuleSummary(LudoElement element)
        {
            return element switch
            {
                LudoElement.Fire =>
                    "Captura también en casillas seguras — para el fuego, " +
                    "ninguna casilla es segura para los rivales.",
                LudoElement.Water =>
                    "Ignora barreras de cualquier color en su propio movimiento. " +
                    "Una barrera de agua sigue bloqueando a los demás con normalidad.",
                LudoElement.Lightning =>
                    "La distancia de movimiento es la tirada + 1. Salir de casa " +
                    "sigue necesitando un 5, y el turno extra/tres seises siguen " +
                    "usando la tirada real.",
                LudoElement.Plant =>
                    "Inmune a las capturas de Agua específicamente. Fuego y Rayo " +
                    "la capturan con normalidad.",
                _ => string.Empty
            };
        }
    }
}
