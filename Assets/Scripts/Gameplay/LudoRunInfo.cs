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
                LudoRunNodeKind.Match => "Partida",
                LudoRunNodeKind.Elite => "Élite",
                LudoRunNodeKind.Boss => "Jefe",
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
                    "Duelo de dados suelto, sin tablero.",
                LudoRunNodeKind.Match =>
                    "Partida completa contra la IA.",
                LudoRunNodeKind.Elite =>
                    "Partida más dura. Mejor recompensa.",
                LudoRunNodeKind.Boss =>
                    "El último. Necesitarás lo que hayas reunido.",
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
