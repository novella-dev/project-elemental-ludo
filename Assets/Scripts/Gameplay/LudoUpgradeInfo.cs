namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Player-facing names and descriptions for the upgrades, kept out of the
    /// catalog so balance numbers and wording can change independently.
    /// </summary>
    public static class LudoUpgradeInfo
    {
        public static string DisplayName(LudoUpgradeKind kind)
        {
            return kind switch
            {
                LudoUpgradeKind.ExtraDie => "Dado extra",
                LudoUpgradeKind.ExtraReroll => "Relanzamiento extra",
                LudoUpgradeKind.ElementalEdge => "Ventaja reforzada",
                LudoUpgradeKind.FlatPips => "Carga de puntos",
                LudoUpgradeKind.HandMastery => "Maestría",
                LudoUpgradeKind.BarrierExemption => "Barrera firme",
                LudoUpgradeKind.MovementRethrow => "Repetir tirada",
                _ => kind.ToString()
            };
        }

        /// <summary>What it does, with the granted upgrade's own numbers in it.</summary>
        public static string Describe(LudoUpgrade upgrade)
        {
            return upgrade.Kind switch
            {
                LudoUpgradeKind.ExtraDie =>
                    $"Tiras {upgrade.WholeMagnitude} dado(s) más en el duelo.",

                LudoUpgradeKind.ExtraReroll =>
                    $"+{upgrade.WholeMagnitude} relanzamiento(s) en el duelo.",

                LudoUpgradeKind.ElementalEdge =>
                    $"+{upgrade.WholeMagnitude} extra a tu ventaja elemental, " +
                    "si ya la tienes.",

                LudoUpgradeKind.FlatPips =>
                    $"+{upgrade.WholeMagnitude} a la suma, antes de multiplicar.",

                LudoUpgradeKind.HandMastery =>
                    $"{LudoCombatInfo.HandName(upgrade.TargetHand)}: " +
                    $"+{upgrade.Magnitude:0.#} al multiplicador.",

                LudoUpgradeKind.BarrierExemption =>
                    "Un 6 no te obliga a romper tu propia barrera.",

                LudoUpgradeKind.MovementRethrow =>
                    "Repites la tirada de movimiento y te quedas la segunda.",

                _ => string.Empty
            };
        }

        public static string ScopeName(LudoUpgradeScope scope)
        {
            return scope switch
            {
                LudoUpgradeScope.Duel => "duelo",
                LudoUpgradeScope.Turn => "turno",
                _ => scope.ToString()
            };
        }
    }
}
