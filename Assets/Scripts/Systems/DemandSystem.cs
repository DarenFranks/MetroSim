// =============================================================================
// DemandSystem.cs  –  Residential, Commercial, Industrial (RCI) demand bars.
//
// Demand drives zone development.  High demand → zones build up fast.
// Demand is influenced by:
//   • Population-to-jobs ratio (residential demand = jobs available)
//   • Tax rates (high taxes suppress demand)
//   • City happiness (unhappy cities lose residents)
//   • Pollution (high pollution suppresses residential demand)
// =============================================================================
using UnityEngine;

namespace MetroSim
{
    public class DemandSystem : MonoBehaviour
    {
        // ── Demand values −1…+1 (negative = abandonment pressure) ────────────
        public float ResidentialDemand { get; private set; } = 0.5f;
        public float CommercialDemand  { get; private set; } = 0.3f;
        public float IndustrialDemand  { get; private set; } = 0.2f;

        // Smoothed display values (avoid jitter in UI bars)
        public float ResDisplay { get; private set; } = 0.5f;
        public float ComDisplay { get; private set; } = 0.3f;
        public float IndDisplay { get; private set; } = 0.2f;

        private const float SMOOTH = 0.08f; // lerp speed

        public void Reset()
        {
            ResidentialDemand = ComDisplay = ResDisplay = 0.5f;
            CommercialDemand  = IndDisplay = ComDisplay = 0.3f;
            IndustrialDemand  = IndDisplay = 0.2f;
        }

        public void Simulate(GridMap map, EconomySystem economy)
        {
            var gm = GameManager.Instance;
            var zm = gm.Zones;

            int pop     = zm.TotalPopulation;
            int comJobs = zm.TotalComJobs;
            int indJobs = zm.TotalIndJobs;
            int hap     = zm.TotalHappiness;

            // ── Residential demand ────────────────────────────────────────────
            // Demand is high when there are more jobs than workers
            float jobsPerWorker = pop > 0 ? (float)(comJobs + indJobs) / pop : 1f;
            float resDemand     = Mathf.Clamp(jobsPerWorker - 0.5f, -0.5f, 1f);

            // Suppress by tax rate (high taxes = leave city)
            resDemand -= (economy.ResidentialTaxRate - Config.DEFAULT_TAX_RATE) * 5f;

            // Suppress by average pollution
            float avgPollution = AveragePollution(map);
            resDemand -= avgPollution / 200f;

            // Happiness bonus/malus
            resDemand += (hap - 50f) / 200f;

            ResidentialDemand = Mathf.Clamp(resDemand, -1f, 1f);

            // ── Commercial demand ─────────────────────────────────────────────
            // Commercial needs consumers (population)
            float popRatio = Mathf.Clamp01(pop / 500f);
            float comDemand = popRatio * 0.8f
                            - (economy.CommercialTaxRate - Config.DEFAULT_TAX_RATE) * 5f;

            // Traffic helps commerce (accessibility)
            comDemand += gm.Traffic.AverageCongestion * 0.2f;
            ComDisplay = comDemand;

            CommercialDemand = Mathf.Clamp(comDemand, -1f, 1f);

            // ── Industrial demand ─────────────────────────────────────────────
            // Industry needs workers and is tax-sensitive
            float workerRatio = (comJobs + indJobs) < pop * 0.5f ? 0.8f : 0.3f;
            float indDemand   = workerRatio
                              - (economy.IndustrialTaxRate - Config.DEFAULT_TAX_RATE) * 4f;

            IndustrialDemand = Mathf.Clamp(indDemand, -1f, 1f);

            // ── Smooth display values ─────────────────────────────────────────
            ResDisplay = Mathf.Lerp(ResDisplay, ResidentialDemand, SMOOTH);
            ComDisplay = Mathf.Lerp(ComDisplay, CommercialDemand,  SMOOTH);
            IndDisplay = Mathf.Lerp(IndDisplay, IndustrialDemand,  SMOOTH);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private float AveragePollution(GridMap map)
        {
            float total = 0f; int count = 0;
            map.ForEach(t =>
            {
                if (t.Zone == ZoneType.Residential)
                {
                    total += t.AirPollution;
                    count++;
                }
            });
            return count > 0 ? total / count : 0f;
        }
    }
}
