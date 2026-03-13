// =============================================================================
// BuildingDef.cs  –  Definitions for every building/structure in the game.
// BuildingDatabase holds the master list keyed by BuildingType enum.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace MetroSim
{
    /// <summary>
    /// Immutable descriptor for a single building type.
    /// All costs, outputs, and visual hints come from here.
    /// </summary>
    public class BuildingDef
    {
        // ── Identity ──────────────────────────────────────────────────────────
        public BuildingType Type       { get; }
        public string        Name      { get; }
        public string        Category  { get; } // "Power","Water","Service", etc.

        // ── Economics ─────────────────────────────────────────────────────────
        public float BuildCost       { get; }   // one-time placement cost
        public float AnnualUpkeep    { get; }   // subtracted from budget each year

        // ── Footprint ─────────────────────────────────────────────────────────
        public int SizeX { get; }   // tiles wide  (1 for most utilities)
        public int SizeY { get; }   // tiles tall

        // ── Power ─────────────────────────────────────────────────────────────
        public float PowerOutput     { get; }   // MW generated (0 if consumer)
        public float PowerDemand     { get; }   // MW consumed

        // ── Water / sewer ─────────────────────────────────────────────────────
        public float WaterOutput     { get; }   // m³/tick produced
        public float WaterDemand     { get; }
        public float SewerOutput     { get; }   // treatment capacity m³/tick

        // ── Pollution ─────────────────────────────────────────────────────────
        public float AirPollution    { get; }   // emitted per tick at source
        public float WaterPollution  { get; }
        public float NoisePollution  { get; }

        // ── Service coverage ──────────────────────────────────────────────────
        public int   CoverageRadius  { get; }   // tiles, 0 = no coverage
        public float LandValueBonus  { get; }   // applied within coverage radius

        // ── Visual (for renderer) ─────────────────────────────────────────────
        public Color BaseColor       { get; }   // procedural colour when no theme asset

        // ── Constructor ───────────────────────────────────────────────────────
        public BuildingDef(
            BuildingType type, string name, string category,
            float buildCost, float annualUpkeep,
            int sizeX = 1, int sizeY = 1,
            float powerOutput = 0f, float powerDemand = 0f,
            float waterOutput = 0f, float waterDemand = 0f,
            float sewerOutput = 0f,
            float airPollution = 0f, float waterPollution = 0f, float noisePollution = 0f,
            int coverageRadius = 0, float landValueBonus = 0f,
            Color baseColor = default)
        {
            Type            = type;
            Name            = name;
            Category        = category;
            BuildCost       = buildCost;
            AnnualUpkeep    = annualUpkeep;
            SizeX           = sizeX;
            SizeY           = sizeY;
            PowerOutput     = powerOutput;
            PowerDemand     = powerDemand;
            WaterOutput     = waterOutput;
            WaterDemand     = waterDemand;
            SewerOutput     = sewerOutput;
            AirPollution    = airPollution;
            WaterPollution  = waterPollution;
            NoisePollution  = noisePollution;
            CoverageRadius  = coverageRadius;
            LandValueBonus  = landValueBonus;
            BaseColor       = (baseColor == default) ? Color.white : baseColor;
        }
    }

    // =========================================================================
    // BuildingDatabase  –  static registry of all BuildingDef objects
    // =========================================================================
    public static class BuildingDatabase
    {
        private static readonly Dictionary<BuildingType, BuildingDef> _defs
            = new Dictionary<BuildingType, BuildingDef>();

        static BuildingDatabase() { RegisterAll(); }

        // ── Lookup ────────────────────────────────────────────────────────────
        public static BuildingDef Get(BuildingType t) =>
            _defs.TryGetValue(t, out var d) ? d : null;

        // ── Registration ─────────────────────────────────────────────────────
        private static void Add(BuildingDef d) => _defs[d.Type] = d;

        private static void RegisterAll()
        {
            // ── Power plants ─────────────────────────────────────────────────
            Add(new BuildingDef(
                BuildingType.PowerCoal, "Coal Power Plant", "Power",
                buildCost: Config.COST_POWER_COAL,    annualUpkeep: Config.UPKEEP_POWER_COAL,
                sizeX: 2, sizeY: 2,
                powerOutput: Config.POWER_COAL_OUTPUT,
                airPollution: Config.POLLUTION_COAL_PLANT, noisePollution: 30f,
                baseColor: new Color(0.3f, 0.3f, 0.3f)));

            Add(new BuildingDef(
                BuildingType.PowerGas, "Gas Power Plant", "Power",
                buildCost: Config.COST_POWER_GAS,     annualUpkeep: Config.UPKEEP_POWER_GAS,
                sizeX: 2, sizeY: 2,
                powerOutput: Config.POWER_GAS_OUTPUT,
                airPollution: 40f, noisePollution: 20f,
                baseColor: new Color(0.5f, 0.4f, 0.2f)));

            Add(new BuildingDef(
                BuildingType.PowerSolar, "Solar Farm", "Power",
                buildCost: Config.COST_POWER_SOLAR,   annualUpkeep: Config.UPKEEP_POWER_SOLAR,
                sizeX: 3, sizeY: 3,
                powerOutput: Config.POWER_SOLAR_OUTPUT,
                baseColor: new Color(0.1f, 0.3f, 0.8f)));

            Add(new BuildingDef(
                BuildingType.PowerWind, "Wind Farm", "Power",
                buildCost: Config.COST_POWER_WIND,    annualUpkeep: Config.UPKEEP_POWER_WIND,
                sizeX: 1, sizeY: 1,
                powerOutput: Config.POWER_WIND_OUTPUT,
                noisePollution: 10f,
                baseColor: new Color(0.8f, 0.9f, 1.0f)));

            Add(new BuildingDef(
                BuildingType.PowerLine, "Power Line", "Power",
                buildCost: Config.COST_POWER_LINE,    annualUpkeep: 2f,
                baseColor: new Color(1f, 0.9f, 0f)));

            // ── Water infrastructure ──────────────────────────────────────────
            Add(new BuildingDef(
                BuildingType.WaterPump, "Water Pump Station", "Water",
                buildCost: Config.COST_WATER_PUMP,    annualUpkeep: Config.UPKEEP_WATER_PUMP,
                sizeX: 1, sizeY: 1,
                waterOutput: Config.WATER_PUMP_OUTPUT,
                powerDemand: 10f,
                baseColor: new Color(0.2f, 0.5f, 1f)));

            Add(new BuildingDef(
                BuildingType.WaterTower, "Water Tower", "Water",
                buildCost: Config.COST_WATER_TOWER,   annualUpkeep: 50f,
                waterOutput: Config.WATER_TOWER_CAPACITY * 0.1f, // distributes stored water
                baseColor: new Color(0.4f, 0.7f, 1f)));

            Add(new BuildingDef(
                BuildingType.WaterPipe, "Water Pipe", "Water",
                buildCost: Config.COST_WATER_PIPE,    annualUpkeep: 1f,
                baseColor: new Color(0.2f, 0.6f, 1f)));

            // ── Sewer infrastructure ──────────────────────────────────────────
            Add(new BuildingDef(
                BuildingType.SewerPipe, "Sewer Pipe", "Sewer",
                buildCost: Config.COST_SEWER_PIPE,    annualUpkeep: 1f,
                baseColor: new Color(0.5f, 0.35f, 0.1f)));

            Add(new BuildingDef(
                BuildingType.SewerPlant, "Sewage Treatment Plant", "Sewer",
                buildCost: Config.COST_SEWER_PLANT,   annualUpkeep: Config.UPKEEP_SEWER_PLANT,
                sizeX: 2, sizeY: 2,
                sewerOutput: Config.SEWER_TREATMENT_OUTPUT,
                powerDemand: 20f,
                waterPollution: -10f,  // negative = reduces water pollution
                noisePollution: 15f,
                baseColor: new Color(0.6f, 0.5f, 0.3f)));

            // ── Services ──────────────────────────────────────────────────────
            Add(new BuildingDef(
                BuildingType.Police, "Police Station", "Service",
                buildCost: Config.COST_POLICE,        annualUpkeep: Config.UPKEEP_POLICE,
                coverageRadius: Config.COVERAGE_POLICE,
                landValueBonus: Config.LAND_VALUE_POLICE_BONUS,
                powerDemand: 5f,
                baseColor: new Color(0.2f, 0.2f, 0.8f)));

            Add(new BuildingDef(
                BuildingType.Fire, "Fire Station", "Service",
                buildCost: Config.COST_FIRE,          annualUpkeep: Config.UPKEEP_FIRE,
                coverageRadius: Config.COVERAGE_FIRE,
                landValueBonus: 5f,
                powerDemand: 5f,
                baseColor: new Color(0.9f, 0.2f, 0.1f)));

            Add(new BuildingDef(
                BuildingType.Hospital, "Hospital", "Service",
                buildCost: Config.COST_HOSPITAL,      annualUpkeep: Config.UPKEEP_HOSPITAL,
                sizeX: 2, sizeY: 2,
                coverageRadius: Config.COVERAGE_HOSPITAL,
                landValueBonus: 8f,
                powerDemand: 30f, waterDemand: 10f,
                baseColor: new Color(1f, 1f, 1f)));

            Add(new BuildingDef(
                BuildingType.School, "School", "Service",
                buildCost: Config.COST_SCHOOL,        annualUpkeep: Config.UPKEEP_SCHOOL,
                sizeX: 2, sizeY: 1,
                coverageRadius: Config.COVERAGE_SCHOOL,
                landValueBonus: Config.LAND_VALUE_SCHOOL_BONUS,
                powerDemand: 10f,
                baseColor: new Color(1f, 0.85f, 0.1f)));

            Add(new BuildingDef(
                BuildingType.Park, "Park", "Service",
                buildCost: Config.COST_PARK,          annualUpkeep: Config.UPKEEP_PARK,
                coverageRadius: Config.COVERAGE_PARK,
                landValueBonus: Config.LAND_VALUE_PARK_BONUS,
                baseColor: new Color(0.1f, 0.75f, 0.2f)));

            Add(new BuildingDef(
                BuildingType.Landfill, "Landfill", "Service",
                buildCost: Config.COST_LANDFILL,      annualUpkeep: Config.UPKEEP_LANDFILL,
                sizeX: 2, sizeY: 2,
                coverageRadius: Config.COVERAGE_LANDFILL,
                airPollution: 25f, waterPollution: 15f, noisePollution: 10f,
                landValueBonus: -15f,
                baseColor: new Color(0.55f, 0.45f, 0.3f)));
        }
    }
}
