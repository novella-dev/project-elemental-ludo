using System.Collections.Generic;
using UnityEngine;

namespace ElementalLudo.Board
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LudoReachableCellsHighlighter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Depth offset applied to the highlight mesh. Use a value slightly in front of the board surface.")]
        private float zOffset = -0.045f;

        private Mesh highlightMesh;
        private Material highlightMaterial;
        private readonly List<Vector3> vertices = new List<Vector3>(32);
        private readonly List<int> triangles = new List<int>(96);
        private readonly List<Color> colors = new List<Color>(32);

        private void OnDestroy()
        {
            if (highlightMesh != null)
            {
                Destroy(highlightMesh);
            }

            if (highlightMaterial != null)
            {
                Destroy(highlightMaterial);
            }
        }

        public void SetCells(IEnumerable<Vector2Int> cells, Color color)
        {
            vertices.Clear();
            triangles.Clear();
            colors.Clear();

            HashSet<Vector2Int> uniqueCells = new HashSet<Vector2Int>(cells);
            foreach (Vector2Int cell in uniqueCells)
            {
                AddCell(cell, color);
            }

            EnsureMesh();
            highlightMesh.Clear();
            highlightMesh.SetVertices(vertices);
            highlightMesh.SetColors(colors);
            highlightMesh.SetTriangles(triangles, 0);
            highlightMesh.RecalculateBounds();
        }

        public void Clear()
        {
            vertices.Clear();
            triangles.Clear();
            colors.Clear();

            if (highlightMesh != null)
            {
                highlightMesh.Clear();
            }
        }

        private void AddCell(Vector2Int cell, Color color)
        {
            float xMin = LudoBoardLayout.ToWorldCoordinate(cell.x - 0.5f);
            float xMax = LudoBoardLayout.ToWorldCoordinate(cell.x + 0.5f);
            float yMin = LudoBoardLayout.ToWorldCoordinate(cell.y - 0.5f);
            float yMax = LudoBoardLayout.ToWorldCoordinate(cell.y + 0.5f);

            Vector3 bottomLeft = new Vector3(xMin, yMin, zOffset);
            Vector3 bottomRight = new Vector3(xMax, yMin, zOffset);
            Vector3 topRight = new Vector3(xMax, yMax, zOffset);
            Vector3 topLeft = new Vector3(xMin, yMax, zOffset);

            int baseIndex = vertices.Count;
            vertices.Add(bottomLeft);
            vertices.Add(bottomRight);
            vertices.Add(topRight);
            vertices.Add(topLeft);

            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 3);
            triangles.Add(baseIndex + 2);
        }

        private void EnsureMesh()
        {
            if (highlightMesh == null)
            {
                highlightMesh = new Mesh
                {
                    name = "ReachableCellsHighlight",
                    hideFlags = HideFlags.DontSave
                };
                GetComponent<MeshFilter>().mesh = highlightMesh;
            }

            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (highlightMaterial == null)
            {
                Shader shader = Shader.Find("Elemental Ludo/Board Vertex Color");
                if (shader == null)
                {
                    Debug.LogError(
                        "ReachableCellsHighlighter: could not find board shader.",
                        this);
                    return;
                }

                highlightMaterial = new Material(shader)
                {
                    name = "ReachableCellsHighlightMaterial",
                    hideFlags = HideFlags.DontSave
                };
            }

            meshRenderer.material = highlightMaterial;
        }
    }
}
