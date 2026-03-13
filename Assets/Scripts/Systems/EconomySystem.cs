// =============================================================================
// EconomySystem.cs  –  Budget simulation: taxes, upkeep, and cash flow.
//
// Revenue streams:
//   • Property tax on residential occupants
//   • Business tax on commercial jobs
//   • Industrial tax on industrial jobs
//
// Expenses:
//   • Annual upkeep for every placed building (from BuildingDef.AnnualUpkeep)
//   • Road maintenance (proportional to road count and type)
//
// YearEnd() is called once per in-game year to apply the net balance.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace MetroSim
{
    public class EconomySystem : MonoBehaviour
    {
        // ── State ─────────────────────────────────────────────────────────────
        public float Funds           { get; set; } = Config.STARTING_FUNDS;
        public float AnnualIncome    { get; private set; }
        public float AnnualExpenses  { get; private set; }
        public float NetIncome       => AnnualIncome - AnnualExpenses;

        // ── Tax rates (0-1, modified via UI sliders) ──────────────────────────
        public float ResidentialTaxRate = Config.DEFAULT_TAX_RATE;
        public float CommercialTaxRate  = Config.DEFAULT_TAX_RATE;
        public float IndustrialTaxRate  = Config.DEFAULT_TAX_RATE;

        // Per-tile tax value (computed each tick, used for display and demand)
        public float TaxPerResident  { get; private set; }
        public float TaxPerComJob    { get; private set; }
        public float TaxPerIndJob    { get; private set; }

        // Expense tracking
        public float UtilityUpkeep  { get; private set; }
        public float ServiceUpkeep  { get; private set; }
        public float RoadUpkeep     { get; private set; }

        // Ledger of recent transactions (last 20 entries)
        private readonly List<string> _ledger = new List<string>(20);

        // ── Reset ─────────────────────────────────────────────────────────────

        public void Reset()
        {
            Funds    = Config.STARTING_FUNDS;
            _ledger.Clear();
        }

        // ── Spend / earn ──────────────────────────────────────────────────────

        public bool CanAfford(float amount) => Funds >= amount;

        public void Spend(float amount, string reason)
        {
            Funds -= amount;
            AddLedger($"-${amount:F0}  {reason}");
        }

        public void Earn(float amount, string reason)
        {
            Funds += amount;
            AddLedger($"+${amount:F0}  {reason}");
        }

        private void AddLedger(string entry)
        {
            if (_ledger.Count >= 20) _ledger.RemoveAt(0);
            _ledger.Add(entry);
        }

        public IReadOnlyList<string> Ledger => _ledger;

        // ── Simulate (called every tick) ──────────────────────────────────────

        public void Simulate(GridMap map, GameManager gm)
        {
            // Recalculate projected annual income/expense each tick for UI display
            // Actual money transfer happens at YearEnd

            // ── Income projection ─────────────────────────────────────────────
            float resIncome = 0f, comIncome = 0f, indIncome = 0f;

            map.ForEach(t =>
            {
                if (t.Density == DensityLevel.Empty) return;
                switch (t.Zone)
                {
                    case ZoneType.Residential:
                        // Tax per person (land value affects how much people earn)
                        float resBase = 100f + t.LandValue * 0.5f;
                        resIncome += t.Occupants * resBase * ResidentialTaxRate;
                        break;
                    case ZoneType.Commercial:
                        float comBase = 150f + t.LandValue * 0.8f;
                        comIncome += t.Occupants * comBase * CommercialTaxRate;
                        break;
                    case ZoneType.Industrial:
                        float indBase = 120f;
                        indIncome += t.Occupants * indBase * IndustrialTaxRate;
                        break;
                }
            });

            TaxPerResident = ResidentialTaxRate * 100f;
            TaxPerComJob   = CommercialTaxRate  * 150f;
            TaxPerIndJob   = IndustrialTaxRate  * 120f;

            // ── Expense projection ────────────────────────────────────────────
            float utilUpkeep = 0f, svcUpkeep = 0f, roadUpkeep = 0f;

            map.ForEach(t =>
            {
                if (t.HasBuilding)
                {
                    var def = BuildingDatabase.Get(t.Building);
                    if (def == null) return;
                    switch (def.Category)
                    {
                        case "Power":
                        case "Water":
                        case "Sewer":
                            utilUpkeep += def.AnnualUpkeep;
                            break;
                        case "Service":
                            svcUpkeep += def.AnnualUpkeep;
                            break;
                    }
                }

                if (t.IsRoad)
                {
                    roadUpkeep += t.Road switch
                    {
                        RoadType.Dirt    => 2f,
                        RoadType.Street  => 5f,
                        RoadType.Avenue  => 10f,
                        RoadType.Highway => 20f,
                        _                => 0f
                    };
                }
            });

            UtilityUpkeep   = utilUpkeep;
            ServiceUpkeep   = svcUpkeep;
            RoadUpkeep      = roadUpkeep;

            AnnualIncome    = resIncome + comIncome + indIncome;
            AnnualExpenses  = utilUpkeep + svcUpkeep + roadUpkeep;
        }

        // ── Year-end settlement ───────────────────────────────────────────────

        public void YearEnd(GameManager gm)
        {
            // Apply the full year's income and expenses in one transaction
            if (AnnualIncome > 0f)
                Earn(AnnualIncome, $"Year {gm.Year} tax revenue");

            if (AnnualExpenses > 0f)
                Spend(AnnualExpenses, $"Year {gm.Year} operating costs");

            // Bankruptcy warning
            if (Funds < 0f)
                gm.Notify("⚠ City is bankrupt! Raise taxes or reduce services.");
            else if (Funds < 500f)
                gm.Notify("⚠ Funds are critically low!");

            Debug.Log($"[Economy] Year {gm.Year} end. Income={AnnualIncome:F0} " +
                      $"Expenses={AnnualExpenses:F0} Balance={Funds:F0}");
        }
    }
}
