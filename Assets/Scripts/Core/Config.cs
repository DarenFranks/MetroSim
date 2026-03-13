// =============================================================================
// Config.cs  –  Global constants and tuning parameters for MetroSim
// All numeric "magic numbers" live here so they are easy to tweak.
// =============================================================================
using UnityEngine;

namespace MetroSim
{
    public static class Config
    {
        // ── World ─────────────────────────────────────────────────────────────
        public const int   MAP_WIDTH       = 128;   // tiles wide
        public const int   MAP_HEIGHT      = 128;   // tiles tall
        public const float TILE_SIZE       = 1f;    // world-units per tile
        public const int   CHUNK_SIZE      = 16;    // tiles per render chunk

        // ── Simulation ────────────────────────────────────────────────────────
        public const float TICK_INTERVAL_1X = 2.0f; // seconds between sim ticks at 1× speed
        public const float TICK_INTERVAL_2X = 0.75f;
        public const float TICK_INTERVAL_3X = 0.2f;
        public const int   TICKS_PER_MONTH  = 12;   // sim ticks that equal one in-game month
        public const int   MONTHS_PER_YEAR  = 12;

        // ── Starting values ───────────────────────────────────────────────────
        public const float STARTING_FUNDS  = 10000f;
        public const float DEFAULT_TAX_RATE = 0.08f;  // 8 %

        // ── Terrain thresholds (Perlin noise 0–1) ─────────────────────────────
        public const float TERRAIN_WATER_MAX  = 0.28f;
        public const float TERRAIN_SAND_MAX   = 0.33f;
        public const float TERRAIN_GRASS_MAX  = 0.58f;
        public const float TERRAIN_FOREST_MAX = 0.72f;
        public const float TERRAIN_HILL_MAX   = 0.86f;
        // above HILL_MAX → mountain

        // ── Road build costs ──────────────────────────────────────────────────
        public const float COST_ROAD_DIRT    = 10f;
        public const float COST_ROAD_STREET  = 50f;
        public const float COST_ROAD_AVENUE  = 100f;
        public const float COST_ROAD_HIGHWAY = 500f;
        public const float COST_DEMOLISH     = 10f;

        // ── Utility build costs ───────────────────────────────────────────────
        public const float COST_POWER_COAL        = 3000f;
        public const float COST_POWER_GAS         = 5000f;
        public const float COST_POWER_SOLAR       = 8000f;
        public const float COST_POWER_WIND        = 4000f;
        public const float COST_POWER_LINE        = 25f;
        public const float COST_WATER_PUMP        = 1000f;
        public const float COST_WATER_TOWER       = 500f;
        public const float COST_WATER_PIPE        = 20f;
        public const float COST_SEWER_PIPE        = 20f;
        public const float COST_SEWER_PLANT       = 4000f;

        // ── Service build costs ───────────────────────────────────────────────
        public const float COST_POLICE   = 500f;
        public const float COST_FIRE     = 500f;
        public const float COST_HOSPITAL = 2000f;
        public const float COST_SCHOOL   = 1000f;
        public const float COST_PARK     = 200f;
        public const float COST_LANDFILL = 300f;

        // ── Annual operating costs (per building, per year) ───────────────────
        public const float UPKEEP_POWER_COAL   = 800f;
        public const float UPKEEP_POWER_GAS    = 1200f;
        public const float UPKEEP_POWER_SOLAR  = 200f;
        public const float UPKEEP_POWER_WIND   = 150f;
        public const float UPKEEP_WATER_PUMP   = 300f;
        public const float UPKEEP_SEWER_PLANT  = 400f;
        public const float UPKEEP_POLICE       = 300f;
        public const float UPKEEP_FIRE         = 250f;
        public const float UPKEEP_HOSPITAL     = 600f;
        public const float UPKEEP_SCHOOL       = 400f;
        public const float UPKEEP_PARK         = 50f;
        public const float UPKEEP_LANDFILL     = 100f;

        // ── Power generation (MW) ─────────────────────────────────────────────
        public const float POWER_COAL_OUTPUT  = 2000f;  // MW
        public const float POWER_GAS_OUTPUT   = 1500f;
        public const float POWER_SOLAR_OUTPUT = 500f;
        public const float POWER_WIND_OUTPUT  = 400f;
        // demand per occupied tile (MW)
        public const float POWER_DEMAND_RES   = 2f;
        public const float POWER_DEMAND_COM   = 5f;
        public const float POWER_DEMAND_IND   = 15f;

        // ── Water / sewer (cubic metres / tick) ───────────────────────────────
        public const float WATER_PUMP_OUTPUT       = 500f;
        public const float WATER_TOWER_CAPACITY    = 1000f;
        public const float WATER_DEMAND_RES        = 2f;
        public const float WATER_DEMAND_COM        = 3f;
        public const float WATER_DEMAND_IND        = 8f;
        public const float SEWER_TREATMENT_OUTPUT  = 600f;

        // ── Pollution ─────────────────────────────────────────────────────────
        public const float POLLUTION_COAL_PLANT    = 80f;  // air pollution radius
        public const float POLLUTION_IND_TILE      = 20f;
        public const float POLLUTION_DECAY_RATE    = 0.05f; // per tick spread decay
        public const float POLLUTION_SPREAD_RADIUS = 5f;    // tiles

        // ── Land value ────────────────────────────────────────────────────────
        public const float LAND_VALUE_BASE        = 50f;
        public const float LAND_VALUE_PARK_BONUS  = 20f;
        public const float LAND_VALUE_POLICE_BONUS = 10f;
        public const float LAND_VALUE_SCHOOL_BONUS = 15f;
        public const float LAND_VALUE_POLLUTION_PENALTY = -30f;

        // ── Service coverage radii (tiles) ────────────────────────────────────
        public const int COVERAGE_POLICE   = 12;
        public const int COVERAGE_FIRE     = 10;
        public const int COVERAGE_HOSPITAL = 15;
        public const int COVERAGE_SCHOOL   = 12;
        public const int COVERAGE_PARK     = 5;
        public const int COVERAGE_LANDFILL = 8;

        // ── Zone development (population per density level) ───────────────────
        public const int POP_RES_LOW    = 10;
        public const int POP_RES_MED    = 30;
        public const int POP_RES_HIGH   = 80;
        public const int JOBS_COM_LOW   = 5;
        public const int JOBS_COM_MED   = 20;
        public const int JOBS_COM_HIGH  = 60;
        public const int JOBS_IND_LOW   = 8;
        public const int JOBS_IND_MED   = 25;
        public const int JOBS_IND_HIGH  = 70;

        // ── Traffic ───────────────────────────────────────────────────────────
        public const int MAX_VEHICLES        = 200;  // max simultaneous car objects
        public const float VEHICLE_SPEED     = 3f;   // tiles/second
        public const float CONGESTION_DECAY  = 0.02f;// per tick congestion reduction

        // ── Camera ────────────────────────────────────────────────────────────
        public const float CAM_ZOOM_MIN  = 2f;
        public const float CAM_ZOOM_MAX  = 60f;
        public const float CAM_PAN_SPEED = 15f;
        public const float CAM_ZOOM_SPEED = 8f;

        // ── Disasters ─────────────────────────────────────────────────────────
        public const float DISASTER_FIRE_SPREAD_CHANCE  = 0.35f;
        public const float DISASTER_FLOOD_RISE_RATE     = 0.1f;
        public const int   DISASTER_MIN_INTERVAL_TICKS  = 60;  // ~2 in-game months min gap
    }
}
