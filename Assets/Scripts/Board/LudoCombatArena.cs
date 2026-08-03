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
        private const string StoneShaderName = "Elemental Ludo/Arena Cel";
        private const string OutlineShaderName = "Elemental Ludo/Token Outline";

        // Just behind the pieces, which stand at Z <= 0.
        private const float FloorDepth = 0.1f;

        // The arena is built on the board's convention: the pieces stand on the
        // XY plane and -Z is up, so "taller" means more negative.
        private static readonly Vector3 Up = new Vector3(0f, 0f, -1f);

        private const float FloorSquash = 0.72f;
        private const int DefaultPreset = 1;
        private const float ViewBlendDuration = 0.45f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        /// <summary>
        /// The four framings offered to the player. Static rather than
        /// serialized on purpose: the arena builds itself at runtime and is
        /// never saved in a prefab, so Inspector fields on it are unreachable —
        /// which is exactly why these need buttons in the first place.
        /// </summary>
        private static readonly ArenaViewPreset[] Presets =
        {
            new ArenaViewPreset("1 · Cenital", 0f, 12f, 16.5f, 34f),
            new ArenaViewPreset("2 · 2.5D", 0f, 38f, 18f, 34f),
            new ArenaViewPreset("3 · Diagonal", 34f, 44f, 18.5f, 36f),
            new ArenaViewPreset("4 · Tribuna", 0f, 62f, 16f, 42f)
        };

        private static readonly string[] PresetLabelCache = BuildPresetLabels();

        [Header("Layout")]
        [SerializeField] private float diceSpacing = 1.15f;
        [SerializeField] private float diceRowOffset = 1.9f;
        [SerializeField] private float tokenRowOffset = 4.4f;
        [SerializeField] private float tokenSize = 1.8f;

        [Header("Camera")]
        [SerializeField] private Color background = new Color(0.05f, 0.05f, 0.09f);

        [Header("Arena Floor")]
        [SerializeField] private float floorRadius = 9f;
        [SerializeField] private Color sandInner = new Color(0.62f, 0.45f, 0.26f);
        [SerializeField] private Color sandOuter = new Color(0.44f, 0.30f, 0.16f);
        [SerializeField] private Color wallColor = new Color(0.30f, 0.20f, 0.12f);
        [SerializeField] private Color wallTop = new Color(0.46f, 0.33f, 0.21f);

        [Header("Coliseum")]
        [Min(2)]
        [SerializeField] private int columnCount = 9;
        [Tooltip("Degrees of the ring the columns cover, centred on the far side. The rest is left open so the near ones never stand between the camera and the dice.")]
        [Range(60f, 340f)]
        [SerializeField] private float columnArc = 224f;
        [SerializeField] private float columnRadius = 0.42f;
        [SerializeField] private float columnHeight = 5.6f;
        [SerializeField] private float archRise = 1.55f;
        [SerializeField] private float archThickness = 0.36f;
        [SerializeField] private float archDepth = 0.52f;
        [SerializeField] private Color stoneLower = new Color(0.50f, 0.38f, 0.27f);
        [SerializeField] private Color stoneUpper = new Color(0.72f, 0.60f, 0.45f);

        [Header("Cel Outline")]
        [SerializeField] private float outlineWidth = 0.055f;
        [SerializeField] private Color outlineColor = new Color(0.03f, 0.02f, 0.02f);

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
        private Material stoneOutlineMaterial;
        private GameObject floorObject;
        private GameObject stoneObject;

        // Board camera state, put back exactly as found when the duel ends.
        private Camera suspendedCamera;
        private LudoBoardOrbitCamera suspendedOrbit;
        private Vector3 suspendedPosition;
        private Quaternion suspendedRotation;
        private bool suspendedOrthographic;
        private float suspendedOrthographicSize;
        private float suspendedFieldOfView;

        private int cameraPreset = DefaultPreset;
        private float viewYaw = Presets[DefaultPreset].Yaw;
        private float viewTilt = Presets[DefaultPreset].Tilt;
        private float viewDistance = Presets[DefaultPreset].Distance;
        private float viewFieldOfView = Presets[DefaultPreset].FieldOfView;
        private float blendFrom;
        private float blendProgress = 1f;
        private ArenaViewPreset blendStart;

        /// <summary>Where the arena sits, well away from the board.</summary>
        private Vector3 Origin => new Vector3(ArenaDistance, 0f, 0f);

        /// <summary>
        /// Button labels for the four framings, in order. Cached rather than
        /// built per call: the only caller is an IMGUI panel, which would ask
        /// for it every frame.
        /// </summary>
        public static IReadOnlyList<string> PresetLabels => PresetLabelCache;

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
            EnsureArena();

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

            AdvanceViewBlend();
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
            DestroyGenerated(stoneMaterial);
            DestroyGenerated(stoneOutlineMaterial);
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

            SuspendBoardCamera();
            ApplyCameraPose();
            arenaCamera.enabled = true;
        }

        private void LeaveArenaView()
        {
            if (arenaCamera != null)
            {
                arenaCamera.enabled = false;
            }

            RestoreBoardCamera();
        }

        /// <summary>
        /// Parks the board's camera for the duration of the duel and remembers
        /// how it was set up.
        ///
        /// Turning off the orbit script matters as much as the camera itself:
        /// it reads the mouse in LateUpdate regardless of whether its camera is
        /// rendering, so without this every drag, scroll and number key aimed
        /// at the arena also quietly re-aimed the board behind it — which is
        /// why the board came back looking wrong.
        /// </summary>
        private void SuspendBoardCamera()
        {
            if (suspendedCamera != null)
            {
                return;
            }

            Camera boardCamera = Camera.main;
            if (boardCamera == null || boardCamera == arenaCamera)
            {
                return;
            }

            suspendedCamera = boardCamera;
            suspendedPosition = boardCamera.transform.position;
            suspendedRotation = boardCamera.transform.rotation;
            suspendedOrthographic = boardCamera.orthographic;
            suspendedOrthographicSize = boardCamera.orthographicSize;
            suspendedFieldOfView = boardCamera.fieldOfView;

            suspendedOrbit = boardCamera.GetComponent<LudoBoardOrbitCamera>();
            if (suspendedOrbit != null)
            {
                suspendedOrbit.enabled = false;
            }

            // Two enabled cameras would both render.
            suspendedCamera.enabled = false;
        }

        private void RestoreBoardCamera()
        {
            if (suspendedCamera == null)
            {
                return;
            }

            // Put the view back before handing control over, so the orbit
            // script picks up from where the player left the board rather than
            // from wherever it happened to be.
            suspendedCamera.transform.SetPositionAndRotation(
                suspendedPosition,
                suspendedRotation);
            suspendedCamera.orthographic = suspendedOrthographic;
            suspendedCamera.orthographicSize = suspendedOrthographicSize;
            suspendedCamera.fieldOfView = suspendedFieldOfView;
            suspendedCamera.ResetProjectionMatrix();
            suspendedCamera.enabled = true;

            if (suspendedOrbit != null)
            {
                suspendedOrbit.enabled = true;
                suspendedOrbit = null;
            }

            suspendedCamera = null;
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

        private void EnsureArena()
        {
            EnsureFloor();
            EnsureColiseum();
        }

        /// <summary>
        /// The ground itself: a sandy oval, built the same procedural
        /// vertex-coloured way as the board. Sits just behind the pieces in Z,
        /// so with the camera tilted it reads as what they're standing on.
        ///
        /// Kept flat and outline-free — an outline around the ground plane
        /// would only draw a ring on the horizon.
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

            List<Vector3> vertices = new List<Vector3>(160);
            List<Color> colors = new List<Color>(160);
            List<int> triangles = new List<int>(160);

            const int segments = 48;
            for (int segment = 0; segment < segments; segment++)
            {
                float a0 = Mathf.PI * 2f * segment / segments;
                float a1 = Mathf.PI * 2f * (segment + 1) / segments;

                int first = vertices.Count;
                vertices.Add(new Vector3(0f, 0f, FloorDepth));
                vertices.Add(EllipsePoint(a0, floorRadius, FloorDepth));
                vertices.Add(EllipsePoint(a1, floorRadius, FloorDepth));
                colors.Add(sandInner);
                colors.Add(sandOuter);
                colors.Add(sandOuter);

                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 1);
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

        /// <summary>
        /// The stonework around the arena: the podium wall, a ring of columns
        /// and the arches spanning them.
        ///
        /// All one mesh so a single outline object can trace the lot. The
        /// columns only cover the far arc — a column standing between the
        /// camera and the dice would be authentic and useless.
        /// </summary>
        private void EnsureColiseum()
        {
            if (stoneObject != null)
            {
                return;
            }

            ArenaMeshBuilder builder = new ArenaMeshBuilder();
            float wallHeight = floorRadius * 0.16f;
            float wallTopZ = FloorDepth - wallHeight;

            BuildPodiumWall(builder, wallHeight);

            float ringRadius = floorRadius * 1.2f;
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
                Vector3 point = EllipsePoint(
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

            Shader celShader = Shader.Find(StoneShaderName);
            if (celShader != null)
            {
                stoneMaterial = new Material(celShader)
                {
                    name = "Arena Stone",
                    hideFlags = HideFlags.DontSave
                };
                stoneMaterial.SetColor(BaseColorId, Color.white);
                stoneRenderer.sharedMaterial = stoneMaterial;
            }

            BuildStoneOutline(mesh);
        }

        /// <summary>
        /// The black border, drawn the way the terrain does it: a second object
        /// sharing the same mesh, with the outline shader pushing it out along
        /// the normals and showing only its back faces.
        /// </summary>
        private void BuildStoneOutline(Mesh mesh)
        {
            Shader outlineShader = Shader.Find(OutlineShaderName);
            if (outlineShader == null)
            {
                Debug.LogWarning(
                    $"CombatArena: outline shader '{OutlineShaderName}' not found.",
                    this);
                return;
            }

            GameObject outlineObject = new GameObject("ArenaColiseumOutline")
            {
                hideFlags = HideFlags.DontSave
            };
            outlineObject.transform.SetParent(stoneObject.transform, false);
            outlineObject.transform.localPosition = Vector3.zero;
            outlineObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer outlineRenderer =
                outlineObject.AddComponent<MeshRenderer>();
            ConfigureRenderer(outlineRenderer);

            stoneOutlineMaterial = new Material(outlineShader)
            {
                name = "Arena Stone Outline",
                hideFlags = HideFlags.DontSave
            };
            stoneOutlineMaterial.SetColor(OutlineColorId, outlineColor);
            stoneOutlineMaterial.SetFloat(OutlineWidthId, outlineWidth);

            // The shader shares its vertex path with the animated terrain; this
            // geometry never moves, so the branch stays off.
            stoneOutlineMaterial.SetFloat("_TerrainAnimationEnabled", 0f);
            outlineRenderer.sharedMaterial = stoneOutlineMaterial;
        }

        private void BuildPodiumWall(ArenaMeshBuilder builder, float wallHeight)
        {
            const int segments = 48;
            float outerRadius = floorRadius * 1.08f;
            float topZ = FloorDepth - wallHeight;

            for (int segment = 0; segment < segments; segment++)
            {
                float a0 = Mathf.PI * 2f * segment / segments;
                float a1 = Mathf.PI * 2f * (segment + 1) / segments;

                Vector3 low0 = EllipsePoint(a0, floorRadius, FloorDepth);
                Vector3 low1 = EllipsePoint(a1, floorRadius, FloorDepth);
                Vector3 high0 = EllipsePoint(a0, outerRadius, topZ);
                Vector3 high1 = EllipsePoint(a1, outerRadius, topZ);

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
            ArenaMeshBuilder builder,
            Vector2 foot,
            float wallTopZ,
            float topZ)
        {
            const int segments = 10;
            float plinthZ = wallTopZ - 0.34f;
            float capitalZ = topZ + 0.42f;

            // Rooted below the wall's top edge so no gap can open under it.
            builder.AddCylinder(
                foot, columnRadius * 1.55f,
                FloorDepth, plinthZ,
                segments, stoneLower, stoneLower);
            builder.AddCylinder(
                foot, columnRadius,
                plinthZ, capitalZ,
                segments, stoneLower, stoneUpper);
            builder.AddCylinder(
                foot, columnRadius * 1.5f,
                capitalZ, topZ,
                segments, stoneUpper, stoneUpper);
            builder.AddDisc(foot, columnRadius * 1.5f, topZ, segments, stoneUpper);
        }

        /// <summary>
        /// A round arch from one column top to the next, given a box section so
        /// it reads as masonry rather than a ribbon. The rise is its own figure
        /// instead of half the span, so widening the ring doesn't send the
        /// arches through the top of the frame.
        /// </summary>
        private void BuildArch(
            ArenaMeshBuilder builder,
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

        private static string[] BuildPresetLabels()
        {
            string[] labels = new string[Presets.Length];
            for (int index = 0; index < Presets.Length; index++)
            {
                labels[index] = Presets[index].Label;
            }

            return labels;
        }

        private static void ConfigureRenderer(MeshRenderer meshRenderer)
        {
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private Vector3 EllipsePoint(float angle, float radius, float depth)
        {
            return new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius * FloorSquash,
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

        /// <summary>
        /// Accumulates positions, normals and vertex colours for the stonework.
        ///
        /// Normals are written per vertex rather than recalculated, then
        /// averaged across coincident positions on build. That averaging is
        /// what stops the outline cracking: every quad here carries its own
        /// four vertices, so left alone the shader would push neighbouring
        /// faces apart along different normals and split the silhouette open at
        /// every seam.
        /// </summary>
        private sealed class ArenaMeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>(2048);
            private readonly List<Vector3> normals = new List<Vector3>(2048);
            private readonly List<Color> colors = new List<Color>(2048);
            private readonly List<int> triangles = new List<int>(3072);

            public void AddQuad(
                Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
                Color ca, Color cb, Color cc, Color cd)
            {
                int first = vertices.Count;

                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);
                normals.Add(na);
                normals.Add(nb);
                normals.Add(nc);
                normals.Add(nd);
                colors.Add(ca);
                colors.Add(cb);
                colors.Add(cc);
                colors.Add(cd);

                triangles.Add(first);
                triangles.Add(first + 1);
                triangles.Add(first + 2);
                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 3);
            }

            public void AddCylinder(
                Vector2 center,
                float radius,
                float bottomZ,
                float topZ,
                int segments,
                Color bottomColor,
                Color topColor)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    float a0 = Mathf.PI * 2f * segment / segments;
                    float a1 = Mathf.PI * 2f * (segment + 1) / segments;

                    Vector3 n0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
                    Vector3 n1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);

                    Vector3 low0 = Ring(center, n0, radius, bottomZ);
                    Vector3 low1 = Ring(center, n1, radius, bottomZ);
                    Vector3 high1 = Ring(center, n1, radius, topZ);
                    Vector3 high0 = Ring(center, n0, radius, topZ);

                    AddQuad(
                        low0, low1, high1, high0,
                        n0, n1, n1, n0,
                        bottomColor, bottomColor, topColor, topColor);
                }
            }

            public void AddDisc(
                Vector2 center,
                float radius,
                float z,
                int segments,
                Color color)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    float a0 = Mathf.PI * 2f * segment / segments;
                    float a1 = Mathf.PI * 2f * (segment + 1) / segments;

                    Vector3 n0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
                    Vector3 n1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);

                    int first = vertices.Count;
                    vertices.Add(new Vector3(center.x, center.y, z));
                    vertices.Add(Ring(center, n0, radius, z));
                    vertices.Add(Ring(center, n1, radius, z));
                    normals.Add(Up);
                    normals.Add(Up);
                    normals.Add(Up);
                    colors.Add(color);
                    colors.Add(color);
                    colors.Add(color);

                    triangles.Add(first);
                    triangles.Add(first + 2);
                    triangles.Add(first + 1);
                }
            }

            public Mesh Build(string name)
            {
                Mesh mesh = new Mesh
                {
                    name = name,
                    hideFlags = HideFlags.DontSave
                };

                mesh.indexFormat = vertices.Count > ushort.MaxValue
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                SmoothSharedNormals(mesh);
                mesh.RecalculateBounds();
                return mesh;
            }

            private static Vector3 Ring(
                Vector2 center,
                Vector3 direction,
                float radius,
                float z)
            {
                return new Vector3(
                    center.x + direction.x * radius,
                    center.y + direction.y * radius,
                    z);
            }

            /// <summary>
            /// Averages the normals of every vertex sharing a position, the same
            /// treatment LudoBoardTerrainRenderer gives its own outline mesh.
            /// </summary>
            private static void SmoothSharedNormals(Mesh mesh)
            {
                Vector3[] meshVertices = mesh.vertices;
                Vector3[] meshNormals = mesh.normals;
                if (meshVertices.Length == 0 ||
                    meshNormals.Length != meshVertices.Length)
                {
                    return;
                }

                Dictionary<Vector3, Vector3> sums =
                    new Dictionary<Vector3, Vector3>(meshVertices.Length);
                for (int index = 0; index < meshVertices.Length; index++)
                {
                    sums.TryGetValue(meshVertices[index], out Vector3 sum);
                    sums[meshVertices[index]] = sum + meshNormals[index];
                }

                for (int index = 0; index < meshVertices.Length; index++)
                {
                    Vector3 sum = sums[meshVertices[index]];
                    if (sum.sqrMagnitude > 0.000001f)
                    {
                        meshNormals[index] = sum.normalized;
                    }
                }

                mesh.normals = meshNormals;
            }
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
