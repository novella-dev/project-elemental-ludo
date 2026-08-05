using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ElementalLudo.Board
{
    /// <summary>
    /// The start menu's backdrop. Built the same way as
    /// <see cref="LudoRunMapView"/> and <see cref="LudoCombatArena"/> — its
    /// own camera and procedural geometry, parked far from the board — so
    /// opening the menu never means staring at a live board nobody has
    /// started playing on yet.
    ///
    /// A plain turntable rather than anything narrative: a dais with the
    /// board's four token colours standing around it, slowly turning. It only
    /// has to prove the game has a look before the player has chosen
    /// anything, not tell a story.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LudoStartMenuBackdrop : MonoBehaviour
    {
        private const float MenuDistance = -1500f;
        private const string MenuShaderName = "Elemental Ludo/Board Vertex Color";
        private const int Segments = 40;

        // Board convention: the XY plane is the ground and -Z is up.
        private static readonly Vector3 Up = new Vector3(0f, 0f, -1f);

        [Header("Camera")]
        [SerializeField] private float cameraFieldOfView = 38f;
        [SerializeField] private float cameraDistance = 10f;
        [SerializeField] private float cameraTilt = 28f;
        [SerializeField] private Color background = new Color(0.03f, 0.035f, 0.05f, 1f);

        [Header("Turntable")]
        [SerializeField] private float floorRadius = 7f;
        [SerializeField] private float daisRadius = 3f;
        [SerializeField] private float pillarRingRadius = 4.6f;
        [SerializeField] private float pillarRadius = 0.4f;
        [SerializeField] private float pillarHeight = 1.6f;
        [SerializeField] private float spinDegreesPerSecond = 3f;

        private Transform root;
        private Transform turntable;
        private Camera menuCamera;
        private Material menuMaterial;
        private bool visible;

        private readonly LudoBoardCameraSuspender boardCamera =
            new LudoBoardCameraSuspender();

        private Vector3 Origin => new Vector3(MenuDistance, 0f, 0f);

        public void Show()
        {
            EnsureRoot();
            EnsureGeometry();
            EnsureCamera();
            menuCamera.enabled = true;
            boardCamera.Suspend(menuCamera);
            visible = true;
        }

        public void Hide()
        {
            visible = false;
            if (menuCamera != null)
            {
                menuCamera.enabled = false;
            }

            boardCamera.Restore();
        }

        private void OnDestroy()
        {
            boardCamera.Restore();
        }

        private void Update()
        {
            if (!visible || turntable == null)
            {
                return;
            }

            turntable.Rotate(
                Vector3.forward,
                spinDegreesPerSecond * Time.unscaledDeltaTime,
                Space.Self);
        }

        private void EnsureRoot()
        {
            if (root != null)
            {
                return;
            }

            GameObject rootObject = new GameObject("StartMenuBackdropRoot")
            {
                hideFlags = HideFlags.DontSave
            };
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.position = Origin;
            root = rootObject.transform;
        }

        private void EnsureCamera()
        {
            if (menuCamera != null)
            {
                return;
            }

            GameObject cameraObject = new GameObject("StartMenuCamera")
            {
                hideFlags = HideFlags.DontSave
            };
            cameraObject.transform.SetParent(root, false);
            menuCamera = cameraObject.AddComponent<Camera>();
            menuCamera.orthographic = false;
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = background;
            menuCamera.fieldOfView = cameraFieldOfView;

            // Same pipeline effect the map and arena turn on: without it the
            // cel outline never appears and the backdrop reads as flatter
            // than the game it is supposed to be advertising.
            UniversalAdditionalCameraData cameraData =
                menuCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing =
                AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;

            // Same rig as the run map: tilting down the -Y axis turns the
            // ground plane into something receding into the distance.
            float radians = cameraTilt * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                0f,
                -Mathf.Sin(radians) * cameraDistance,
                -Mathf.Cos(radians) * cameraDistance);
            Vector3 up = new Vector3(0f, Mathf.Cos(radians), -Mathf.Sin(radians));
            Vector3 focus = Origin + new Vector3(0f, 0.6f, -0.8f);

            menuCamera.transform.position = focus + offset;
            menuCamera.transform.rotation =
                Quaternion.LookRotation(-offset.normalized, up);
        }

        private void EnsureGeometry()
        {
            if (turntable != null)
            {
                return;
            }

            GameObject turntableObject = new GameObject("Turntable")
            {
                hideFlags = HideFlags.DontSave
            };
            turntableObject.transform.SetParent(root, false);
            turntable = turntableObject.transform;

            turntableObject.AddComponent<MeshFilter>().sharedMesh = BuildMesh();

            MeshRenderer renderer = turntableObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            Shader shader = Shader.Find(MenuShaderName);
            if (shader == null)
            {
                Debug.LogError(
                    $"StartMenuBackdrop: shader '{MenuShaderName}' not found.", this);
                return;
            }

            menuMaterial = new Material(shader)
            {
                name = "Start Menu Backdrop",
                hideFlags = HideFlags.DontSave
            };
            renderer.sharedMaterial = menuMaterial;
        }

        /// <summary>
        /// A floor, a tapered dais, and four small pillars in the board's
        /// token colours standing around it. Everything shares one mesh so
        /// the whole thing can spin as a single turntable.
        /// </summary>
        private Mesh BuildMesh()
        {
            LudoProceduralMesh builder = new LudoProceduralMesh();

            Color floorColor = LudoBoardVisualStyle.Shade(LudoBoardVisualStyle.Paper, 0.85f);
            Color daisLow = LudoBoardVisualStyle.Shade(LudoBoardVisualStyle.SandDark, 0.2f);
            Color daisHigh = LudoBoardVisualStyle.SandMid;
            Color daisTop = LudoBoardVisualStyle.SandLight;

            builder.AddDisc(Vector2.zero, floorRadius, 0f, Segments, floorColor, Up);
            builder.AddFrustum(
                Vector2.zero,
                daisRadius,
                daisRadius * 0.82f,
                0f,
                -0.55f,
                Segments,
                daisLow,
                daisHigh);
            builder.AddDisc(
                Vector2.zero, daisRadius * 0.82f, -0.55f, Segments, daisTop, Up);

            Color[] pillarColors =
            {
                LudoBoardVisualStyle.Red,
                LudoBoardVisualStyle.Blue,
                LudoBoardVisualStyle.Green,
                LudoBoardVisualStyle.Yellow
            };

            int pillarSegments = Mathf.Max(8, Segments / 2);
            for (int index = 0; index < pillarColors.Length; index++)
            {
                float angle = Mathf.PI * 2f * index / pillarColors.Length + Mathf.PI / 4f;
                Vector2 at = new Vector2(
                    Mathf.Cos(angle) * pillarRingRadius,
                    Mathf.Sin(angle) * pillarRingRadius);

                Color color = pillarColors[index];
                Color shaded = LudoBoardVisualStyle.Shade(color, 0.15f);
                builder.AddCylinder(
                    at, pillarRadius, 0f, -pillarHeight, pillarSegments, shaded, color);
                builder.AddDisc(
                    at, pillarRadius, -pillarHeight, pillarSegments, color, Up);
            }

            return builder.Build("StartMenuBackdrop");
        }
    }
}
