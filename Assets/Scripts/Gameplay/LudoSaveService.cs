using System;
using System.Collections.Generic;
using System.IO;
using ElementalLudo.Tokens;
using UnityEngine;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Reads and writes the two things Adventure needs to survive the
    /// process ending: the run in progress, and the element unlocks that
    /// outlive it. Two files rather than one, because their lifetimes
    /// differ — a run's file is deleted the moment it ends, win or lose,
    /// while the progress file is never deleted, only grown.
    ///
    /// JSON via <see cref="JsonUtility"/> rather than anything reflection-
    /// heavier: the save data is a handful of ints and small lists, and this
    /// is the one place in the project a save format is decided at all.
    /// </summary>
    public static class LudoSaveService
    {
        private const string RunFileName = "run_save.json";
        private const string ProgressFileName = "element_progress.json";

        private static string RunPath =>
            Path.Combine(Application.persistentDataPath, RunFileName);

        private static string ProgressPath =>
            Path.Combine(Application.persistentDataPath, ProgressFileName);

        public static bool HasSavedRun() => File.Exists(RunPath);

        /// <summary>
        /// Writes the run as-is. Callers only ever call this while
        /// <see cref="LudoRunState.Status"/> is <see cref="LudoRunStatus.Choosing"/>
        /// — the one point with no duel, match or reward offer in flight to
        /// lose by saving.
        /// </summary>
        public static void SaveRun(LudoRunState run)
        {
            if (run == null)
            {
                return;
            }

            LudoRunSaveData data = new LudoRunSaveData
            {
                element = run.Element,
                mapSeed = run.Map.Seed,
                stageCount = run.Map.StageCount,
                stage = run.Stage,
                lane = run.Lane,
                lives = run.Lives,
                refreshesLeft = run.RefreshesLeft,
            };

            foreach (LudoRunNode node in run.Path)
            {
                data.path.Add(new LudoRunPathStepSaveData
                {
                    stage = node.Stage,
                    lane = node.Lane
                });
            }

            foreach (LudoUpgradeSlot slot in run.Upgrades.Slots)
            {
                data.upgrades.Add(new LudoRunUpgradeSaveData
                {
                    kind = slot.Upgrade.Kind,
                    level = slot.Upgrade.Level,
                    targetHand = slot.Upgrade.TargetHand,
                    chargesLeft = slot.ChargesLeft,
                    armed = slot.Armed
                });
            }

            WriteFile(RunPath, JsonUtility.ToJson(data));
        }

        public static void DeleteRun()
        {
            if (File.Exists(RunPath))
            {
                File.Delete(RunPath);
            }
        }

        /// <summary>
        /// Rebuilds the saved run, or null when there is none or the file on
        /// disk cannot be read back — a corrupt or foreign save should never
        /// crash the menu, only fail to offer a run to continue.
        /// </summary>
        public static LudoRunState LoadRun()
        {
            LudoRunSaveData data = ReadFile<LudoRunSaveData>(RunPath);
            if (data == null || data.path.Count == 0)
            {
                return null;
            }

            // Regenerating the map and replaying the saved path both index
            // straight into it, so a save left over from a build where the
            // map generator or stage count has since changed can walk off
            // the end of a stage. That is exactly the kind of "foreign save"
            // this method promises never to crash on, so it gets the same
            // catch-and-fail-quiet treatment as the JSON parse above.
            try
            {
                LudoRunMap map = LudoRunMap.Generate(data.stageCount, data.mapSeed, data.element);

                LudoRunState run = LudoRunState.Restore(
                    data.element,
                    map,
                    data.stage,
                    data.lane,
                    data.lives,
                    data.refreshesLeft,
                    data.path);

                foreach (LudoRunUpgradeSaveData upgrade in data.upgrades)
                {
                    run.Upgrades.RestoreSlot(
                        new LudoUpgrade(upgrade.kind, upgrade.level, upgrade.targetHand),
                        upgrade.chargesLeft,
                        upgrade.armed);
                }

                return run;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"No se pudo reconstruir la partida guardada: {exception.Message}");
                return null;
            }
        }

        public static void SaveProgress(LudoElementProgress progress)
        {
            if (progress == null)
            {
                return;
            }

            LudoElementProgressSaveData data = new LudoElementProgressSaveData();
            data.unlocked.AddRange(progress.Unlocked());
            WriteFile(ProgressPath, JsonUtility.ToJson(data));
        }

        /// <summary>Applies the saved unlocks onto a fresh <see cref="LudoElementProgress"/>, if any were saved.</summary>
        public static void LoadProgressInto(LudoElementProgress progress)
        {
            LudoElementProgressSaveData data = ReadFile<LudoElementProgressSaveData>(ProgressPath);
            if (data == null || data.unlocked.Count == 0)
            {
                return;
            }

            progress.Restore(data.unlocked);
        }

        private static void WriteFile(string path, string json)
        {
            try
            {
                File.WriteAllText(path, json);
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"No se pudo guardar la partida en {path}: {exception.Message}");
            }
        }

        private static T ReadFile<T>(string path) where T : class
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<T>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"No se pudo leer la partida guardada en {path}: {exception.Message}");
                return null;
            }
        }
    }
}
