// =============================================================================
// WaterNetwork.cs  –  Simulates water distribution via pipe networks.
//
// Water flows from pumps → pipes → water towers → consumers.
// Coverage is determined by BFS along tiles with WaterPipe=true or
// that are WaterPump / WaterTower buildings.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace MetroSim
{
    public class WaterNetwork : MonoBehaviour
    {
        // ── Public stats ──────────────────────────────────────────────────────
        public float TotalProduction { get; private set; }
        public float TotalDemand     { get; private set; }
        public bool  IsInShortage    => TotalDemand > TotalProduction + 0.01f;

        public void Simulate(GridMap map)
        {
            map.ForEach(t => t.HasWater = false);

            TotalProduction = 0f;
            TotalDemand     = 0f;

            // ── Gather water source tiles (pumps, towers) ─────────────────────
            var sources = new List<TileData>();
            map.ForEach(t =>
            {
                if (!t.HasBuilding) return;
                if (t.Building == BuildingType.WaterPump ||
                    t.Building == BuildingType.WaterTower)
                {
                    var def = BuildingDatabase.Get(t.Building);
                    if (def != null)
                    {
                        TotalProduction += def.WaterOutput;
                        sources.Add(t);
                    }
                }
            });

            if (sources.Count == 0) return;

            // ── BFS along water pipe network ──────────────────────────────────
            var visited = new HashSet<int>();
            var queue   = new Queue<TileData>();

            foreach (var src in sources)
            {
                int key = src.Y * map.Width + src.X;
                if (!visited.Add(key)) continue;
                src.HasWater = true;
                queue.Enqueue(src);
            }

            while (queue.Count > 0)
            {
                TileData cur = queue.Dequeue();
                foreach (TileData nb in map.GetNeighbours4(cur.X, cur.Y))
                {
                    int key = nb.Y * map.Width + nb.X;
                    if (!visited.Add(key)) continue;
                    // Water travels through pipe tiles and road tiles (pipes run under roads)
                    bool hasPipe = nb.WaterPipe || nb.IsRoad || nb.HasBuilding;
                    if (!hasPipe) continue;
                    nb.HasWater = true;
                    queue.Enqueue(nb);
                }
            }

            // ── Calculate demand ──────────────────────────────────────────────
            map.ForEach(t =>
            {
                if (!t.HasWater) return;
                TotalDemand += t.Zone switch
                {
                    ZoneType.Residential => Config.WATER_DEMAND_RES * (int)t.Density,
                    ZoneType.Commercial  => Config.WATER_DEMAND_COM * (int)t.Density,
                    ZoneType.Industrial  => Config.WATER_DEMAND_IND * (int)t.Density,
                    _                    => 0f
                };
                if (t.HasBuilding)
                {
                    var def = BuildingDatabase.Get(t.Building);
                    if (def != null) TotalDemand += def.WaterDemand;
                }
            });

            // ── Shortage: remove water from less-served tiles ─────────────────
            if (IsInShortage)
            {
                float ratio = 1f - TotalProduction / Mathf.Max(TotalDemand, 1f);
                map.ForEach(t =>
                {
                    if (!t.HasWater || t.Zone == ZoneType.None) return;
                    if (Random.value < ratio)
                        t.HasWater = false;
                });
            }
        }
    }
}
