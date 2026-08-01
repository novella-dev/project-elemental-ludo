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
        private const int CellLabelStartIndex = 29;

        private static readonly Color BoardWhite = new Color32(248, 247, 242, 255);
        private static readonly Color GridColor = new Color32(73, 78, 78, 255);
        private static readonly Color Red = new Color32(211, 17, 54, 255);
        private static readonly Color Blue = new Color32(62, 158, 207, 255);
        private static readonly Color Green = new Color32(10, 105, 72, 255);
        private static readonly Color Yellow = new Color32(242, 211, 62, 255);
        private static readonly Color SafeCell = new Color32(118, 125, 125, 92);

        private readonly List<Vector3> vertices = new List<Vector3>(2048);
        private readonly List<Color> colors = new List<Color>(2048);
        private readonly List<int> triangles = new List<int>(3072);

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

        private void DrawBoard()
        {
            const float outerEdge = 9.5f;
            const float centerEdge = 1.5f;
            const float lineWidth = 0.025f;

            AddRect(-9.68f, -9.68f, 9.68f, 9.68f, GridColor, 0.08f);
            AddRect(-outerEdge, -outerEdge, outerEdge, outerEdge, BoardWhite, 0.06f);

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

            DrawHome(new Vector2(-4.5f, 4.5f), Red);
            DrawHome(new Vector2(4.5f, 4.5f), Blue);
            DrawHome(new Vector2(-4.5f, -4.5f), Green);
            DrawHome(new Vector2(4.5f, -4.5f), Yellow);

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
            Vector2 worldCenter = LudoBoardLayout.ToWorld(center);
            AddWorldCircle(worldCenter, 0.29f, color, -0.055f);
            AddWorldRing(
                worldCenter,
                0.30f,
                0.275f,
                new Color(GridColor.r, GridColor.g, GridColor.b, 0.32f),
                -0.06f);
        }

        private void DrawHome(Vector2 center, Color homeColor)
        {
            AddCircle(center, 2.04f, GridColor, 0.015f);
            AddCircle(center, 1.98f, homeColor, 0.005f);
            AddRing(center, 1.24f, 1.17f, BoardWhite, -0.015f);
            AddCircle(center, 0.62f, GridColor, -0.02f);
            AddCircle(center, 0.55f, BoardWhite, -0.03f);
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

        private void AddCircle(Vector2 center, float radius, Color color, float depth)
        {
            for (int segment = 0; segment < CircleSegments; segment++)
            {
                float startAngle = Mathf.PI * 2f * segment / CircleSegments;
                float endAngle = Mathf.PI * 2f * (segment + 1) / CircleSegments;

                AddTriangle(
                    center,
                    center + new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle)) * radius,
                    center + new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * radius,
                    color,
                    depth);
            }
        }

        private void AddRing(
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
                vertices.Add(ToVector3(center + startDirection * innerRadius, depth));
                vertices.Add(ToVector3(center + startDirection * outerRadius, depth));
                vertices.Add(ToVector3(center + endDirection * outerRadius, depth));
                vertices.Add(ToVector3(center + endDirection * innerRadius, depth));
                AddColors(color, 4);

                triangles.Add(firstVertex);
                triangles.Add(firstVertex + 2);
                triangles.Add(firstVertex + 1);
                triangles.Add(firstVertex);
                triangles.Add(firstVertex + 3);
                triangles.Add(firstVertex + 2);
            }
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
