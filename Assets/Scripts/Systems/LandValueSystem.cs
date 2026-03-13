// =============================================================================
// LandValueSystem.cs  –  Computes per-tile land value each tick.
//
// Land value = base terrain value
//            + service coverage bonuses
//            + park proximity bonus
//            − pollution penalty
//            − traffic noise penalty
//            + density bonus (established neighbourhoods are valuable)
// =============================================================================
using UnityEngine;

namespace MetroSim
{
    public class LandValueSystem : MonoBehaviour
    {
        public float CityAverageLandValue { get; private set; }

        public void Simulate(GridMap map)
        {
            float total = 0f; int count = 0;

            map.ForEach(t =>
            {
                // Non-buildable tiles have 0 land value
                if (!t.IsBuildable) { t.LandValue = 0f; return; }

                // Start from terrain base
                float lv = t.HeightValue > Config.TERRAIN_WATER_MAX
                    ? Mathf.Lerp(20f, 80f, (t.HeightValue - Config.TERRAIN_WATER_MAX)
                                           / (Config.TERRAIN_GRASS_MAX - Config.TERRAIN_WATER_MAX))
                    : 10f;

                // Service coverage bonuses
                if (t.CoveredByPolice)   lv += Config.LAND_VALUE_POLICE_BONUS;
                if (t.CoveredByFire)     lv += 5f;
                if (t.CoveredByHospital) lv += 8f;
                if (t.CoveredBySchool)   lv += Config.LAND_VALUE_SCHOOL_BONUS;

                // Park bonus via proximity (handled by PollutionSystem marking tiles)
                // A simple check: if a park is nearby
                lv += ParkProximityBonus(map, t.X, t.Y);

                // Pollution penalty
                lv += Config.LAND_VALUE_POLLUTION_PENALTY * (t.TotalPollution / 100f);

                // Traffic noise penalty (residential cares most)
                if (t.Zone == ZoneType.Residential)
                    lv -= t.NoisePollution * 0.2f;

                // Density bonus – established neighbourhoods self-reinforce
                lv += t.Density switch
                {
                    DensityLevel.Low    => 5f,
                    DensityLevel.Medium => 12f,
                    DensityLevel.High   => 25f,
                    _                   => 0f
                };

                // Clamp to reasonable range
                t.LandValue = Mathf.Clamp(lv, 0f, 200f);

                total += t.LandValue;
                count++;
            });

            CityAverageLandValue = count > 0 ? total / count : 0f;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private float ParkProximityBonus(GridMap map, int x, int y)
        {
            float bonus = 0f;
            int radius = Config.COVERAGE_PARK;
            var tiles = map.GetTilesInRadius(x, y, radius);
            foreach (var t in tiles)
            {
                if (!t.HasBuilding || t.Building != BuildingType.Park) continue;
                int dist = Mathf.Abs(t.X - x) + Mathf.Abs(t.Y - y);
                bonus += Config.LAND_VALUE_PARK_BONUS * (1f - (float)dist / (radius + 1));
            }
            return Mathf.Min(bonus, 30f);  // cap multiple park bonus
        }
    }
}
