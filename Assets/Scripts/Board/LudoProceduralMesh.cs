using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalLudo.Board
{
    /// <summary>
    /// Accumulates positions, normals and vertex colours for geometry built at
    /// runtime, then hands back a mesh.
    ///
    /// Written for the combat arena's stonework and shared from there, so it
    /// speaks the board's conventions: the XY plane is the ground and -Z is up.
    ///
    /// Normals are written per vertex rather than recalculated, then averaged
    /// across coincident positions on build. That averaging is what stops a cel
    /// outline cracking: every quad here carries its own four vertices, so left
    /// alone the shader would push neighbouring faces apart along different
    /// normals and split the silhouette open at every seam.
    /// </summary>
    public sealed class LudoProceduralMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>(2048);
        private readonly List<Vector3> normals = new List<Vector3>(2048);
        private readonly List<Color> colors = new List<Color>(2048);
        private readonly List<int> triangles = new List<int>(3072);

        public void AddQuad(
            Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
            Color ca, Color cb, Color cc, Color cd)
        {
            int first = vertices.Count;

            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            normals.Add(na);
            normals.Add(nb);
            normals.Add(nc);
            normals.Add(nd);
            colors.Add(ca);
            colors.Add(cb);
            colors.Add(cc);
            colors.Add(cd);

            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }

        public void AddCylinder(
            Vector2 center,
            float radius,
            float bottomZ,
            float topZ,
            int segments,
            Color bottomColor,
            Color topColor)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                float a0 = Mathf.PI * 2f * segment / segments;
                float a1 = Mathf.PI * 2f * (segment + 1) / segments;

                Vector3 n0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
                Vector3 n1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);

                Vector3 low0 = Ring(center, n0, radius, bottomZ);
                Vector3 low1 = Ring(center, n1, radius, bottomZ);
                Vector3 high1 = Ring(center, n1, radius, topZ);
                Vector3 high0 = Ring(center, n0, radius, topZ);

                AddQuad(
                    low0, low1, high1, high0,
                    n0, n1, n1, n0,
                    bottomColor, bottomColor, topColor, topColor);
            }
        }

        /// <summary>
        /// A flat ring closing the step between two stacked cylinders of
        /// different radii. Without these the column is an open tube and
        /// the camera looks straight into its unlit hollow, which reads as
        /// a black core running up the middle of the stone.
        /// </summary>
        public void AddAnnulus(
            Vector2 center,
            float innerRadius,
            float outerRadius,
            float z,
            int segments,
            Color color,
            Vector3 normal)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                float a0 = Mathf.PI * 2f * segment / segments;
                float a1 = Mathf.PI * 2f * (segment + 1) / segments;

                Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
                Vector3 d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);

                AddQuad(
                    Ring(center, d0, innerRadius, z),
                    Ring(center, d1, innerRadius, z),
                    Ring(center, d1, outerRadius, z),
                    Ring(center, d0, outerRadius, z),
                    normal, normal, normal, normal,
                    color, color, color, color);
            }
        }

        public void AddDisc(
            Vector2 center,
            float radius,
            float z,
            int segments,
            Color color,
            Vector3 normal)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                float a0 = Mathf.PI * 2f * segment / segments;
                float a1 = Mathf.PI * 2f * (segment + 1) / segments;

                Vector3 n0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
                Vector3 n1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);

                int first = vertices.Count;
                vertices.Add(new Vector3(center.x, center.y, z));
                vertices.Add(Ring(center, n0, radius, z));
                vertices.Add(Ring(center, n1, radius, z));
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);
                colors.Add(color);
                colors.Add(color);
                colors.Add(color);

                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 1);
            }
        }

        public Mesh Build(string name)
        {
            Mesh mesh = new Mesh
            {
                name = name,
                hideFlags = HideFlags.DontSave
            };

            mesh.indexFormat = vertices.Count > ushort.MaxValue
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            SmoothSharedNormals(mesh);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Ring(
            Vector2 center,
            Vector3 direction,
            float radius,
            float z)
        {
            return new Vector3(
                center.x + direction.x * radius,
                center.y + direction.y * radius,
                z);
        }

        /// <summary>
        /// Averages the normals of every vertex sharing a position, the same
        /// treatment LudoBoardTerrainRenderer gives its own outline mesh.
        /// </summary>
        private static void SmoothSharedNormals(Mesh mesh)
        {
            Vector3[] meshVertices = mesh.vertices;
            Vector3[] meshNormals = mesh.normals;
            if (meshVertices.Length == 0 ||
                meshNormals.Length != meshVertices.Length)
            {
                return;
            }

            Dictionary<Vector3, Vector3> sums =
                new Dictionary<Vector3, Vector3>(meshVertices.Length);
            for (int index = 0; index < meshVertices.Length; index++)
            {
                sums.TryGetValue(meshVertices[index], out Vector3 sum);
                sums[meshVertices[index]] = sum + meshNormals[index];
            }

            for (int index = 0; index < meshVertices.Length; index++)
            {
                Vector3 sum = sums[meshVertices[index]];
                if (sum.sqrMagnitude > 0.000001f)
                {
                    meshNormals[index] = sum.normalized;
                }
            }

            mesh.normals = meshNormals;
        }
    }
}
