using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalLudo.Tokens
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TokenVisual : MonoBehaviour
    {
        private const int RadialSegments = 40;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color NeutralColor = new Color32(190, 193, 193, 255);

        private static readonly Vector2[] Profile =
        {
            new Vector2(0.27f, 0.01f),
            new Vector2(0.33f, -0.06f),
            new Vector2(0.34f, -0.15f),
            new Vector2(0.28f, -0.22f),
            new Vector2(0.20f, -0.39f),
            new Vector2(0.15f, -0.54f),
            new Vector2(0.15f, -0.59f),
            new Vector2(0.21f, -0.62f),
            new Vector2(0.27f, -0.70f),
            new Vector2(0.29f, -0.78f),
            new Vector2(0.25f, -0.87f),
            new Vector2(0.16f, -0.94f),
            new Vector2(0.025f, -0.98f)
        };

        private readonly List<Vector3> vertices = new List<Vector3>(640);
        private readonly List<Vector2> uvs = new List<Vector2>(640);
        private readonly List<int> triangles = new List<int>(1200);

        private Mesh tokenMesh;
        private MaterialPropertyBlock propertyBlock;
        private Color currentColor = NeutralColor;

        private void OnEnable()
        {
            RebuildMesh();
            ApplyColor();
        }

        private void OnValidate()
        {
            RebuildMesh();
            ApplyColor();
        }

        [ContextMenu("Rebuild Token Mesh")]
        public void RebuildMesh()
        {
            vertices.Clear();
            uvs.Clear();
            triangles.Clear();

            for (int profileIndex = 0; profileIndex < Profile.Length; profileIndex++)
            {
                Vector2 profilePoint = Profile[profileIndex];
                float verticalUv = (float)profileIndex / (Profile.Length - 1);

                for (int segment = 0; segment < RadialSegments; segment++)
                {
                    float horizontalUv = (float)segment / RadialSegments;
                    float angle = horizontalUv * Mathf.PI * 2f;
                    vertices.Add(new Vector3(
                        Mathf.Cos(angle) * profilePoint.x,
                        Mathf.Sin(angle) * profilePoint.x,
                        profilePoint.y));
                    uvs.Add(new Vector2(horizontalUv, verticalUv));
                }
            }

            for (int profileIndex = 0; profileIndex < Profile.Length - 1; profileIndex++)
            {
                int currentRing = profileIndex * RadialSegments;
                int nextRing = (profileIndex + 1) * RadialSegments;

                for (int segment = 0; segment < RadialSegments; segment++)
                {
                    int nextSegment = (segment + 1) % RadialSegments;
                    int current = currentRing + segment;
                    int currentNext = currentRing + nextSegment;
                    int above = nextRing + segment;
                    int aboveNext = nextRing + nextSegment;

                    triangles.Add(current);
                    triangles.Add(aboveNext);
                    triangles.Add(above);
                    triangles.Add(current);
                    triangles.Add(currentNext);
                    triangles.Add(aboveNext);
                }
            }

            if (tokenMesh == null)
            {
                tokenMesh = new Mesh
                {
                    name = "Procedural Ludo Token",
                    hideFlags = HideFlags.DontSave
                };
            }
            else
            {
                tokenMesh.Clear();
            }

            tokenMesh.SetVertices(vertices);
            tokenMesh.SetUVs(0, uvs);
            tokenMesh.SetTriangles(triangles, 0);
            tokenMesh.RecalculateNormals();
            tokenMesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = tokenMesh;

            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        public void SetColor(Color color)
        {
            currentColor = color;
            ApplyColor();
        }

        public void UseNeutralColor()
        {
            currentColor = NeutralColor;
            ApplyColor();
        }

        private void ApplyColor()
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, currentColor);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnDestroy()
        {
            if (tokenMesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(tokenMesh);
            }
            else
            {
                DestroyImmediate(tokenMesh);
            }
        }
    }
}
