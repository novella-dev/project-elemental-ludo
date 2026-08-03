using System.Collections.Generic;
using ElementalLudo.Tokens;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalLudo.Board
{
    /// <summary>
    /// Drops a small coloured disc under every token out on the track, so the
    /// square a token occupies stays readable when the camera angle makes the
    /// board hard to judge.
    ///
    /// Rebuilt every frame from the tokens' live transforms rather than from
    /// board state, so a disc follows its token while it walks instead of
    /// jumping once it lands — and so it inherits the same-cell offset for
    /// free when two tokens share a square.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LudoTokenGroundMarkers : MonoBehaviour
    {
        private const string BoardShaderName = "Elemental Ludo/Board Vertex Color";
        private const int Segments = 20;

        [Min(0f)]
        [SerializeField] private float radius = 0.34f;
        [Tooltip("Depth of the discs. Sits in front of the reachable-cell highlight (-0.08) and well behind the tokens (-0.34).")]
        [SerializeField] private float zOffset = -0.10f;
        [Range(0f, 1f)]
        [Tooltip("How far the token's colour is pushed toward black, so the disc reads as a shadow rather than a second token.")]
        [SerializeField] private float darkening = 0.42f;
        [Range(0f, 1f)]
        [SerializeField] private float opacity = 0.8f;

        private readonly List<Token> tokens = new List<Token>(16);
        private readonly List<Vector3> vertices = new List<Vector3>(512);
        private readonly List<int> triangles = new List<int>(768);
        private readonly List<Color> colors = new List<Color>(512);

        private Mesh markerMesh;
        private Material markerMaterial;

        /// <summary>The tokens to mark. Only ones currently on the track.</summary>
        public void SetTokens(IReadOnlyList<Token> trackTokens)
        {
            tokens.Clear();
            if (trackTokens == null)
            {
                return;
            }

            foreach (Token token in trackTokens)
            {
                if (token != null)
                {
                    tokens.Add(token);
                }
            }
        }

        public void Clear()
        {
            tokens.Clear();
            if (markerMesh != null)
            {
                markerMesh.Clear();
            }
        }

        private void LateUpdate()
        {
            Rebuild();
        }

        private void OnDestroy()
        {
            DestroyGenerated(markerMesh);
            DestroyGenerated(markerMaterial);
        }

        private void Rebuild()
        {
            vertices.Clear();
            triangles.Clear();
            colors.Clear();

            foreach (Token token in tokens)
            {
                if (token == null || token.OwnerStyle == null)
                {
                    continue;
                }

                Vector3 position = token.transform.position;
                Color tokenColor = token.OwnerStyle.TokenColor;

                Color core = Color.Lerp(tokenColor, Color.black, darkening);
                core.a = opacity;
                Color rim = Color.Lerp(tokenColor, Color.white, 0.25f);
                rim.a = opacity;

                AddDisc(position, radius * 0.82f, core, core);
                AddRing(position, radius, radius * 0.82f, rim);
            }

            EnsureMesh();
            markerMesh.Clear();
            markerMesh.SetVertices(vertices);
            markerMesh.SetColors(colors);
            markerMesh.SetTriangles(triangles, 0);
            markerMesh.RecalculateBounds();
        }

        private void AddDisc(Vector3 center, float discRadius, Color centerColor, Color edgeColor)
        {
            for (int segment = 0; segment < Segments; segment++)
            {
                float startAngle = Mathf.PI * 2f * segment / Segments;
                float endAngle = Mathf.PI * 2f * (segment + 1) / Segments;

                int first = vertices.Count;
                vertices.Add(new Vector3(center.x, center.y, zOffset));
                vertices.Add(new Vector3(
                    center.x + Mathf.Cos(startAngle) * discRadius,
                    center.y + Mathf.Sin(startAngle) * discRadius,
                    zOffset));
                vertices.Add(new Vector3(
                    center.x + Mathf.Cos(endAngle) * discRadius,
                    center.y + Mathf.Sin(endAngle) * discRadius,
                    zOffset));
                colors.Add(centerColor);
                colors.Add(edgeColor);
                colors.Add(edgeColor);

                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 1);
            }
        }

        private void AddRing(Vector3 center, float outerRadius, float innerRadius, Color color)
        {
            for (int segment = 0; segment < Segments; segment++)
            {
                float startAngle = Mathf.PI * 2f * segment / Segments;
                float endAngle = Mathf.PI * 2f * (segment + 1) / Segments;
                Vector2 start = new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle));
                Vector2 end = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));

                int first = vertices.Count;
                vertices.Add(new Vector3(center.x + start.x * innerRadius, center.y + start.y * innerRadius, zOffset));
                vertices.Add(new Vector3(center.x + start.x * outerRadius, center.y + start.y * outerRadius, zOffset));
                vertices.Add(new Vector3(center.x + end.x * outerRadius, center.y + end.y * outerRadius, zOffset));
                vertices.Add(new Vector3(center.x + end.x * innerRadius, center.y + end.y * innerRadius, zOffset));
                colors.Add(color);
                colors.Add(color);
                colors.Add(color);
                colors.Add(color);

                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 1);
                triangles.Add(first);
                triangles.Add(first + 3);
                triangles.Add(first + 2);
            }
        }

        private void EnsureMesh()
        {
            if (markerMesh == null)
            {
                markerMesh = new Mesh
                {
                    name = "TokenGroundMarkers",
                    hideFlags = HideFlags.DontSave
                };
                GetComponent<MeshFilter>().mesh = markerMesh;
            }

            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            if (markerMaterial == null)
            {
                Shader shader = Shader.Find(BoardShaderName);
                if (shader == null)
                {
                    Debug.LogError("TokenGroundMarkers: board shader not found.", this);
                    return;
                }

                markerMaterial = new Material(shader)
                {
                    name = "TokenGroundMarkersMaterial (Generated)",
                    hideFlags = HideFlags.DontSave
                };
            }

            meshRenderer.material = markerMaterial;
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
    }
}
