// =============================================================================
// RoadNetwork.cs  –  Manages road connectivity and tile road-access flags.
//
// Road access is computed every tick via a simple flood-fill from all
// road tiles.  Any zone tile adjacent to a reachable road gets HasRoadAccess=true.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace MetroSim
{
    public class RoadNetwork : MonoBehaviour
    {
        /// <summary>
        /// BFS flood-fill from every road tile, then marks adjacent zone tiles
        /// as having road access.
        /// </summary>
        public void UpdateRoadAccess(GridMap map)
        {
            // ── Step 1: clear all road-access flags ───────────────────────────
            map.ForEach(t => t.HasRoadAccess = false);

            // ── Step 2: BFS from every road tile ──────────────────────────────
            var visited = new HashSet<int>();  // encoded tile index
            var queue   = new Queue<TileData>();

            // Seed the BFS with every existing road tile
            map.ForEach(t =>
            {
                if (t.IsRoad)
                {
                    t.HasRoadAccess = true;
                    int key = t.Y * map.Width + t.X;
                    if (visited.Add(key)) queue.Enqueue(t);
                }
            });

            // Spread road access through connected road tiles
            while (queue.Count > 0)
            {
                TileData cur = queue.Dequeue();
                foreach (TileData nb in map.GetNeighbours4(cur.X, cur.Y))
                {
                    if (!nb.IsRoad) continue;
                    int key = nb.Y * map.Width + nb.X;
                    if (!visited.Add(key)) continue;
                    nb.HasRoadAccess = true;
                    queue.Enqueue(nb);
                }
            }

            // ── Step 3: mark zone tiles adjacent to roads ─────────────────────
            map.ForEach(t =>
            {
                if (t.Zone == ZoneType.None && !t.HasBuilding) return;
                if (t.IsRoad) return;  // skip road tiles themselves
                t.HasRoadAccess = map.HasAdjacentRoad(t.X, t.Y);
            });
        }

        // ── Road type helpers ─────────────────────────────────────────────────

        /// <summary>Traffic capacity of a road type (vehicles/tick).</summary>
        public static int Capacity(RoadType road) => road switch
        {
            RoadType.Dirt    => 5,
            RoadType.Street  => 20,
            RoadType.Avenue  => 60,
            RoadType.Highway => 200,
            _                => 0
        };

        /// <summary>Pathfinding cost multiplier (lower = faster road).</summary>
        public static float TravelCost(RoadType road) => road switch
        {
            RoadType.Highway => 0.3f,
            RoadType.Avenue  => 0.6f,
            RoadType.Street  => 1.0f,
            RoadType.Dirt    => 2.5f,
            _                => float.MaxValue  // impassable
        };

        /// <summary>Display name for road type.</summary>
        public static string Name(RoadType road) => road switch
        {
            RoadType.Dirt    => "Dirt Road",
            RoadType.Street  => "Street",
            RoadType.Avenue  => "Avenue",
            RoadType.Highway => "Highway",
            _                => "None"
        };
    }
}
