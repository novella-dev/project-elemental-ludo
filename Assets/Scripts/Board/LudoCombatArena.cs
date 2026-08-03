using System.Collections.Generic;
using ElementalLudo.DiceSystem;
using ElementalLudo.Gameplay;
using ElementalLudo.Tokens;
using UnityEngine;
using UnityEngine.Rendering;

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
        private const string DiceShaderName = "Elemental Ludo/Token";
        private const string FloorShaderName = "Elemental Ludo/Board Vertex Color";

        // Just behind the pieces, which stand at Z <= 0.
        private const float FloorDepth = 0.1f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Layout")]
        [SerializeField] private float diceSpacing = 1.15f;
        [SerializeField] private float diceRowOffset = 1.9f;
        [SerializeField] private float tokenRowOffset = 4.4f;
        [SerializeField] private float tokenSize = 1.8f;

        [Header("Camera")]
        [SerializeField] private float cameraDistance = 18f;
        [Tooltip("Degrees away from straight-on. Higher tilts further toward looking down at the ground.")]
        [Range(0f, 70f)]
        [SerializeField] private float cameraTilt = 38f;
        [Tooltip("Perspective, not orthographic, so the far side genuinely reads as further away.")]
        [SerializeField] private float cameraFieldOfView = 34f;
        [SerializeField] private Color background = new Color(0.05f, 0.05f, 0.09f);

        [Header("Arena Floor")]
        [SerializeField] private float floorRadius = 9f;
        [SerializeField] private Color sandInner = new Color(0.62f, 0.45f, 0.26f);
        [SerializeField] private Color sandOuter = new Color(0.44f, 0.30f, 0.16f);
        [SerializeField] private Color wallColor = new Color(0.30f, 0.20f, 0.12f);
        [SerializeField] private Color wallTop = new Color(0.46f, 0.33f, 0.21f);

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
        private Material diceBodyMaterial;
        private Material dicePipMaterial;
        private Material floorMaterial;
        private GameObject floorObject;

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
            EnsureFloor();

            // Laid out by who is watching, not by who attacks: the player is
            // the defender half the time, and they should still be the side
            // nearest the camera.
            float attackerRow = combatSession.AttackerIsHuman
                ? -diceRowOffset
                : diceRowOffset;
            BuildSide(attackerDice, combatSession.Attacker.Dice.Count, attackerRow);
            BuildSide(defenderDice, combatSession.Defender.Dice.Count, -attackerRow);
            BuildCombatants(combatSession, attackerRow);
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

            DestroyGenerated(diceBodyMaterial);
            DestroyGenerated(dicePipMaterial);
            DestroyGenerated(floorMaterial);
        }

        private static void DestroyGenerated(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
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
                arenaCamera.orthographic = false;
                arenaCamera.clearFlags = CameraClearFlags.SolidColor;
                arenaCamera.backgroundColor = background;
            }

            arenaCamera.fieldOfView = cameraFieldOfView;

            // Dice show their value toward -Z, matching the board, so the
            // camera stays on that side. Tilting it down the -Y axis turns the
            // XY plane the pieces stand on into visible ground, which is what
            // gives the 2.5D read — and it puts the near side genuinely closer
            // to the lens, so the player's own pieces sit forward.
            float radians = cameraTilt * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                0f,
                -Mathf.Sin(radians) * cameraDistance,
                -Mathf.Cos(radians) * cameraDistance);
            arenaCamera.transform.position = Origin + offset;
            arenaCamera.transform.rotation = Quaternion.LookRotation(
                -offset.normalized,
                new Vector3(0f, Mathf.Cos(radians), -Mathf.Sin(radians)));
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

        /// <summary>
        /// The arena itself: a sandy oval with a raised wall around it, built
        /// the same procedural vertex-coloured way as the board. Sits just
        /// behind the pieces in Z, so with the camera tilted it reads as the
        /// ground they're standing on.
        /// </summary>
        private void EnsureFloor()
        {
            if (floorObject != null)
            {
                return;
            }

            floorObject = new GameObject("ArenaFloor")
            {
                hideFlags = HideFlags.DontSave
            };
            floorObject.transform.SetParent(root, false);
            floorObject.transform.localPosition = Vector3.zero;

            List<Vector3> vertices = new List<Vector3>(512);
            List<Color> colors = new List<Color>(512);
            List<int> triangles = new List<int>(1024);

            const int segments = 48;
            const float squash = 0.72f;
            float wallHeight = floorRadius * 0.16f;

            // Ground: a fan from the middle out, darkening toward the edge.
            for (int segment = 0; segment < segments; segment++)
            {
                float a0 = Mathf.PI * 2f * segment / segments;
                float a1 = Mathf.PI * 2f * (segment + 1) / segments;

                int first = vertices.Count;
                vertices.Add(new Vector3(0f, 0f, FloorDepth));
                vertices.Add(EllipsePoint(a0, floorRadius, squash, FloorDepth));
                vertices.Add(EllipsePoint(a1, floorRadius, squash, FloorDepth));
                colors.Add(sandInner);
                colors.Add(sandOuter);
                colors.Add(sandOuter);

                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 1);
            }

            // Wall: a band standing up out of the ground at the rim.
            for (int segment = 0; segment < segments; segment++)
            {
                float a0 = Mathf.PI * 2f * segment / segments;
                float a1 = Mathf.PI * 2f * (segment + 1) / segments;

                Vector3 low0 = EllipsePoint(a0, floorRadius, squash, FloorDepth);
                Vector3 low1 = EllipsePoint(a1, floorRadius, squash, FloorDepth);
                Vector3 high0 = EllipsePoint(a0, floorRadius * 1.08f, squash, FloorDepth - wallHeight);
                Vector3 high1 = EllipsePoint(a1, floorRadius * 1.08f, squash, FloorDepth - wallHeight);

                int first = vertices.Count;
                vertices.Add(low0);
                vertices.Add(low1);
                vertices.Add(high1);
                vertices.Add(high0);
                colors.Add(wallColor);
                colors.Add(wallColor);
                colors.Add(wallTop);
                colors.Add(wallTop);

                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 1);
                triangles.Add(first);
                triangles.Add(first + 3);
                triangles.Add(first + 2);
            }

            Mesh mesh = new Mesh
            {
                name = "ArenaFloor",
                hideFlags = HideFlags.DontSave
            };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            floorObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer meshRenderer = floorObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            Shader shader = Shader.Find(FloorShaderName);
            if (shader != null)
            {
                floorMaterial = new Material(shader)
                {
                    name = "Arena Floor",
                    hideFlags = HideFlags.DontSave
                };
                meshRenderer.sharedMaterial = floorMaterial;
            }
        }

        private static Vector3 EllipsePoint(float angle, float radius, float squash, float depth)
        {
            return new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius * squash,
                depth);
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

            DiceVisual visual = visualObject.AddComponent<DiceVisual>();

            // DiceVisual never assigns materials — the Dice prefab supplies
            // them from the Inspector. Built at runtime it would keep Unity's
            // default one, which renders magenta under URP, so it gets the
            // pair its mesh expects: body first, pips second.
            visualObject.GetComponent<MeshRenderer>().sharedMaterials =
                new[] { GetDiceBodyMaterial(), GetDicePipMaterial() };

            return new ArenaDie
            {
                Root = dieRoot.transform,
                Visual = visual,
                Value = 0
            };
        }

        private Material GetDiceBodyMaterial()
        {
            if (diceBodyMaterial == null)
            {
                diceBodyMaterial = CreateDiceMaterial(
                    "Combat Die Body",
                    new Color(0.93f, 0.92f, 0.88f));
            }

            return diceBodyMaterial;
        }

        private Material GetDicePipMaterial()
        {
            if (dicePipMaterial == null)
            {
                dicePipMaterial = CreateDiceMaterial(
                    "Combat Die Pips",
                    new Color(0.035f, 0.04f, 0.045f));
            }

            return dicePipMaterial;
        }

        /// <summary>Matches the colours the LudoDiceMaterial assets carry.</summary>
        private static Material CreateDiceMaterial(string name, Color color)
        {
            Shader shader = Shader.Find(DiceShaderName);
            if (shader == null)
            {
                Debug.LogError($"CombatArena: shader '{DiceShaderName}' not found.");
                return null;
            }

            Material material = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.DontSave
            };
            material.SetColor(BaseColorId, color);
            return material;
        }

        private void BuildCombatants(LudoCombatSession combatSession, float attackerRow)
        {
            float sign = Mathf.Sign(attackerRow);
            attackerModel = ReplaceCombatant(
                attackerModel,
                combatSession.AttackerToken,
                tokenRowOffset * sign);
            defenderModel = ReplaceCombatant(
                defenderModel,
                combatSession.DefenderToken,
                -tokenRowOffset * sign);
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

            FitToSize(model, parent);
            return model;
        }

        /// <summary>
        /// Scales the model to a readable size and then drops it onto the
        /// holder's position. The recentre matters: these are imported GLBs
        /// whose pivots sit wherever the artist left them, and without it a
        /// model can end up scaled correctly but far outside the frame.
        /// </summary>
        private void FitToSize(GameObject model, Transform holder)
        {
            if (!TryGetWorldBounds(model, out Bounds bounds))
            {
                return;
            }

            float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (largest > Mathf.Epsilon)
            {
                model.transform.localScale *= tokenSize / largest;
            }

            if (TryGetWorldBounds(model, out bounds))
            {
                // Centre on the holder, then lift so the model rests on the
                // arena floor instead of being buried half-way into it.
                Vector3 target = holder.position;
                target.z = FloorDepth - bounds.size.z * 0.5f;
                model.transform.position += target - bounds.center;
            }
        }

        private static bool TryGetWorldBounds(GameObject model, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return false;
            }

            bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return true;
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
