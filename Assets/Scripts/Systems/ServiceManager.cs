// =============================================================================
// ServiceManager.cs  –  Police, fire, hospital, school, park coverage.
//
// Each tick, coverage maps are rebuilt by BFS from each service building.
// Tiles within coverage radius get their CoveredByXxx flags set, which
// feed into desirability and land value calculations.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace MetroSim
{
    public class ServiceManager : MonoBehaviour
    {
        // ── Coverage stats ────────────────────────────────────────────────────
        public float PoliceCoverage   { get; private set; }
        public float FireCoverage     { get; private set; }
        public float HospitalCoverage { get; private set; }
        public float SchoolCoverage   { get; private set; }

        public void Simulate(GridMap map)
        {
            // ── Clear all coverage flags ───────────────────────────────────────
            map.ForEach(t =>
            {
                t.CoveredByPolice   = false;
                t.CoveredByFire     = false;
                t.CoveredByHospital = false;
                t.CoveredBySchool   = false;
            });

            // ── Apply coverage for each service building ───────────────────────
            map.ForEach(t =>
            {
                if (!t.HasBuilding) return;

                switch (t.Building)
                {
                    case BuildingType.Police:
                        ApplyCoverage(map, t.X, t.Y, Config.COVERAGE_POLICE,
                                      nb => nb.CoveredByPolice = true);
                        break;
                    case BuildingType.Fire:
                        ApplyCoverage(map, t.X, t.Y, Config.COVERAGE_FIRE,
                                      nb => nb.CoveredByFire = true);
                        break;
                    case BuildingType.Hospital:
                        ApplyCoverage(map, t.X, t.Y, Config.COVERAGE_HOSPITAL,
                                      nb => nb.CoveredByHospital = true);
                        break;
                    case BuildingType.School:
                        ApplyCoverage(map, t.X, t.Y, Config.COVERAGE_SCHOOL,
                                      nb => nb.CoveredBySchool = true);
                        break;
                }
            });

            // ── Compute coverage percentages ──────────────────────────────────
            int resTotal = 0, polCov = 0, firCov = 0, hosCov = 0, schCov = 0;
            map.ForEach(t =>
            {
                if (t.Zone != ZoneType.Residential || t.Density == DensityLevel.Empty) return;
                resTotal++;
                if (t.CoveredByPolice)   polCov++;
                if (t.CoveredByFire)     firCov++;
                if (t.CoveredByHospital) hosCov++;
                if (t.CoveredBySchool)   schCov++;
            });

            if (resTotal > 0)
            {
                PoliceCoverage   = (float)polCov / resTotal;
                FireCoverage     = (float)firCov / resTotal;
                HospitalCoverage = (float)hosCov / resTotal;
                SchoolCoverage   = (float)schCov / resTotal;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ApplyCoverage(GridMap map, int cx, int cy, int radius,
                                   System.Action<TileData> setFlag)
        {
            var tiles = map.GetTilesInCircle(cx, cy, radius);
            foreach (var t in tiles) setFlag(t);
        }

        // ── Fire-fighting integration ─────────────────────────────────────────

        /// <summary>
        /// Returns true if a burning tile at (x,y) is within fire station coverage.
        /// Used by DisasterManager to determine extinguish probability.
        /// </summary>
        public bool IsFireCovered(GridMap map, int x, int y) =>
            map.Get(x, y)?.CoveredByFire ?? false;
    }
}
