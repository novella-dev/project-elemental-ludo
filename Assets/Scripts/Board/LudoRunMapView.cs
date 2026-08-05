using System.Collections.Generic;
using ElementalLudo.Gameplay;
using ElementalLudo.Tokens;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

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
        [SerializeField] private float nodeRadius = 0.85f;
        [SerializeField] private float nodeHeight = 0.45f;
        [SerializeField] private float linkWidth = 0.16f;
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
        // Dark, with the nodes standing on a mid-toned plate rather than
        // straight against it. Read as luminance, the node palette spans from
        // the boss at 0.13 to a reward at 0.79, so no single backdrop can
        // contrast with all of them — the plate at 0.35 sits in the middle and
        // gives every node an edge in one direction or the other.
        [SerializeField] private Color background =
            LudoBoardVisualStyle.Shade(LudoBoardVisualStyle.SafeCell, 0.80f);

        [SerializeField] private Color plateColor =
            LudoBoardVisualStyle.Shade(LudoBoardVisualStyle.SafeCell, 0.52f);

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
            }

            mapCamera.fieldOfView = cameraFieldOfView;

            // Same rig as the arena: tilting down the -Y axis turns the plane
            // the nodes sit on into ground receding into the distance, which is
            // what makes a flat graph read as a journey.
            float radians = cameraTilt * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                0f,
                -Mathf.Sin(radians) * cameraDistance,
                -Mathf.Cos(radians) * cameraDistance);
            Vector3 up = new Vector3(0f, Mathf.Cos(radians), -Mathf.Sin(radians));

            mapCamera.transform.position = Origin + offset;
            mapCamera.transform.rotation =
                Quaternion.LookRotation(-offset.normalized, up);
            mapCamera.enabled = true;

            boardCamera.Suspend(mapCamera);
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

            ClearPickers();
            ClearRivalTokens();
            for (int stage = 0; stage < run.Map.StageCount; stage++)
            {
                foreach (LudoRunNode node in run.Map.Stage(stage))
                {
                    BuildNode(builder, node);
                    BuildPicker(node);
                    BuildRivalToken(node);
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

            // Fractionally below the ground plane, so the links laid on top of
            // it never fight it for the same pixels.
            const float depth = 0.01f;

            builder.AddQuad(
                new Vector3(-halfWidth, -halfDepth, depth),
                new Vector3(halfWidth, -halfDepth, depth),
                new Vector3(halfWidth, halfDepth, depth),
                new Vector3(-halfWidth, halfDepth, depth),
                Up, Up, Up, Up,
                plateColor, plateColor, plateColor, plateColor);
        }

        /// <summary>
        /// A node as a low disc. Reachable ones stand taller and keep their
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

            Color side = LudoBoardVisualStyle.Shade(top, 0.3f);
            float height = live ? nodeHeight * 1.6f : nodeHeight;
            float radius = node.Kind == LudoRunNodeKind.Boss
                ? nodeRadius * 1.5f
                : node.Kind == LudoRunNodeKind.Elite
                    ? nodeRadius * 1.2f
                    : nodeRadius;

            int segments = node.Kind == LudoRunNodeKind.Boss ? 6 : 12;
            builder.AddCylinder(flat, radius, 0f, -height, segments, side, top);
            builder.AddDisc(flat, radius, -height, segments, top, Up);
            BuildNodeMarker(builder, node, flat, radius, -height, live);
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
                colour = LudoBoardVisualStyle.Paper;
            }
            else
            {
                colour = LudoBoardVisualStyle.Shade(LudoBoardVisualStyle.SafeCell, 0.35f);
            }

            float width = walked ? linkWidth * 1.9f : linkWidth;

            // Perpendicular within the ground plane, which is XY here.
            Vector3 side = new Vector3(-along.y, along.x, 0f) * (width * 0.5f);

            // Just clear of the ground so it never z-fights the base, and a
            // walked road sits above an unwalked one where they overlap.
            float lift = walked ? 0.04f : 0.02f;
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
                    LudoBoardVisualStyle.Lighten(LudoBoardVisualStyle.Blue, 0.15f),
                LudoRunNodeKind.Match => LudoBoardVisualStyle.SandMid,
                LudoRunNodeKind.Elite =>
                    LudoBoardVisualStyle.Lighten(LudoBoardVisualStyle.Red, 0.38f),
                LudoRunNodeKind.Boss =>
                    LudoBoardVisualStyle.Shade(LudoBoardVisualStyle.Red, 0.55f),
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
            return new Vector3(x, y, 0f);
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

        private void ClearPickers()
        {
            nodePickers.Clear();
            if (pickers == null)
            {
                return;
            }

            Destroy(pickers);
            pickers = null;
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

            Ray ray = mapCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                return;
            }

            if (!nodePickers.TryGetValue(hit.collider, out LudoRunNode node) ||
                !run.IsChoice(node))
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
