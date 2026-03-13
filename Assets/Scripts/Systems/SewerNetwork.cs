// =============================================================================
// SewerNetwork.cs  –  Simulates sewage collection and treatment.
//
// Sewage flows from residential/commercial/industrial zones via sewer pipes
// to treatment plants.  Tiles not connected to a treatment plant generate
// water pollution.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace MetroSim
{
    public class SewerNetwork : MonoBehaviour
    {
        // ── Public stats ──────────────────────────────────────────────────────
        public float TotalCapacity   { get; private set; }
        public float TotalLoad       { get; private set; }
        public bool  IsOverloaded    => TotalLoad > TotalCapacity + 0.01f;

        public void Simulate(GridMap map)
        {
            map.ForEach(t => t.HasSewer = false);

            TotalCapacity = 0f;
            TotalLoad     = 0f;

            // ── Find treatment plant tiles ────────────────────────────────────
            var plants = new List<TileData>();
            map.ForEach(t =>
            {
                if (t.HasBuilding && t.Building == BuildingType.SewerPlant)
                {
                    var def = BuildingDatabase.Get(t.Building);
                    if (def != null)
                    {
                        TotalCapacity += def.SewerOutput;
                        plants.Add(t);
                    }
                }
            });

            if (plants.Count == 0)
            {
                // No treatment: all occupied tiles generate water pollution
                map.ForEach(t =>
                {
                    if (t.Density > DensityLevel.Empty)
                        t.WaterPollution = Mathf.Min(100f, t.WaterPollution + 1f);
                });
                return;
            }

            // ── BFS backward from treatment plants along sewer pipes ──────────
            var visited = new HashSet<int>();
            var queue   = new Queue<TileData>();

            foreach (var plant in plants)
            {
                int key = plant.Y * map.Width + plant.X;
                if (!visited.Add(key)) continue;
                plant.HasSewer = true;
                queue.Enqueue(plant);
            }

            while (queue.Count > 0)
            {
                TileData cur = queue.Dequeue();
                foreach (TileData nb in map.GetNeighbours4(cur.X, cur.Y))
                {
                    int key = nb.Y * map.Width + nb.X;
                    if (!visited.Add(key)) continue;
                    // Sewer travels through: sewer pipes or road tiles (pipes under roads)
                    bool hasPipe = nb.SewerPipe || nb.IsRoad;
                    if (!hasPipe) continue;
                    nb.HasSewer = true;
                    queue.Enqueue(nb);
                }
            }

            // ── Calculate load and pollution for unconnected tiles ────────────
            map.ForEach(t =>
            {
                if (t.Density == DensityLevel.Empty) return;

                float sewage = t.Zone switch
                {
                    ZoneType.Residential => 1.5f * (int)t.Density,
                    ZoneType.Commercial  => 2.0f * (int)t.Density,
                    ZoneType.Industrial  => 5.0f * (int)t.Density,
                    _                    => 0f
                };

                if (t.HasSewer)
                {
                    TotalLoad += sewage;
                    // Reduce water pollution near treatment
                    t.WaterPollution = Mathf.Max(0f, t.WaterPollution - 0.5f);
                }
                else
                {
                    // Untreated sewage causes water pollution
                    t.WaterPollution = Mathf.Min(100f, t.WaterPollution + 0.8f);
                }
            });

            // ── Overloaded plant: some sewage bypasses treatment ──────────────
            if (IsOverloaded)
            {
                float spillRatio = 1f - TotalCapacity / Mathf.Max(TotalLoad, 1f);
                map.ForEach(t =>
                {
                    if (!t.HasSewer || t.Density == DensityLevel.Empty) return;
                    if (Random.value < spillRatio * 0.5f)
                        t.WaterPollution = Mathf.Min(100f, t.WaterPollution + 1f);
                });
            }
        }
    }
}
