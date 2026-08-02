using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalLudo.Board
{
    /// <summary>
    /// Decorative landmark terrain in each corner, themed to that player's
    /// element: a volcano (Fire/Red), a lake (Water/Blue), a mountain
    /// (Plant/Green), and floating storm rocks (Lightning/Yellow). Pure
    /// procedural vertex-colored geometry, same technique and shader as
    /// LudoBoardRenderer/LudoBoardDepthRenderer — no new art assets. Purely
    /// visual: doesn't touch board layout, routes, or gameplay in any way.
    ///
    /// First pass, built without being able to see it render — placement
    /// and scale are a reasonable guess (corners are cramped between the
    /// home markers and the board edge), likely to need one round of
    /// visual tuning once someone can actually look at it in the Editor.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LudoBoardTerrainRenderer : MonoBehaviour
    {
        private const string BoardShaderName = "Elemental Ludo/Board Vertex Color";
        private const int RadialSegments = 40;
        private const float CornerPushDistance = 2.35f;

        // Volcano (Red)
        private static readonly Color VolcanoSkirt = new Color32(64, 46, 40, 255);
        private static readonly Color VolcanoRock = new Color32(83, 58, 50, 255);
        private static readonly Color VolcanoRockWarm = new Color32(120, 66, 46, 255);
        private static readonly Color LavaGlow = new Color32(255, 106, 28, 255);
        private static readonly Color LavaCore = new Color32(255, 205, 64, 255);

        // Lake (Blue)
        private static readonly Color LakeShore = new Color32(150, 168, 140, 255);
        private static readonly Color LakeShoreWet = new Color32(96, 140, 140, 255);
        private static readonly Color LakeWaterShallow = new Color32(64, 150, 178, 255);
        private static readonly Color LakeWaterDeep = new Color32(18, 74, 112, 255);

        // Mountain (Green)
        private static readonly Color ForestGreenDark = new Color32(24, 56, 32, 255);
        private static readonly Color ForestGreen = new Color32(38, 82, 46, 255);
        private static readonly Color MountainRock = new Color32(96, 100, 92, 255);
        private static readonly Color MountainRockLight = new Color32(150, 152, 146, 255);
        private static readonly Color MountainSnow = new Color32(240, 244, 248, 255);

        // Storm rocks (Yellow)
        private static readonly Color StormShadow = new Color32(90, 96, 104, 130);
        private static readonly Color StormRockDark = new Color32(58, 54, 52, 255);
        private static readonly Color FloatingRock = new Color32(84, 78, 74, 255);
        private static readonly Color StormCloudDark = new Color32(58, 62, 78, 255);
        private static readonly Color StormCloud = new Color32(96, 100, 118, 255);
        private static readonly Color LightningGlow = new Color32(232, 222, 128, 255);

        private readonly List<Vector3> vertices = new List<Vector3>(4096);
        private readonly List<Color> colors = new List<Color>(4096);
        private readonly List<int> triangles = new List<int>(6144);

        private Mesh terrainMesh;
        private Material fallbackMaterial;

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnValidate()
        {
            Rebuild();
        }

        [ContextMenu("Rebuild Terrain")]
        public void Rebuild()
        {
            vertices.Clear();
            colors.Clear();
            triangles.Clear();

            float homeCenter = LudoBoardLayout.HomeLogicalCenter;
            DrawVolcano(OuterCorner(-homeCenter, homeCenter));
            DrawLake(OuterCorner(homeCenter, homeCenter));
            DrawMountain(OuterCorner(-homeCenter, -homeCenter));
            DrawStormRocks(OuterCorner(homeCenter, -homeCenter));

            if (terrainMesh == null)
            {
                terrainMesh = new Mesh
                {
                    name = "Ludo Board Terrain Features",
                    hideFlags = HideFlags.DontSave
                };
            }
            else
            {
                terrainMesh.Clear();
            }

            terrainMesh.SetVertices(vertices);
            terrainMesh.SetColors(colors);
            terrainMesh.SetTriangles(triangles, 0);
            terrainMesh.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = terrainMesh;

            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            if (meshRenderer.sharedMaterial == null)
            {
                Shader shader = Shader.Find(BoardShaderName);
                if (shader != null)
                {
                    if (fallbackMaterial == null || fallbackMaterial.shader != shader)
                    {
                        DestroyGeneratedObject(fallbackMaterial);
                        fallbackMaterial = new Material(shader)
                        {
                            name = "Ludo Board Terrain Material (Generated)",
                            hideFlags = HideFlags.DontSave
                        };
                    }

                    meshRenderer.sharedMaterial = fallbackMaterial;
                }
            }
        }

        private void OnDestroy()
        {
            DestroyGeneratedObject(terrainMesh);
            DestroyGeneratedObject(fallbackMaterial);
        }

        private static Vector2 OuterCorner(float logicalX, float logicalY)
        {
            Vector2 homeWorld = LudoBoardLayout.ToWorld(new Vector2(logicalX, logicalY));
            return homeWorld + homeWorld.normalized * CornerPushDistance;
        }

        private void DrawVolcano(Vector2 center)
        {
            AddConeSurface(center, 2.0f, 0.05f, VolcanoSkirt, 1.15f, -1.5f, VolcanoRock);
            AddConeSurface(center, 1.15f, -1.5f, VolcanoRock, 0.55f, -2.1f, VolcanoRockWarm);
            AddConeSurface(center, 0.55f, -2.1f, VolcanoRockWarm, 0.28f, -2.35f, LavaGlow);
            AddConeSurface(center, 0.28f, -2.35f, LavaGlow, 0.14f, -1.95f, LavaCore);
            AddCap(center, 0.14f, -1.95f, LavaCore);
        }

        private void DrawLake(Vector2 center)
        {
            AddConeSurface(center, 2.0f, 0.05f, LakeShore, 1.3f, 0.3f, LakeShoreWet);
            AddConeSurface(center, 1.3f, 0.3f, LakeShoreWet, 0.9f, 0.5f, LakeWaterShallow);
            AddConeSurface(center, 0.9f, 0.5f, LakeWaterShallow, 0.5f, 0.62f, LakeWaterDeep);
            AddCap(center, 0.5f, 0.62f, LakeWaterDeep);
        }

        private void DrawMountain(Vector2 center)
        {
            AddConeSurface(center, 2.0f, 0.05f, ForestGreenDark, 1.2f, -1.1f, ForestGreen);
            AddConeSurface(center, 1.2f, -1.1f, ForestGreen, 0.65f, -1.9f, MountainRock);
            AddConeSurface(center, 0.65f, -1.9f, MountainRock, 0.28f, -2.35f, MountainRockLight);
            AddConeSurface(center, 0.28f, -2.35f, MountainRockLight, 0f, -2.65f, MountainSnow);
        }

        private void DrawStormRocks(Vector2 center)
        {
            AddCap(center, 1.6f, 0.05f, StormShadow);

            DrawFloatingRock(center + new Vector2(-0.55f, -0.3f), 0.5f, -0.9f, StormRockDark);
            DrawFloatingRock(center + new Vector2(0.45f, -0.05f), 0.6f, -1.4f, FloatingRock);
            DrawFloatingRock(center + new Vector2(-0.1f, 0.55f), 0.4f, -1.1f, StormRockDark);

            AddConeSurface(center, 1.7f, -2.15f, StormCloudDark, 1.05f, -2.35f, StormCloud);
            AddCap(center, 1.05f, -2.35f, StormCloud);
            AddCap(center + new Vector2(0.12f, -0.08f), 0.32f, -2.45f, LightningGlow);
        }

        private void DrawFloatingRock(Vector2 center, float baseRadius, float peakDepth, Color color)
        {
            float baseDepth = peakDepth + 0.4f;
            float peakRadius = baseRadius * 0.35f;
            Color baseColor = Shade(color, 0.35f);

            AddConeSurface(center, baseRadius, baseDepth, baseColor, peakRadius, peakDepth, color);
            AddCap(center, peakRadius, peakDepth, color);
        }

        private void AddConeSurface(
            Vector2 center,
            float radiusA, float depthA, Color colorA,
            float radiusB, float depthB, Color colorB)
        {
            for (int segment = 0; segment < RadialSegments; segment++)
            {
                float startAngle = Mathf.PI * 2f * segment / RadialSegments;
                float endAngle = Mathf.PI * 2f * (segment + 1) / RadialSegments;
                Vector2 startDirection = new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle));
                Vector2 endDirection = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));

                Vector3 aStart = new Vector3(
                    center.x + startDirection.x * radiusA, center.y + startDirection.y * radiusA, depthA);
                Vector3 aEnd = new Vector3(
                    center.x + endDirection.x * radiusA, center.y + endDirection.y * radiusA, depthA);
                Vector3 bStart = new Vector3(
                    center.x + startDirection.x * radiusB, center.y + startDirection.y * radiusB, depthB);
                Vector3 bEnd = new Vector3(
                    center.x + endDirection.x * radiusB, center.y + endDirection.y * radiusB, depthB);

                if (radiusA <= 0.001f)
                {
                    AddTriangleColored(aStart, bStart, bEnd, colorA, colorB, colorB);
                }
                else if (radiusB <= 0.001f)
                {
                    AddTriangleColored(bStart, aStart, aEnd, colorB, colorA, colorA);
                }
                else
                {
                    AddQuadColored(aStart, bStart, bEnd, aEnd, colorA, colorB, colorB, colorA);
                }
            }
        }

        private void AddCap(Vector2 center, float radius, float depth, Color color)
        {
            for (int segment = 0; segment < RadialSegments; segment++)
            {
                float startAngle = Mathf.PI * 2f * segment / RadialSegments;
                float endAngle = Mathf.PI * 2f * (segment + 1) / RadialSegments;
                Vector2 startDirection = new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle));
                Vector2 endDirection = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));

                Vector3 apex = new Vector3(center.x, center.y, depth);
                Vector3 start = new Vector3(
                    center.x + startDirection.x * radius, center.y + startDirection.y * radius, depth);
                Vector3 end = new Vector3(
                    center.x + endDirection.x * radius, center.y + endDirection.y * radius, depth);

                AddTriangleColored(apex, start, end, color, color, color);
            }
        }

        private void AddQuadColored(
            Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Color colorA, Color colorB, Color colorC, Color colorD)
        {
            int firstVertex = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            colors.Add(colorA);
            colors.Add(colorB);
            colors.Add(colorC);
            colors.Add(colorD);

            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 2);
            triangles.Add(firstVertex + 1);
            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 3);
            triangles.Add(firstVertex + 2);
        }

        private void AddTriangleColored(Vector3 a, Vector3 b, Vector3 c, Color colorA, Color colorB, Color colorC)
        {
            int firstVertex = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            colors.Add(colorA);
            colors.Add(colorB);
            colors.Add(colorC);

            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 2);
            triangles.Add(firstVertex + 1);
        }

        private static Color Shade(Color color, float strength)
        {
            return Color.Lerp(color, Color.black, strength);
        }

        private static void DestroyGeneratedObject(Object target)
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
