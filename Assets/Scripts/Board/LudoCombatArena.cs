using System.Collections.Generic;
using ElementalLudo.DiceSystem;
using ElementalLudo.Gameplay;
using ElementalLudo.Tokens;
using UnityEngine;

namespace ElementalLudo.Board
{
    /// <summary>
    /// Stages a duel somewhere the board isn't: attacker below, defender
    /// above, five dice each, with its own camera.
    ///
    /// It lives far off in the same scene rather than in a scene of its own.
    /// The match is held in memory inside LudoGameController and there is no
    /// persistence layer yet, so loading a real scene would mean deciding what
    /// survives the trip — a lot of machinery to buy for a camera move. Pulling
    /// this into its own scene later is mechanical.
    ///
    /// A dedicated camera, rather than borrowing the board's, keeps it out of
    /// a fight with LudoBoardViewCamera and LudoBoardOrbitCamera, which both
    /// drive that one and always aim it at the origin.
    ///
    /// Reads the session every frame instead of being told what to animate: a
    /// die whose face no longer matches what it is showing tumbles to the new
    /// one. That means a reroll animates whether the player or the AI caused
    /// it, with nothing to keep in sync.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LudoCombatArena : MonoBehaviour
    {
        // Far enough that nothing on the board can drift into frame.
        private const float ArenaDistance = 500f;

        [Header("Layout")]
        [SerializeField] private float diceSpacing = 1.15f;
        [SerializeField] private float diceRowOffset = 1.9f;
        [SerializeField] private float tokenRowOffset = 4.4f;
        [SerializeField] private float tokenSize = 1.8f;

        [Header("Camera")]
        [SerializeField] private float cameraDistance = 14f;
        [SerializeField] private float cameraSize = 6.2f;
        [SerializeField] private Color background = new Color(0.05f, 0.05f, 0.09f);

        [Header("Dice Animation")]
        [Min(0.05f)]
        [SerializeField] private float tumbleDuration = 0.55f;
        [Min(0f)]
        [SerializeField] private float tumbleHop = 0.9f;
        [Min(0f)]
        [SerializeField] private float tumbleTurns = 2.5f;

        private readonly List<ArenaDie> attackerDice = new List<ArenaDie>(5);
        private readonly List<ArenaDie> defenderDice = new List<ArenaDie>(5);

        private LudoCombatSession session;
        private Camera arenaCamera;
        private Camera suspendedCamera;
        private Transform root;
        private GameObject attackerModel;
        private GameObject defenderModel;

        /// <summary>Where the arena sits, well away from the board.</summary>
        private Vector3 Origin => new Vector3(ArenaDistance, 0f, 0f);

        public void Show(LudoCombatSession combatSession)
        {
            if (combatSession == null)
            {
                Hide();
                return;
            }

            session = combatSession;
            EnsureRoot();
            BuildSide(attackerDice, combatSession.Attacker.Dice.Count, -diceRowOffset);
            BuildSide(defenderDice, combatSession.Defender.Dice.Count, diceRowOffset);
            BuildCombatants(combatSession);
            SyncDice(true);
            EnterArenaView();
        }

        public void Hide()
        {
            session = null;
            LeaveArenaView();
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (session == null)
            {
                return;
            }

            SyncDice(false);
            AdvanceTumbles();
        }

        private void OnDestroy()
        {
            LeaveArenaView();
            if (root != null)
            {
                Destroy(root.gameObject);
            }
        }

        // ------------------------------------------------------------------
        // View
        // ------------------------------------------------------------------

        private void EnterArenaView()
        {
            root.gameObject.SetActive(true);

            if (arenaCamera == null)
            {
                GameObject cameraObject = new GameObject("CombatCamera")
                {
                    hideFlags = HideFlags.DontSave
                };
                cameraObject.transform.SetParent(root, false);
                arenaCamera = cameraObject.AddComponent<Camera>();
                arenaCamera.orthographic = true;
                arenaCamera.clearFlags = CameraClearFlags.SolidColor;
                arenaCamera.backgroundColor = background;
            }

            arenaCamera.orthographicSize = cameraSize;
            // The dice show their value toward -Z, matching the board, so the
            // camera has to sit on that side and look back along +Z.
            arenaCamera.transform.position = Origin + new Vector3(0f, 0f, -cameraDistance);
            arenaCamera.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            arenaCamera.enabled = true;

            // Two enabled cameras would both render; park the board's.
            if (suspendedCamera == null)
            {
                Camera boardCamera = Camera.main;
                if (boardCamera != null && boardCamera != arenaCamera)
                {
                    suspendedCamera = boardCamera;
                    suspendedCamera.enabled = false;
                }
            }
        }

        private void LeaveArenaView()
        {
            if (arenaCamera != null)
            {
                arenaCamera.enabled = false;
            }

            if (suspendedCamera != null)
            {
                suspendedCamera.enabled = true;
                suspendedCamera = null;
            }
        }

        // ------------------------------------------------------------------
        // Building
        // ------------------------------------------------------------------

        private void EnsureRoot()
        {
            if (root != null)
            {
                return;
            }

            GameObject rootObject = new GameObject("CombatArena")
            {
                hideFlags = HideFlags.DontSave
            };
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.position = Origin;
            root = rootObject.transform;
        }

        private void BuildSide(List<ArenaDie> dice, int count, float rowY)
        {
            while (dice.Count < count)
            {
                dice.Add(CreateDie());
            }

            float span = (count - 1) * diceSpacing;
            for (int index = 0; index < dice.Count; index++)
            {
                bool used = index < count;
                dice[index].Root.gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                dice[index].Root.position = Origin + new Vector3(
                    index * diceSpacing - span * 0.5f,
                    rowY,
                    0f);
            }
        }

        private ArenaDie CreateDie()
        {
            GameObject dieRoot = new GameObject("CombatDie")
            {
                hideFlags = HideFlags.DontSave
            };
            dieRoot.transform.SetParent(root, false);

            // DiceVisual parks itself at its own rest offset relative to its
            // parent, so it needs a holder to be positioned by.
            GameObject visualObject = new GameObject("Visual")
            {
                hideFlags = HideFlags.DontSave
            };
            visualObject.transform.SetParent(dieRoot.transform, false);

            return new ArenaDie
            {
                Root = dieRoot.transform,
                Visual = visualObject.AddComponent<DiceVisual>(),
                Value = 0
            };
        }

        private void BuildCombatants(LudoCombatSession combatSession)
        {
            attackerModel = ReplaceCombatant(
                attackerModel,
                combatSession.AttackerToken,
                -tokenRowOffset);
            defenderModel = ReplaceCombatant(
                defenderModel,
                combatSession.DefenderToken,
                tokenRowOffset);
        }

        private GameObject ReplaceCombatant(GameObject existing, Token token, float rowY)
        {
            if (existing != null)
            {
                Destroy(existing);
            }

            if (token == null || token.OwnerStyle == null)
            {
                return null;
            }

            GameObject holder = new GameObject("Combatant")
            {
                hideFlags = HideFlags.DontSave
            };
            holder.transform.SetParent(root, false);
            holder.transform.position = Origin + new Vector3(0f, rowY, 0f);

            GameObject model = BuildCombatantModel(token.OwnerStyle, holder.transform);
            if (model == null)
            {
                Destroy(holder);
                return null;
            }

            return holder;
        }

        private GameObject BuildCombatantModel(PlayerStyle style, Transform parent)
        {
            if (style.TokenModel == null)
            {
                return null;
            }

            GameObject model = Instantiate(style.TokenModel, parent, false);
            model.hideFlags = HideFlags.DontSave;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(style.TokenModelEulerAngles);

            // Nothing here should be clickable; the board's raycasts are still
            // live even while the arena has the screen.
            foreach (Collider modelCollider in model.GetComponentsInChildren<Collider>(true))
            {
                modelCollider.enabled = false;
            }

            FitToSize(model);
            return model;
        }

        private void FitToSize(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (largest > Mathf.Epsilon)
            {
                model.transform.localScale *= tokenSize / largest;
            }
        }

        // ------------------------------------------------------------------
        // Dice
        // ------------------------------------------------------------------

        private void SyncDice(bool immediate)
        {
            SyncSide(attackerDice, session.Attacker.Dice, session.AttackerToken, immediate);
            SyncSide(defenderDice, session.Defender.Dice, session.DefenderToken, immediate);
        }

        private void SyncSide(
            List<ArenaDie> dice,
            IReadOnlyList<int> values,
            Token owner,
            bool immediate)
        {
            Color tint = owner != null && owner.OwnerStyle != null
                ? owner.OwnerStyle.TokenColor
                : Color.white;

            for (int index = 0; index < values.Count && index < dice.Count; index++)
            {
                ArenaDie die = dice[index];
                die.Visual.SetAccentColor(tint, 0.35f);

                if (die.Value == values[index])
                {
                    continue;
                }

                die.Value = values[index];
                if (immediate)
                {
                    die.Timer = 0f;
                    die.Visual.ShowValue(die.Value);
                    continue;
                }

                // A face that no longer matches means somebody rerolled it.
                die.Timer = tumbleDuration;
                die.StartRotation = die.Visual.transform.localRotation;
                die.Axis = Random.onUnitSphere;
                if (die.Axis.sqrMagnitude <= Mathf.Epsilon)
                {
                    die.Axis = Vector3.up;
                }

                die.Axis.Normalize();
            }
        }

        private void AdvanceTumbles()
        {
            foreach (ArenaDie die in attackerDice)
            {
                AdvanceTumble(die);
            }

            foreach (ArenaDie die in defenderDice)
            {
                AdvanceTumble(die);
            }
        }

        private void AdvanceTumble(ArenaDie die)
        {
            if (die.Timer <= 0f)
            {
                return;
            }

            die.Timer = Mathf.Max(0f, die.Timer - Time.unscaledDeltaTime);
            float progress = 1f - die.Timer / tumbleDuration;
            float eased = 1f - Mathf.Pow(1f - progress, 3f);

            if (die.Timer <= 0f)
            {
                die.Visual.ShowValue(die.Value);
                return;
            }

            // Extra spin unwinds to nothing, so the die eases onto its face
            // rather than snapping to it on the last frame.
            Quaternion landing = DiceVisual.RotationForValue(die.Value);
            Quaternion extraSpin = Quaternion.AngleAxis(
                360f * tumbleTurns * (1f - eased),
                die.Axis);
            Quaternion settle = Quaternion.Slerp(die.StartRotation, landing, eased);
            die.Visual.ShowRoll(extraSpin * settle, Mathf.Sin(progress * Mathf.PI) * tumbleHop);
        }

        private sealed class ArenaDie
        {
            public Transform Root;
            public DiceVisual Visual;
            public int Value;
            public float Timer;
            public Quaternion StartRotation;
            public Vector3 Axis;
        }
    }
}
