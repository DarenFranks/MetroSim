// =============================================================================
// PathFinder.cs  –  A* pathfinding over the road network.
//
// Only road tiles are walkable.  The travel cost is determined by road type
// (highways are fastest, dirt roads are slowest).  Used by TrafficManager
// to route vehicles between origin and destination tiles.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace MetroSim
{
    public static class PathFinder
    {
        // ── A* Node ───────────────────────────────────────────────────────────
        private class Node
        {
            public int X, Y;
            public float G;   // cost from start
            public float H;   // heuristic to goal
            public float F => G + H;
            public Node Parent;

            public Node(int x, int y, float g, float h, Node parent)
            { X=x; Y=y; G=g; H=h; Parent=parent; }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Finds a road path from (sx,sy) to (gx,gy).
        /// Returns a list of (x,y) tile coords from start to goal,
        /// or an empty list if no path exists.
        /// Caps search at maxNodes to stay performant in large maps.
        /// </summary>
        public static List<Vector2Int> FindPath(
            GridMap map, int sx, int sy, int gx, int gy, int maxNodes = 2000)
        {
            if (!map.InBounds(sx, sy) || !map.InBounds(gx, gy))
                return new List<Vector2Int>();

            // If start or goal are not road-accessible, snap to nearest road
            if (!map.Get(sx, sy).IsRoad) (sx, sy) = NearestRoad(map, sx, sy);
            if (!map.Get(gx, gy).IsRoad) (gx, gy) = NearestRoad(map, gx, gy);
            if (sx < 0 || gx < 0) return new List<Vector2Int>();

            var open   = new SortedSet<(float f, int id)>();
            var nodes  = new Dictionary<int, Node>();
            var closed = new HashSet<int>();

            int startId = sy * map.Width + sx;
            var startNode = new Node(sx, sy, 0f, Heuristic(sx,sy,gx,gy), null);
            nodes[startId] = startNode;
            open.Add((startNode.F, startId));

            int[] dx = { -1, 1,  0, 0 };
            int[] dy = {  0, 0, -1, 1 };

            int iterations = 0;
            while (open.Count > 0 && iterations++ < maxNodes)
            {
                var (_, curId) = open.Min;
                open.Remove(open.Min);

                if (closed.Contains(curId)) continue;
                closed.Add(curId);

                Node cur = nodes[curId];
                if (cur.X == gx && cur.Y == gy)
                    return ReconstructPath(cur);

                for (int d = 0; d < 4; d++)
                {
                    int nx = cur.X + dx[d];
                    int ny = cur.Y + dy[d];
                    if (!map.InBounds(nx, ny)) continue;

                    TileData tile = map.Get(nx, ny);
                    if (!tile.IsRoad) continue;

                    int nbId = ny * map.Width + nx;
                    if (closed.Contains(nbId)) continue;

                    float cost = RoadNetwork.TravelCost(tile.Road);
                    // Add traffic congestion cost
                    cost += tile.TrafficDensity * 2f;

                    float newG = cur.G + cost;
                    if (!nodes.TryGetValue(nbId, out Node nbNode) || newG < nbNode.G)
                    {
                        var newNode = new Node(nx, ny, newG, Heuristic(nx,ny,gx,gy), cur);
                        nodes[nbId] = newNode;
                        open.Add((newNode.F, nbId));
                    }
                }
            }
            return new List<Vector2Int>(); // no path found
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static float Heuristic(int x1, int y1, int x2, int y2) =>
            Mathf.Abs(x1 - x2) + Mathf.Abs(y1 - y2); // Manhattan distance

        private static List<Vector2Int> ReconstructPath(Node goal)
        {
            var path = new List<Vector2Int>();
            Node cur = goal;
            while (cur != null) { path.Add(new Vector2Int(cur.X, cur.Y)); cur = cur.Parent; }
            path.Reverse();
            return path;
        }

        /// <summary>BFS to find the nearest road tile from (x,y).</summary>
        private static (int x, int y) NearestRoad(GridMap map, int sx, int sy)
        {
            var visited = new HashSet<int>();
            var queue   = new Queue<(int x, int y)>();
            queue.Enqueue((sx, sy));
            visited.Add(sy * map.Width + sx);

            int[] dx = {-1,1,0,0};
            int[] dy = {0,0,-1,1};

            int limit = 15;
            while (queue.Count > 0 && limit-- > 0)
            {
                var (cx, cy) = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx+dx[d], ny = cy+dy[d];
                    if (!map.InBounds(nx,ny)) continue;
                    int key = ny*map.Width+nx;
                    if (!visited.Add(key)) continue;
                    if (map.Get(nx,ny).IsRoad) return (nx,ny);
                    queue.Enqueue((nx,ny));
                }
            }
            return (-1,-1);
        }
    }
}
