using System.Collections.Generic;
using ElementalLudo.Gameplay;
using ElementalLudo.Tokens;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ElementalLudo.Board
{
    /// <summary>
    /// The run's map as a place rather than a list: stages receding into the
    /// distance, nodes you can click, and the player's own elemental token
    /// standing on the one they are on.
    ///
    /// Built the same way as <see cref="LudoCombatArena"/> — its own camera and
    /// procedural geometry, parked far from the board — because that approach
    /// is already proven here and needs no scene loading, which the game has no
    /// persistence layer to survive yet.
    ///
    /// Colours come from <see cref="LudoBoardVisualStyle"/> and the geometry
    /// uses the board's own shader, so the map sits in the same world as the
    /// board and the arena instead of looking like a menu bolted on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LudoRunMapView : MonoBehaviour
    {
        // Far from the board at the origin and from the arena at +500, so
        // nothing can drift into frame from either.
        private const float MapDistance = -500f;

        private const string MapShaderName = "Elemental Ludo/Board Vertex Color";

        // Board convention: the XY plane is the ground and -Z is up.
        private static readonly Vector3 Up = new Vector3(0f, 0f, -1f);

        [Header("Layout")]
        [SerializeField] private float stageSpacing = 3.6f;
        [SerializeField] private float laneSpacing = 3.4f;
        [SerializeField] private float nodeRadius = 1f;

        // Sized against the screen, not by eye. At this camera the map covers
        // about 21 units for 1080 pixels, and heights are foreshortened by the
        // tilt on top of that — so the first pass at 0.45 came out sixteen
        // pixels tall with a seven-pixel bevel, which is why correct geometry
        // still looked like nothing had changed.
        [SerializeField] private float nodeHeight = 0.46f;

        [Tooltip("How much higher each stage stands than the one before it.")]
        [SerializeField] private float stageRise = 0.30f;
        [SerializeField] private float linkWidth = 0.20f;
        [SerializeField] private float tokenSize = 1.5f;

        [Tooltip("Rival tokens shown over duel nodes, relative to the player's own.")]
        [SerializeField] private float rivalTokenScale = 0.7f;

        [Header("Camera")]
        // Framed by working out where the corners project rather than by eye:
        // the outermost nodes land at 72% of the frame, the plate's far edge
        // stays inside it, and its near edge falls just off the bottom, so the
        // ground runs off screen the way ground should instead of ending in a
        // visible ledge.
        [Range(0f, 80f)]
        [SerializeField] private float cameraTilt = 46f;
        [SerializeField] private float cameraDistance = 29f;
        [SerializeField] private float cameraFieldOfView = 40f;

        [Tooltip("How far ahead of the player the camera looks, in world units.")]
        [SerializeField] private float cameraLookAhead = 5.5f;
        [Min(0.5f)]
        [SerializeField] private float cameraFollowSpeed = 4f;
        // Dark, with the nodes standing on a mid-toned plate rather than
        // straight against it. Read as luminance, the node palette spans from
        // the boss at 0.13 to a reward at 0.79, so no single backdrop can
        // contrast with all of them — the plate at 0.35 sits in the middle and
        // gives every node an edge in one direction or the other.
        [SerializeField] private Color background =
            new Color(0.055f, 0.07f, 0.09f, 1f);

        [SerializeField] private Color plateColor =
            new Color(0.008f, 0.012f, 0.018f, 1f);

        [Tooltip("How far the plate reaches past the outermost nodes.")]
        [SerializeField] private float plateMargin = 2.2f;

        [Header("Walk")]
        [Min(0.05f)]
        [Tooltip("How long the token takes to travel between two nodes.")]
        [SerializeField] private float walkDuration = 0.7f;
        [Min(0f)]
        [SerializeField] private float walkArc = 0.6f;

        private readonly Dictionary<Collider, LudoRunNode> nodePickers =
            new Dictionary<Collider, LudoRunNode>(24);

        // Reused so hovering costs no allocation per frame.
        private readonly RaycastHit[] rayHits = new RaycastHit[16];

        /// <summary>Which map the colliders and rival tokens were built for.</summary>
        private LudoRunMap builtMap;

        private LudoRunState run;
        private Camera mapCamera;
        private Transform root;
        private GameObject geometry;
        private GameObject pickers;
        private GameObject rivalTokens;
        private GameObject tokenObject;
        private Material mapMaterial;
        private Mesh mapMesh;

        private readonly LudoBoardCameraSuspender boardCamera =
            new LudoBoardCameraSuspender();

        // What the built mesh currently reflects, so it is only rebuilt when
        // something it draws actually changed.
        private int builtStage = -1;
        private int builtLane = -1;
        private LudoRunStatus builtStatus = LudoRunStatus.Lost;

        private LudoRunNode hoveredNode;
        private Vector3 walkFrom;
        private Vector3 walkTo;
        private float walkProgress = 1f;
        private float focusY;
        private float focusZ;

        /// <summary>
        /// Raised with the node the player clicked. The map spots the click
        /// because it owns the camera, but never moves the run itself — the
        /// controller decides whether the move is legal and when to walk.
        /// </summary>
        public System.Action<LudoRunNode> NodeClicked;

        /// <summary>True while the token is travelling between two nodes.</summary>
        public bool IsWalking => walkProgress < 1f;

        private Vector3 Origin => new Vector3(MapDistance, 0f, 0f);

        public void Show(LudoRunState runState)
        {
            if (runState == null)
            {
                Hide();
                return;
            }

            run = runState;
            EnsureRoot();
            root.gameObject.SetActive(true);
            EnsureNodeObjects();
            RebuildIfChanged();
            EnsureToken();
            EnterMapView();
        }

        public void Hide()
        {
            run = null;
            hoveredNode = null;
            if (mapCamera != null)
            {
                mapCamera.enabled = false;
            }

            boardCamera.Restore();
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Sends the token walking to a node. The controller waits on
        /// <see cref="IsWalking"/> before entering it, so the move is something
        /// the player watches rather than a jump they miss.
        /// </summary>
        public void BeginWalk(LudoRunNode target)
        {
            if (target == null || tokenObject == null)
            {
                return;
            }

            walkFrom = tokenObject.transform.position;
            walkTo = NodeWorldPosition(target.Stage, target.Lane) + Up * nodeHeight;
            walkProgress = 0f;
        }

        private void LateUpdate()
        {
            if (run == null)
            {
                return;
            }

            AdvanceWalk();
            UpdateCameraPose(false);

            // Rebuilt only on a real change, so standing on the map costs
            // nothing per frame.
            RebuildIfChanged();
            HandlePicking();
        }

        private void OnDestroy()
        {
            boardCamera.Restore();
            if (root != null)
            {
                Destroy(root.gameObject);
            }

            DestroyGenerated(mapMaterial);
            DestroyGenerated(mapMesh);
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

        private void EnterMapView()
        {
            if (mapCamera == null)
            {
                GameObject cameraObject = new GameObject("RunMapCamera")
                {
                    hideFlags = HideFlags.DontSave
                };
                cameraObject.transform.SetParent(root, false);
                mapCamera = cameraObject.AddComponent<Camera>();
                mapCamera.orthographic = false;
                mapCamera.clearFlags = CameraClearFlags.SolidColor;
                mapCamera.backgroundColor = background;

                // The cel outline is a pipeline effect, not something each
                // object carries, so a camera that skips post-processing gets
                // none of it. The arena turns this on and the map did not,
                // which is most of why the two looked like different games.
                UniversalAdditionalCameraData cameraData =
                    mapCamera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
                cameraData.antialiasing =
                    AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
            }

            mapCamera.fieldOfView = cameraFieldOfView;
            UpdateCameraPose(true);
            mapCamera.enabled = true;

            boardCamera.Suspend(mapCamera);
        }

        /// <summary>
        /// Keeps the camera over the player rather than over the whole map.
        ///
        /// Twelve stages are more than twice what one fixed shot can hold, so
        /// it follows instead, biased forward so more of the road ahead is
        /// visible than the road already walked — what is behind has already
        /// been decided.
        /// </summary>
        private void UpdateCameraPose(bool snap)
        {
            if (mapCamera == null || run == null)
            {
                return;
            }

            Vector3 here = NodeLocalPosition(run.Stage, run.Lane);
            float target = here.y + cameraLookAhead;
            float targetZ = here.z;
            float blend = snap
                ? 1f
                : 1f - Mathf.Exp(-cameraFollowSpeed * Time.unscaledDeltaTime);
            focusY = Mathf.Lerp(focusY, target, blend);
            focusZ = Mathf.Lerp(focusZ, targetZ, blend);

            // Same rig as the arena: tilting down the -Y axis turns the plane
            // the nodes sit on into ground receding into the distance, which is
            // what makes a flat graph read as a journey.
            float radians = cameraTilt * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                0f,
                -Mathf.Sin(radians) * cameraDistance,
                -Mathf.Cos(radians) * cameraDistance);
            Vector3 up = new Vector3(0f, Mathf.Cos(radians), -Mathf.Sin(radians));

            Vector3 focus = Origin + new Vector3(0f, focusY, focusZ);
            mapCamera.transform.position = focus + offset;
            mapCamera.transform.rotation =
                Quaternion.LookRotation(-offset.normalized, up);
        }

        private void EnsureRoot()
        {
            if (root != null)
            {
                return;
            }

            GameObject rootObject = new GameObject("RunMap")
            {
                hideFlags = HideFlags.DontSave
            };
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.position = Origin;
            root = rootObject.transform;
        }

        // ------------------------------------------------------------------
        // Geometry
        // ------------------------------------------------------------------

        private void RebuildIfChanged()
        {
            if (run == null)
            {
                return;
            }

            if (geometry != null &&
                builtStage == run.Stage &&
                builtLane == run.Lane &&
                builtStatus == run.Status)
            {
                return;
            }

            builtStage = run.Stage;
            builtLane = run.Lane;
            builtStatus = run.Status;
            Rebuild();
        }

        private void Rebuild()
        {
            LudoProceduralMesh builder = new LudoProceduralMesh();

            BuildPlate(builder);

            for (int stage = 0; stage < run.Map.StageCount - 1; stage++)
            {
                foreach (LudoRunNode node in run.Map.Stage(stage))
                {
                    foreach (int lane in node.NextLanes)
                    {
                        BuildLink(builder, node, run.Map.Node(stage + 1, lane));
                    }
                }
            }

            for (int stage = 0; stage < run.Map.StageCount; stage++)
            {
                foreach (LudoRunNode node in run.Map.Stage(stage))
                {
                    BuildNode(builder, node);
                }
            }

            EnsureGeometryObject();
            DestroyGenerated(mapMesh);
            mapMesh = builder.Build("RunMap");
            geometry.GetComponent<MeshFilter>().sharedMesh = mapMesh;
        }

        /// <summary>
        /// The ground the map sits on, sized from the map itself so a longer
        /// run or a wider stage still lands on it. Without this the nodes float
        /// against the backdrop and no single backdrop colour can suit them all.
        /// </summary>
        private void BuildPlate(LudoProceduralMesh builder)
        {
            float widest = 0f;
            for (int stage = 0; stage < run.Map.StageCount; stage++)
            {
                widest = Mathf.Max(widest, run.Map.Stage(stage).Count);
            }

            float halfWidth = (widest - 1) * 0.5f * laneSpacing + plateMargin;
            float halfDepth =
                (run.Map.StageCount - 1) * 0.5f * stageSpacing + plateMargin;

            // Built one strip per stage rather than as a single slab, so it
            // climbs with the nodes. A flat plate under a rising path would
            // leave the far stages hanging over nothing.
            int strips = run.Map.StageCount;
            Color far = LudoBoardVisualStyle.Lighten(plateColor, 0.12f);

            for (int stage = 0; stage <= strips; stage++)
            {
                float y0 = -halfDepth + (halfDepth * 2f) * stage / (strips + 1);
                float y1 = -halfDepth + (halfDepth * 2f) * (stage + 1) / (strips + 1);
                float z0 = PlateHeightAt(y0) + 0.01f;
                float z1 = PlateHeightAt(y1) + 0.01f;

                // Sloped, so it catches a different lighting band from the
                // node faces standing on it.
                Vector3 normal = new Vector3(0f, z1 - z0, y1 - y0).normalized;
                normal = new Vector3(0f, normal.y, normal.z);
                if (Vector3.Dot(normal, Up) < 0f)
                {
                    normal = -normal;
                }

                Color near = Color.Lerp(plateColor, far, (float)stage / (strips + 1));
                Color next = Color.Lerp(plateColor, far, (stage + 1f) / (strips + 1));

                builder.AddQuad(
                    new Vector3(-halfWidth, y0, z0),
                    new Vector3(halfWidth, y0, z0),
                    new Vector3(halfWidth, y1, z1),
                    new Vector3(-halfWidth, y1, z1),
                    Up, Up, Up, Up,
                    near, near, next, next);

                // Side walls, so the path has an edge instead of ending in
                // nothing where it leaves the frame.
                Color edge = LudoBoardVisualStyle.Shade(near, 0.4f);
                float skirt = 0.9f;
                builder.AddQuad(
                    new Vector3(-halfWidth, y0, z0),
                    new Vector3(-halfWidth, y1, z1),
                    new Vector3(-halfWidth, y1, z1 + skirt),
                    new Vector3(-halfWidth, y0, z0 + skirt),
                    Vector3.left, Vector3.left, Vector3.left, Vector3.left,
                    edge, edge, edge, edge);
                builder.AddQuad(
                    new Vector3(halfWidth, y0, z0),
                    new Vector3(halfWidth, y0, z0 + skirt),
                    new Vector3(halfWidth, y1, z1 + skirt),
                    new Vector3(halfWidth, y1, z1),
                    Vector3.right, Vector3.right, Vector3.right, Vector3.right,
                    edge, edge, edge, edge);
            }
        }

        /// <summary>The plate's height at a depth, matching the stages' climb.</summary>
        private float PlateHeightAt(float y)
        {
            float stage = y / stageSpacing + (run.Map.StageCount - 1) * 0.5f;
            return -stage * stageRise;
        }

        /// <summary>
        /// A node as a low bevelled platform. Reachable ones stand taller and keep their
        /// full colour; everything else is dimmed but still drawn, so the shape
        /// of the run ahead stays readable.
        /// </summary>
        private void BuildNode(LudoProceduralMesh builder, LudoRunNode node)
        {
            Vector3 centre = NodeLocalPosition(node.Stage, node.Lane);
            Vector2 flat = new Vector2(centre.x, centre.y);

            bool current = run.IsCurrent(node);
            bool reachable = run.IsChoice(node);
            bool live = current || reachable;

            Color top = NodeColor(node.Kind);
            if (!live)
            {
                top = LudoBoardVisualStyle.Shade(top, 0.55f);
            }
            else if (reachable)
            {
                top = LudoBoardVisualStyle.Lighten(top, 0.22f);
            }

            Color side = LudoBoardVisualStyle.Shade(top, 0.42f);
            float height = live ? nodeHeight * 1.6f : nodeHeight;
            float radius = node.Kind == LudoRunNodeKind.Boss
                ? nodeRadius * 1.5f
                : node.Kind == LudoRunNodeKind.Elite
                    ? nodeRadius * 1.2f
                    : nodeRadius;

            float groundZ = centre.z;
            float topZ = groundZ - height;

            // The reference uses chunky square tiles rather than cairns or
            // circular pedestals. Two stacked boxes provide its dark base,
            // coloured shoulder and smaller highlighted face.
            AddBar(builder, flat, radius, radius * 0.72f, groundZ, topZ + 0.12f, side, true);
            Color face = LudoBoardVisualStyle.Lighten(top, 0.08f);
            AddBar(
                builder,
                flat,
                radius * 0.88f,
                radius * 0.60f,
                topZ + 0.12f,
                topZ,
                face,
                true);
            BuildNodeMarker(builder, node, flat, radius * 0.72f, topZ, live);
        }

        /// <summary>
        /// What sits on top of a node beyond its disc.
        ///
        /// A duel gets the rival's actual token, since it is one opponent and
        /// knowing which element it is decides whether the fight is worth
        /// taking. A match or an elite is played against every other seat at
        /// once, so a single token there would be a lie — those get three pips
        /// instead, one per rival. The boss gets a ring of spikes.
        /// </summary>
        private void BuildNodeMarker(
            LudoProceduralMesh builder,
            LudoRunNode node,
            Vector2 flat,
            float radius,
            float topZ,
            bool live)
        {
            Color pip = live
                ? LudoBoardVisualStyle.Paper
                : LudoBoardVisualStyle.Shade(LudoBoardVisualStyle.Paper, 0.45f);

            switch (node.Kind)
            {
                case LudoRunNodeKind.Heal:
                {
                    // A plus sign, built as two crossed bars so it reads as a
                    // health cross at any angle the camera happens to be at.
                    float arm = radius * 0.62f;
                    float thick = radius * 0.2f;
                    float lift = topZ - thick;
                    Color cross = live
                        ? LudoBoardVisualStyle.Paper
                        : LudoBoardVisualStyle.Shade(LudoBoardVisualStyle.Paper, 0.45f);

                    AddBar(builder, flat, arm, thick, topZ, lift, cross, true);
                    AddBar(builder, flat, arm, thick, topZ, lift, cross, false);
                    break;
                }

                case LudoRunNodeKind.Match:
                case LudoRunNodeKind.Elite:
                {
                    float spread = radius * 0.45f;
                    float pipRadius = radius * 0.17f;
                    for (int index = 0; index < 3; index++)
                    {
                        float angle = Mathf.PI * 0.5f + index * Mathf.PI * 2f / 3f;
                        Vector2 at = flat + new Vector2(
                            Mathf.Cos(angle) * spread,
                            Mathf.Sin(angle) * spread);
                        builder.AddCylinder(
                            at, pipRadius, topZ, topZ - pipRadius * 1.6f, 8, pip, pip);
                        builder.AddDisc(
                            at, pipRadius, topZ - pipRadius * 1.6f, 8, pip, Up);
                    }

                    break;
                }

                case LudoRunNodeKind.Boss:
                {
                    float spread = radius * 0.62f;
                    float spikeRadius = radius * 0.14f;
                    for (int index = 0; index < 5; index++)
                    {
                        float angle = Mathf.PI * 0.5f + index * Mathf.PI * 2f / 5f;
                        Vector2 at = flat + new Vector2(
                            Mathf.Cos(angle) * spread,
                            Mathf.Sin(angle) * spread);
                        float tall = spikeRadius * (index % 2 == 0 ? 4.2f : 2.8f);
                        builder.AddCylinder(
                            at, spikeRadius, topZ, topZ - tall, 6, pip, pip);
                        builder.AddDisc(at, spikeRadius, topZ - tall, 6, pip, Up);
                    }

                    break;
                }
            }
        }

        /// <summary>One bar of the heal cross, raised off the node's top face.</summary>
        private static void AddBar(
            LudoProceduralMesh builder,
            Vector2 centre,
            float halfLength,
            float halfWidth,
            float baseZ,
            float topZ,
            Color colour,
            bool alongX)
        {
            float halfX = alongX ? halfLength : halfWidth;
            float halfY = alongX ? halfWidth : halfLength;

            Vector3 a = new Vector3(centre.x - halfX, centre.y - halfY, topZ);
            Vector3 b = new Vector3(centre.x + halfX, centre.y - halfY, topZ);
            Vector3 c = new Vector3(centre.x + halfX, centre.y + halfY, topZ);
            Vector3 d = new Vector3(centre.x - halfX, centre.y + halfY, topZ);
            builder.AddQuad(a, b, c, d, Up, Up, Up, Up, colour, colour, colour, colour);

            // Sides, so the cross has thickness from a low angle rather than
            // looking painted on.
            Vector3 la = new Vector3(a.x, a.y, baseZ);
            Vector3 lb = new Vector3(b.x, b.y, baseZ);
            Vector3 lc = new Vector3(c.x, c.y, baseZ);
            Vector3 ld = new Vector3(d.x, d.y, baseZ);
            Color side = LudoBoardVisualStyle.Shade(colour, 0.28f);

            AddSide(builder, la, lb, b, a, side);
            AddSide(builder, lb, lc, c, b, side);
            AddSide(builder, lc, ld, d, c, side);
            AddSide(builder, ld, la, a, d, side);
        }

        private static void AddSide(
            LudoProceduralMesh builder,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Color colour)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            builder.AddQuad(
                a, b, c, d,
                normal, normal, normal, normal,
                colour, colour, colour, colour);
        }

        /// <summary>A flat ribbon on the ground joining two nodes.</summary>
        private void BuildLink(
            LudoProceduralMesh builder,
            LudoRunNode from,
            LudoRunNode to)
        {
            Vector3 a = NodeLocalPosition(from.Stage, from.Lane);
            Vector3 b = NodeLocalPosition(to.Stage, to.Lane);

            Vector3 along = b - a;
            if (along.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            along.Normalize();

            // Walked roads keep the player's own colour and run wider, so the
            // map shows the line taken through it and not just the fork ahead.
            bool walked = run.HasWalked(from, to);
            Color colour;
            if (walked)
            {
                colour = PlayerColor();
            }
            else if (run.IsCurrent(from) && run.IsChoice(to))
            {
                colour = LudoBoardVisualStyle.Lighten(NodeColor(to.Kind), 0.16f);
            }
            else
            {
                colour = LudoBoardVisualStyle.Shade(NodeColor(to.Kind), 0.42f);
            }

            float width = walked ? linkWidth * 1.9f : linkWidth;

            // Perpendicular to the run of the link but still level across it,
            // so a ribbon climbing between stages stays flat side to side
            // rather than twisting.
            Vector3 side = new Vector3(-along.y, along.x, 0f).normalized * (width * 0.5f);

            // Just clear of the ground so it never z-fights the base, and a
            // walked road sits above an unwalked one where they overlap.
            float lift = walked ? 0.06f : 0.03f;
            Vector3 lifted = Up * lift;

            builder.AddQuad(
                a - side + lifted,
                b - side + lifted,
                b + side + lifted,
                a + side + lifted,
                Up, Up, Up, Up,
                colour, colour, colour, colour);
        }

        /// <summary>
        /// Node colours, checked against the plate they sit on rather than
        /// picked by eye. Pure red reads far darker than it looks — luminance
        /// weights green most — so an unlightened elite came out at 0.27
        /// against a 0.35 plate and would have all but vanished. The boss goes
        /// the other way instead of the same way as the elite, both to clear
        /// the plate downward and to tell the two apart.
        /// </summary>
        private static Color NodeColor(LudoRunNodeKind kind)
        {
            return kind switch
            {
                LudoRunNodeKind.Reward => LudoBoardVisualStyle.Yellow,
                LudoRunNodeKind.Duel =>
                    LudoBoardVisualStyle.Lighten(LudoBoardVisualStyle.Red, 0.12f),
                LudoRunNodeKind.Heal =>
                    LudoBoardVisualStyle.Lighten(LudoBoardVisualStyle.Blue, 0.2f),
                LudoRunNodeKind.Match =>
                    LudoBoardVisualStyle.Lighten(LudoBoardVisualStyle.Red, 0.05f),
                LudoRunNodeKind.Elite =>
                    LudoBoardVisualStyle.Lighten(LudoBoardVisualStyle.Red, 0.28f),
                LudoRunNodeKind.Boss =>
                    LudoBoardVisualStyle.Lighten(LudoBoardVisualStyle.Yellow, 0.08f),
                _ => LudoBoardVisualStyle.SafeCell
            };
        }

        /// <summary>
        /// Where a node sits relative to the map's own root. Stages run away
        /// from the camera and lanes across, centred so the whole map is framed
        /// by one fixed shot.
        ///
        /// Local, not world, and the distinction matters: mesh vertices are
        /// drawn by an object already parented to the root, so folding the
        /// origin in here too would place the geometry twice as far out as the
        /// camera looking at it — invisible, while transform-positioned things
        /// like the token still landed correctly.
        /// </summary>
        private Vector3 NodeLocalPosition(int stage, int lane)
        {
            int laneCount = run.Map.Stage(stage).Count;
            float x = (lane - (laneCount - 1) * 0.5f) * laneSpacing;
            float y = (stage - (run.Map.StageCount - 1) * 0.5f) * stageSpacing;

            // Each stage stands a little higher than the last, so the run reads
            // as a climb toward the final game rather than a flat chart. It
            // also breaks the sameness of a scene made almost entirely of
            // level surfaces, which is what left everything in one lighting
            // band.
            float z = -stage * stageRise;
            return new Vector3(x, y, z);
        }

        /// <summary>Where a node sits in the world, for anything positioned by transform.</summary>
        private Vector3 NodeWorldPosition(int stage, int lane)
        {
            return Origin + NodeLocalPosition(stage, lane);
        }

        private void EnsureGeometryObject()
        {
            if (geometry != null)
            {
                return;
            }

            geometry = new GameObject("RunMapGeometry")
            {
                hideFlags = HideFlags.DontSave
            };
            geometry.transform.SetParent(root, false);
            geometry.transform.localPosition = Vector3.zero;
            geometry.AddComponent<MeshFilter>();

            MeshRenderer renderer = geometry.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            Shader shader = Shader.Find(MapShaderName);
            if (shader == null)
            {
                Debug.LogError($"RunMap: shader '{MapShaderName}' not found.", this);
                return;
            }

            mapMaterial = new Material(shader)
            {
                name = "Run Map",
                hideFlags = HideFlags.DontSave
            };
            renderer.sharedMaterial = mapMaterial;
        }

        // ------------------------------------------------------------------
        // Picking
        // ------------------------------------------------------------------

        /// <summary>
        /// Colliders and rival tokens, built once for a map rather than with
        /// every mesh rebuild.
        ///
        /// Neither depends on anything the rebuild changes — a node never moves
        /// and its rival never changes — and remaking them was actively
        /// harmful: Destroy only takes effect at the end of the frame, so for
        /// one frame the old colliders were still in the scene while the
        /// dictionary held only the new ones, and a ray hitting an old one
        /// found nothing and gave up.
        /// </summary>
        private void EnsureNodeObjects()
        {
            if (builtMap == run.Map)
            {
                return;
            }

            builtMap = run.Map;
            ClearNodeObjects();

            for (int stage = 0; stage < run.Map.StageCount; stage++)
            {
                foreach (LudoRunNode node in run.Map.Stage(stage))
                {
                    BuildPicker(node);
                    BuildRivalToken(node);
                }
            }
        }

        private void ClearNodeObjects()
        {
            nodePickers.Clear();
            if (pickers != null)
            {
                Destroy(pickers);
                pickers = null;
            }

            ClearRivalTokens();
        }

        /// <summary>
        /// One invisible collider per node. Kept separate from the drawn mesh
        /// so the whole map can be one mesh for rendering and still be picked
        /// node by node.
        /// </summary>
        private void BuildPicker(LudoRunNode node)
        {
            if (pickers == null)
            {
                pickers = new GameObject("RunMapPickers")
                {
                    hideFlags = HideFlags.DontSave
                };
                pickers.transform.SetParent(root, false);
                pickers.transform.localPosition = Vector3.zero;
            }

            GameObject picker = new GameObject($"Node{node.Stage}_{node.Lane}")
            {
                hideFlags = HideFlags.DontSave
            };
            picker.transform.SetParent(pickers.transform, false);
            picker.transform.position = NodeWorldPosition(node.Stage, node.Lane);

            SphereCollider collider = picker.AddComponent<SphereCollider>();
            collider.radius = nodeRadius * 1.15f;
            nodePickers[collider] = node;
        }

        private void HandlePicking()
        {
            hoveredNode = null;
            if (mapCamera == null || run == null || run.IsOver || IsWalking)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            // Every hit, not just the nearest. A rival token floats over its
            // node, and taking only the first thing the ray met would let it
            // shadow the very node it advertises — which is exactly the case
            // where the player most wants to click.
            Ray ray = mapCamera.ScreenPointToRay(mouse.position.ReadValue());
            int count = Physics.RaycastNonAlloc(ray, rayHits, 200f);

            LudoRunNode node = null;
            float nearest = float.MaxValue;
            for (int index = 0; index < count; index++)
            {
                if (!nodePickers.TryGetValue(rayHits[index].collider, out LudoRunNode found) ||
                    !run.IsChoice(found) ||
                    rayHits[index].distance >= nearest)
                {
                    continue;
                }

                nearest = rayHits[index].distance;
                node = found;
            }

            if (node == null)
            {
                return;
            }

            hoveredNode = node;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                NodeClicked?.Invoke(node);
            }
        }

        /// <summary>The node under the pointer, for the panel to name.</summary>
        public LudoRunNode HoveredNode => hoveredNode;

        // ------------------------------------------------------------------
        // Token
        // ------------------------------------------------------------------

        private void ClearRivalTokens()
        {
            if (rivalTokens == null)
            {
                return;
            }

            Destroy(rivalTokens);
            rivalTokens = null;
        }

        /// <summary>
        /// The opponent's own token, floating over a duel node.
        ///
        /// Only duels get one: they are fought against a single rival, so the
        /// element on show is the one the player will actually face and the
        /// elemental advantage is readable before committing to a fork. Matches
        /// and elites are played against every seat at once and get pips
        /// instead.
        /// </summary>
        private void BuildRivalToken(LudoRunNode node)
        {
            if (node.Kind != LudoRunNodeKind.Duel)
            {
                return;
            }

            PlayerStyle style = StyleForElement(node.RivalElement);
            if (style == null || style.TokenModel == null)
            {
                return;
            }

            if (rivalTokens == null)
            {
                rivalTokens = new GameObject("RunMapRivals")
                {
                    hideFlags = HideFlags.DontSave
                };
                rivalTokens.transform.SetParent(root, false);
                rivalTokens.transform.localPosition = Vector3.zero;
            }

            GameObject holder = new GameObject($"Rival{node.Stage}_{node.Lane}")
            {
                hideFlags = HideFlags.DontSave
            };
            holder.transform.SetParent(rivalTokens.transform, false);
            holder.transform.position =
                NodeWorldPosition(node.Stage, node.Lane) + Up * (nodeHeight * 2.6f);

            GameObject visualObject = new GameObject("RivalVisual")
            {
                hideFlags = HideFlags.DontSave
            };
            visualObject.transform.SetParent(holder.transform, false);
            TokenVisual visual = visualObject.AddComponent<TokenVisual>();
            visual.SetStyle(style);
            visual.SetUseElementalModel(true);
            visualObject.transform.localScale =
                Vector3.one * (tokenSize * rivalTokenScale);

            // Nothing here is clickable: the node's own picker is what the
            // player aims at, and a collider up here would shadow it.
            foreach (Collider modelCollider in
                     visualObject.GetComponentsInChildren<Collider>(true))
            {
                modelCollider.enabled = false;
            }
        }

        private void EnsureToken()
        {
            if (tokenObject != null)
            {
                // Left alone mid-walk. Show runs every frame, and the run's
                // stage does not move until the token arrives, so snapping it
                // here would drag the piece back to where it set off from on
                // every frame of its own journey.
                if (!IsWalking)
                {
                    tokenObject.transform.position =
                        NodeWorldPosition(run.Stage, run.Lane) + Up * nodeHeight;
                }

                return;
            }

            PlayerStyle style = StyleForElement(run.Element);
            if (style == null || style.TokenModel == null)
            {
                return;
            }

            tokenObject = new GameObject("RunToken")
            {
                hideFlags = HideFlags.DontSave
            };
            tokenObject.transform.SetParent(root, false);
            tokenObject.transform.position =
                NodeWorldPosition(run.Stage, run.Lane) + Up * nodeHeight;

            // Through TokenVisual, like the arena does, so the piece on the map
            // is the same cartoon-shaded token the player moves on the board.
            GameObject visualObject = new GameObject("RunTokenVisual")
            {
                hideFlags = HideFlags.DontSave
            };
            visualObject.transform.SetParent(tokenObject.transform, false);
            TokenVisual visual = visualObject.AddComponent<TokenVisual>();
            visual.SetStyle(style);
            visual.SetUseElementalModel(true);
            visualObject.transform.localScale = Vector3.one * tokenSize;

            foreach (Collider modelCollider in
                     visualObject.GetComponentsInChildren<Collider>(true))
            {
                modelCollider.enabled = false;
            }
        }

        /// <summary>The run element's own colour, for the trail behind the token.</summary>
        private Color PlayerColor()
        {
            PlayerStyle style = StyleForElement(run.Element);
            return style != null
                ? LudoBoardVisualStyle.Lighten(style.TokenColor, 0.12f)
                : LudoBoardVisualStyle.Paper;
        }

        private static PlayerStyle StyleForElement(LudoElement element)
        {
            foreach (Token token in FindObjectsByType<Token>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (token.OwnerStyle != null && token.OwnerStyle.Element == element)
                {
                    return token.OwnerStyle;
                }
            }

            return null;
        }

        private void AdvanceWalk()
        {
            if (walkProgress >= 1f || tokenObject == null)
            {
                return;
            }

            walkProgress = Mathf.Min(
                1f,
                walkProgress + Time.unscaledDeltaTime / walkDuration);
            float eased =
                walkProgress * walkProgress * (3f - 2f * walkProgress);

            // A hop rather than a slide, so a short step still reads as a move.
            Vector3 position = Vector3.Lerp(walkFrom, walkTo, eased);
            position += Up * (Mathf.Sin(walkProgress * Mathf.PI) * walkArc);
            tokenObject.transform.position = position;
        }
    }
}
