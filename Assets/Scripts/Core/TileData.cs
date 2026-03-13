// =============================================================================
// TileData.cs  –  All enumerations and the per-tile data structure.
// Each cell in the world grid is represented by one TileData instance.
// =============================================================================
using System;
using UnityEngine;

namespace MetroSim
{
    // ─── Terrain ──────────────────────────────────────────────────────────────
    public enum TerrainType
    {
        Water   = 0,
        Sand    = 1,
        Grass   = 2,
        Forest  = 3,
        Hill    = 4,
        Mountain= 5
    }

    // ─── Zone ─────────────────────────────────────────────────────────────────
    public enum ZoneType
    {
        None        = 0,
        Residential = 1,
        Commercial  = 2,
        Industrial  = 3
    }

    // ─── Density / development stage ─────────────────────────────────────────
    public enum DensityLevel
    {
        Empty       = 0,   // zoned but nothing built yet
        Low         = 1,
        Medium      = 2,
        High        = 3
    }

    // ─── Road types ───────────────────────────────────────────────────────────
    public enum RoadType
    {
        None    = 0,
        Dirt    = 1,
        Street  = 2,
        Avenue  = 3,
        Highway = 4
    }

    // ─── Special building types placed by the player ─────────────────────────
    public enum BuildingType
    {
        None            = 0,
        // Power
        PowerCoal       = 10,
        PowerGas        = 11,
        PowerSolar      = 12,
        PowerWind       = 13,
        PowerLine       = 14,
        // Water
        WaterPump       = 20,
        WaterTower      = 21,
        WaterPipe       = 22,
        // Sewer
        SewerPipe       = 30,
        SewerPlant      = 31,
        // Services
        Police          = 40,
        Fire            = 41,
        Hospital        = 42,
        School          = 43,
        Park            = 44,
        Landfill        = 45,
        // Disaster markers
        Rubble          = 99
    }

    // ─── Overlay types for visualisation ─────────────────────────────────────
    public enum OverlayType
    {
        None      = 0,
        Power     = 1,
        Water     = 2,
        Sewer     = 3,
        Pollution = 4,
        LandValue = 5,
        Traffic   = 6
    }

    // =========================================================================
    // TileData  –  the complete state of a single grid cell
    // This is a class (reference type) so GridMap can update it in place.
    // =========================================================================
    [Serializable]
    public class TileData
    {
        // ── Coordinates ───────────────────────────────────────────────────────
        public int X;
        public int Y;

        // ── Terrain ───────────────────────────────────────────────────────────
        public TerrainType Terrain     = TerrainType.Grass;
        public float       HeightValue = 0f;   // raw 0-1 noise value

        // ── Zone & development ────────────────────────────────────────────────
        public ZoneType    Zone        = ZoneType.None;
        public DensityLevel Density    = DensityLevel.Empty;
        /// <summary>Population (residential) or jobs (commercial/industrial).</summary>
        public int         Occupants   = 0;
        /// <summary>0-100 happiness of occupants.</summary>
        public float       Happiness   = 50f;
        /// <summary>Ticks since last zone-development attempt.</summary>
        public int         DevCooldown = 0;

        // ── Road ──────────────────────────────────────────────────────────────
        public RoadType    Road        = RoadType.None;
        /// <summary>Cached – is this tile reachable from a road?</summary>
        public bool        HasRoadAccess = false;
        /// <summary>Traffic density this tick (0-1).</summary>
        public float       TrafficDensity = 0f;

        // ── Special building ──────────────────────────────────────────────────
        public BuildingType Building   = BuildingType.None;
        /// <summary>True once placed; false if bulldozed or destroyed.</summary>
        public bool         IsBuilt    = false;

        // ── Utility coverage ─────────────────────────────────────────────────
        public bool HasPower    = false;
        public bool HasWater    = false;
        public bool HasSewer    = false;
        /// <summary>Water pipe present on this tile.</summary>
        public bool WaterPipe   = false;
        /// <summary>Sewer pipe present on this tile.</summary>
        public bool SewerPipe   = false;
        /// <summary>Power line present on this tile (separate from road).</summary>
        public bool PowerLine   = false;

        // ── Environment ───────────────────────────────────────────────────────
        /// <summary>Air pollution level 0-100.</summary>
        public float AirPollution   = 0f;
        /// <summary>Water pollution level 0-100.</summary>
        public float WaterPollution = 0f;
        /// <summary>Noise pollution level 0-100.</summary>
        public float NoisePollution = 0f;
        /// <summary>Composite land value 0-200.</summary>
        public float LandValue      = Config.LAND_VALUE_BASE;

        // ── Service coverage flags ────────────────────────────────────────────
        public bool CoveredByPolice   = false;
        public bool CoveredByFire     = false;
        public bool CoveredByHospital = false;
        public bool CoveredBySchool   = false;

        // ── Fire / disaster state ─────────────────────────────────────────────
        public bool IsOnFire      = false;
        public bool IsFlooded     = false;
        public int  FireDuration  = 0;  // ticks remaining if on fire

        // ── Constructor ───────────────────────────────────────────────────────
        public TileData(int x, int y)
        {
            X = x;
            Y = y;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>True if this tile can be built on (not water, not mountain).</summary>
        public bool IsBuildable =>
            Terrain != TerrainType.Water &&
            Terrain != TerrainType.Mountain &&
            !IsFlooded;

        /// <summary>True if this tile is a road tile.</summary>
        public bool IsRoad => Road != RoadType.None;

        /// <summary>True if this tile has a special player-placed building.</summary>
        public bool HasBuilding => Building != BuildingType.None && IsBuilt;

        /// <summary>
        /// Returns the combined pollution value influencing land value.
        /// </summary>
        public float TotalPollution => AirPollution + WaterPollution * 0.5f + NoisePollution * 0.3f;

        /// <summary>
        /// Whether a zone can develop on this tile:
        /// needs road access, power, water, sewer, not flooded/on fire.
        /// </summary>
        public bool CanDevelop(bool requireWater, bool requireSewer) =>
            IsBuildable         &&
            Zone != ZoneType.None &&
            !HasBuilding        &&
            HasRoadAccess       &&
            HasPower            &&
            (!requireWater  || HasWater) &&
            (!requireSewer  || HasSewer) &&
            !IsOnFire           &&
            DevCooldown <= 0;

        /// <summary>Clears all zone and building data (bulldoze).</summary>
        public void Bulldoze()
        {
            Zone      = ZoneType.None;
            Density   = DensityLevel.Empty;
            Occupants = 0;
            Building  = BuildingType.None;
            IsBuilt   = false;
            IsOnFire  = false;
            FireDuration = 0;
        }

        public override string ToString() =>
            $"Tile({X},{Y}) Terrain={Terrain} Zone={Zone} Density={Density} " +
            $"Pop={Occupants} Power={HasPower} Water={HasWater} Sewer={HasSewer}";
    }
}
