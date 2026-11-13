using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VRScienceLab.Module2.EditorTools
{
    /// <summary>
    /// Generates the meshes the laboratory models are assembled from (bevelled housings, lathed
    /// glassware, knobs, tubes) and stores them as assets, so the scene needs no imported model
    /// for its equipment. All meshes use welded vertices with area-weighted normals: large faces
    /// stay flat while the bevels catch the light, which is what makes a box read as a product.
    /// </summary>
    public static class ProceduralMeshFactory
    {
        private const string MeshFolder = Module2SceneBuilder.ModuleRoot + "/Models/Generated";

        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>Forgets the meshes of a previous build (assets are overwritten on demand).</summary>
        public static void BeginBuild()
        {
            Cache.Clear();
        }

        // ------------------------------------------------------------------
        // Bevelled box (rounded vertical corners + chamfered top and bottom edges)
        // ------------------------------------------------------------------

        /// <summary>
        /// Box centred on X/Z with its base at y = 0. <paramref name="cornerRadius"/> rounds the
        /// vertical edges, <paramref name="bevel"/> chamfers the top and bottom edges.
        /// </summary>
        public static Mesh BevelBox(float width, float height, float depth, float cornerRadius, float bevel, int cornerSegments = 4)
        {
            string key = $"BevelBox_{F(width)}_{F(height)}_{F(depth)}_{F(cornerRadius)}_{F(bevel)}_{cornerSegments}";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            cornerRadius = Mathf.Min(cornerRadius, Mathf.Min(width, depth) * 0.5f - 0.0001f);
            bevel = Mathf.Min(bevel, Mathf.Min(height * 0.5f, cornerRadius > 0f ? cornerRadius : height * 0.5f) - 0.00005f);
            bevel = Mathf.Max(bevel, 0.0002f);

            var rings = new List<Vector3[]>
            {
                RoundedRectRing(width - 2f * bevel, depth - 2f * bevel, Mathf.Max(cornerRadius - bevel, 0.0001f), 0f, cornerSegments),
                RoundedRectRing(width, depth, cornerRadius, bevel, cornerSegments),
                RoundedRectRing(width, depth, cornerRadius, height - bevel, cornerSegments),
                RoundedRectRing(width - 2f * bevel, depth - 2f * bevel, Mathf.Max(cornerRadius - bevel, 0.0001f), height, cornerSegments),
            };

            return Store(key, BuildFromRings(rings, true, true));
        }

        /// <summary>
        /// Wedge-shaped console housing: a bevelled box whose top slopes from
        /// <paramref name="backHeight"/> (at +Z) down to <paramref name="frontHeight"/> (at -Z).
        /// </summary>
        public static Mesh Wedge(float width, float depth, float frontHeight, float backHeight, float cornerRadius, float bevel, int cornerSegments = 4)
        {
            string key = $"Wedge_{F(width)}_{F(depth)}_{F(frontHeight)}_{F(backHeight)}_{F(cornerRadius)}_{F(bevel)}";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            float inner = Mathf.Max(cornerRadius - bevel, 0.0001f);
            Vector3[] bottom = RoundedRectRing(width - 2f * bevel, depth - 2f * bevel, inner, 0f, cornerSegments);
            Vector3[] low = RoundedRectRing(width, depth, cornerRadius, bevel, cornerSegments);
            Vector3[] high = RoundedRectRing(width, depth, cornerRadius, 0f, cornerSegments);
            Vector3[] top = RoundedRectRing(width - 2f * bevel, depth - 2f * bevel, inner, 0f, cornerSegments);

            for (int i = 0; i < high.Length; i++)
            {
                high[i].y = SlopeHeight(high[i].z, depth, frontHeight, backHeight) - bevel;
                top[i].y = SlopeHeight(top[i].z, depth, frontHeight, backHeight);
            }

            return Store(key, BuildFromRings(new List<Vector3[]> { bottom, low, high, top }, true, true));
        }

        private static float SlopeHeight(float z, float depth, float frontHeight, float backHeight)
        {
            float t = Mathf.InverseLerp(-depth * 0.5f, depth * 0.5f, z);
            return Mathf.Lerp(frontHeight, backHeight, t);
        }

        private static Vector3[] RoundedRectRing(float width, float depth, float radius, float y, int segments)
        {
            radius = Mathf.Clamp(radius, 0.0001f, Mathf.Min(width, depth) * 0.5f);
            float halfWidth = width * 0.5f - radius;
            float halfDepth = depth * 0.5f - radius;
            var ring = new Vector3[4 * (segments + 1)];
            int index = 0;

            // Counter-clockwise seen from above (+Y), starting at the +X/+Z corner.
            for (int corner = 0; corner < 4; corner++)
            {
                float centerX = (corner == 0 || corner == 3) ? halfWidth : -halfWidth;
                float centerZ = (corner < 2) ? halfDepth : -halfDepth;
                float startAngle = corner * 90f;
                for (int s = 0; s <= segments; s++)
                {
                    float angle = (startAngle + 90f * s / segments) * Mathf.Deg2Rad;
                    ring[index++] = new Vector3(centerX + Mathf.Cos(angle) * radius, y, centerZ + Mathf.Sin(angle) * radius);
                }
            }

            return ring;
        }

        // ------------------------------------------------------------------
        // Lathe (surface of revolution around Y)
        // ------------------------------------------------------------------

        /// <summary>
        /// Revolves a profile around the Y axis. Each profile point is (radius, height); list the
        /// points from the bottom up along the outside (and back down the inside for hollow glassware).
        /// A point with radius 0 closes the surface on the axis.
        /// </summary>
        public static Mesh Lathe(string name, Vector2[] profile, int segments = 32)
        {
            string key = "Lathe_" + name;
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            var rings = new List<Vector3[]>(profile.Length);
            for (int p = 0; p < profile.Length; p++)
            {
                float radius = Mathf.Max(profile[p].x, 0f);
                var ring = new Vector3[segments];
                for (int s = 0; s < segments; s++)
                {
                    // Counter-clockwise seen from above: a profile listed bottom-up faces outward.
                    float angle = 2f * Mathf.PI * s / segments;
                    ring[s] = new Vector3(Mathf.Cos(angle) * radius, profile[p].y, Mathf.Sin(angle) * radius);
                }

                rings.Add(ring);
            }

            return Store(key, BuildFromRings(rings, false, false));
        }

        // ------------------------------------------------------------------
        // Tube along a path (cables, pipes, probe arms)
        // ------------------------------------------------------------------

        /// <summary>Round tube that follows <paramref name="path"/> (local space), with closed ends.</summary>
        public static Mesh Tube(string name, Vector3[] path, float radius, int sides = 10)
        {
            string key = "Tube_" + name;
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            var rings = new List<Vector3[]>(path.Length + 2);
            Vector3 normal = Vector3.zero;

            for (int i = 0; i < path.Length; i++)
            {
                Vector3 tangent = i == 0 ? path[1] - path[0]
                    : i == path.Length - 1 ? path[i] - path[i - 1]
                    : path[i + 1] - path[i - 1];
                tangent.Normalize();

                if (i == 0)
                {
                    normal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                }
                else
                {
                    // Parallel transport keeps the tube from twisting.
                    normal = (normal - tangent * Vector3.Dot(normal, tangent)).normalized;
                }

                Vector3 binormal = Vector3.Cross(normal, tangent);
                var ring = new Vector3[sides];
                for (int s = 0; s < sides; s++)
                {
                    float angle = 2f * Mathf.PI * s / sides;
                    ring[s] = path[i] + (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * radius;
                }

                if (i == 0) rings.Add(Collapse(ring, path[0]));
                rings.Add(ring);
                if (i == path.Length - 1) rings.Add(Collapse(ring, path[i]));
            }

            return Store(key, BuildFromRings(rings, false, false));
        }

        /// <summary>Samples a smooth curve through control points (Catmull-Rom).</summary>
        public static Vector3[] SmoothPath(Vector3[] controlPoints, int samplesPerSegment = 8)
        {
            var result = new List<Vector3>();
            for (int i = 0; i < controlPoints.Length - 1; i++)
            {
                Vector3 p0 = controlPoints[Mathf.Max(i - 1, 0)];
                Vector3 p1 = controlPoints[i];
                Vector3 p2 = controlPoints[i + 1];
                Vector3 p3 = controlPoints[Mathf.Min(i + 2, controlPoints.Length - 1)];
                for (int s = 0; s < samplesPerSegment; s++)
                {
                    float t = s / (float)samplesPerSegment;
                    result.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
                }
            }

            result.Add(controlPoints[controlPoints.Length - 1]);
            return result.ToArray();
        }

        private static Vector3[] Collapse(Vector3[] ring, Vector3 center)
        {
            var collapsed = new Vector3[ring.Length];
            for (int i = 0; i < ring.Length; i++) collapsed[i] = center;
            return collapsed;
        }

        // ------------------------------------------------------------------
        // Shared construction
        // ------------------------------------------------------------------

        // Stitches consecutive rings (all with the same vertex count) into quads and optionally
        // closes the first and last ring with a fan. Rings must run counter-clockwise seen from
        // the outside-top for outward normals (see RoundedRectRing / Lathe).
        private static Mesh BuildFromRings(List<Vector3[]> rings, bool capBottom, bool capTop)
        {
            int ringSize = rings[0].Length;
            var vertices = new List<Vector3>(rings.Count * ringSize + 2);
            var uvs = new List<Vector2>(rings.Count * ringSize + 2);
            var triangles = new List<int>(rings.Count * ringSize * 6);

            for (int r = 0; r < rings.Count; r++)
            {
                for (int i = 0; i < ringSize; i++)
                {
                    vertices.Add(rings[r][i]);
                    uvs.Add(new Vector2(i / (float)ringSize, r / (float)Mathf.Max(rings.Count - 1, 1)));
                }
            }

            for (int r = 0; r < rings.Count - 1; r++)
            {
                int a = r * ringSize;
                int b = (r + 1) * ringSize;
                for (int i = 0; i < ringSize; i++)
                {
                    int next = (i + 1) % ringSize;
                    // Ring runs counter-clockwise seen from above: (lower i, upper i, upper next) faces outward.
                    AddTriangle(triangles, vertices, a + i, b + i, b + next);
                    AddTriangle(triangles, vertices, a + i, b + next, a + next);
                }
            }

            if (capBottom)
            {
                int center = vertices.Count;
                vertices.Add(Average(rings[0]));
                uvs.Add(new Vector2(0.5f, 0f));
                for (int i = 0; i < ringSize; i++)
                {
                    AddTriangle(triangles, vertices, center, i, (i + 1) % ringSize);
                }
            }

            if (capTop)
            {
                int offset = (rings.Count - 1) * ringSize;
                int center = vertices.Count;
                vertices.Add(Average(rings[rings.Count - 1]));
                uvs.Add(new Vector2(0.5f, 1f));
                for (int i = 0; i < ringSize; i++)
                {
                    AddTriangle(triangles, vertices, center, offset + (i + 1) % ringSize, offset + i);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.SetNormals(AreaWeightedNormals(vertices, triangles));
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        // Skips degenerate triangles (collapsed rings, poles of a lathe).
        private static void AddTriangle(List<int> triangles, List<Vector3> vertices, int a, int b, int c)
        {
            Vector3 cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (cross.sqrMagnitude < 1e-16f) return;
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        private static Vector3 Average(Vector3[] points)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < points.Length; i++) sum += points[i];
            return sum / points.Length;
        }

        // Vertices that share a position share a normal (poles, collapsed rings), weighted by face area.
        private static List<Vector3> AreaWeightedNormals(List<Vector3> vertices, List<int> triangles)
        {
            var byPosition = new Dictionary<Vector3Int, Vector3>();
            for (int t = 0; t < triangles.Count; t += 3)
            {
                Vector3 a = vertices[triangles[t]];
                Vector3 b = vertices[triangles[t + 1]];
                Vector3 c = vertices[triangles[t + 2]];
                Vector3 weighted = Vector3.Cross(b - a, c - a); // length = 2 * area
                for (int k = 0; k < 3; k++)
                {
                    Vector3Int cell = Quantize(vertices[triangles[t + k]]);
                    byPosition.TryGetValue(cell, out Vector3 sum);
                    byPosition[cell] = sum + weighted;
                }
            }

            var normals = new List<Vector3>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                byPosition.TryGetValue(Quantize(vertices[i]), out Vector3 sum);
                normals.Add(sum.sqrMagnitude > 0f ? sum.normalized : Vector3.up);
            }

            return normals;
        }

        private static Vector3Int Quantize(Vector3 position)
        {
            const float precision = 20000f; // 0.05 mm
            return new Vector3Int(Mathf.RoundToInt(position.x * precision), Mathf.RoundToInt(position.y * precision), Mathf.RoundToInt(position.z * precision));
        }

        private static Mesh Store(string key, Mesh mesh)
        {
            Module2SceneBuilder.EnsureFolder(MeshFolder);
            string safeName = key.Replace('.', 'p').Replace('-', 'm');
            if (safeName.Length > 80) safeName = safeName.Substring(0, 60) + "_" + (uint)key.GetHashCode();
            string path = $"{MeshFolder}/{safeName}.asset";

            mesh.name = safeName;
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Overwrite in place so prefabs and scenes that reference the asset keep their link.
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = safeName;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
            }

            Cache[key] = mesh;
            return mesh;
        }

        private static string F(float value)
        {
            return Mathf.RoundToInt(value * 10000f).ToString();
        }
    }
}
