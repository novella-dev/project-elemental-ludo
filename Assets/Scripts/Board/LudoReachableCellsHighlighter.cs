using System.Collections.Generic;
using UnityEngine;

namespace ElementalLudo.Board
{
    /// <summary>A square to highlight, with the colour it should take.</summary>
    public readonly struct LudoHighlightCell
    {
        public Vector2Int Cell { get; }
        public Color Color { get; }

        public LudoHighlightCell(Vector2Int cell, Color color)
        {
            Cell = cell;
            Color = color;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LudoReachableCellsHighlighter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Depth of the highlight. Must sit in front of the raised track tiles (-0.05) or the tiles cover it and only the grid gap shows through.")]
        private float zOffset = -0.08f;

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

        /// <summary>
        /// Colour is per square rather than shared, so a picked destination
        /// can stand out from the alternatives still on offer.
        /// </summary>
        public void SetCells(IReadOnlyList<LudoHighlightCell> cells)
        {
            vertices.Clear();
            triangles.Clear();
            colors.Clear();

            if (cells != null)
            {
                foreach (LudoHighlightCell entry in cells)
                {
                    AddCell(entry.Cell, entry.Color);
                }
            }

            EnsureMesh();
            highlightMesh.Clear();
            highlightMesh.SetVertices(vertices);
            highlightMesh.SetColors(colors);
            highlightMesh.SetTriangles(triangles, 0);
            highlightMesh.RecalculateNormals();
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
            // Match the raised tile rather than the full 1x1 cell, so the
            // highlight lands exactly on the tile top instead of spilling into
            // the grid gaps around it.
            const float half = LudoBoardLayout.TrackCellHalfExtent;
            float xMin = LudoBoardLayout.ToWorldCoordinate(cell.x - half);
            float xMax = LudoBoardLayout.ToWorldCoordinate(cell.x + half);
            float yMin = LudoBoardLayout.ToWorldCoordinate(cell.y - half);
            float yMax = LudoBoardLayout.ToWorldCoordinate(cell.y + half);

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
