// HeadlessSim: AI navigation that really plans. The NavMesh is approximated by a 2D occupancy grid
// (5 cm cells) baked from the colliders below a NavMeshSurface for the default Humanoid agent
// (radius 0.5 m, height 2 m); NavMeshAgent runs A* on it and walks the path. Not part of the Unity project.
using System;
using System.Collections.Generic;
using System.Linq;
using HeadlessSim;
using Unity.AI.Navigation;

namespace UnityEngine.AI
{
    public enum NavMeshPathStatus { PathComplete, PathPartial, PathInvalid }
    public enum ObstacleAvoidanceType { NoObstacleAvoidance, LowQualityObstacleAvoidance, MedQualityObstacleAvoidance, GoodQualityObstacleAvoidance, HighQualityObstacleAvoidance }
    public enum NavMeshObstacleShape { Capsule, Box }
    public enum NavMeshCollectGeometry { RenderMeshes, PhysicsColliders }

    public struct NavMeshHit
    {
        public Vector3 position;
        public Vector3 normal;
        public float distance;
        public int mask;
        public bool hit;
    }

    public class NavMeshPath
    {
        internal Vector3[] cornerArray = new Vector3[0];
        public Vector3[] corners => (Vector3[])cornerArray.Clone();
        public NavMeshPathStatus status { get; internal set; } = NavMeshPathStatus.PathInvalid;
        public void ClearCorners() { cornerArray = new Vector3[0]; status = NavMeshPathStatus.PathInvalid; }
        public int GetCornersNonAlloc(Vector3[] results) { int n = Math.Min(results.Length, cornerArray.Length); Array.Copy(cornerArray, results, n); return n; }
    }

    /// <summary>The baked occupancy grid.</summary>
    public sealed class NavGrid
    {
        public const float Cell = 0.05f;
        public float OriginX, OriginZ, FloorY;
        public int Width, Height;
        public bool[] Walkable;
        public float AgentRadius = 0.5f;

        public int Index(int x, int z) => z * Width + x;
        public bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Height;
        public int CellX(float worldX) => Mathf.FloorToInt((worldX - OriginX) / Cell);
        public int CellZ(float worldZ) => Mathf.FloorToInt((worldZ - OriginZ) / Cell);
        public Vector3 Center(int x, int z) => new Vector3(OriginX + (x + 0.5f) * Cell, FloorY, OriginZ + (z + 0.5f) * Cell);
        public float WalkableArea => Walkable.Count(w => w) * Cell * Cell;
        public int RegionCount;
    }

    public class NavMeshData : Object
    {
        internal readonly List<NavGrid> grids = new List<NavGrid>();
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Bounds sourceBounds;
        /// <summary>Simulator: the baked grid with the largest walkable area (null when the bake produced nothing).</summary>
        public NavGrid SimGrid => grids.OrderByDescending(g => g.WalkableArea).FirstOrDefault();
        /// <summary>Simulator: one grid per walkable height level.</summary>
        public IReadOnlyList<NavGrid> SimGrids => grids;
        public NavMeshData() { }
        public NavMeshData(int agentTypeId) { }
    }

    public struct NavMeshBuildSettings
    {
        public int agentTypeID;
        public float agentRadius, agentHeight, agentSlope, agentClimb, minRegionArea, voxelSize;
        public bool overrideVoxelSize, overrideTileSize;
        public int tileSize;
    }

    /// <summary>Simulator internals shared by NavMesh, NavMeshAgent and NavMeshSurface.</summary>
    public static class NavMeshWorld
    {
        private struct Blocker
        {
            public bool Circle;
            public float MinX, MaxX, MinZ, MaxZ; // rectangle footprint
            public float CenterX, CenterZ, Radius; // circle footprint
        }

        internal static void Reset() { }

        /// <summary>Grids of the active surfaces of the scene.</summary>
        public static List<NavGrid> Grids()
        {
            var grids = new List<NavGrid>();
            foreach (NavMeshSurface surface in Simulation.FindAll<NavMeshSurface>(false))
            {
                if (!surface.enabled) continue;
                if (surface.navMeshData == null) continue;
                foreach (NavGrid grid in surface.navMeshData.grids)
                {
                    if (!grids.Contains(grid)) grids.Add(grid);
                }
            }
            return grids;
        }

        public static string Describe()
        {
            List<NavGrid> grids = Grids();
            if (grids.Count == 0) return "no baked NavMesh in the scene";
            return string.Join("; ", grids.Select(g => $"{g.Width}x{g.Height} cells of {NavGrid.Cell * 100f:0} cm, floor y={g.FloorY:0.00}, walkable {g.WalkableArea:0.00} m2 in {g.RegionCount} region(s)"));
        }

        // ------------------------------------------------------------ Bake
        internal static List<NavGrid> Bake(NavMeshSurface surface)
        {
            var result = new List<NavGrid>();

            var sources = new List<KeyValuePair<Component, int>>(); // collider or renderer, area
            IEnumerable<GameObject> objects;
            if (surface.collectObjects == CollectObjects.Children) objects = surface.gameObject.GetComponentsInChildren<Transform>(false).Select(t => t.gameObject);
            else objects = Simulation.AllSceneObjects(false);

            Bounds volume = PhysicsMath.TransformBounds(surface.transform, new Bounds(surface.center, surface.size));
            foreach (GameObject go in objects)
            {
                if (((1 << go.layer) & surface.layerMask.value) == 0) continue;
                if (surface.ignoreNavMeshAgent && go.GetComponentInParent<NavMeshAgent>(true) != null) continue;
                if (surface.ignoreNavMeshObstacle && go.GetComponentInParent<NavMeshObstacle>(true) != null) continue;

                int area = surface.defaultArea;
                bool ignore = false;
                bool marked = false;
                for (Transform t = go.transform; t != null; t = t.parent)
                {
                    NavMeshModifier modifier = t.GetComponent<NavMeshModifier>();
                    if (modifier == null || !modifier.enabled) continue;
                    if (!ReferenceEquals(t, go.transform) && !modifier.applyToChildren) continue;
                    marked = true;
                    if (modifier.ignoreFromBuild) ignore = true;
                    else if (modifier.overrideArea) area = modifier.area;
                    break;
                }
                if (ignore) continue;
                if (surface.collectObjects == CollectObjects.MarkedWithModifier && !marked) continue;

                if (surface.useGeometry == NavMeshCollectGeometry.PhysicsColliders)
                {
                    foreach (Collider collider in go.GetComponents<Collider>())
                    {
                        if (!collider.enabled || collider.isTrigger) continue;
                        if (surface.collectObjects == CollectObjects.Volume && !volume.Intersects(collider.bounds)) continue;
                        sources.Add(new KeyValuePair<Component, int>(collider, area));
                    }
                }
                else
                {
                    var renderer = go.GetComponent<MeshRenderer>();
                    if (renderer == null || !renderer.enabled || go.GetComponent<MeshFilter>()?.sharedMesh == null) continue;
                    if (surface.collectObjects == CollectObjects.Volume && !volume.Intersects(renderer.bounds)) continue;
                    sources.Add(new KeyValuePair<Component, int>(renderer, area));
                }
            }

            if (sources.Count == 0) return result;

            // Every top face of a walkable source is a candidate floor; one grid per height level.
            var boundsOf = new Dictionary<Component, Bounds>();
            var levels = new List<float>();
            foreach (KeyValuePair<Component, int> source in sources)
            {
                Bounds b = source.Key is Collider c ? c.bounds : ((Renderer)source.Key).bounds;
                boundsOf[source.Key] = b;
                if (source.Value != 0 || b.size.x * b.size.z <= 0f) continue;
                if (!levels.Any(level => Mathf.Abs(level - b.max.y) <= 0.1f)) levels.Add(b.max.y);
            }

            levels.Sort();
            foreach (float level in levels.Take(32))
            {
                NavGrid grid = BakeLevel(surface, sources, boundsOf, level);
                if (grid != null) result.Add(grid);
            }
            return result;
        }

        private static NavGrid BakeLevel(NavMeshSurface surface, List<KeyValuePair<Component, int>> sources, Dictionary<Component, Bounds> boundsOf, float floorY)
        {
            const float agentRadius = 0.5f, agentHeight = 2.0f, minObstacleHeight = 0.1f;
            var floors = new List<Bounds>();
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (KeyValuePair<Component, int> source in sources)
            {
                Bounds b = boundsOf[source.Key];
                if (source.Value != 0 || Mathf.Abs(b.max.y - floorY) > 0.1f) continue;
                floors.Add(b);
                minX = Mathf.Min(minX, b.min.x); maxX = Mathf.Max(maxX, b.max.x);
                minZ = Mathf.Min(minZ, b.min.z); maxZ = Mathf.Max(maxZ, b.max.z);
            }
            if (floors.Count == 0) return null;

            float margin = agentRadius + NavGrid.Cell * 2f;
            var grid = new NavGrid { OriginX = minX - margin, OriginZ = minZ - margin, FloorY = floorY, AgentRadius = agentRadius };
            grid.Width = Mathf.CeilToInt((maxX - minX + 2f * margin) / NavGrid.Cell);
            grid.Height = Mathf.CeilToInt((maxZ - minZ + 2f * margin) / NavGrid.Cell);
            if (grid.Width <= 0 || grid.Height <= 0 || (long)grid.Width * grid.Height > 16000000L) return null;

            // 1) Cells whose centre lies on the floor.
            var onFloor = new bool[grid.Width * grid.Height];
            foreach (Bounds floor in floors)
            {
                int x0 = Mathf.Max(grid.CellX(floor.min.x), 0), x1 = Mathf.Min(grid.CellX(floor.max.x), grid.Width - 1);
                int z0 = Mathf.Max(grid.CellZ(floor.min.z), 0), z1 = Mathf.Min(grid.CellZ(floor.max.z), grid.Height - 1);
                for (int z = z0; z <= z1; z++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        Vector3 c = grid.Center(x, z);
                        if (c.x >= floor.min.x && c.x <= floor.max.x && c.z >= floor.min.z && c.z <= floor.max.z) onFloor[grid.Index(x, z)] = true;
                    }
                }
            }

            // 2) Erode by the agent radius from the floor border.
            var blocked = new bool[onFloor.Length];
            int reach = Mathf.CeilToInt(agentRadius / NavGrid.Cell);
            for (int z = 0; z < grid.Height; z++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (onFloor[grid.Index(x, z)]) continue;
                    bool border = (x > 0 && onFloor[grid.Index(x - 1, z)]) || (x < grid.Width - 1 && onFloor[grid.Index(x + 1, z)]) ||
                                  (z > 0 && onFloor[grid.Index(x, z - 1)]) || (z < grid.Height - 1 && onFloor[grid.Index(x, z + 1)]);
                    blocked[grid.Index(x, z)] = true;
                    if (!border) continue;
                    for (int dz = -reach; dz <= reach; dz++)
                    {
                        for (int dx = -reach; dx <= reach; dx++)
                        {
                            if ((dx * dx + dz * dz) * NavGrid.Cell * NavGrid.Cell > agentRadius * agentRadius) continue;
                            if (grid.InBounds(x + dx, z + dz)) blocked[grid.Index(x + dx, z + dz)] = true;
                        }
                    }
                }
            }

            // 3) Obstacles: anything that reaches into the agent's height range, dilated by the agent radius.
            foreach (KeyValuePair<Component, int> source in sources)
            {
                Bounds b = boundsOf[source.Key];
                bool isFloor = source.Value == 0 && Mathf.Abs(b.max.y - floorY) <= 0.1f;
                if (isFloor) continue;
                if (b.max.y <= floorY + minObstacleHeight || b.min.y >= floorY + agentHeight) continue;
                Stamp(grid, blocked, FootprintOf(source.Key, b), agentRadius);
            }

            grid.Walkable = new bool[onFloor.Length];
            for (int i = 0; i < onFloor.Length; i++) grid.Walkable[i] = onFloor[i] && !blocked[i];

            RemoveSmallRegions(grid, surface.minRegionArea);
            return grid.Walkable.Any(w => w) ? grid : null;
        }

        private static Blocker FootprintOf(Component source, Bounds bounds)
        {
            if (source is SphereCollider || (source is CapsuleCollider capsule && capsule.direction == 1 && Mathf.Abs(Vector3.Dot(capsule.transform.up, Vector3.up)) > 0.99f))
            {
                return new Blocker { Circle = true, CenterX = bounds.center.x, CenterZ = bounds.center.z, Radius = Mathf.Max(bounds.extents.x, bounds.extents.z) };
            }
            return new Blocker { MinX = bounds.min.x, MaxX = bounds.max.x, MinZ = bounds.min.z, MaxZ = bounds.max.z };
        }

        private static void Stamp(NavGrid grid, bool[] blocked, Blocker blocker, float inflate)
        {
            float minX, maxX, minZ, maxZ;
            if (blocker.Circle)
            {
                float r = blocker.Radius + inflate;
                minX = blocker.CenterX - r; maxX = blocker.CenterX + r; minZ = blocker.CenterZ - r; maxZ = blocker.CenterZ + r;
            }
            else
            {
                minX = blocker.MinX - inflate; maxX = blocker.MaxX + inflate; minZ = blocker.MinZ - inflate; maxZ = blocker.MaxZ + inflate;
            }

            int x0 = Mathf.Max(grid.CellX(minX), 0), x1 = Mathf.Min(grid.CellX(maxX), grid.Width - 1);
            int z0 = Mathf.Max(grid.CellZ(minZ), 0), z1 = Mathf.Min(grid.CellZ(maxZ), grid.Height - 1);
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector3 c = grid.Center(x, z);
                    float distance;
                    if (blocker.Circle)
                    {
                        float dx = c.x - blocker.CenterX, dz = c.z - blocker.CenterZ;
                        distance = Mathf.Sqrt(dx * dx + dz * dz) - blocker.Radius;
                    }
                    else
                    {
                        float dx = Mathf.Max(blocker.MinX - c.x, 0f, c.x - blocker.MaxX);
                        float dz = Mathf.Max(blocker.MinZ - c.z, 0f, c.z - blocker.MaxZ);
                        distance = Mathf.Sqrt(dx * dx + dz * dz);
                    }
                    if (distance < inflate) blocked[grid.Index(x, z)] = true;
                }
            }
        }

        private static void RemoveSmallRegions(NavGrid grid, float minRegionArea)
        {
            var label = new int[grid.Walkable.Length];
            var stack = new Stack<int>();
            int regions = 0, kept = 0;
            for (int start = 0; start < label.Length; start++)
            {
                if (!grid.Walkable[start] || label[start] != 0) continue;
                regions++;
                var cells = new List<int>();
                stack.Push(start);
                label[start] = regions;
                while (stack.Count > 0)
                {
                    int index = stack.Pop();
                    cells.Add(index);
                    int x = index % grid.Width, z = index / grid.Width;
                    TryPush(grid, label, stack, x + 1, z, regions);
                    TryPush(grid, label, stack, x - 1, z, regions);
                    TryPush(grid, label, stack, x, z + 1, regions);
                    TryPush(grid, label, stack, x, z - 1, regions);
                }

                if (cells.Count * NavGrid.Cell * NavGrid.Cell < minRegionArea)
                {
                    foreach (int index in cells) grid.Walkable[index] = false;
                }
                else kept++;
            }
            grid.RegionCount = kept;
        }

        private static void TryPush(NavGrid grid, int[] label, Stack<int> stack, int x, int z, int region)
        {
            if (!grid.InBounds(x, z)) return;
            int index = grid.Index(x, z);
            if (!grid.Walkable[index] || label[index] != 0) return;
            label[index] = region;
            stack.Push(index);
        }

        // ------------------------------------------------------------ Live grid (baked + carving obstacles)
        /// <summary>Simulator: walkable cells right now (baked grid minus carving obstacles).</summary>
        public static bool[] SimLive(NavGrid grid) => Live(grid);

        internal static bool[] Live(NavGrid grid)
        {
            var live = (bool[])grid.Walkable.Clone();
            var blocked = new bool[live.Length];
            bool any = false;
            foreach (NavMeshObstacle obstacle in Simulation.FindAll<NavMeshObstacle>(false))
            {
                if (!obstacle.enabled || !obstacle.carving) continue;
                Bounds b = obstacle.SimWorldBounds;
                if (b.max.y <= grid.FloorY + 0.05f || b.min.y >= grid.FloorY + 2.0f) continue;
                Blocker blocker = obstacle.shape == NavMeshObstacleShape.Capsule
                    ? new Blocker { Circle = true, CenterX = b.center.x, CenterZ = b.center.z, Radius = Mathf.Max(b.extents.x, b.extents.z) }
                    : new Blocker { MinX = b.min.x, MaxX = b.max.x, MinZ = b.min.z, MaxZ = b.max.z };
                Stamp(grid, blocked, blocker, grid.AgentRadius);
                any = true;
            }
            if (any) for (int i = 0; i < live.Length; i++) if (blocked[i]) live[i] = false;
            return live;
        }

        /// <summary>Nearest walkable cell (horizontal distance) within <paramref name="maxDistance"/>.</summary>
        internal static bool Nearest(NavGrid grid, bool[] live, Vector3 position, float maxDistance, out int cellX, out int cellZ, out Vector3 point)
        {
            cellX = grid.CellX(position.x); cellZ = grid.CellZ(position.z);
            if (grid.InBounds(cellX, cellZ) && live[grid.Index(cellX, cellZ)])
            {
                point = new Vector3(position.x, grid.FloorY, position.z);
                return true;
            }

            int reach = Mathf.CeilToInt(maxDistance / NavGrid.Cell) + 1;
            float best = maxDistance * maxDistance;
            bool found = false;
            int bx = 0, bz = 0;
            for (int dz = -reach; dz <= reach; dz++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int x = cellX + dx, z = cellZ + dz;
                    if (!grid.InBounds(x, z) || !live[grid.Index(x, z)]) continue;
                    Vector3 c = grid.Center(x, z);
                    float sqr = (c.x - position.x) * (c.x - position.x) + (c.z - position.z) * (c.z - position.z);
                    if (sqr <= best) { best = sqr; bx = x; bz = z; found = true; }
                }
            }

            cellX = bx; cellZ = bz;
            point = found ? grid.Center(bx, bz) : position;
            return found;
        }

        internal static bool FindGrid(Vector3 position, float maxDistance, out NavGrid grid, out bool[] live, out int cellX, out int cellZ, out Vector3 point)
        {
            grid = null; live = null; cellX = 0; cellZ = 0; point = position;
            float best = float.MaxValue;
            foreach (NavGrid candidate in Grids())
            {
                if (Mathf.Abs(position.y - candidate.FloorY) > Mathf.Max(maxDistance, 2f)) continue;
                bool[] candidateLive = Live(candidate);
                if (!Nearest(candidate, candidateLive, position, maxDistance, out int x, out int z, out Vector3 p)) continue;
                float sqr = (p - position).sqrMagnitude; // 3D: the level closest to the query height wins
                if (sqr < best) { best = sqr; grid = candidate; live = candidateLive; cellX = x; cellZ = z; point = p; }
            }
            return grid != null;
        }

        // ------------------------------------------------------------ A*
        private static readonly int[] NeighbourX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] NeighbourZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// <summary>Plans from <paramref name="start"/> to <paramref name="target"/>. Corners are in world space.</summary>
        internal static NavMeshPathStatus Plan(Vector3 start, Vector3 target, List<Vector3> corners)
        {
            corners.Clear();
            if (!FindGrid(start, 1.5f, out NavGrid grid, out bool[] live, out int sx, out int sz, out Vector3 startPoint)) return NavMeshPathStatus.PathInvalid;
            if (!Nearest(grid, live, target, 2.0f, out int tx, out int tz, out Vector3 targetPoint)) return NavMeshPathStatus.PathInvalid;

            int width = grid.Width, total = live.Length;
            int startIndex = grid.Index(sx, sz), goalIndex = grid.Index(tx, tz);
            var cost = new float[total];
            var parent = new int[total];
            var closed = new bool[total];
            for (int i = 0; i < total; i++) { cost[i] = float.MaxValue; parent[i] = -1; }

            var open = new MinHeap();
            cost[startIndex] = 0f;
            open.Push(startIndex, Heuristic(sx, sz, tx, tz));
            int closest = startIndex;
            float closestDistance = Heuristic(sx, sz, tx, tz);
            bool reached = false;

            while (open.Count > 0)
            {
                int current = open.Pop();
                if (closed[current]) continue;
                closed[current] = true;
                int cx = current % width, cz = current / width;
                float h = Heuristic(cx, cz, tx, tz);
                if (h < closestDistance) { closestDistance = h; closest = current; }
                if (current == goalIndex) { reached = true; break; }

                for (int n = 0; n < 8; n++)
                {
                    int nx = cx + NeighbourX[n], nz = cz + NeighbourZ[n];
                    if (!grid.InBounds(nx, nz)) continue;
                    int neighbour = grid.Index(nx, nz);
                    if (!live[neighbour] || closed[neighbour]) continue;
                    if (n >= 4 && (!live[grid.Index(cx + NeighbourX[n], cz)] || !live[grid.Index(cx, cz + NeighbourZ[n])])) continue; // no corner cutting
                    float step = n < 4 ? 1f : 1.41421356f;
                    float candidate = cost[current] + step;
                    if (candidate >= cost[neighbour]) continue;
                    cost[neighbour] = candidate;
                    parent[neighbour] = current;
                    open.Push(neighbour, candidate + Heuristic(nx, nz, tx, tz));
                }
            }

            int end = reached ? goalIndex : closest;
            var cells = new List<int>();
            for (int index = end; index != -1; index = parent[index]) cells.Add(index);
            cells.Reverse();

            // String pulling: keep only the cells where the straight line would leave the walkable area.
            var points = new List<Vector3>();
            foreach (int index in cells) points.Add(grid.Center(index % width, index / width));
            points[0] = startPoint;
            if (reached) points[points.Count - 1] = targetPoint;

            corners.Add(points[0]);
            int anchor = 0;
            while (anchor < points.Count - 1)
            {
                int next = anchor + 1;
                for (int probe = points.Count - 1; probe > anchor + 1; probe--)
                {
                    if (LineWalkable(grid, live, points[anchor], points[probe])) { next = probe; break; }
                }
                corners.Add(points[next]);
                anchor = next;
            }

            return reached ? NavMeshPathStatus.PathComplete : NavMeshPathStatus.PathPartial;
        }

        private static float Heuristic(int x, int z, int tx, int tz)
        {
            int dx = Math.Abs(x - tx), dz = Math.Abs(z - tz);
            return Math.Max(dx, dz) + 0.41421356f * Math.Min(dx, dz);
        }

        internal static bool LineWalkable(NavGrid grid, bool[] live, Vector3 a, Vector3 b)
        {
            float length = new Vector2(b.x - a.x, b.z - a.z).magnitude;
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / (NavGrid.Cell * 0.5f)));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = grid.CellX(a.x + (b.x - a.x) * t), z = grid.CellZ(a.z + (b.z - a.z) * t);
                if (!grid.InBounds(x, z) || !live[grid.Index(x, z)]) return false;
            }
            return true;
        }

        private sealed class MinHeap
        {
            private readonly List<KeyValuePair<float, int>> items = new List<KeyValuePair<float, int>>();
            public int Count => items.Count;
            public void Push(int value, float priority)
            {
                items.Add(new KeyValuePair<float, int>(priority, value));
                int i = items.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (items[p].Key <= items[i].Key) break;
                    KeyValuePair<float, int> swap = items[p]; items[p] = items[i]; items[i] = swap;
                    i = p;
                }
            }
            public int Pop()
            {
                int result = items[0].Value;
                int last = items.Count - 1;
                items[0] = items[last];
                items.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, smallest = i;
                    if (l < items.Count && items[l].Key < items[smallest].Key) smallest = l;
                    if (r < items.Count && items[r].Key < items[smallest].Key) smallest = r;
                    if (smallest == i) break;
                    KeyValuePair<float, int> swap = items[smallest]; items[smallest] = items[i]; items[i] = swap;
                    i = smallest;
                }
                return result;
            }
        }
    }

    public static class NavMesh
    {
        public const int AllAreas = -1;
        public static float avoidancePredictionTime { get; set; } = 2f;
        public static int pathfindingIterationsPerFrame { get; set; } = 100;

        public static int GetAreaFromName(string areaName)
        {
            switch (areaName)
            {
                case "Walkable": return 0;
                case "Not Walkable": return 1;
                case "Jump": return 2;
                default: return -1;
            }
        }
        public static string[] GetAreaNames() => new[] { "Walkable", "Not Walkable", "Jump" };
        public static float GetAreaCost(int areaIndex) => 1f;
        public static void SetAreaCost(int areaIndex, float cost) { }

        public static bool SamplePosition(Vector3 sourcePosition, out NavMeshHit hit, float maxDistance, int areaMask)
        {
            hit = new NavMeshHit();
            if (!NavMeshWorld.FindGrid(sourcePosition, maxDistance, out NavGrid grid, out bool[] _, out int _, out int _, out Vector3 point)) return false;
            float distance = Vector3.Distance(point, sourcePosition);
            if (distance > maxDistance + 1e-4f) return false;
            hit.position = point;
            hit.distance = distance;
            hit.normal = Vector3.up;
            hit.mask = 1;
            hit.hit = true;
            return true;
        }

        public static bool CalculatePath(Vector3 sourcePosition, Vector3 targetPosition, int areaMask, NavMeshPath path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            var corners = new List<Vector3>();
            NavMeshPathStatus status = NavMeshWorld.Plan(sourcePosition, targetPosition, corners);
            path.cornerArray = corners.ToArray();
            path.status = status;
            return status != NavMeshPathStatus.PathInvalid;
        }

        /// <summary>True when the straight line from source to target is blocked (like Unity: returns true on a hit).</summary>
        public static bool Raycast(Vector3 sourcePosition, Vector3 targetPosition, out NavMeshHit hit, int areaMask)
        {
            hit = new NavMeshHit { position = targetPosition };
            if (!NavMeshWorld.FindGrid(sourcePosition, 1f, out NavGrid grid, out bool[] live, out int _, out int _, out Vector3 _)) { hit.hit = true; hit.position = sourcePosition; return true; }
            bool clear = NavMeshWorld.LineWalkable(grid, live, sourcePosition, targetPosition);
            hit.hit = !clear;
            return !clear;
        }

        public static bool FindClosestEdge(Vector3 sourcePosition, out NavMeshHit hit, int areaMask) => SamplePosition(sourcePosition, out hit, 1f, areaMask);
        public static void RemoveAllNavMeshData() { foreach (NavMeshSurface surface in Simulation.FindAll<NavMeshSurface>(true)) surface.navMeshData = null; }
        public static NavMeshBuildSettings GetSettingsByID(int agentTypeID) => new NavMeshBuildSettings { agentTypeID = agentTypeID, agentRadius = 0.5f, agentHeight = 2f, agentSlope = 45f, agentClimb = 0.75f };
        public static int GetSettingsCount() => 1;
        public static string GetSettingsNameFromID(int agentTypeID) => "Humanoid";
    }

    public class NavMeshObstacle : Behaviour
    {
        public NavMeshObstacleShape shape = NavMeshObstacleShape.Box;
        public Vector3 center;
        public Vector3 size = Vector3.one;
        public float radius = 0.5f;
        public float height = 2f;
        public bool carving;
        public bool carveOnlyStationary = true;
        public float carvingMoveThreshold = 0.1f;
        public float carvingTimeToStationary = 0.5f;
        [NonSerialized] private Vector3 velocityValue;
        public Vector3 velocity { get => velocityValue; set => velocityValue = value; }

        /// <summary>Simulator: world-space bounds of the obstacle shape.</summary>
        public Bounds SimWorldBounds
        {
            get
            {
                Vector3 local = shape == NavMeshObstacleShape.Capsule ? new Vector3(radius * 2f, Mathf.Max(height, radius * 2f), radius * 2f) : size;
                return PhysicsMath.TransformBounds(transform, new Bounds(center, local));
            }
        }
    }

    public class NavMeshAgent : Behaviour
    {
        public int agentTypeID;
        public float radius = 0.5f;
        public float height = 2f;
        public float baseOffset;
        public float speed = 3.5f;
        public float angularSpeed = 120f;
        public float acceleration = 8f;
        public float stoppingDistance;
        public bool autoBraking = true;
        public ObstacleAvoidanceType obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        public int avoidancePriority = 50;
        public int areaMask = -1;
        public bool autoRepath = true;
        public bool autoTraverseOffMeshLink = true;
        public bool updatePosition = true;
        public bool updateRotation = true;
        public bool updateUpAxis = true;

        // Runtime state (never serialized or cloned).
        [NonSerialized] private bool onMesh;
        [NonSerialized] private bool stopped;
        [NonSerialized] private bool pending;
        [NonSerialized] private readonly List<Vector3> corners = new List<Vector3>();
        [NonSerialized] private int cornerIndex;
        [NonSerialized] private float currentSpeed;
        [NonSerialized] private Vector3 velocityValue;
        [NonSerialized] private Vector3 destinationValue;
        [NonSerialized] private NavMeshPathStatus status = NavMeshPathStatus.PathComplete;
        [NonSerialized] private Vector3 simulatedPosition;

        internal override bool SimTicks => true;

        private bool Active => Application.isPlaying && enabled && gameObject.activeInHierarchy && !destroyed;
        public bool isOnNavMesh => Active && onMesh;
        public bool isActiveOnNavMesh => isOnNavMesh;
        public bool isOnOffMeshLink => false;
        public bool pathPending => pending;
        public bool hasPath => corners.Count > 0 && cornerIndex < corners.Count;
        public bool isPathStale => false;
        public NavMeshPathStatus pathStatus => status;
        public Vector3 velocity { get => velocityValue; set => velocityValue = value; }
        public Vector3 desiredVelocity => hasPath && !stopped ? (corners[cornerIndex] - Position).normalized * speed : Vector3.zero;
        public Vector3 steeringTarget => hasPath ? corners[cornerIndex] : Position;
        public Vector3 nextPosition { get => updatePosition ? transform.position : simulatedPosition; set { simulatedPosition = value; if (updatePosition) transform.position = value; } }
        public Vector3 pathEndPosition => corners.Count > 0 ? corners[corners.Count - 1] : Position;
        public float areaCostUnder => 1f;
        private Vector3 Position => updatePosition ? transform.position : simulatedPosition;

        internal override void SimOnEnable()
        {
            simulatedPosition = transform.position;
            onMesh = Place(transform.position, true);
        }

        internal override void SimOnDisable()
        {
            onMesh = false;
            corners.Clear();
            velocityValue = Vector3.zero;
            currentSpeed = 0f;
            pending = false;
        }

        private bool Place(Vector3 position, bool warnOnFailure)
        {
            if (NavMeshWorld.FindGrid(position, 1.0f, out NavGrid grid, out bool[] _, out int _, out int _, out Vector3 point) &&
                Mathf.Abs(position.y - baseOffset - grid.FloorY) <= Mathf.Max(height, 1f))
            {
                var placed = new Vector3(point.x, grid.FloorY + baseOffset, point.z);
                simulatedPosition = placed;
                if (updatePosition) transform.position = placed;
                return true;
            }

            if (warnOnFailure) Debug.LogWarning($"Failed to create agent because it is not close enough to the NavMesh ({name} at {position}).");
            return false;
        }

        public bool Warp(Vector3 newPosition)
        {
            if (!Active) return false;
            corners.Clear();
            velocityValue = Vector3.zero;
            currentSpeed = 0f;
            pending = false;
            onMesh = Place(newPosition, false);
            return onMesh;
        }

        private bool RequireOnMesh(string operation)
        {
            if (isOnNavMesh) return true;
            Debug.LogError($"\"{operation}\" can only be called on an active agent that has been placed on a NavMesh.");
            return false;
        }

        public Vector3 destination
        {
            get => destinationValue;
            set => SetDestination(value);
        }

        public bool SetDestination(Vector3 target)
        {
            if (!RequireOnMesh("SetDestination")) return false;
            var planned = new List<Vector3>();
            NavMeshPathStatus result = NavMeshWorld.Plan(Position, target, planned);
            if (result == NavMeshPathStatus.PathInvalid)
            {
                status = NavMeshPathStatus.PathInvalid;
                corners.Clear();
                return false;
            }

            corners.Clear();
            corners.AddRange(planned);
            cornerIndex = corners.Count > 1 ? 1 : 0;
            destinationValue = corners[corners.Count - 1];
            status = result;
            pending = true;
            return true;
        }

        public bool CalculatePath(Vector3 targetPosition, NavMeshPath path) => NavMesh.CalculatePath(Position, targetPosition, areaMask, path);

        public NavMeshPath path
        {
            get
            {
                var result = new NavMeshPath { status = status };
                var remaining = new List<Vector3> { Position };
                for (int i = cornerIndex; i < corners.Count; i++) remaining.Add(corners[i]);
                result.cornerArray = hasPath ? remaining.ToArray() : new Vector3[0];
                return result;
            }
            set => SetPath(value);
        }

        public bool SetPath(NavMeshPath newPath)
        {
            if (!RequireOnMesh("SetPath") || newPath == null || newPath.cornerArray.Length == 0) return false;
            corners.Clear();
            corners.AddRange(newPath.cornerArray);
            cornerIndex = corners.Count > 1 ? 1 : 0;
            destinationValue = corners[corners.Count - 1];
            status = newPath.status;
            return true;
        }

        public void ResetPath()
        {
            if (!RequireOnMesh("ResetPath")) return;
            corners.Clear();
            pending = false;
        }

        public bool isStopped
        {
            get => stopped;
            set
            {
                if (!RequireOnMesh(value ? "Stop" : "Resume")) return;
                stopped = value;
            }
        }

        [Obsolete("Set isStopped to true instead")] public void Stop() { isStopped = true; }
        [Obsolete("Set isStopped to false instead")] public void Resume() { isStopped = false; }

        public float remainingDistance
        {
            get
            {
                if (!RequireOnMesh("GetRemainingDistance")) return 0f;
                if (!hasPath) return 0f;
                float distance = FlatDistance(Position, corners[cornerIndex]);
                for (int i = cornerIndex; i < corners.Count - 1; i++) distance += FlatDistance(corners[i], corners[i + 1]);
                return distance;
            }
        }

        public void Move(Vector3 offset)
        {
            if (!isOnNavMesh) return;
            nextPosition = Position + offset;
        }

        public bool Raycast(Vector3 targetPosition, out NavMeshHit hit) => NavMesh.Raycast(Position, targetPosition, out hit, areaMask);
        public bool SamplePathPosition(int mask, float maxDistance, out NavMeshHit hit) { hit = new NavMeshHit { position = Position, hit = false }; return false; }
        public void CompleteOffMeshLink() { }

        private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        internal override void SimUpdate(float deltaTime)
        {
            pending = false;
            if (!onMesh || deltaTime <= 0f) { velocityValue = Vector3.zero; return; }
            if (!hasPath || stopped)
            {
                currentSpeed = 0f;
                velocityValue = Vector3.zero;
                return;
            }

            float remaining = remainingDistanceUnchecked();
            float desired = speed;
            if (autoBraking) desired = Mathf.Min(speed, Mathf.Sqrt(2f * Mathf.Max(acceleration, 0.01f) * Mathf.Max(remaining - stoppingDistance, 0f)) + 0.05f);
            currentSpeed = Mathf.MoveTowards(currentSpeed, desired, Mathf.Max(acceleration, 0.01f) * deltaTime);

            Vector3 start = Position;
            Vector3 position = start;
            float budget = Mathf.Min(currentSpeed * deltaTime, Mathf.Max(remaining - stoppingDistance, 0f));
            while (budget > 0f && cornerIndex < corners.Count)
            {
                Vector3 corner = corners[cornerIndex];
                var flat = new Vector3(corner.x - position.x, 0f, corner.z - position.z);
                float distance = flat.magnitude;
                if (distance <= budget)
                {
                    position = new Vector3(corner.x, position.y, corner.z);
                    budget -= distance;
                    if (cornerIndex < corners.Count - 1) cornerIndex++;
                    else break;
                }
                else
                {
                    position += flat / distance * budget;
                    budget = 0f;
                }
            }

            velocityValue = (position - start) / deltaTime;
            simulatedPosition = position;
            if (updatePosition) transform.position = position;

            if (updateRotation && velocityValue.sqrMagnitude > 1e-4f)
            {
                Quaternion look = Quaternion.LookRotation(new Vector3(velocityValue.x, 0f, velocityValue.z), Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, angularSpeed * deltaTime);
            }

            if (remainingDistanceUnchecked() <= stoppingDistance + 1e-3f)
            {
                // Arrived: the path is consumed, the agent rests.
                corners.Clear();
                cornerIndex = 0;
                currentSpeed = 0f;
                velocityValue = Vector3.zero;
            }
        }

        private float remainingDistanceUnchecked()
        {
            if (!hasPath) return 0f;
            float distance = FlatDistance(Position, corners[cornerIndex]);
            for (int i = cornerIndex; i < corners.Count - 1; i++) distance += FlatDistance(corners[i], corners[i + 1]);
            return distance;
        }
    }

    public class OffMeshLink : Behaviour { public Transform startTransform, endTransform; public bool biDirectional = true, activated = true; public float costOverride = -1f; }
}

namespace Unity.AI.Navigation
{
    using UnityEngine;
    using UnityEngine.AI;

    public enum CollectObjects { All = 0, Volume = 1, Children = 2, MarkedWithModifier = 3 }

    [DefaultExecutionOrder(-102)]
    public class NavMeshSurface : MonoBehaviour
    {
        public int agentTypeID;
        public CollectObjects collectObjects = CollectObjects.All;
        public Vector3 size = new Vector3(10f, 10f, 10f);
        public Vector3 center = new Vector3(0f, 2f, 0f);
        public LayerMask layerMask = ~0;
        public NavMeshCollectGeometry useGeometry = NavMeshCollectGeometry.RenderMeshes;
        public int defaultArea;
        public bool generateLinks;
        public bool ignoreNavMeshAgent = true;
        public bool ignoreNavMeshObstacle = true;
        public bool overrideTileSize;
        public int tileSize = 256;
        public bool overrideVoxelSize;
        public float voxelSize;
        public float minRegionArea = 2f;
        public bool buildHeightMesh;
        [SerializeField] private NavMeshData m_NavMeshData;

        public NavMeshData navMeshData { get => m_NavMeshData; set => m_NavMeshData = value; }
        public static List<NavMeshSurface> activeSurfaces => Simulation.FindAll<NavMeshSurface>(false);

        public void BuildNavMesh()
        {
            List<NavGrid> grids = NavMeshWorld.Bake(this);
            if (grids.Count == 0)
            {
                m_NavMeshData = null;
                return;
            }
            m_NavMeshData = new NavMeshData { name = "NavMesh-" + name, position = transform.position, rotation = transform.rotation };
            m_NavMeshData.grids.AddRange(grids);
        }

        public AsyncOperation UpdateNavMesh(NavMeshData data)
        {
            List<NavGrid> grids = NavMeshWorld.Bake(this);
            if (data != null) { data.grids.Clear(); data.grids.AddRange(grids); }
            return new AsyncOperation();
        }

        public void AddData() { }
        public void RemoveData() { }
        public NavMeshBuildSettings GetBuildSettings() => NavMesh.GetSettingsByID(agentTypeID);
    }

    public class NavMeshModifier : MonoBehaviour
    {
        public bool overrideArea;
        public int area;
        public bool ignoreFromBuild;
        public bool applyToChildren = true;
        public bool overrideGenerateLinks;
        public bool generateLinks;
        public bool AffectsAgentType(int agentTypeID) => true;
    }

    public class NavMeshModifierVolume : MonoBehaviour
    {
        public Vector3 size = new Vector3(4f, 3f, 4f);
        public Vector3 center = new Vector3(0f, 1f, 0f);
        public int area;
    }

    public class NavMeshLink : MonoBehaviour
    {
        public int agentTypeID;
        public Vector3 startPoint = new Vector3(0f, 0f, -2.5f), endPoint = new Vector3(0f, 0f, 2.5f);
        public float width;
        public bool bidirectional = true;
        public bool autoUpdate;
        public int area;
    }
}
