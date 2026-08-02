using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalLudo.DiceSystem
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class DiceVisual : MonoBehaviour
    {
        public const float HalfSize = 0.45f;

        private const int FaceResolution = 6;
        private const int PipSegments = 24;
        private const float CornerRadius = 0.1f;
        private const float PipRadius = 0.065f;
        private const float PipSpacing = 0.18f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Min(0f)]
        [Tooltip("Gap between the board surface and the die's underside when resting. " +
                 "The die is positioned from this, so editing the Transform directly " +
                 "will not stick — change this instead.")]
        [SerializeField] private float restHeight = 0.3f;

        private readonly List<Vector3> vertices = new List<Vector3>(1200);
        private readonly List<Vector3> normals = new List<Vector3>(1200);
        private readonly List<int> bodyTriangles = new List<int>(1800);
        private readonly List<int> pipTriangles = new List<int>(1800);

        private MaterialPropertyBlock propertyBlock;
        private Color baseBodyColor = new Color(0.93f, 0.92f, 0.88f, 1f);
        private bool baseBodyColorCached;

        private Mesh diceMesh;

        private void OnEnable()
        {
            RebuildMesh();
        }

        private void OnValidate()
        {
            RebuildMesh();
        }

        [ContextMenu("Rebuild Dice Mesh")]
        public void RebuildMesh()
        {
            vertices.Clear();
            normals.Clear();
            bodyTriangles.Clear();
            pipTriangles.Clear();

            AddRoundedFace(Vector3.back, Vector3.right, Vector3.up);
            AddRoundedFace(Vector3.forward, Vector3.left, Vector3.up);
            AddRoundedFace(Vector3.right, Vector3.forward, Vector3.up);
            AddRoundedFace(Vector3.left, Vector3.back, Vector3.up);
            AddRoundedFace(Vector3.up, Vector3.right, Vector3.forward);
            AddRoundedFace(Vector3.down, Vector3.right, Vector3.back);

            AddFacePips(1, Vector3.back, Vector3.right, Vector3.up);
            AddFacePips(6, Vector3.forward, Vector3.left, Vector3.up);
            AddFacePips(3, Vector3.right, Vector3.forward, Vector3.up);
            AddFacePips(4, Vector3.left, Vector3.back, Vector3.up);
            AddFacePips(2, Vector3.up, Vector3.right, Vector3.forward);
            AddFacePips(5, Vector3.down, Vector3.right, Vector3.back);

            if (diceMesh == null)
            {
                diceMesh = new Mesh
                {
                    name = "Procedural Rounded Dice",
                    hideFlags = HideFlags.DontSave
                };
            }
            else
            {
                diceMesh.Clear();
            }

            diceMesh.SetVertices(vertices);
            diceMesh.SetNormals(normals);
            diceMesh.subMeshCount = 2;
            diceMesh.SetTriangles(bodyTriangles, 0);
            diceMesh.SetTriangles(pipTriangles, 1);
            diceMesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = diceMesh;

            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>
        /// Where the die sits when not mid-roll. Derived from
        /// <see cref="restHeight"/> rather than read from the Transform:
        /// this component rewrites its own position, so a hand-edited
        /// Transform value would just be overwritten.
        /// </summary>
        public Vector3 RestLocalPosition =>
            new Vector3(0f, 0f, -(restHeight + HalfSize));

        /// <summary>
        /// Settles the die: the requested face turns to camera and any roll
        /// hop is cleared.
        /// </summary>
        public void ShowValue(int value)
        {
            transform.localRotation = RotationForValue(value);
            transform.localPosition = RestLocalPosition;
        }

        /// <summary>
        /// One frame of a roll: an arbitrary tumble orientation lifted
        /// <paramref name="hopHeight"/> above the resting spot (the board's
        /// "up" is -Z, matching the rest of the scene).
        /// </summary>
        public void ShowRoll(Quaternion rotation, float hopHeight)
        {
            Vector3 rest = RestLocalPosition;
            transform.localRotation = rotation;
            transform.localPosition = new Vector3(rest.x, rest.y, rest.z - hopHeight);
        }

        /// <summary>
        /// Orientation that puts <paramref name="value"/>'s face toward the
        /// camera. Exposed so a roll animation can ease into the exact
        /// landing pose instead of snapping to it.
        /// </summary>
        public static Quaternion RotationForValue(int value)
        {
            return Quaternion.FromToRotation(
                FaceNormal(Mathf.Clamp(value, 1, 6)),
                Vector3.back);
        }

        /// <summary>
        /// Tints the die body (submesh 0, pips untouched) toward
        /// <paramref name="accent"/>. Uses a property block so the shared
        /// material asset is never modified.
        /// </summary>
        public void SetAccentColor(Color accent, float strength)
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                return;
            }

            if (!baseBodyColorCached)
            {
                Material[] materials = meshRenderer.sharedMaterials;
                if (materials.Length > 0 &&
                    materials[0] != null &&
                    materials[0].HasProperty(BaseColorId))
                {
                    baseBodyColor = materials[0].GetColor(BaseColorId);
                }

                baseBodyColorCached = true;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            meshRenderer.GetPropertyBlock(propertyBlock, 0);
            propertyBlock.SetColor(
                BaseColorId,
                Color.Lerp(baseBodyColor, accent, Mathf.Clamp01(strength)));
            meshRenderer.SetPropertyBlock(propertyBlock, 0);
        }

        private void AddRoundedFace(Vector3 faceNormal, Vector3 horizontal, Vector3 vertical)
        {
            int firstVertex = vertices.Count;

            for (int row = 0; row <= FaceResolution; row++)
            {
                float verticalPosition = Mathf.Lerp(
                    -HalfSize,
                    HalfSize,
                    (float)row / FaceResolution);

                for (int column = 0; column <= FaceResolution; column++)
                {
                    float horizontalPosition = Mathf.Lerp(
                        -HalfSize,
                        HalfSize,
                        (float)column / FaceResolution);
                    Vector3 cubePoint =
                        faceNormal * HalfSize +
                        horizontal * horizontalPosition +
                        vertical * verticalPosition;

                    Vector3 innerPoint = new Vector3(
                        Mathf.Clamp(cubePoint.x, -HalfSize + CornerRadius, HalfSize - CornerRadius),
                        Mathf.Clamp(cubePoint.y, -HalfSize + CornerRadius, HalfSize - CornerRadius),
                        Mathf.Clamp(cubePoint.z, -HalfSize + CornerRadius, HalfSize - CornerRadius));
                    Vector3 roundedNormal = (cubePoint - innerPoint).normalized;

                    vertices.Add(innerPoint + roundedNormal * CornerRadius);
                    normals.Add(roundedNormal);
                }
            }

            int rowLength = FaceResolution + 1;
            for (int row = 0; row < FaceResolution; row++)
            {
                for (int column = 0; column < FaceResolution; column++)
                {
                    int bottomLeft = firstVertex + row * rowLength + column;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + rowLength;
                    int topRight = topLeft + 1;

                    bodyTriangles.Add(bottomLeft);
                    bodyTriangles.Add(topRight);
                    bodyTriangles.Add(topLeft);
                    bodyTriangles.Add(bottomLeft);
                    bodyTriangles.Add(bottomRight);
                    bodyTriangles.Add(topRight);
                }
            }
        }

        private void AddFacePips(
            int faceValue,
            Vector3 faceNormal,
            Vector3 horizontal,
            Vector3 vertical)
        {
            Vector2[] positions = PipPositions(faceValue);
            foreach (Vector2 position in positions)
            {
                AddPip(
                    faceNormal * (HalfSize + 0.006f) +
                    horizontal * position.x +
                    vertical * position.y,
                    faceNormal,
                    horizontal,
                    vertical);
            }
        }

        private void AddPip(
            Vector3 center,
            Vector3 faceNormal,
            Vector3 horizontal,
            Vector3 vertical)
        {
            int centerVertex = vertices.Count;
            vertices.Add(center);
            normals.Add(faceNormal);

            for (int segment = 0; segment < PipSegments; segment++)
            {
                float angle = Mathf.PI * 2f * segment / PipSegments;
                vertices.Add(
                    center +
                    horizontal * (Mathf.Cos(angle) * PipRadius) +
                    vertical * (Mathf.Sin(angle) * PipRadius));
                normals.Add(faceNormal);
            }

            for (int segment = 0; segment < PipSegments; segment++)
            {
                int current = centerVertex + 1 + segment;
                int next = centerVertex + 1 + (segment + 1) % PipSegments;
                pipTriangles.Add(centerVertex);
                pipTriangles.Add(next);
                pipTriangles.Add(current);
            }
        }

        private static Vector2[] PipPositions(int value)
        {
            Vector2 topLeft = new Vector2(-PipSpacing, PipSpacing);
            Vector2 topRight = new Vector2(PipSpacing, PipSpacing);
            Vector2 middleLeft = new Vector2(-PipSpacing, 0f);
            Vector2 middleRight = new Vector2(PipSpacing, 0f);
            Vector2 bottomLeft = new Vector2(-PipSpacing, -PipSpacing);
            Vector2 bottomRight = new Vector2(PipSpacing, -PipSpacing);

            return value switch
            {
                1 => new[] { Vector2.zero },
                2 => new[] { topLeft, bottomRight },
                3 => new[] { topLeft, Vector2.zero, bottomRight },
                4 => new[] { topLeft, topRight, bottomLeft, bottomRight },
                5 => new[] { topLeft, topRight, Vector2.zero, bottomLeft, bottomRight },
                6 => new[]
                {
                    topLeft,
                    topRight,
                    middleLeft,
                    middleRight,
                    bottomLeft,
                    bottomRight
                },
                _ => new[] { Vector2.zero }
            };
        }

        private static Vector3 FaceNormal(int value)
        {
            return value switch
            {
                1 => Vector3.back,
                2 => Vector3.up,
                3 => Vector3.right,
                4 => Vector3.left,
                5 => Vector3.down,
                6 => Vector3.forward,
                _ => Vector3.back
            };
        }

        private void OnDestroy()
        {
            if (diceMesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(diceMesh);
            }
            else
            {
                DestroyImmediate(diceMesh);
            }
        }
    }
}
