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
            int diceCount = LudoCombatResolver.DefaultDiceCount,
            int rerolls = LudoCombatHand.DefaultRerolls)
        {
            AttackerToken = attackerToken;
            DefenderToken = defenderToken;
            AttackerIsHuman = attackerIsHuman;
            DefenderIsHuman = defenderIsHuman;
            Attacker = new LudoCombatHand(diceCount, rerolls);
            Defender = new LudoCombatHand(diceCount, rerolls);
            Phase = LudoCombatPhase.AttackerTurn;
        }

        public Token AttackerToken { get; }
        public Token DefenderToken { get; }
        public bool AttackerIsHuman { get; }
        public bool DefenderIsHuman { get; }
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
            return new LudoCombatReport(AttackerToken, DefenderToken, BuildOutcome());
        }
    }
}
