namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Player-facing text for the run map, kept out of the model so wording can
    /// change without touching how a run works.
    /// </summary>
    public static class LudoRunInfo
    {
        public static string NodeName(LudoRunNodeKind kind)
        {
            return kind switch
            {
                LudoRunNodeKind.Reward => "Recompensa",
                LudoRunNodeKind.Duel => "Combate",
                LudoRunNodeKind.Heal => "Curación",
                LudoRunNodeKind.Match => "Partida",
                LudoRunNodeKind.Elite => "Élite",
                LudoRunNodeKind.Boss => "Partida final",
                _ => kind.ToString()
            };
        }

        public static string NodeSummary(LudoRunNodeKind kind)
        {
            return kind switch
            {
                LudoRunNodeKind.Reward =>
                    "Sin pelea. Elige una mejora.",
                LudoRunNodeKind.Duel =>
                    "Duelo de dados al mejor de 3. Perder cuesta una vida.",
                LudoRunNodeKind.Heal =>
                    $"Sin pelea. Recuperas {LudoRunState.HealAmount} vidas " +
                    $"(máximo {LudoRunState.MaxLives}).",
                LudoRunNodeKind.Match =>
                    "Partida completa contra la IA.",
                LudoRunNodeKind.Elite =>
                    "Duelo al mejor de 5, rival duro. Mejor recompensa.",
                LudoRunNodeKind.Boss =>
                    "La partida de parchís. Juegas con tantas fichas como vidas te queden.",
                _ => string.Empty
            };
        }

        public static string StatusLine(LudoRunState run)
        {
            if (run == null)
            {
                return string.Empty;
            }

            return run.Status switch
            {
                LudoRunStatus.AtNode =>
                    $"{NodeName(run.CurrentNode.Kind)} — etapa " +
                    $"{run.Stage + 1}/{run.Map.StageCount}",
                LudoRunStatus.Choosing => "Elige por dónde seguir.",
                LudoRunStatus.Won => "¡RUN COMPLETADA!",
                LudoRunStatus.Lost => "La run termina aquí.",
                _ => string.Empty
            };
        }
    }
}
