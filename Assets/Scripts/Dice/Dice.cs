using System;
using System.Collections;
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

        [Header("Roll Animation")]
        [Min(0f)]
        [Tooltip("Seconds the die tumbles before the result is announced. Zero rolls instantly.")]
        [SerializeField] private float rollDuration = 0.6f;
        [Min(0f)]
        [Tooltip("How high the die hops at the middle of the roll.")]
        [SerializeField] private float rollHopHeight = 0.6f;
        [Min(0f)]
        [Tooltip("Full tumbles spun before settling on the result.")]
        [SerializeField] private float rollSpinTurns = 2.5f;

        [Header("Active Player Tint")]
        [Range(0f, 1f)]
        [Tooltip("How far the die body shifts toward the active player's color.")]
        [SerializeField] private float accentStrength = 0.4f;

        private Coroutine rollRoutine;

        public int Value => value;
        public bool RollEnabled => rollEnabled;
        public bool IsRolling => rollRoutine != null;
        public event Action<int> Rolled;

        private void OnEnable()
        {
            RefreshVisual();
        }

        private void OnDisable()
        {
            // Unity already stopped the coroutine; drop the stale handle so a
            // later roll isn't blocked by it.
            rollRoutine = null;
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

        /// <summary>
        /// Starts a roll. With an animation the result is only announced via
        /// <see cref="Rolled"/> once the die has settled, so the rest of the
        /// game reacts to the number at the moment the player can read it.
        /// </summary>
        public bool TryRoll()
        {
            if (!rollEnabled || rollRoutine != null)
            {
                return false;
            }

            int rolledValue = UnityEngine.Random.Range(1, 7);

            // Edit mode has no coroutines, and a zero duration means the
            // animation is switched off.
            if (!Application.isPlaying || rollDuration <= Mathf.Epsilon)
            {
                SetValue(rolledValue);
                Rolled?.Invoke(rolledValue);
                return true;
            }

            // Latch the die shut for the whole animation so a second click
            // can't start another roll before this result is announced.
            rollEnabled = false;
            rollRoutine = StartCoroutine(RollRoutine(rolledValue));
            return true;
        }

        /// <summary>
        /// Abandons an in-flight roll without announcing a result. Needed if
        /// the game is restarted mid-animation, otherwise the pending
        /// <see cref="Rolled"/> would land on the fresh game.
        /// </summary>
        public void CancelRoll()
        {
            if (rollRoutine != null)
            {
                StopCoroutine(rollRoutine);
                rollRoutine = null;
            }

            RefreshVisual();
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

        /// <summary>Tints the die toward the active player's color.</summary>
        public void SetAccentColor(Color color)
        {
            if (EnsureVisual())
            {
                visual.SetAccentColor(color, accentStrength);
            }
        }

        private IEnumerator RollRoutine(int rolledValue)
        {
            bool hasVisual = EnsureVisual();
            Quaternion start = hasVisual
                ? visual.transform.localRotation
                : Quaternion.identity;
            Quaternion landing = DiceVisual.RotationForValue(rolledValue);

            Vector3 spinAxis = UnityEngine.Random.onUnitSphere;
            if (spinAxis.sqrMagnitude <= Mathf.Epsilon)
            {
                spinAxis = Vector3.up;
            }

            spinAxis.Normalize();
            float totalSpin = 360f * rollSpinTurns;

            float elapsed = 0f;
            while (elapsed < rollDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / rollDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);

                if (hasVisual)
                {
                    // The extra spin unwinds to nothing as `eased` reaches 1,
                    // so the die eases into the exact landing pose rather than
                    // snapping to it on the last frame.
                    Quaternion extraSpin = Quaternion.AngleAxis(
                        totalSpin * (1f - eased),
                        spinAxis);
                    Quaternion settle = Quaternion.Slerp(start, landing, eased);
                    float hop = Mathf.Sin(progress * Mathf.PI) * rollHopHeight;
                    visual.ShowRoll(extraSpin * settle, hop);
                }

                yield return null;
            }

            rollRoutine = null;
            value = rolledValue;
            RefreshVisual();
            Rolled?.Invoke(value);
        }

        private void RefreshVisual()
        {
            if (EnsureVisual())
            {
                visual.ShowValue(value);
            }
        }

        private bool EnsureVisual()
        {
            if (visual == null)
            {
                visual = GetComponentInChildren<DiceVisual>(true);
            }

            return visual != null;
        }
    }
}
