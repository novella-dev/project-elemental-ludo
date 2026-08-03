using System.Collections.Generic;
using UnityEngine;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// One side's dice while a duel is being played out: the faces showing and
    /// how many rerolls are left. Mutable on purpose — the player pokes at it
    /// through the combat UI and the AI through <see cref="PlayOutWithAI"/>,
    /// and both go through the same methods so neither can do something the
    /// other couldn't.
    /// </summary>
    public sealed class LudoCombatHand
    {
        public const int DefaultRerolls = 3;

        private readonly int[] dice;

        public LudoCombatHand(
            int diceCount = LudoCombatResolver.DefaultDiceCount,
            int rerolls = DefaultRerolls,
            int elementBonus = 0)
        {
            dice = new int[Mathf.Max(1, diceCount)];
            RerollsLeft = Mathf.Max(0, rerolls);
            ElementBonus = elementBonus;
            ThrowAll();
        }

        public IReadOnlyList<int> Dice => dice;
        public int RerollsLeft { get; private set; }
        public bool CanReroll => RerollsLeft > 0;

        /// <summary>
        /// The elemental edge this side carries into the duel, fixed for its
        /// whole length. Held here rather than passed to <see cref="Evaluate"/>
        /// so the score is right everywhere it is read — the arena and the
        /// panel both call Evaluate with no arguments, and either could
        /// otherwise show a total the fight is not actually using.
        /// </summary>
        public int ElementBonus { get; }

        /// <summary>Opening throw. Doesn't cost a reroll.</summary>
        public void ThrowAll()
        {
            for (int index = 0; index < dice.Length; index++)
            {
                dice[index] = UnityEngine.Random.Range(1, 7);
            }
        }

        /// <summary>Spends one reroll on a single die. False if it couldn't.</summary>
        public bool TryReroll(int index)
        {
            if (RerollsLeft <= 0 || index < 0 || index >= dice.Length)
            {
                return false;
            }

            dice[index] = UnityEngine.Random.Range(1, 7);
            RerollsLeft--;
            return true;
        }

        /// <summary>
        /// Spends the rerolls the way the AI would. Stops early when the
        /// suggestion is to stand pat, so leftover charges are deliberate
        /// rather than wasted.
        /// </summary>
        public void PlayOutWithAI()
        {
            while (RerollsLeft > 0)
            {
                int index = LudoCombatResolver.SuggestReroll(dice);
                if (index < 0 || !TryReroll(index))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Scores the hand as it stands. Snapshots the dice, so a result kept
        /// for display doesn't change under a later reroll.
        /// </summary>
        public LudoCombatRoll Evaluate()
        {
            int[] snapshot = new int[dice.Length];
            System.Array.Copy(dice, snapshot, dice.Length);
            return LudoCombatResolver.Evaluate(snapshot, ElementBonus);
        }
    }
}
