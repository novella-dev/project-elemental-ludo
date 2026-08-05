using System.Collections.Generic;
using ElementalLudo.Tokens;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Which elements Adventure has been unlocked with, in the Slay the Spire
    /// sense: you start with one and earn the rest by finishing runs.
    ///
    /// This is the only state in the game meant to outlive a run, and right now
    /// it does not outlive the process — A7 gives it a file. Kept apart from
    /// <see cref="LudoRunState"/> for exactly that reason: they have different
    /// lifetimes, and mixing them would mean A7 has to untangle them first.
    /// </summary>
    public sealed class LudoElementProgress
    {
        /// <summary>
        /// Unlock order, and so the order a run's reward arrives in. Fire opens
        /// the game because its matchup beats Plant, the element the earliest
        /// AI seats are most likely to be holding.
        /// </summary>
        private static readonly LudoElement[] Order =
        {
            LudoElement.Fire,
            LudoElement.Water,
            LudoElement.Lightning,
            LudoElement.Plant
        };

        private readonly HashSet<LudoElement> unlocked =
            new HashSet<LudoElement> { Order[0] };

        public IReadOnlyList<LudoElement> UnlockOrder => Order;

        public bool IsUnlocked(LudoElement element) => unlocked.Contains(element);

        public int UnlockedCount => unlocked.Count;

        public bool AllUnlocked => unlocked.Count >= Order.Length;

        /// <summary>Every element the player may currently start a run with.</summary>
        public IEnumerable<LudoElement> Unlocked()
        {
            foreach (LudoElement element in Order)
            {
                if (unlocked.Contains(element))
                {
                    yield return element;
                }
            }
        }

        /// <summary>
        /// Grants the next locked element, returning it, or null when there is
        /// nothing left to give. Called on finishing a run, whichever element
        /// it was finished with — the reward is the next one in order rather
        /// than one tied to what was played, so no run can be a dead end.
        /// </summary>
        public LudoElement? UnlockNext()
        {
            foreach (LudoElement element in Order)
            {
                if (unlocked.Add(element))
                {
                    return element;
                }
            }

            return null;
        }

        public void Reset()
        {
            unlocked.Clear();
            unlocked.Add(Order[0]);
        }

        /// <summary>
        /// Replaces the unlocked set with a saved one, for
        /// <see cref="LudoSaveService"/> alone. Falls back to the starting
        /// element if the save somehow held none, so the invariant that at
        /// least one element is always playable survives a bad file.
        /// </summary>
        internal void Restore(IEnumerable<LudoElement> unlockedElements)
        {
            unlocked.Clear();
            foreach (LudoElement element in unlockedElements)
            {
                unlocked.Add(element);
            }

            if (unlocked.Count == 0)
            {
                unlocked.Add(Order[0]);
            }
        }
    }
}
