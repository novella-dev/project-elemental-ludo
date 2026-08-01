using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalLudo.Board
{
    public enum LudoBoardDepthMode
    {
        Isometric,
        Full3D
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LudoBoardDepthRenderer : MonoBehaviour
    {
        private const string BoardShaderName = "Elemental Ludo/Board Vertex Color";
        private const int CircleSegments = 48;

        private static readonly Color BoardWhite = new Color32(248, 247, 242, 255);
        private static readonly Color GridColor = new Color32(73, 78, 78, 255);
        private static readonly Color Red = new Color32(211, 17, 54, 255);
        private static readonly Color Blue = new Color32(62, 158, 207, 255);
        private static readonly Color Green = new Color32(10, 105, 72, 255);
        private static readonly Color Yellow = new Color32(242, 211, 62, 255);
        private static readonly Color SafeCell = new Color32(172, 176, 176, 255);

        [SerializeField] private LudoBoardDepthMode mode;

        private readonly List<Vector3> vertices = new List<Vector3>(4096);
        private readonly List<Color> colors = new List<Color>(4096);
        private readonly List<int> triangles = new List<int>(6144);

        private Mesh depthMesh;
        private Material fallbackMaterial;

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnValidate()
        {
            Rebuild();
        }

        [ContextMenu("Rebuild Depth")]
        public void Rebuild()
        {
            vertices.Clear();
            colors.Clear();
            triangles.Clear();

            DrawBase();
            if (mode == LudoBoardDepthMode.Full3D)
            {
                DrawRaisedTrack();
                DrawRaisedCenter();
                DrawRaisedHomes();
                DrawRaisedSafeCells();
            }

            if (depthMesh == null)
            {
                depthMesh = new Mesh
                {
                    name = mode == LudoBoardDepthMode.Full3D
                        ? "Ludo Board Full 3D Geometry"
                        : "Ludo Board Isometric Base",
                    hideFlags = HideFlags.DontSave
                };
            }
            else
            {
                depthMesh.Clear();
            }

            depthMesh.SetVertices(vertices);
            depthMesh.SetColors(colors);
            depthMesh.SetTriangles(triangles, 0);
            depthMesh.RecalculateBounds();
            depthMesh.RecalculateNormals();

            GetComponent<MeshFilter>().sharedMesh = depthMesh;

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
                            name = "Ludo Board Depth Material (Generated)",
                            hideFlags = HideFlags.DontSave
                        };
                    }

                    meshRenderer.sharedMaterial = fallbackMaterial;
                }
            }
        }

        private void OnDestroy()
        {
            DestroyGeneratedObject(depthMesh);
            DestroyGeneratedObject(fallbackMaterial);
        }

        private void DrawBase()
        {
            float backDepth = mode == LudoBoardDepthMode.Full3D ? 1.25f : 0.78f;
            AddBox(
                -9.72f,
                -9.72f,
                9.72f,
                9.72f,
                0.18f,
                backDepth,
                GridColor,
                Shade(GridColor, 0.46f));
        }

        private void DrawRaisedTrack()
        {
            for (int x = -9; x <= 9; x++)
            {
                for (int y = -9; y <= 9; y++)
                {
                    bool horizontalArm = Mathf.Abs(y) <= 1 && Mathf.Abs(x) >= 2;
                    bool verticalArm = Mathf.Abs(x) <= 1 && Mathf.Abs(y) >= 2;
                    if (!horizontalArm && !verticalArm)
                    {
                        continue;
                    }

                    Color topColor = TrackColor(x, y);
                    AddBox(
                        x - 0.455f,
                        y - 0.455f,
                        x + 0.455f,
                        y + 0.455f,
                        -0.05f,
                        0.025f,
                        topColor,
                        Shade(topColor, 0.28f));
                }
            }
        }

        private void DrawRaisedCenter()
        {
            Vector2 center = Vector2.zero;
            Vector2 topLeft = new Vector2(-1.5f, 1.5f);
            Vector2 topRight = new Vector2(1.5f, 1.5f);
            Vector2 bottomRight = new Vector2(1.5f, -1.5f);
            Vector2 bottomLeft = new Vector2(-1.5f, -1.5f);

            AddTrianglePrism(topLeft, topRight, center, -0.23f, 0.02f, Red);
            AddTrianglePrism(topRight, bottomRight, center, -0.23f, 0.02f, Blue);
            AddTrianglePrism(bottomRight, bottomLeft, center, -0.23f, 0.02f, Yellow);
            AddTrianglePrism(bottomLeft, topLeft, center, -0.23f, 0.02f, Green);
        }

        private void DrawRaisedHomes()
        {
            float homeCenter = LudoBoardLayout.HomeLogicalCenter;
            DrawRaisedHome(new Vector2(-homeCenter, homeCenter), Red);
            DrawRaisedHome(new Vector2(homeCenter, homeCenter), Blue);
            DrawRaisedHome(new Vector2(-homeCenter, -homeCenter), Green);
            DrawRaisedHome(new Vector2(homeCenter, -homeCenter), Yellow);
        }

        private void DrawRaisedHome(Vector2 center, Color color)
        {
            float scale = LudoBoardLayout.HomeSizeScale;
            AddRingPrism(
                center,
                2.06f * scale,
                2.0f * scale,
                -0.305f,
                0.03f,
                GridColor,
                Shade(GridColor, 0.4f));
            AddRingPrism(
                center,
                2f * scale,
                0.61f * scale,
                -0.29f,
                0.025f,
                color,
                Shade(color, 0.31f));
            AddRingPrism(
                center,
                1.25f * scale,
                1.16f * scale,
                -0.335f,
                -0.295f,
                BoardWhite,
                Shade(BoardWhite, 0.22f));
            AddRingPrism(
                center,
                0.66f * scale,
                0.60f * scale,
                -0.325f,
                -0.285f,
                GridColor,
                Shade(GridColor, 0.35f));
        }

        private void DrawRaisedSafeCells()
        {
            DrawMarker(new Vector2(0f, 9f), SafeCell);
            DrawMarker(new Vector2(-1f, 5f), Lighten(Red));
            DrawMarker(new Vector2(1f, 5f), SafeCell);
            DrawMarker(new Vector2(-5f, 1f), SafeCell);
            DrawMarker(new Vector2(5f, 1f), Lighten(Blue));
            DrawMarker(new Vector2(-9f, 0f), SafeCell);
            DrawMarker(new Vector2(9f, 0f), SafeCell);
            DrawMarker(new Vector2(-5f, -1f), Lighten(Green));
            DrawMarker(new Vector2(5f, -1f), SafeCell);
            DrawMarker(new Vector2(-1f, -5f), SafeCell);
            DrawMarker(new Vector2(1f, -5f), Lighten(Yellow));
            DrawMarker(new Vector2(0f, -9f), SafeCell);
        }

        private void DrawMarker(Vector2 center, Color color)
        {
            AddWorldCylinder(
                LudoBoardLayout.ToWorld(center),
                0.285f,
                -0.275f,
                -0.195f,
                color,
                Shade(color, 0.3f));
        }

        private static Color TrackColor(int x, int y)
        {
            if ((x == 0 && y >= 2 && y <= 8) || (x == -1 && y == 5))
            {
                return Red;
            }

            if ((y == 0 && x >= 2 && x <= 8) || (x == 5 && y == 1))
            {
                return Blue;
            }

            if ((x == 0 && y >= -8 && y <= -2) || (x == 1 && y == -5))
            {
                return Yellow;
            }

            if ((y == 0 && x >= -8 && x <= -2) || (x == -5 && y == -1))
            {
                return Green;
            }

            return BoardWhite;
        }

        private void AddBox(
            float xMin,
            float yMin,
            float xMax,
            float yMax,
            float frontDepth,
            float backDepth,
            Color frontColor,
            Color sideColor)
        {
            Vector3 frontBottomLeft = ToVector3(new Vector2(xMin, yMin), frontDepth);
            Vector3 frontBottomRight = ToVector3(new Vector2(xMax, yMin), frontDepth);
            Vector3 frontTopRight = ToVector3(new Vector2(xMax, yMax), frontDepth);
            Vector3 frontTopLeft = ToVector3(new Vector2(xMin, yMax), frontDepth);
            Vector3 backBottomLeft = ToVector3(new Vector2(xMin, yMin), backDepth);
            Vector3 backBottomRight = ToVector3(new Vector2(xMax, yMin), backDepth);
            Vector3 backTopRight = ToVector3(new Vector2(xMax, yMax), backDepth);
            Vector3 backTopLeft = ToVector3(new Vector2(xMin, yMax), backDepth);

            AddQuad(frontBottomLeft, frontBottomRight, frontTopRight, frontTopLeft, frontColor);
            AddQuad(frontBottomRight, backBottomRight, backTopRight, frontTopRight, sideColor);
            AddQuad(frontTopRight, backTopRight, backTopLeft, frontTopLeft, Shade(sideColor, 0.12f));
            AddQuad(frontTopLeft, backTopLeft, backBottomLeft, frontBottomLeft, sideColor);
            AddQuad(frontBottomLeft, backBottomLeft, backBottomRight, frontBottomRight, Shade(sideColor, 0.2f));
        }

        private void AddTrianglePrism(
            Vector2 a,
            Vector2 b,
            Vector2 c,
            float frontDepth,
            float backDepth,
            Color color)
        {
            Vector3 frontA = ToVector3(a, frontDepth);
            Vector3 frontB = ToVector3(b, frontDepth);
            Vector3 frontC = ToVector3(c, frontDepth);
            Vector3 backA = ToVector3(a, backDepth);
            Vector3 backB = ToVector3(b, backDepth);
            Vector3 backC = ToVector3(c, backDepth);
            Color sideColor = Shade(color, 0.32f);

            AddTriangle(frontA, frontB, frontC, color);
            AddQuad(frontA, backA, backB, frontB, sideColor);
            AddQuad(frontB, backB, backC, frontC, Shade(sideColor, 0.08f));
            AddQuad(frontC, backC, backA, frontA, Shade(sideColor, 0.16f));
        }

        private void AddWorldCylinder(
            Vector2 center,
            float radius,
            float frontDepth,
            float backDepth,
            Color frontColor,
            Color sideColor)
        {
            Vector3 frontCenter = new Vector3(center.x, center.y, frontDepth);

            for (int segment = 0; segment < CircleSegments; segment++)
            {
                float startAngle = Mathf.PI * 2f * segment / CircleSegments;
                float endAngle = Mathf.PI * 2f * (segment + 1) / CircleSegments;
                Vector2 startDirection = new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle));
                Vector2 endDirection = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));
                Vector3 frontStart = new Vector3(
                    center.x + startDirection.x * radius,
                    center.y + startDirection.y * radius,
                    frontDepth);
                Vector3 frontEnd = new Vector3(
                    center.x + endDirection.x * radius,
                    center.y + endDirection.y * radius,
                    frontDepth);
                Vector3 backStart = new Vector3(
                    center.x + startDirection.x * radius,
                    center.y + startDirection.y * radius,
                    backDepth);
                Vector3 backEnd = new Vector3(
                    center.x + endDirection.x * radius,
                    center.y + endDirection.y * radius,
                    backDepth);

                AddTriangle(frontCenter, frontStart, frontEnd, frontColor);
                AddQuad(frontStart, backStart, backEnd, frontEnd, sideColor);
            }
        }

        private void AddRingPrism(
            Vector2 center,
            float outerRadius,
            float innerRadius,
            float frontDepth,
            float backDepth,
            Color frontColor,
            Color sideColor)
        {
            for (int segment = 0; segment < CircleSegments; segment++)
            {
                float startAngle = Mathf.PI * 2f * segment / CircleSegments;
                float endAngle = Mathf.PI * 2f * (segment + 1) / CircleSegments;
                Vector2 startDirection = new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle));
                Vector2 endDirection = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));

                Vector3 innerFrontStart = ToVector3(center + startDirection * innerRadius, frontDepth);
                Vector3 outerFrontStart = ToVector3(center + startDirection * outerRadius, frontDepth);
                Vector3 outerFrontEnd = ToVector3(center + endDirection * outerRadius, frontDepth);
                Vector3 innerFrontEnd = ToVector3(center + endDirection * innerRadius, frontDepth);
                Vector3 innerBackStart = ToVector3(center + startDirection * innerRadius, backDepth);
                Vector3 outerBackStart = ToVector3(center + startDirection * outerRadius, backDepth);
                Vector3 outerBackEnd = ToVector3(center + endDirection * outerRadius, backDepth);
                Vector3 innerBackEnd = ToVector3(center + endDirection * innerRadius, backDepth);

                AddQuad(
                    innerFrontStart,
                    outerFrontStart,
                    outerFrontEnd,
                    innerFrontEnd,
                    frontColor);
                AddQuad(
                    outerFrontStart,
                    outerBackStart,
                    outerBackEnd,
                    outerFrontEnd,
                    sideColor);
                AddQuad(
                    innerFrontEnd,
                    innerBackEnd,
                    innerBackStart,
                    innerFrontStart,
                    Shade(sideColor, 0.15f));
            }
        }

        private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            int firstVertex = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            AddColors(color, 4);

            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 2);
            triangles.Add(firstVertex + 1);
            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 3);
            triangles.Add(firstVertex + 2);
        }

        private void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            int firstVertex = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            AddColors(color, 3);

            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 2);
            triangles.Add(firstVertex + 1);
        }

        private void AddColors(Color color, int count)
        {
            for (int index = 0; index < count; index++)
            {
                colors.Add(color);
            }
        }

        private static Vector3 ToVector3(Vector2 point, float depth)
        {
            return LudoBoardLayout.ToWorld(point, depth);
        }

        private static Color Lighten(Color color)
        {
            return Color.Lerp(color, Color.white, 0.22f);
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
