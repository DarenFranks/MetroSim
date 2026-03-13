// =============================================================================
// ZoneManager.cs  –  Automatic zone development and population simulation.
//
// Each tick, every zoned tile evaluates whether it should:
//   • Develop (increase density)
//   • Stay the same
//   • Abandon (decrease density / lose population)
//
// Development is gated by: demand, utilities, road access, land value,
// pollution, and service coverage.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace MetroSim
{
    public class ZoneManager : MonoBehaviour
    {
        // ── Cached stats ──────────────────────────────────────────────────────
        public int TotalPopulation   { get; private set; }
        public int TotalResJobs      { get; private set; }  // residential occupants
        public int TotalComJobs      { get; private set; }  // commercial jobs
        public int TotalIndJobs      { get; private set; }  // industrial jobs
        public int TotalHappiness    { get; private set; }  // average 0-100

        // Dev cooldown in ticks between attempts (avoids instant cities)
        private const int DEV_COOLDOWN = 4;

        // ── Main simulate ─────────────────────────────────────────────────────

        public void Simulate(GridMap map, DemandSystem demand)
        {
            int pop = 0, comJobs = 0, indJobs = 0, happiness = 0, happCount = 0;

            map.ForEach(tile =>
            {
                if (tile.Zone == ZoneType.None) return;

                // Decrement cooldown
                if (tile.DevCooldown > 0) { tile.DevCooldown--; return; }

                bool utilOk = tile.HasPower && tile.HasWater && tile.HasSewer;

                switch (tile.Zone)
                {
                    case ZoneType.Residential:
                        EvolveResidential(tile, demand, utilOk);
                        pop += tile.Occupants;
                        break;
                    case ZoneType.Commercial:
                        EvolveCommercial(tile, demand, utilOk);
                        comJobs += tile.Occupants;
                        break;
                    case ZoneType.Industrial:
                        EvolveIndustrial(tile, demand, utilOk);
                        indJobs += tile.Occupants;
                        break;
                }

                if (tile.Density != DensityLevel.Empty)
                {
                    happiness  += (int)tile.Happiness;
                    happCount++;
                }
            });

            TotalPopulation = pop;
            TotalComJobs    = comJobs;
            TotalIndJobs    = indJobs;
            TotalResJobs    = pop;
            TotalHappiness  = happCount > 0 ? happiness / happCount : 50;
        }

        // ── Residential evolution ─────────────────────────────────────────────

        private void EvolveResidential(TileData tile, DemandSystem demand, bool utilOk)
        {
            // Calculate desirability (0-100)
            float desirability = CalcDesirability(tile, demand.ResidentialDemand, utilOk);

            float abandonThreshold = 25f;
            float devThreshold     = 60f;

            if (desirability < abandonThreshold && tile.Density > DensityLevel.Empty)
            {
                // Abandon: step down density
                StepDown(tile, ZoneType.Residential);
                tile.DevCooldown = DEV_COOLDOWN * 2;
            }
            else if (desirability > devThreshold && demand.ResidentialDemand > 0.1f)
            {
                // Develop: step up density
                StepUp(tile, ZoneType.Residential);
                tile.DevCooldown = DEV_COOLDOWN;
            }

            // Update happiness based on desirability
            tile.Happiness = Mathf.Lerp(tile.Happiness, desirability, 0.1f);
        }

        // ── Commercial evolution ──────────────────────────────────────────────

        private void EvolveCommercial(TileData tile, DemandSystem demand, bool utilOk)
        {
            float desirability = CalcDesirability(tile, demand.CommercialDemand, utilOk);

            // Commercial needs population nearby (customers)
            int nearbyPop = CountNearbyPopulation(GameManager.Instance.Grid, tile.X, tile.Y, 10);
            float popFactor = Mathf.Clamp01(nearbyPop / 200f);
            desirability *= (0.4f + 0.6f * popFactor);

            if (desirability < 20f && tile.Density > DensityLevel.Empty)
            {
                StepDown(tile, ZoneType.Commercial);
                tile.DevCooldown = DEV_COOLDOWN * 2;
            }
            else if (desirability > 55f && demand.CommercialDemand > 0.05f)
            {
                StepUp(tile, ZoneType.Commercial);
                tile.DevCooldown = DEV_COOLDOWN;
            }

            tile.Happiness = Mathf.Lerp(tile.Happiness, desirability, 0.1f);
        }

        // ── Industrial evolution ──────────────────────────────────────────────

        private void EvolveIndustrial(TileData tile, DemandSystem demand, bool utilOk)
        {
            // Industry cares less about land value; more about road/power
            float baseDesire = 50f;
            if (!tile.HasRoadAccess) baseDesire -= 40f;
            if (!utilOk)            baseDesire -= 30f;
            if (!tile.HasPower)     baseDesire -= 20f;

            // Industry adds pollution, which hurts nearby residential but industry itself
            // isn't affected by air pollution much
            float desirability = Mathf.Clamp01(baseDesire / 100f) * 100f;
            desirability *= Mathf.Clamp01(demand.IndustrialDemand + 0.1f);

            if (desirability < 15f && tile.Density > DensityLevel.Empty)
            {
                StepDown(tile, ZoneType.Industrial);
                tile.DevCooldown = DEV_COOLDOWN * 2;
            }
            else if (desirability > 40f && demand.IndustrialDemand > 0f)
            {
                StepUp(tile, ZoneType.Industrial);
                tile.DevCooldown = DEV_COOLDOWN;
            }

            tile.Happiness = Mathf.Lerp(tile.Happiness, desirability, 0.08f);
        }

        // ── Desirability calculation ──────────────────────────────────────────

        /// <summary>
        /// Computes a 0-100 desirability score combining multiple factors.
        /// </summary>
        private float CalcDesirability(TileData tile, float baseDemand, bool utilOk)
        {
            float score = 50f;

            // Road access is required
            if (!tile.HasRoadAccess) score -= 35f;

            // Utilities
            if (!tile.HasPower)  score -= 20f;
            if (!utilOk)         score -= 10f;

            // Land value (normalised to 0-1 range over 0-200)
            score += (tile.LandValue / 200f) * 20f;

            // Pollution penalty
            float pollPenalty = Mathf.Clamp01(tile.TotalPollution / 100f) * 30f;
            score -= pollPenalty;

            // Service coverage bonuses
            if (tile.CoveredByPolice)   score += 5f;
            if (tile.CoveredByFire)     score += 5f;
            if (tile.CoveredByHospital) score += 8f;
            if (tile.CoveredBySchool)   score += 7f;

            // Traffic (heavy traffic hurts residential, helps commercial)
            if (tile.Zone == ZoneType.Residential)
                score -= tile.TrafficDensity * 15f;
            else
                score += tile.TrafficDensity * 5f;

            // Global demand multiplier
            score *= Mathf.Clamp(baseDemand + 0.5f, 0.1f, 2f);

            return Mathf.Clamp(score, 0f, 100f);
        }

        // ── Density helpers ───────────────────────────────────────────────────

        private void StepUp(TileData tile, ZoneType zone)
        {
            DensityLevel next = tile.Density switch
            {
                DensityLevel.Empty  => DensityLevel.Low,
                DensityLevel.Low    => DensityLevel.Medium,
                DensityLevel.Medium => DensityLevel.High,
                _                   => tile.Density
            };
            if (next == tile.Density) return;

            tile.Density   = next;
            tile.Occupants = OccupantsForDensity(zone, next);
            GameManager.Instance.Grid.MarkDirty(tile.X, tile.Y);
        }

        private void StepDown(TileData tile, ZoneType zone)
        {
            DensityLevel prev = tile.Density switch
            {
                DensityLevel.High   => DensityLevel.Medium,
                DensityLevel.Medium => DensityLevel.Low,
                DensityLevel.Low    => DensityLevel.Empty,
                _                   => tile.Density
            };
            if (prev == tile.Density) return;

            tile.Density   = prev;
            tile.Occupants = OccupantsForDensity(zone, prev);
            GameManager.Instance.Grid.MarkDirty(tile.X, tile.Y);
        }

        private int OccupantsForDensity(ZoneType zone, DensityLevel d)
        {
            return zone switch
            {
                ZoneType.Residential => d switch
                {
                    DensityLevel.Low    => Config.POP_RES_LOW,
                    DensityLevel.Medium => Config.POP_RES_MED,
                    DensityLevel.High   => Config.POP_RES_HIGH,
                    _                   => 0
                },
                ZoneType.Commercial => d switch
                {
                    DensityLevel.Low    => Config.JOBS_COM_LOW,
                    DensityLevel.Medium => Config.JOBS_COM_MED,
                    DensityLevel.High   => Config.JOBS_COM_HIGH,
                    _                   => 0
                },
                ZoneType.Industrial => d switch
                {
                    DensityLevel.Low    => Config.JOBS_IND_LOW,
                    DensityLevel.Medium => Config.JOBS_IND_MED,
                    DensityLevel.High   => Config.JOBS_IND_HIGH,
                    _                   => 0
                },
                _ => 0
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private int CountNearbyPopulation(GridMap map, int cx, int cy, int radius)
        {
            int count = 0;
            var tiles = map.GetTilesInRadius(cx, cy, radius);
            foreach (var t in tiles)
                if (t.Zone == ZoneType.Residential)
                    count += t.Occupants;
            return count;
        }
    }
}
