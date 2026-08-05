namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Player-facing names and descriptions for the upgrades, kept out of the
    /// catalog so wording and balance can change independently.
    /// </summary>
    public static class LudoUpgradeInfo
    {
        /// <summary>The name without its level, for headings and logs.</summary>
        public static string BaseName(LudoUpgrade upgrade)
        {
            return upgrade.Kind switch
            {
                LudoUpgradeKind.ExtraDie => "Dado del Destino",
                LudoUpgradeKind.ExtraReroll => "Bendecido por la Fortuna",
                LudoUpgradeKind.ElementalEdge => "Furia Elemental",
                LudoUpgradeKind.FlatPips => "Carga Rúnica",
                LudoUpgradeKind.HandMastery =>
                    $"Maestría: {LudoCombatInfo.HandName(upgrade.TargetHand)}",
                LudoUpgradeKind.LoadedDice => "Dados Cargados",
                LudoUpgradeKind.BarrierExemption => "Barrera Firme",
                LudoUpgradeKind.MovementRethrow => "Mano del Viajero",
                _ => upgrade.Kind.ToString()
            };
        }

        /// <summary>e.g. "Bendecido por la Fortuna  Lvl. 2".</summary>
        public static string DisplayName(LudoUpgrade upgrade)
        {
            return $"{BaseName(upgrade)}  Lvl. {upgrade.Level}";
        }

        /// <summary>Kept for callers that only have a kind to go on.</summary>
        public static string DisplayName(LudoUpgradeKind kind)
        {
            return BaseName(LudoUpgradeCatalog.Default(kind));
        }

        /// <summary>What it does, with this level's own numbers in it.</summary>
        public static string Describe(LudoUpgrade upgrade)
        {
            return upgrade.Kind switch
            {
                LudoUpgradeKind.ExtraDie =>
                    $"Tiras {upgrade.WholeMagnitude} dado(s) de más en el duelo.",

                LudoUpgradeKind.ExtraReroll =>
                    $"+{upgrade.WholeMagnitude} relanzamiento(s) en el duelo.",

                LudoUpgradeKind.ElementalEdge =>
                    $"+{upgrade.WholeMagnitude} a tu ventaja elemental, si ya la tienes.",

                LudoUpgradeKind.FlatPips =>
                    $"+{upgrade.WholeMagnitude} a la suma, antes de multiplicar.",

                LudoUpgradeKind.HandMastery =>
                    $"{LudoCombatInfo.HandName(upgrade.TargetHand)}: " +
                    $"+{upgrade.Magnitude:0.#} al multiplicador.",

                LudoUpgradeKind.LoadedDice =>
                    $"Tus dados nunca bajan de {1 + upgrade.WholeMagnitude}.",

                LudoUpgradeKind.BarrierExemption =>
                    "Un 6 no te obliga a romper tu propia barrera.",

                LudoUpgradeKind.MovementRethrow =>
                    "Repites la tirada de movimiento y te quedas la segunda.",

                _ => string.Empty
            };
        }

        /// <summary>What taking it again would add, for the reward screen.</summary>
        public static string NextLevelHint(LudoUpgrade upgrade)
        {
            LudoUpgrade next = upgrade.AtLevel(upgrade.Level + 1);
            return $"Ya la tienes. Subirá a Lvl. {next.Level}: {Describe(next)}";
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
