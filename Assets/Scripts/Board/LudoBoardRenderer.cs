using System.Collections.Generic;
using ElementalLudo.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalLudo.Board
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LudoBoardRenderer : MonoBehaviour
    {
        private const string BoardShaderName = "Elemental Ludo/Board Vertex Color";
        private const int CircleSegments = 64;
        private const int CellLabelCount = 68;
        private const int CellLabelStartIndex = LudoBoardRoutes.CellLabelStartIndex;

        private static readonly Color GridColor = new Color32(73, 78, 78, 255);
        private static readonly Color Red = new Color32(211, 17, 54, 255);
        private static readonly Color Blue = new Color32(62, 158, 207, 255);
        private static readonly Color Green = new Color32(10, 105, 72, 255);
        private static readonly Color Yellow = new Color32(242, 211, 62, 255);
        private static readonly Color SafeCell = new Color32(118, 125, 125, 92);

        private readonly List<Vector3> vertices = new List<Vector3>(8192);
        private readonly List<Color> colors = new List<Color>(8192);
        private readonly List<int> triangles = new List<int>(12288);

        private readonly List<GameObject> cellLabels = new List<GameObject>(CellLabelCount);

        private Mesh boardMesh;
        private Material fallbackMaterial;

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnValidate()
        {
            Rebuild();
        }

        [ContextMenu("Rebuild Board")]
        public void Rebuild()
        {
            vertices.Clear();
            colors.Clear();
            triangles.Clear();

            DrawBoard();

            if (boardMesh == null)
            {
                boardMesh = new Mesh
                {
                    name = "Procedural Ludo Board",
                    hideFlags = HideFlags.DontSave
                };
            }
            else
            {
                boardMesh.Clear();
            }

            boardMesh.SetVertices(vertices);
            boardMesh.SetColors(colors);
            boardMesh.SetTriangles(triangles, 0);
            boardMesh.RecalculateBounds();

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            meshFilter.sharedMesh = boardMesh;

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
                            name = "Ludo Board Material (Generated)",
                            hideFlags = HideFlags.DontSave
                        };
                    }

                    meshRenderer.sharedMaterial = fallbackMaterial;
                }
            }

            BuildCellLabels();
        }

        private void LateUpdate()
        {
            if (cellLabels.Count == 0)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            foreach (GameObject label in cellLabels)
            {
                if (label != null)
                {
                    label.transform.LookAt(
                        label.transform.position + cam.transform.forward,
                        cam.transform.up);
                }
            }
        }

        private void OnDestroy()
        {
            DestroyGeneratedObject(boardMesh);
            DestroyGeneratedObject(fallbackMaterial);
            ClearCellLabels();
        }

        private void ClearCellLabels()
        {
            foreach (GameObject label in cellLabels)
            {
                if (label != null)
                {
                    DestroyGeneratedObject(label);
                }
            }

            cellLabels.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("CellLabel_"))
                {
                    DestroyGeneratedObject(child.gameObject);
                }
            }
        }

        private void BuildCellLabels()
        {
            ClearCellLabels();

            for (int number = 1; number <= CellLabelCount; number++)
            {
                int pathIndex = (CellLabelStartIndex + number - 1) % CellLabelCount;
                Vector2Int cell = LudoBoardRoutes.GetSharedPathCell(pathIndex);

                if (Mathf.Abs(cell.x) <= 1 && Mathf.Abs(cell.y) <= 1)
                {
                    continue;
                }

                Vector3 worldPosition = LudoBoardLayout.ToWorld(cell, -0.25f);

                GameObject label = new GameObject($"CellLabel_{number}")
                {
                    hideFlags = HideFlags.DontSave
                };
                label.transform.SetParent(transform);
                label.transform.localPosition = worldPosition;
                label.transform.localRotation = Quaternion.identity;
                label.transform.localScale = Vector3.one * 0.2f;

                TextMesh textMesh = label.AddComponent<TextMesh>();
                textMesh.text = number.ToString();
                textMesh.fontSize = 48;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.characterSize = 0.18f;
                textMesh.color = new Color(0.10f, 0.10f, 0.10f, 0.85f);
                textMesh.fontStyle = FontStyle.Bold;
                textMesh.offsetZ = 0f;

                cellLabels.Add(label);
            }
        }

        private void DrawSandSurface(
            float xMin, float yMin, float xMax, float yMax, float depth)
        {
            const int subdivisions = 28;
            float stepX = (xMax - xMin) / subdivisions;
            float stepY = (yMax - yMin) / subdivisions;

            for (int iy = 0; iy < subdivisions; iy++)
            {
                float cy = yMin + iy * stepY;
                float ny = cy + stepY;

                for (int ix = 0; ix < subdivisions; ix++)
                {
                    float cx = xMin + ix * stepX;
                    float nx = cx + stepX;

                    int firstVertex = vertices.Count;
                    vertices.Add(ToVector3(new Vector2(cx, cy), depth));
                    vertices.Add(ToVector3(new Vector2(nx, cy), depth));
                    vertices.Add(ToVector3(new Vector2(nx, ny), depth));
                    vertices.Add(ToVector3(new Vector2(cx, ny), depth));
                    colors.Add(SandColor(cx, cy));
                    colors.Add(SandColor(nx, cy));
                    colors.Add(SandColor(nx, ny));
                    colors.Add(SandColor(cx, ny));

                    triangles.Add(firstVertex);
                    triangles.Add(firstVertex + 2);
                    triangles.Add(firstVertex + 1);
                    triangles.Add(firstVertex);
                    triangles.Add(firstVertex + 3);
                    triangles.Add(firstVertex + 2);
                }
            }
        }

        private static Color SandColor(float x, float y)
        {
            const float noiseScale = 3.5f;
            float n0 = Mathf.PerlinNoise(x * noiseScale, y * noiseScale);
            float n1 = Mathf.PerlinNoise(x * noiseScale * 2f + 5.3f,
                                        y * noiseScale * 2f + 7.1f);
            float n2 = Mathf.PerlinNoise(x * noiseScale * 4f + 13.7f,
                                        y * noiseScale * 4f + 11.3f);
            float noise = n0 * 0.5f + n1 * 0.3f + n2 * 0.2f;

            Color sandBase = new Color(0.85f, 0.76f, 0.58f);
            float variation = (noise - 0.5f) * 0.06f;
            return new Color(
                Mathf.Clamp01(sandBase.r + variation),
                Mathf.Clamp01(sandBase.g + variation),
                Mathf.Clamp01(sandBase.b + variation),
                1f);
        }

        private void DrawBoard()
        {
            const float outerEdge = 9.5f;
            const float centerEdge = 1.5f;
            const float lineWidth = 0.025f;

            AddRect(-9.68f, -9.68f, 9.68f, 9.68f, GridColor, 0.08f);
            DrawSandSurface(-outerEdge, -outerEdge, outerEdge, outerEdge, 0.06f);

            // Finishing lanes.
            AddRect(-0.5f, centerEdge, 0.5f, 8.5f, Red, 0.02f);
            AddRect(centerEdge, -0.5f, 8.5f, 0.5f, Blue, 0.02f);
            AddRect(-0.5f, -8.5f, 0.5f, -centerEdge, Yellow, 0.02f);
            AddRect(-8.5f, -0.5f, -centerEdge, 0.5f, Green, 0.02f);

            // Entry squares.
            AddRect(-1.5f, 4.5f, -0.5f, 5.5f, Red, 0.015f);
            AddRect(4.5f, 0.5f, 5.5f, 1.5f, Blue, 0.015f);
            AddRect(0.5f, -5.5f, 1.5f, -4.5f, Yellow, 0.015f);
            AddRect(-5.5f, -1.5f, -4.5f, -0.5f, Green, 0.015f);

            DrawGrid(outerEdge, centerEdge, lineWidth);
            DrawCenter(lineWidth);
            DrawSafeCells();

            // Home corners are drawn by LudoBoardTerrainRenderer now, as
            // elemental biome plates instead of flat colored discs.

            AddLine(
                new Vector2(-outerEdge, -outerEdge),
                new Vector2(outerEdge, -outerEdge),
                lineWidth * 2f,
                GridColor,
                -0.08f);
            AddLine(
                new Vector2(outerEdge, -outerEdge),
                new Vector2(outerEdge, outerEdge),
                lineWidth * 2f,
                GridColor,
                -0.08f);
            AddLine(
                new Vector2(outerEdge, outerEdge),
                new Vector2(-outerEdge, outerEdge),
                lineWidth * 2f,
                GridColor,
                -0.08f);
            AddLine(
                new Vector2(-outerEdge, outerEdge),
                new Vector2(-outerEdge, -outerEdge),
                lineWidth * 2f,
                GridColor,
                -0.08f);
        }

        private void DrawGrid(float outerEdge, float centerEdge, float lineWidth)
        {
            for (int index = 0; index <= 8; index++)
            {
                float negative = -outerEdge + index;
                float positive = centerEdge + index;

                AddLine(
                    new Vector2(negative, -centerEdge),
                    new Vector2(negative, centerEdge),
                    lineWidth,
                    GridColor,
                    -0.04f);
                AddLine(
                    new Vector2(positive, -centerEdge),
                    new Vector2(positive, centerEdge),
                    lineWidth,
                    GridColor,
                    -0.04f);
                AddLine(
                    new Vector2(-centerEdge, negative),
                    new Vector2(centerEdge, negative),
                    lineWidth,
                    GridColor,
                    -0.04f);
                AddLine(
                    new Vector2(-centerEdge, positive),
                    new Vector2(centerEdge, positive),
                    lineWidth,
                    GridColor,
                    -0.04f);
            }

            for (int index = 0; index <= 3; index++)
            {
                float crossCoordinate = -centerEdge + index;

                AddLine(
                    new Vector2(-outerEdge, crossCoordinate),
                    new Vector2(-centerEdge, crossCoordinate),
                    lineWidth,
                    GridColor,
                    -0.04f);
                AddLine(
                    new Vector2(centerEdge, crossCoordinate),
                    new Vector2(outerEdge, crossCoordinate),
                    lineWidth,
                    GridColor,
                    -0.04f);
                AddLine(
                    new Vector2(crossCoordinate, -outerEdge),
                    new Vector2(crossCoordinate, -centerEdge),
                    lineWidth,
                    GridColor,
                    -0.04f);
                AddLine(
                    new Vector2(crossCoordinate, centerEdge),
                    new Vector2(crossCoordinate, outerEdge),
                    lineWidth,
                    GridColor,
                    -0.04f);
            }
        }

        private void DrawCenter(float lineWidth)
        {
            Vector2 center = Vector2.zero;
            Vector2 topLeft = new Vector2(-1.5f, 1.5f);
            Vector2 topRight = new Vector2(1.5f, 1.5f);
            Vector2 bottomRight = new Vector2(1.5f, -1.5f);
            Vector2 bottomLeft = new Vector2(-1.5f, -1.5f);

            AddTriangle(topLeft, topRight, center, Red, -0.01f);
            AddTriangle(topRight, bottomRight, center, Blue, -0.01f);
            AddTriangle(bottomRight, bottomLeft, center, Yellow, -0.01f);
            AddTriangle(bottomLeft, topLeft, center, Green, -0.01f);

            AddLine(topLeft, center, lineWidth * 1.5f, GridColor, -0.06f);
            AddLine(topRight, center, lineWidth * 1.5f, GridColor, -0.06f);
            AddLine(bottomRight, center, lineWidth * 1.5f, GridColor, -0.06f);
            AddLine(bottomLeft, center, lineWidth * 1.5f, GridColor, -0.06f);
            AddLine(topLeft, topRight, lineWidth, GridColor, -0.06f);
            AddLine(topRight, bottomRight, lineWidth, GridColor, -0.06f);
            AddLine(bottomRight, bottomLeft, lineWidth, GridColor, -0.06f);
            AddLine(bottomLeft, topLeft, lineWidth, GridColor, -0.06f);
        }

        private void DrawSafeCells()
        {
            foreach (Vector2Int cell in LudoBoardRoutes.GetSafeCells())
            {
                DrawMarker(cell, GetSafeCellColor(cell));
            }
        }

        private Color GetSafeCellColor(Vector2Int cell)
        {
            Vector2Int start;
            if (LudoBoardRoutes.TryGetRouteStartCell("red", out start) && cell == start)
            {
                return Lighten(Red);
            }

            if (LudoBoardRoutes.TryGetRouteStartCell("blue", out start) && cell == start)
            {
                return Lighten(Blue);
            }

            if (LudoBoardRoutes.TryGetRouteStartCell("yellow", out start) && cell == start)
            {
                return Lighten(Yellow);
            }

            if (LudoBoardRoutes.TryGetRouteStartCell("green", out start) && cell == start)
            {
                return Lighten(Green);
            }

            return SafeCell;
        }

        private void DrawMarker(Vector2 center, Color color)
        {
            Vector2 worldCenter = LudoBoardLayout.ToWorld(center);
            AddWorldCircle(worldCenter, 0.29f, color, -0.055f);
            AddWorldRing(
                worldCenter,
                0.30f,
                0.275f,
                new Color(GridColor.r, GridColor.g, GridColor.b, 0.32f),
                -0.06f);
        }

        private static Color Lighten(Color color)
        {
            return Color.Lerp(color, Color.white, 0.22f);
        }

        private void AddRect(float xMin, float yMin, float xMax, float yMax, Color color, float depth)
        {
            int firstVertex = vertices.Count;
            vertices.Add(ToVector3(new Vector2(xMin, yMin), depth));
            vertices.Add(ToVector3(new Vector2(xMax, yMin), depth));
            vertices.Add(ToVector3(new Vector2(xMax, yMax), depth));
            vertices.Add(ToVector3(new Vector2(xMin, yMax), depth));
            AddColors(color, 4);

            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 2);
            triangles.Add(firstVertex + 1);
            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 3);
            triangles.Add(firstVertex + 2);
        }

        private void AddTriangle(Vector2 a, Vector2 b, Vector2 c, Color color, float depth)
        {
            int firstVertex = vertices.Count;
            vertices.Add(ToVector3(a, depth));
            vertices.Add(ToVector3(b, depth));
            vertices.Add(ToVector3(c, depth));
            AddColors(color, 3);

            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 2);
            triangles.Add(firstVertex + 1);
        }

        private void AddLine(Vector2 start, Vector2 end, float width, Color color, float depth)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Vector2 offset = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
            int firstVertex = vertices.Count;

            vertices.Add(ToVector3(start - offset, depth));
            vertices.Add(ToVector3(end - offset, depth));
            vertices.Add(ToVector3(end + offset, depth));
            vertices.Add(ToVector3(start + offset, depth));
            AddColors(color, 4);

            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 2);
            triangles.Add(firstVertex + 1);
            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 3);
            triangles.Add(firstVertex + 2);
        }

        private void AddWorldCircle(Vector2 center, float radius, Color color, float depth)
        {
            for (int segment = 0; segment < CircleSegments; segment++)
            {
                float startAngle = Mathf.PI * 2f * segment / CircleSegments;
                float endAngle = Mathf.PI * 2f * (segment + 1) / CircleSegments;

                AddWorldTriangle(
                    center,
                    center + new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle)) * radius,
                    center + new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * radius,
                    color,
                    depth);
            }
        }

        private void AddWorldRing(
            Vector2 center,
            float outerRadius,
            float innerRadius,
            Color color,
            float depth)
        {
            for (int segment = 0; segment < CircleSegments; segment++)
            {
                float startAngle = Mathf.PI * 2f * segment / CircleSegments;
                float endAngle = Mathf.PI * 2f * (segment + 1) / CircleSegments;
                Vector2 startDirection = new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle));
                Vector2 endDirection = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));

                int firstVertex = vertices.Count;
                vertices.Add(new Vector3(
                    center.x + startDirection.x * innerRadius,
                    center.y + startDirection.y * innerRadius,
                    depth));
                vertices.Add(new Vector3(
                    center.x + startDirection.x * outerRadius,
                    center.y + startDirection.y * outerRadius,
                    depth));
                vertices.Add(new Vector3(
                    center.x + endDirection.x * outerRadius,
                    center.y + endDirection.y * outerRadius,
                    depth));
                vertices.Add(new Vector3(
                    center.x + endDirection.x * innerRadius,
                    center.y + endDirection.y * innerRadius,
                    depth));
                AddColors(color, 4);

                triangles.Add(firstVertex);
                triangles.Add(firstVertex + 2);
                triangles.Add(firstVertex + 1);
                triangles.Add(firstVertex);
                triangles.Add(firstVertex + 3);
                triangles.Add(firstVertex + 2);
            }
        }

        private void AddWorldTriangle(
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Color color,
            float depth)
        {
            int firstVertex = vertices.Count;
            vertices.Add(new Vector3(a.x, a.y, depth));
            vertices.Add(new Vector3(b.x, b.y, depth));
            vertices.Add(new Vector3(c.x, c.y, depth));
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
