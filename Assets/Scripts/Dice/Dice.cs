using System;
using UnityEngine;

namespace ElementalLudo.DiceSystem
{
    [SelectionBase]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Dice : MonoBehaviour
    {
        [Range(1, 6)]
        [SerializeField] private int value = 1;
        [SerializeField] private bool rollEnabled = true;
        [SerializeField] private DiceVisual visual;

        public int Value => value;
        public bool RollEnabled => rollEnabled;
        public event Action<int> Rolled;

        private void OnEnable()
        {
            RefreshVisual();
        }

        private void OnValidate()
        {
            value = Mathf.Clamp(value, 1, 6);
            RefreshVisual();
        }

        [ContextMenu("Roll Dice")]
        public void Roll()
        {
            TryRoll();
        }

        public bool TryRoll()
        {
            if (!rollEnabled)
            {
                return false;
            }

            SetValue(UnityEngine.Random.Range(1, 7));
            Rolled?.Invoke(value);
            return true;
        }

        public void SetRollEnabled(bool enabled)
        {
            rollEnabled = enabled;
        }

        public void SetValue(int newValue)
        {
            value = Mathf.Clamp(newValue, 1, 6);
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (visual == null)
            {
                visual = GetComponentInChildren<DiceVisual>(true);
            }

            if (visual != null)
            {
                visual.ShowValue(value);
            }
        }
    }
}
