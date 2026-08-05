using System;
using System.Collections.Generic;
using ElementalLudo.Tokens;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// One node the run walked, as the lane index it landed on at a stage —
    /// enough to rebuild <see cref="LudoRunState.Path"/> against a map
    /// regenerated from <see cref="LudoRunSaveData.MapSeed"/>, since the map
    /// itself is never serialised.
    /// </summary>
    [Serializable]
    public sealed class LudoRunPathStepSaveData
    {
        public int stage;
        public int lane;
    }

    /// <summary>
    /// One held upgrade, saved as the numbers <see cref="LudoUpgrade"/> keeps
    /// itself (kind, level, target hand) plus the slot's own spent state,
    /// which the struct does not carry.
    /// </summary>
    [Serializable]
    public sealed class LudoRunUpgradeSaveData
    {
        public LudoUpgradeKind kind;
        public int level;
        public LudoDiceHand targetHand;
        public int chargesLeft;
        public bool armed;
    }

    /// <summary>
    /// A run in progress, reduced to what cannot be regenerated: the map's
    /// seed rather than its graph, and the lane path rather than node
    /// references — the same trick <see cref="LudoRunNode"/> already banks on.
    ///
    /// Only ever written while <see cref="LudoRunState.Status"/> is
    /// <see cref="LudoRunStatus.Choosing"/>, so there is never a duel, a match
    /// or a pending reward offer to account for.
    /// </summary>
    [Serializable]
    public sealed class LudoRunSaveData
    {
        public LudoElement element;
        public int mapSeed;
        public int stageCount;
        public int stage;
        public int lane;
        public int lives;
        public int refreshesLeft;
        public List<LudoRunPathStepSaveData> path = new List<LudoRunPathStepSaveData>();
        public List<LudoRunUpgradeSaveData> upgrades = new List<LudoRunUpgradeSaveData>();
    }

    /// <summary>Which elements Adventure has been unlocked with, as a plain list.</summary>
    [Serializable]
    public sealed class LudoElementProgressSaveData
    {
        public List<LudoElement> unlocked = new List<LudoElement>();
    }
}
