using ElementalLudo.Tokens;

namespace ElementalLudo.Gameplay
{
    public enum LudoCombatPhase
    {
        /// <summary>Attacker is throwing and spending rerolls.</summary>
        AttackerTurn,

        /// <summary>Defender's turn, knowing exactly what it has to beat.</summary>
        DefenderTurn,

        /// <summary>Both hands final; the outcome stands.</summary>
        Resolved
    }

    /// <summary>
    /// A duel being played out, turn by turn. The attacker finishes its hand
    /// first and the defender then plays knowing the score to beat, which is
    /// a real information advantage — worth remembering if the defending AI
    /// ever learns to play to a target instead of just greedily.
    ///
    /// Holds no presentation: the arena reads it to draw, the UI pokes it to
    /// reroll, and the controller advances it.
    /// </summary>
    public sealed class LudoCombatSession
    {
        public LudoCombatSession(
            Token attackerToken,
            Token defenderToken,
            bool attackerIsHuman,
            bool defenderIsHuman,
            bool elementalRules,
            LudoUpgradeInventory inventory = null,
            int diceCount = LudoCombatResolver.DefaultDiceCount,
            int rerolls = LudoCombatHand.DefaultRerolls)
        {
            AttackerToken = attackerToken;
            DefenderToken = defenderToken;
            AttackerIsHuman = attackerIsHuman;
            DefenderIsHuman = defenderIsHuman;
            ElementalRules = elementalRules;

            // Worked out once, at the start: neither the matchup nor the armed
            // upgrades can change while the duel runs, and baking them into the
            // hands keeps every later reading of the score consistent.
            //
            // Upgrades only ever join the human's side. Which side that is
            // depends on who attacked, so the two are resolved separately
            // rather than assuming the player is the attacker.
            LudoCombatModifiers attackerModifiers = BuildModifiers(
                attackerToken, defenderToken, attackerIsHuman, elementalRules, inventory);
            LudoCombatModifiers defenderModifiers = BuildModifiers(
                defenderToken, attackerToken, defenderIsHuman, elementalRules, inventory);

            Attacker = new LudoCombatHand(diceCount, rerolls, attackerModifiers);
            Defender = new LudoCombatHand(diceCount, rerolls, defenderModifiers);
            Phase = LudoCombatPhase.AttackerTurn;
        }

        /// <summary>
        /// One side's numbers: its elemental edge always, plus the armed
        /// upgrades if this is the side the player is on.
        /// </summary>
        private static LudoCombatModifiers BuildModifiers(
            Token own,
            Token rival,
            bool isHuman,
            bool elementalRules,
            LudoUpgradeInventory inventory)
        {
            int elementBonus = elementalRules
                ? LudoCombatResolver.ElementBonusFor(own, rival)
                : 0;

            if (!isHuman || inventory == null)
            {
                return elementBonus == 0
                    ? LudoCombatModifiers.None
                    : LudoCombatModifiers.None.WithElementBonus(elementBonus);
            }

            return inventory.BuildCombatModifiers(elementBonus);
        }

        public Token AttackerToken { get; }
        public Token DefenderToken { get; }
        public bool AttackerIsHuman { get; }
        public bool DefenderIsHuman { get; }

        /// <summary>Whether the elemental layer is on for this match.</summary>
        public bool ElementalRules { get; }
        public LudoCombatHand Attacker { get; }
        public LudoCombatHand Defender { get; }
        public LudoCombatPhase Phase { get; private set; }

        /// <summary>The hand currently being played, or null once resolved.</summary>
        public LudoCombatHand CurrentHand
        {
            get
            {
                return Phase switch
                {
                    LudoCombatPhase.AttackerTurn => Attacker,
                    LudoCombatPhase.DefenderTurn => Defender,
                    _ => null
                };
            }
        }

        /// <summary>Whether the person at the keyboard is the one deciding right now.</summary>
        public bool IsHumanTurn
        {
            get
            {
                return Phase switch
                {
                    LudoCombatPhase.AttackerTurn => AttackerIsHuman,
                    LudoCombatPhase.DefenderTurn => DefenderIsHuman,
                    _ => false
                };
            }
        }

        /// <summary>
        /// Score the defender has to beat. Only meaningful once the attacker
        /// has finished, which is exactly when the defender starts.
        /// </summary>
        public int ScoreToBeat => Attacker.Evaluate().Score;

        public bool TryReroll(int dieIndex)
        {
            LudoCombatHand hand = CurrentHand;
            return hand != null && hand.TryReroll(dieIndex);
        }

        /// <summary>Locks in the current side's hand and moves on.</summary>
        public void EndCurrentTurn()
        {
            Phase = Phase switch
            {
                LudoCombatPhase.AttackerTurn => LudoCombatPhase.DefenderTurn,
                LudoCombatPhase.DefenderTurn => LudoCombatPhase.Resolved,
                _ => LudoCombatPhase.Resolved
            };
        }

        public LudoCombatOutcome BuildOutcome()
        {
            return new LudoCombatOutcome(Attacker.Evaluate(), Defender.Evaluate());
        }

        public LudoCombatReport BuildReport()
        {
            return new LudoCombatReport(
                AttackerToken,
                DefenderToken,
                BuildOutcome(),
                AttackerIsHuman);
        }
    }
}
