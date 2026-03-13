// =============================================================================
// PollutionSystem.cs  –  Air, water, and noise pollution simulation.
//
// Sources:
//   • Power plants (coal/gas) → heavy air pollution
//   • Industrial zones → moderate air + noise
//   • Traffic congestion → air + noise on road tiles
//   • Landfills → air + water + noise
//   • Untreated sewage (handled in SewerNetwork) → water
//
// Each tick pollution spreads slightly to adjacent tiles and decays.
// =============================================================================
using UnityEngine;

namespace MetroSim
{
    public class PollutionSystem : MonoBehaviour
    {
        public float TotalAirPollution   { get; private set; }
        public float TotalWaterPollution { get; private set; }

        public void Simulate(GridMap map)
        {
            // ── Step 1: emit pollution from sources ───────────────────────────
            map.ForEach(t =>
            {
                // Power plant emissions
                if (t.HasBuilding)
                {
                    var def = BuildingDatabase.Get(t.Building);
                    if (def != null)
                    {
                        if (def.AirPollution > 0f)
                            SpreadAirPollution(map, t.X, t.Y, def.AirPollution,
                                               (int)Config.POLLUTION_SPREAD_RADIUS);
                        if (def.NoisePollution > 0f)
                            t.NoisePollution = Mathf.Min(100f, t.NoisePollution + def.NoisePollution * 0.1f);
                        // Water pollution from landfills etc.
                        if (def.WaterPollution > 0f)
                            t.WaterPollution = Mathf.Min(100f, t.WaterPollution + def.WaterPollution * 0.1f);
                    }
                }

                // Industrial zone emissions
                if (t.Zone == ZoneType.Industrial && t.Density > DensityLevel.Empty)
                {
                    float indPol = Config.POLLUTION_IND_TILE * (int)t.Density;
                    SpreadAirPollution(map, t.X, t.Y, indPol, 4);
                    t.NoisePollution = Mathf.Min(100f, t.NoisePollution + 2f * (int)t.Density);
                }

                // Traffic noise on roads
                if (t.IsRoad && t.TrafficDensity > 0.3f)
                {
                    t.NoisePollution = Mathf.Min(100f,
                        t.NoisePollution + t.TrafficDensity * 5f);
                    t.AirPollution   = Mathf.Min(100f,
                        t.AirPollution + t.TrafficDensity * 3f);
                }
            });

            // ── Step 2: decay all pollution each tick ─────────────────────────
            float decayRate = Config.POLLUTION_DECAY_RATE;
            map.ForEach(t =>
            {
                t.AirPollution   = Mathf.Max(0f, t.AirPollution   - decayRate * t.AirPollution);
                t.WaterPollution = Mathf.Max(0f, t.WaterPollution  - decayRate * t.WaterPollution);
                t.NoisePollution = Mathf.Max(0f, t.NoisePollution  - decayRate * t.NoisePollution * 2f);
            });

            // ── Step 3: forests absorb air pollution in their cells ───────────
            map.ForEach(t =>
            {
                if (t.Terrain == TerrainType.Forest)
                    t.AirPollution = Mathf.Max(0f, t.AirPollution - 2f);
            });

            // ── Aggregate stats ───────────────────────────────────────────────
            float totAir = 0f, totWater = 0f; int n = 0;
            map.ForEach(t => { totAir += t.AirPollution; totWater += t.WaterPollution; n++; });
            TotalAirPollution   = n > 0 ? totAir   / n : 0f;
            TotalWaterPollution = n > 0 ? totWater / n : 0f;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Spreads air pollution from a source using a falloff from the centre.
        /// Each tile in the radius receives amount * (1 - dist/radius).
        /// </summary>
        private void SpreadAirPollution(GridMap map, int cx, int cy, float amount, int radius)
        {
            var tiles = map.GetTilesInRadius(cx, cy, radius);
            foreach (var t in tiles)
            {
                int   dist    = Mathf.Abs(t.X - cx) + Mathf.Abs(t.Y - cy);
                float falloff = 1f - (float)dist / (radius + 1);
                t.AirPollution = Mathf.Min(100f, t.AirPollution + amount * falloff * 0.05f);
            }
        }
    }
}
