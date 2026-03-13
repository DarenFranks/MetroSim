// =============================================================================
// PowerNetwork.cs  –  Simulates power generation and distribution.
//
// Algorithm:
//   1. Sum all generator output (MW).
//   2. BFS from each generator through road tiles, power lines, and occupied
//      buildings to spread HasPower=true.
//   3. Compute demand from all occupied tiles.
//   4. If demand > supply, randomly remove power from the most distant tiles.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace MetroSim
{
    public class PowerNetwork : MonoBehaviour
    {
        // ── Public stats (read by UI) ─────────────────────────────────────────
        public float TotalGeneration { get; private set; }
        public float TotalDemand     { get; private set; }
        public bool  IsInShortage    => TotalDemand > TotalGeneration + 0.01f;

        public void Simulate(GridMap map)
        {
            // ── Clear all power flags ──────────────────────────────────────────
            map.ForEach(t => t.HasPower = false);

            TotalGeneration = 0f;
            TotalDemand     = 0f;

            // ── Gather generator tiles ────────────────────────────────────────
            var generators = new List<TileData>();
            map.ForEach(t =>
            {
                if (!t.HasBuilding) return;
                var def = BuildingDatabase.Get(t.Building);
                if (def != null && def.PowerOutput > 0f)
                {
                    TotalGeneration += def.PowerOutput;
                    generators.Add(t);
                }
            });

            if (generators.Count == 0) return;  // no power at all

            // ── BFS flood from every generator ────────────────────────────────
            // Power travels through: road tiles, power lines, and buildings
            var visited = new HashSet<int>();
            var queue   = new Queue<TileData>();

            foreach (var gen in generators)
            {
                int key = gen.Y * map.Width + gen.X;
                if (!visited.Add(key)) continue;
                gen.HasPower = true;
                queue.Enqueue(gen);
            }

            while (queue.Count > 0)
            {
                TileData cur = queue.Dequeue();
                foreach (TileData nb in map.GetNeighbours4(cur.X, cur.Y))
                {
                    int key = nb.Y * map.Width + nb.X;
                    if (!visited.Add(key)) continue;
                    // Transmit through: road, power line, or any built structure
                    if (!nb.IsRoad && !nb.PowerLine && !nb.HasBuilding && nb.Zone == ZoneType.None)
                        continue;
                    nb.HasPower = true;
                    queue.Enqueue(nb);
                }
            }

            // ── Calculate total demand ────────────────────────────────────────
            map.ForEach(t =>
            {
                if (!t.HasPower) return;
                if (t.Zone == ZoneType.None && !t.HasBuilding) return;

                TotalDemand += t.Zone switch
                {
                    ZoneType.Residential => Config.POWER_DEMAND_RES * (int)t.Density,
                    ZoneType.Commercial  => Config.POWER_DEMAND_COM * (int)t.Density,
                    ZoneType.Industrial  => Config.POWER_DEMAND_IND * (int)t.Density,
                    _                    => 0f
                };

                if (t.HasBuilding)
                {
                    var def = BuildingDatabase.Get(t.Building);
                    if (def != null) TotalDemand += def.PowerDemand;
                }
            });

            // ── If shortage, randomly cut power to some tiles ─────────────────
            if (IsInShortage)
            {
                float shortage   = TotalDemand - TotalGeneration;
                float cutPerTile = shortage / Mathf.Max(1, TotalDemand) * 100f;

                map.ForEach(t =>
                {
                    if (!t.HasPower || t.Zone == ZoneType.None) return;
                    if (Random.value * 100f < cutPerTile)
                        t.HasPower = false;
                });
            }
        }
    }
}
