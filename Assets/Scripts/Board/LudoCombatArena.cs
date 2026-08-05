using System.Collections.Generic;
using ElementalLudo.DiceSystem;
using ElementalLudo.Gameplay;
using ElementalLudo.Tokens;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
        // The board's own shader: it already bands the same key light into
        // three flat steps and reads vertex colour, which is the cel look the
        // stonework wants. A dedicated arena shader was tried and silently
        // failed to resolve, leaving the mesh material-less and magenta.
        private const string FloorShaderName = "Elemental Ludo/Board Vertex Color";

        // Just behind the pieces, which stand at Z <= 0.
        private const float FloorDepth = 0.1f;

        // The arena is built on the board's convention: the pieces stand on the
        // XY plane and -Z is up, so "taller" means more negative.
        private static readonly Vector3 Up = new Vector3(0f, 0f, -1f);

        /// <summary>Index into <see cref="Presets"/>: the diagonal, chosen by the user.</summary>
        private const int DefaultPreset = 2;
        private const float ViewBlendDuration = 0.45f;

        /// <summary>How far a die rises when the pointer is over it.</summary>
        private const float HoverLift = 0.32f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Ink = LudoBoardVisualStyle.Ink;
        private static readonly Color Paper = LudoBoardVisualStyle.Paper;

        /// <summary>
        /// The framings that were tried. The arena is fixed on
        /// <see cref="DefaultPreset"/> now that one was picked, but the rest
        /// are kept named so switching is a one-line change — they cannot be
        /// Inspector fields, since the arena builds itself at runtime and never
        /// lands in a prefab.
        /// </summary>
        private static readonly ArenaViewPreset[] Presets =
        {
            new ArenaViewPreset("1 · Cenital", 0f, 12f, 25f, 34f),
            new ArenaViewPreset("2 · 2.5D", 0f, 38f, 26f, 34f),
            new ArenaViewPreset("3 · Diagonal", 32f, 43f, 29f, 38f),
            new ArenaViewPreset("4 · Tribuna", 0f, 62f, 24f, 42f)
        };

        [Header("Layout")]
        [SerializeField] private float diceSpacing = 1.15f;
        [SerializeField] private float diceRowOffset = 1.9f;
        [SerializeField] private float tokenRowOffset = 4.4f;
        [SerializeField] private float tokenSize = 2.2f;

        [Header("Camera")]
        [SerializeField] private Color background =
            LudoBoardVisualStyle.Lighten(LudoBoardVisualStyle.SafeCell, 0.28f);

        [Header("Arena Floor")]
        [SerializeField] private float floorRadius = 9f;
        [SerializeField] private Color sandInner = LudoBoardVisualStyle.SandLight;
        [SerializeField] private Color sandOuter = LudoBoardVisualStyle.SandMid;
        [SerializeField] private Color wallColor = LudoBoardVisualStyle.SandDark;
        [SerializeField] private Color wallTop = LudoBoardVisualStyle.SandMid;

        [Header("Coliseum")]
        [Min(2)]
        [SerializeField] private int columnCount = 9;
        [Tooltip("Degrees of the ring the columns cover, centred on the far side. The rest is left open so the near ones never stand between the camera and the dice.")]
        [Range(60f, 340f)]
        [SerializeField] private float columnArc = 160f;
        [SerializeField] private float columnRadius = 0.42f;
        [SerializeField] private float columnHeight = 4.0f;
        [SerializeField] private float archRise = 1.05f;
        [SerializeField] private float archThickness = 0.36f;
        [SerializeField] private float archDepth = 0.52f;
        // Kept bright: the shader's darkest band multiplies by 0.62, and any
        // face turned away from the key light lands there. Stone that reads
        // well lit has to survive that cut without going to mud.
        [SerializeField] private Color stoneLower = LudoBoardVisualStyle.SandDark;
        [SerializeField] private Color stoneUpper = LudoBoardVisualStyle.SandLight;

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
        private Transform root;
        private GameObject attackerModel;
        private GameObject defenderModel;
        private Material diceBodyMaterial;
        private Material dicePipMaterial;
        private Material floorMaterial;
        private Material stoneMaterial;
        private Mesh floorMesh;
        private GameObject floorObject;
        private GameObject stoneObject;

        // Board camera, put back exactly as found when the duel ends.
        private readonly LudoBoardCameraSuspender boardCamera =
            new LudoBoardCameraSuspender();

        private int cameraPreset = DefaultPreset;
        private float viewYaw = Presets[DefaultPreset].Yaw;
        private float viewTilt = Presets[DefaultPreset].Tilt;
        private float viewDistance = Presets[DefaultPreset].Distance;
        private float viewFieldOfView = Presets[DefaultPreset].FieldOfView;
        private float blendFrom;
        private float blendProgress = 1f;
        private ArenaViewPreset blendStart;

        private int hoveredDie = -1;

        /// <summary>
        /// Raised with the index of a die the player clicked to reroll,
        /// returning whether it was actually spent. The arena spots the click
        /// because it owns the camera the player is looking through, but it
        /// never touches the hand itself — the controller decides whether the
        /// reroll is legal, and the answer is what the arena animates off.
        /// </summary>
        public System.Func<int, bool> RerollRequested;

        /// <summary>Where the arena sits, well away from the board.</summary>
        private Vector3 Origin => new Vector3(ArenaDistance, 0f, 0f);

        public int CameraPreset => cameraPreset;

        /// <summary>
        /// Swings the camera to one of the four framings. Blended rather than
        /// snapped so the player can tell the arena turned rather than cut.
        /// </summary>
        public void SetCameraPreset(int index)
        {
            if (index < 0 || index >= Presets.Length)
            {
                return;
            }

            cameraPreset = index;
            blendStart = new ArenaViewPreset(
                string.Empty,
                viewYaw,
                viewTilt,
                viewDistance,
                viewFieldOfView);
            blendProgress = 0f;
            blendFrom = 0f;
        }

        public void Show(LudoCombatSession combatSession)
        {
            if (combatSession == null)
            {
                Hide();
                return;
            }

            session = combatSession;
            EnsureRoot();

            // Laid out by who is watching, not by who attacks: the player is
            // the defender half the time, and they should still be the side
            // nearest the camera.
            float attackerRow = combatSession.AttackerIsHuman
                ? -diceRowOffset
                : diceRowOffset;
            EnsureArena(combatSession, attackerRow);
            BuildSide(attackerDice, combatSession.Attacker.Dice.Count, attackerRow);
            BuildSide(defenderDice, combatSession.Defender.Dice.Count, -attackerRow);
            BuildCombatants(combatSession, attackerRow);
            // Rolled in, not laid out already thrown: every die starts at zero
            // and so differs from its face, which sends the whole handful
            // tumbling the moment the arena opens.
            SyncDice(false);
            EnterArenaView();
        }

        public void Hide()
        {
            ClearHover();
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

            AdvanceViewBlend();
            SyncDice(false);
            AdvanceTumbles();

            // After the sync, which rewrites every die's tint each frame and
            // would otherwise wipe the highlight straight back off.
            HandleDiceInput();
        }

        // ------------------------------------------------------------------
        // Picking
        // ------------------------------------------------------------------

        /// <summary>
        /// Lets the player reroll by clicking the die itself. Only their own
        /// side is live, and only while they still have rerolls left — a die
        /// that cannot be thrown again never lights up, so the highlight
        /// doubles as the affordance.
        /// </summary>
        private void HandleDiceInput()
        {
            List<ArenaDie> side = ActiveHumanSide();
            if (side == null)
            {
                ClearHover();
                return;
            }

            int hit = FindDieUnderPointer(side);
            SetHover(side, hit);

            Mouse mouse = Mouse.current;
            if (hit < 0 ||
                mouse == null ||
                !mouse.leftButton.wasPressedThisFrame ||
                RerollRequested == null)
            {
                return;
            }

            if (!RerollRequested(hit))
            {
                return;
            }

            // Animated off the reroll having happened, not off the face
            // changing. One throw in six comes up the same number it already
            // showed, and inferring the spin from the value left those looking
            // exactly like a click that charged a reroll and did nothing.
            StartTumble(side[hit], session.CurrentHand.Dice[hit]);
        }

        /// <summary>The dice the player may click right now, or null.</summary>
        private List<ArenaDie> ActiveHumanSide()
        {
            if (session == null || arenaCamera == null || !session.IsHumanTurn)
            {
                return null;
            }

            LudoCombatHand hand = session.CurrentHand;
            if (hand == null || !hand.CanReroll)
            {
                return null;
            }

            return session.Phase == LudoCombatPhase.AttackerTurn
                ? attackerDice
                : defenderDice;
        }

        private int FindDieUnderPointer(List<ArenaDie> side)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return -1;
            }

            Ray ray = arenaCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                return -1;
            }

            for (int index = 0; index < side.Count; index++)
            {
                Transform dieRoot = side[index].Root;
                if (dieRoot != null && hit.collider.transform.IsChildOf(dieRoot))
                {
                    return index;
                }
            }

            return -1;
        }

        private void SetHover(List<ArenaDie> side, int index)
        {
            if (hoveredDie >= 0 && hoveredDie != index)
            {
                ClearHover();
            }

            hoveredDie = index;
            if (index < 0 || index >= side.Count)
            {
                return;
            }

            ArenaDie die = side[index];
            die.Root.position = die.BasePosition + Up * HoverLift;
            die.Visual.SetAccentColor(
                Color.Lerp(die.Tint, Color.white, 0.55f),
                0.8f);
        }

        private void ClearHover()
        {
            if (hoveredDie < 0)
            {
                return;
            }

            // Cheaper than tracking which list it came from, and a die that is
            // not lifted is unaffected by being put back down.
            RestoreRestingPositions(attackerDice);
            RestoreRestingPositions(defenderDice);
            hoveredDie = -1;
        }

        private static void RestoreRestingPositions(List<ArenaDie> dice)
        {
            foreach (ArenaDie die in dice)
            {
                if (die.Root != null)
                {
                    die.Root.position = die.BasePosition;
                }
            }
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
            DestroyGenerated(stoneMaterial);
            DestroyGenerated(floorMesh);
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

                UniversalAdditionalCameraData cameraData =
                    arenaCamera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
                cameraData.antialiasing =
                    AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
            }

            boardCamera.Suspend(arenaCamera);
            ApplyCameraPose();
            arenaCamera.enabled = true;
        }

        private void LeaveArenaView()
        {
            if (arenaCamera != null)
            {
                arenaCamera.enabled = false;
            }

            boardCamera.Restore();
        }

        private void AdvanceViewBlend()
        {
            if (blendProgress >= 1f)
            {
                return;
            }

            blendFrom += Time.unscaledDeltaTime;
            blendProgress = Mathf.Clamp01(blendFrom / ViewBlendDuration);
            float eased =
                blendProgress * blendProgress * (3f - 2f * blendProgress);

            ArenaViewPreset target = Presets[cameraPreset];
            viewYaw = Mathf.LerpAngle(blendStart.Yaw, target.Yaw, eased);
            viewTilt = Mathf.Lerp(blendStart.Tilt, target.Tilt, eased);
            viewDistance = Mathf.Lerp(blendStart.Distance, target.Distance, eased);
            viewFieldOfView = Mathf.Lerp(
                blendStart.FieldOfView,
                target.FieldOfView,
                eased);
            ApplyCameraPose();
        }

        /// <summary>
        /// Places the camera from the current yaw, tilt, distance and field of
        /// view.
        ///
        /// Dice show their value toward -Z, matching the board, so the camera
        /// stays on that side. Tilting it down the -Y axis turns the XY plane
        /// the pieces stand on into visible ground, which is what gives the
        /// 2.5D read — and it puts the near side genuinely closer to the lens,
        /// so the player's own pieces sit forward. Yaw then swings that whole
        /// rig around the arena's up axis.
        /// </summary>
        private void ApplyCameraPose()
        {
            if (arenaCamera == null)
            {
                return;
            }

            arenaCamera.fieldOfView = viewFieldOfView;

            float radians = viewTilt * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                0f,
                -Mathf.Sin(radians) * viewDistance,
                -Mathf.Cos(radians) * viewDistance);
            Vector3 up = new Vector3(
                0f,
                Mathf.Cos(radians),
                -Mathf.Sin(radians));

            Quaternion yaw = Quaternion.AngleAxis(viewYaw, Vector3.forward);
            offset = yaw * offset;
            up = yaw * up;

            arenaCamera.transform.position = Origin + offset;
            arenaCamera.transform.rotation =
                Quaternion.LookRotation(-offset.normalized, up);
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

        private void EnsureArena(
            LudoCombatSession combatSession,
            float attackerRow)
        {
            EnsureFloor(combatSession, attackerRow);
            EnsureColiseum();
        }

        /// <summary>
        /// The ground itself: a sandy oval, built the same procedural
        /// vertex-coloured way as the board. Sits just behind the pieces in Z,
        /// so with the camera tilted it reads as what they're standing on.
        ///
        /// A paper-coloured inner circle and ink-separated player ring carry
        /// the same graphic language as the board without obscuring the dice.
        /// </summary>
        private void EnsureFloor(
            LudoCombatSession combatSession,
            float attackerRow)
        {
            if (floorObject == null)
            {
                floorObject = new GameObject("ArenaFloor")
                {
                    hideFlags = HideFlags.DontSave
                };
                floorObject.transform.SetParent(root, false);
                floorObject.transform.localPosition = Vector3.zero;

                MeshRenderer meshRenderer =
                    floorObject.AddComponent<MeshRenderer>();
                ConfigureRenderer(meshRenderer);

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

            RebuildFloor(combatSession, attackerRow);
        }

        private void RebuildFloor(
            LudoCombatSession combatSession,
            float attackerRow)
        {
            List<Vector3> vertices = new List<Vector3>(2600);
            List<Color> colors = new List<Color>(2600);
            List<int> triangles = new List<int>(4000);

            const int segments = 64;
            AddFloorDisc(
                vertices,
                colors,
                triangles,
                floorRadius,
                FloorDepth,
                sandInner,
                sandOuter,
                segments);

            float paperRadius = floorRadius * 0.47f;
            AddFloorDisc(
                vertices,
                colors,
                triangles,
                paperRadius,
                FloorDepth - 0.008f,
                Paper,
                Paper,
                segments);
            AddFloorRing(
                vertices,
                colors,
                triangles,
                paperRadius,
                paperRadius + 0.13f,
                FloorDepth - 0.014f,
                _ => Ink,
                segments);

            Color attackerColor = CombatantColor(
                combatSession.AttackerToken,
                LudoBoardVisualStyle.Red);
            Color defenderColor = CombatantColor(
                combatSession.DefenderToken,
                LudoBoardVisualStyle.Blue);
            Color positiveColor = attackerRow > 0f
                ? attackerColor
                : defenderColor;
            Color negativeColor = attackerRow > 0f
                ? defenderColor
                : attackerColor;

            float accentInner = floorRadius * 0.79f;
            float accentOuter = floorRadius * 0.93f;
            AddFloorRing(
                vertices,
                colors,
                triangles,
                accentInner - 0.11f,
                accentOuter + 0.11f,
                FloorDepth - 0.010f,
                _ => Ink,
                segments);
            AddFloorRing(
                vertices,
                colors,
                triangles,
                accentInner,
                accentOuter,
                FloorDepth - 0.018f,
                angle => Color.Lerp(
                    Mathf.Sin(angle) >= 0f ? positiveColor : negativeColor,
                    Paper,
                    0.08f),
                segments);

            AddCombatantPedestal(
                vertices,
                colors,
                triangles,
                new Vector2(0f, tokenRowOffset),
                positiveColor,
                segments);
            AddCombatantPedestal(
                vertices,
                colors,
                triangles,
                new Vector2(0f, -tokenRowOffset),
                negativeColor,
                segments);

            AddFloorDiscAt(
                vertices,
                colors,
                triangles,
                Vector2.zero,
                0.86f,
                FloorDepth - 0.040f,
                Ink,
                Ink,
                segments);
            AddSplitFloorDiscAt(
                vertices,
                colors,
                triangles,
                Vector2.zero,
                0.69f,
                FloorDepth - 0.048f,
                positiveColor,
                negativeColor,
                segments);

            if (floorMesh == null)
            {
                floorMesh = new Mesh
                {
                    name = "ArenaFloor",
                    hideFlags = HideFlags.DontSave
                };
            }
            else
            {
                floorMesh.Clear();
            }

            floorMesh.SetVertices(vertices);
            floorMesh.SetColors(colors);
            floorMesh.SetTriangles(triangles, 0);
            floorMesh.RecalculateNormals();
            floorMesh.RecalculateBounds();

            MeshFilter meshFilter = floorObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = floorObject.AddComponent<MeshFilter>();
            }

            meshFilter.sharedMesh = floorMesh;
        }

        /// <summary>
        /// The stonework around the arena: the podium wall, a ring of columns
        /// and the arches spanning them.
        ///
        /// All one mesh so its cel-shaded colour bands stay consistent. The
        /// columns only cover the far arc — a column standing between the
        /// camera and the dice would be authentic and useless.
        /// </summary>
        private void EnsureColiseum()
        {
            if (stoneObject != null)
            {
                return;
            }

            LudoProceduralMesh builder = new LudoProceduralMesh();

            // Deliberately low. The camera looks down at the arena, so a tall
            // wall on the far rim climbs the screen and lands on top of the
            // piece standing in front of it — which is what was burying the
            // rival.
            float wallHeight = floorRadius * 0.11f;
            float wallTopZ = FloorDepth - wallHeight;

            BuildPodiumWall(builder, wallHeight);

            float ringRadius = floorRadius * 1.14f;
            float columnTopZ = FloorDepth - columnHeight;
            Vector2[] feet = new Vector2[Mathf.Max(columnCount, 2)];

            float halfArc = columnArc * 0.5f;
            for (int index = 0; index < feet.Length; index++)
            {
                float t = feet.Length == 1
                    ? 0.5f
                    : (float)index / (feet.Length - 1);

                // Centred on 90°, the far side of the oval.
                float degrees = 90f - halfArc + columnArc * t;
                Vector3 point = RingPoint(
                    degrees * Mathf.Deg2Rad,
                    ringRadius,
                    0f);
                feet[index] = new Vector2(point.x, point.y);
                BuildColumn(builder, feet[index], wallTopZ, columnTopZ);
            }

            for (int index = 0; index < feet.Length - 1; index++)
            {
                BuildArch(builder, feet[index], feet[index + 1], columnTopZ);
            }

            stoneObject = new GameObject("ArenaColiseum")
            {
                hideFlags = HideFlags.DontSave
            };
            stoneObject.transform.SetParent(root, false);
            stoneObject.transform.localPosition = Vector3.zero;

            Mesh mesh = builder.Build("ArenaColiseum");
            stoneObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer stoneRenderer = stoneObject.AddComponent<MeshRenderer>();
            ConfigureRenderer(stoneRenderer);

            Shader celShader = Shader.Find(FloorShaderName);
            if (celShader == null)
            {
                Debug.LogError(
                    $"CombatArena: shader '{FloorShaderName}' not found.",
                    this);
            }
            else
            {
                stoneMaterial = new Material(celShader)
                {
                    name = "Arena Stone",
                    hideFlags = HideFlags.DontSave
                };
                stoneRenderer.sharedMaterial = stoneMaterial;
            }

        }

        private void BuildPodiumWall(LudoProceduralMesh builder, float wallHeight)
        {
            const int segments = 48;
            float outerRadius = floorRadius * 1.08f;
            float topZ = FloorDepth - wallHeight;

            for (int segment = 0; segment < segments; segment++)
            {
                float a0 = Mathf.PI * 2f * segment / segments;
                float a1 = Mathf.PI * 2f * (segment + 1) / segments;

                Vector3 low0 = RingPoint(a0, floorRadius, FloorDepth);
                Vector3 low1 = RingPoint(a1, floorRadius, FloorDepth);
                Vector3 high0 = RingPoint(a0, outerRadius, topZ);
                Vector3 high1 = RingPoint(a1, outerRadius, topZ);

                // Facing inward, toward the fight.
                Vector3 n0 = new Vector3(-Mathf.Cos(a0), -Mathf.Sin(a0), 0f);
                Vector3 n1 = new Vector3(-Mathf.Cos(a1), -Mathf.Sin(a1), 0f);

                builder.AddQuad(
                    low0, low1, high1, high0,
                    n0, n1, n1, n0,
                    wallColor, wallColor, wallTop, wallTop);
            }
        }

        private void BuildColumn(
            LudoProceduralMesh builder,
            Vector2 foot,
            float wallTopZ,
            float topZ)
        {
            const int segments = 10;
            float plinthRadius = columnRadius * 1.55f;
            float capitalRadius = columnRadius * 1.5f;
            float plinthZ = wallTopZ - 0.34f;
            float capitalZ = topZ + 0.42f;

            // Rooted below the wall's top edge so no gap can open under it.
            builder.AddCylinder(
                foot, plinthRadius,
                FloorDepth, plinthZ,
                segments, stoneLower, stoneLower);
            builder.AddCylinder(
                foot, columnRadius,
                plinthZ, capitalZ,
                segments, stoneLower, stoneUpper);
            builder.AddCylinder(
                foot, capitalRadius,
                capitalZ, topZ,
                segments, stoneUpper, stoneUpper);

            // Every step in radius leaves an opening into the hollow shaft.
            // Closed here, top and bottom, so no part of the column shows its
            // unlit inside.
            builder.AddDisc(foot, plinthRadius, FloorDepth, segments, stoneLower, -Up);
            builder.AddAnnulus(
                foot, columnRadius, plinthRadius,
                plinthZ, segments, stoneLower, Up);
            builder.AddAnnulus(
                foot, columnRadius, capitalRadius,
                capitalZ, segments, stoneUpper, -Up);
            builder.AddDisc(foot, capitalRadius, topZ, segments, stoneUpper, Up);
        }

        /// <summary>
        /// A round arch from one column top to the next, given a box section so
        /// it reads as masonry rather than a ribbon. The rise is its own figure
        /// instead of half the span, so widening the ring doesn't send the
        /// arches through the top of the frame.
        /// </summary>
        private void BuildArch(
            LudoProceduralMesh builder,
            Vector2 from,
            Vector2 to,
            float springZ)
        {
            Vector3 start = new Vector3(from.x, from.y, springZ);
            Vector3 end = new Vector3(to.x, to.y, springZ);
            Vector3 along = end - start;
            float span = along.magnitude;
            if (span <= Mathf.Epsilon)
            {
                return;
            }

            along /= span;
            Vector3 middle = (start + end) * 0.5f;
            float halfSpan = span * 0.5f;

            // Horizontal, perpendicular to the span: the arch's thin axis.
            Vector3 side = Vector3.Cross(along, Up).normalized * (archDepth * 0.5f);

            const int steps = 12;
            for (int step = 0; step < steps; step++)
            {
                float t0 = Mathf.PI * step / steps;
                float t1 = Mathf.PI * (step + 1) / steps;

                ArchSection s0 = SectionAt(middle, along, side, halfSpan, t0);
                ArchSection s1 = SectionAt(middle, along, side, halfSpan, t1);

                // Outer curve, then the underside, then the two flat cheeks.
                builder.AddQuad(
                    s0.OuterNear, s1.OuterNear, s1.OuterFar, s0.OuterFar,
                    s0.Outward, s1.Outward, s1.Outward, s0.Outward,
                    stoneUpper, stoneUpper, stoneUpper, stoneUpper);
                builder.AddQuad(
                    s0.InnerNear, s1.InnerNear, s1.InnerFar, s0.InnerFar,
                    -s0.Outward, -s1.Outward, -s1.Outward, -s0.Outward,
                    stoneLower, stoneLower, stoneLower, stoneLower);

                Vector3 cheek = side.normalized;
                builder.AddQuad(
                    s0.InnerNear, s1.InnerNear, s1.OuterNear, s0.OuterNear,
                    -cheek, -cheek, -cheek, -cheek,
                    stoneLower, stoneLower, stoneUpper, stoneUpper);
                builder.AddQuad(
                    s0.InnerFar, s1.InnerFar, s1.OuterFar, s0.OuterFar,
                    cheek, cheek, cheek, cheek,
                    stoneLower, stoneLower, stoneUpper, stoneUpper);
            }
        }

        private ArchSection SectionAt(
            Vector3 middle,
            Vector3 along,
            Vector3 side,
            float halfSpan,
            float angle)
        {
            Vector3 outward =
                (-Mathf.Cos(angle) * along + Mathf.Sin(angle) * Up).normalized;
            Vector3 inner = middle
                + -Mathf.Cos(angle) * halfSpan * along
                + Mathf.Sin(angle) * archRise * Up;
            Vector3 outer = inner + outward * archThickness;

            return new ArchSection
            {
                InnerNear = inner - side,
                InnerFar = inner + side,
                OuterNear = outer - side,
                OuterFar = outer + side,
                Outward = outward
            };
        }

        private static void ConfigureRenderer(MeshRenderer meshRenderer)
        {
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Vector3 RingPoint(float angle, float radius, float depth)
        {
            return new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                depth);
        }

        private static void AddFloorDisc(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            float radius,
            float depth,
            Color centerColor,
            Color edgeColor,
            int segments)
        {
            AddFloorDiscAt(
                vertices,
                colors,
                triangles,
                Vector2.zero,
                radius,
                depth,
                centerColor,
                edgeColor,
                segments);
        }

        private static void AddFloorDiscAt(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 center,
            float radius,
            float depth,
            Color centerColor,
            Color edgeColor,
            int segments)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                float a0 = Mathf.PI * 2f * segment / segments;
                float a1 = Mathf.PI * 2f * (segment + 1) / segments;
                int first = vertices.Count;

                vertices.Add(new Vector3(center.x, center.y, depth));
                vertices.Add(FloorPoint(center, a0, radius, depth));
                vertices.Add(FloorPoint(center, a1, radius, depth));
                colors.Add(centerColor);
                colors.Add(edgeColor);
                colors.Add(edgeColor);

                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 1);
            }
        }

        private static void AddSplitFloorDiscAt(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 center,
            float radius,
            float depth,
            Color positiveColor,
            Color negativeColor,
            int segments)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                float a0 = Mathf.PI * 2f * segment / segments;
                float a1 = Mathf.PI * 2f * (segment + 1) / segments;
                Color color = Mathf.Sin((a0 + a1) * 0.5f) >= 0f
                    ? positiveColor
                    : negativeColor;
                int first = vertices.Count;

                vertices.Add(new Vector3(center.x, center.y, depth));
                vertices.Add(FloorPoint(center, a0, radius, depth));
                vertices.Add(FloorPoint(center, a1, radius, depth));
                colors.Add(color);
                colors.Add(color);
                colors.Add(color);

                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 1);
            }
        }

        private static void AddCombatantPedestal(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 center,
            Color playerColor,
            int segments)
        {
            AddFloorDiscAt(
                vertices,
                colors,
                triangles,
                center,
                1.22f,
                FloorDepth - 0.022f,
                Ink,
                Ink,
                segments);
            AddFloorDiscAt(
                vertices,
                colors,
                triangles,
                center,
                1.08f,
                FloorDepth - 0.030f,
                playerColor,
                LudoBoardVisualStyle.Lighten(playerColor, 0.12f),
                segments);
            AddFloorDiscAt(
                vertices,
                colors,
                triangles,
                center,
                0.70f,
                FloorDepth - 0.038f,
                Paper,
                Paper,
                segments);
        }

        private static Vector3 FloorPoint(
            Vector2 center,
            float angle,
            float radius,
            float depth)
        {
            return new Vector3(
                center.x + Mathf.Cos(angle) * radius,
                center.y + Mathf.Sin(angle) * radius,
                depth);
        }

        private static void AddFloorRing(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            float innerRadius,
            float outerRadius,
            float depth,
            System.Func<float, Color> colorAtAngle,
            int segments)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                float a0 = Mathf.PI * 2f * segment / segments;
                float a1 = Mathf.PI * 2f * (segment + 1) / segments;
                Color color = colorAtAngle((a0 + a1) * 0.5f);
                int first = vertices.Count;

                vertices.Add(RingPoint(a0, innerRadius, depth));
                vertices.Add(RingPoint(a1, innerRadius, depth));
                vertices.Add(RingPoint(a1, outerRadius, depth));
                vertices.Add(RingPoint(a0, outerRadius, depth));
                colors.Add(color);
                colors.Add(color);
                colors.Add(color);
                colors.Add(color);

                triangles.Add(first);
                triangles.Add(first + 1);
                triangles.Add(first + 2);
                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 3);
            }
        }

        private static Color CombatantColor(Token token, Color fallback)
        {
            return token != null && token.OwnerStyle != null
                ? token.OwnerStyle.TokenColor
                : fallback;
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

                dice[index].BasePosition = Origin + new Vector3(
                    index * diceSpacing - span * 0.5f,
                    rowY,
                    0f);
                dice[index].Root.position = dice[index].BasePosition;
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

            // What makes the die clickable. The mesh is built around the
            // visual's own origin, so a plain cube of the same size lines up
            // without any offset, and it travels with the tumble animation.
            BoxCollider dieCollider = visualObject.AddComponent<BoxCollider>();
            dieCollider.size = Vector3.one * (DiceVisual.HalfSize * 2f);

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
                // A style with no model, or one with nothing to measure, still
                // has to show up: a duel with an invisible combatant reads as a
                // bug even when the fight itself is running fine.
                BuildStandInCombatant(holder.transform, token.OwnerStyle.TokenColor);
            }

            return holder;
        }

        /// <summary>A plain coloured pillar, used when the real model is unusable.</summary>
        private void BuildStandInCombatant(Transform parent, Color color)
        {
            LudoProceduralMesh builder = new LudoProceduralMesh();
            float radius = tokenSize * 0.28f;
            builder.AddCylinder(
                Vector2.zero, radius,
                FloorDepth, FloorDepth - tokenSize,
                14, color * 0.75f, color);
            builder.AddDisc(
                Vector2.zero, radius, FloorDepth - tokenSize, 14, color, Up);

            GameObject standIn = new GameObject("CombatantStandIn")
            {
                hideFlags = HideFlags.DontSave
            };
            standIn.transform.SetParent(parent, false);
            standIn.transform.localPosition = Vector3.zero;
            standIn.AddComponent<MeshFilter>().sharedMesh =
                builder.Build("CombatantStandIn");

            MeshRenderer standInRenderer = standIn.AddComponent<MeshRenderer>();
            ConfigureRenderer(standInRenderer);
            standInRenderer.sharedMaterial = stoneMaterial;
        }

        private GameObject BuildCombatantModel(PlayerStyle style, Transform parent)
        {
            if (style.TokenModel == null)
            {
                return null;
            }

            // Build through the same component as the board tokens. This keeps
            // their cartoon material treatment, tint, emission, outline and
            // idle spin instead of showing the raw imported GLB in the duel.
            GameObject model = new GameObject("CombatantVisual")
            {
                hideFlags = HideFlags.DontSave
            };
            model.transform.SetParent(parent, false);
            TokenVisual visual = model.AddComponent<TokenVisual>();
            visual.SetStyle(style);
            visual.SetUseElementalModel(true);

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
        /// Scales the model to a readable size, centres it on the holder and
        /// stands it on the arena floor.
        ///
        /// Every measurement is taken in the holder's own space, the way
        /// TokenVisual does it on the board. Working in world space here is
        /// what made both combatants vanish: the arena sits 500 units out, and
        /// Renderer.bounds can still be reporting the prefab's untouched
        /// position on the frame it is instantiated. Differencing a fresh world
        /// target against a stale world centre then produced an offset of
        /// roughly the arena's whole distance, throwing the model far out of
        /// any camera's reach.
        /// </summary>
        private void FitToSize(GameObject model, Transform holder)
        {
            if (!TryGetLocalBounds(model, holder, out Bounds bounds))
            {
                return;
            }

            float largest = Mathf.Max(
                bounds.size.x,
                Mathf.Max(bounds.size.y, bounds.size.z));
            if (largest <= Mathf.Epsilon)
            {
                return;
            }

            model.transform.localScale *= tokenSize / largest;
            if (!TryGetLocalBounds(model, holder, out bounds))
            {
                return;
            }

            // Centred across the board plane, and resting on the floor rather
            // than buried half-way into it. -Z is up, so the model's lowest
            // point is its largest Z.
            model.transform.localPosition += new Vector3(
                -bounds.center.x,
                -bounds.center.y,
                FloorDepth - bounds.max.z);
        }

        /// <summary>
        /// Measures the model in <paramref name="space"/> the same way
        /// TokenVisual measures one on the board: through Renderer.localBounds
        /// and the transform matrices, never Renderer.bounds.
        ///
        /// The distinction is the whole fix. Transform matrices are correct the
        /// moment they are written; a renderer's world bounds can still be
        /// reporting the prefab's untouched position on the frame it was
        /// instantiated. localBounds also works for any renderer type, so a
        /// skinned model measures as readily as a plain mesh.
        /// </summary>
        private static bool TryGetLocalBounds(
            GameObject model,
            Transform space,
            out Bounds bounds)
        {
            bounds = default;
            bool started = false;

            foreach (Renderer modelRenderer in
                     model.GetComponentsInChildren<Renderer>(true))
            {
                if (modelRenderer == null || !modelRenderer.enabled)
                {
                    continue;
                }

                Bounds local = modelRenderer.localBounds;
                Matrix4x4 toSpace = space.worldToLocalMatrix *
                                    modelRenderer.localToWorldMatrix;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = toSpace.MultiplyPoint3x4(
                        local.center + new Vector3(
                            (corner & 1) == 0 ? -local.extents.x : local.extents.x,
                            (corner & 2) == 0 ? -local.extents.y : local.extents.y,
                            (corner & 4) == 0 ? -local.extents.z : local.extents.z));

                    if (!started)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        started = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            return started;
        }

        // ------------------------------------------------------------------
        // Dice
        // ------------------------------------------------------------------

        private void SyncDice(bool immediate)
        {
            SyncSide(attackerDice, session.Attacker.Dice, session.AttackerToken, immediate);

            // The defender's dice are thrown when the session is built, long
            // before their turn, so showing them during the attacker's would
            // hand over the very information the duel is built around keeping
            // back. They stay off the table until it is their throw, and then
            // tumble in like a real one.
            bool defenderThrown = session.Phase != LudoCombatPhase.AttackerTurn;
            SetSideVisible(defenderDice, session.Defender.Dice.Count, defenderThrown);
            if (defenderThrown)
            {
                SyncSide(
                    defenderDice,
                    session.Defender.Dice,
                    session.DefenderToken,
                    immediate);
            }
        }

        private static void SetSideVisible(
            List<ArenaDie> dice,
            int count,
            bool visible)
        {
            for (int index = 0; index < dice.Count; index++)
            {
                bool shown = visible && index < count;
                if (dice[index].Root.gameObject.activeSelf != shown)
                {
                    dice[index].Root.gameObject.SetActive(shown);
                }

                // Held at nothing while hidden so the first sync after they
                // appear counts as a change and sends them rolling.
                if (!shown)
                {
                    dice[index].Value = 0;
                }
            }
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
                die.Tint = tint;
                die.Visual.SetAccentColor(tint, 0.60f);

                if (die.Value == values[index])
                {
                    continue;
                }

                if (immediate)
                {
                    die.Value = values[index];
                    die.Timer = 0f;
                    die.Visual.ShowValue(die.Value);
                    continue;
                }

                // A face that no longer matches means the AI rerolled it. The
                // player's own rerolls are animated at the click instead, since
                // a change in value can't detect one that landed on the same
                // face it started from.
                StartTumble(die, values[index]);
            }
        }

        /// <summary>Sends a die tumbling and lands it on <paramref name="value"/>.</summary>
        private void StartTumble(ArenaDie die, int value)
        {
            die.Value = value;
            die.Timer = tumbleDuration;
            die.StartRotation = die.Visual.transform.localRotation;
            die.Axis = Random.onUnitSphere;
            if (die.Axis.sqrMagnitude <= Mathf.Epsilon)
            {
                die.Axis = Vector3.up;
            }

            die.Axis.Normalize();
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

        // ------------------------------------------------------------------
        // Support types
        // ------------------------------------------------------------------

        private readonly struct ArenaViewPreset
        {
            public string Label { get; }
            public float Yaw { get; }
            public float Tilt { get; }
            public float Distance { get; }
            public float FieldOfView { get; }

            public ArenaViewPreset(
                string label,
                float yaw,
                float tilt,
                float distance,
                float fieldOfView)
            {
                Label = label;
                Yaw = yaw;
                Tilt = tilt;
                Distance = distance;
                FieldOfView = fieldOfView;
            }
        }

        private struct ArchSection
        {
            public Vector3 InnerNear;
            public Vector3 InnerFar;
            public Vector3 OuterNear;
            public Vector3 OuterFar;
            public Vector3 Outward;
        }

        private sealed class ArenaDie
        {
            public Transform Root;
            public DiceVisual Visual;
            public int Value;
            public float Timer;
            public Quaternion StartRotation;
            public Vector3 Axis;

            /// <summary>Where it sits when nothing is hovering it.</summary>
            public Vector3 BasePosition;

            /// <summary>Owner's colour, kept so the highlight can brighten it.</summary>
            public Color Tint;
        }
    }
}
