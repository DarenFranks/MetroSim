// =============================================================================
// GameManager.cs  –  Central singleton that owns all subsystems and the grid.
// Acts as the "city object" – every other system reads/writes through this.
// =============================================================================
using UnityEngine;
using System;

namespace MetroSim
{
    public class GameManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static GameManager Instance { get; private set; }

        // ── Subsystem references (assigned in Awake / inspector) ──────────────
        [Header("Systems (auto-assigned if null)")]
        public SimulationEngine  SimEngine;
        public ZoneManager       Zones;
        public RoadNetwork       Roads;
        public PowerNetwork      Power;
        public WaterNetwork      Water;
        public SewerNetwork      Sewer;
        public TrafficManager    Traffic;
        public EconomySystem     Economy;
        public DemandSystem      Demand;
        public ServiceManager    Services;
        public PollutionSystem   Pollution;
        public LandValueSystem   LandValue;
        public DisasterManager   Disasters;
        public ThemeManager      Themes;
        public GridRenderer      GridRend;
        public OverlayRenderer   OverlayRend;
        public UIManager         UI;
        public SaveLoadSystem    SaveLoad;
        public CameraController  CamCtrl;

        // ── World state ───────────────────────────────────────────────────────
        public GridMap Grid        { get; set; }
        public int     Seed        { get; private set; }

        // ── City metadata ─────────────────────────────────────────────────────
        public string  CityName    = "New City";
        public int     Year        = 1;
        public int     Month       = 1;   // 1-12
        public int     TickCount   = 0;

        // ── Player state ──────────────────────────────────────────────────────
        /// <summary>Currently selected build tool (matches button data-tool strings).</summary>
        public string  ActiveTool  = "select";
        /// <summary>Currently active overlay type.</summary>
        public OverlayType ActiveOverlay = OverlayType.None;
        /// <summary>Currently active theme name.</summary>
        public string  ActiveTheme = "north_american";

        // ── Input drag / brush state ──────────────────────────────────────────
        public bool    IsDragging   = false;
        public Vector2Int DragStart = Vector2Int.zero;

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<TileData>   OnTileSelected;
        public event Action<string>     OnNotification;
        public event Action             OnSimTick;
        public event Action             OnYearEnd;

        // ── Unity lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Bootstrap();
        }

        /// <summary>
        /// Creates or gathers all subsystem references,
        /// generates a fresh city, and starts the simulation.
        /// </summary>
        private void Bootstrap()
        {
            // Prefer inspector/SceneSetup-assigned references; only GetOrAdd when null.
            // (Bootstrap() runs after Unity restores serialized field values,
            //  so inspector assignments survive into Play mode.)
            if (!SimEngine)   SimEngine   = GetOrAdd<SimulationEngine>();
            if (!Zones)       Zones       = GetOrAdd<ZoneManager>();
            if (!Roads)       Roads       = GetOrAdd<RoadNetwork>();
            if (!Power)       Power       = GetOrAdd<PowerNetwork>();
            if (!Water)       Water       = GetOrAdd<WaterNetwork>();
            if (!Sewer)       Sewer       = GetOrAdd<SewerNetwork>();
            if (!Traffic)     Traffic     = GetOrAdd<TrafficManager>();
            if (!Economy)     Economy     = GetOrAdd<EconomySystem>();
            if (!Demand)      Demand      = GetOrAdd<DemandSystem>();
            if (!Services)    Services    = GetOrAdd<ServiceManager>();
            if (!Pollution)   Pollution   = GetOrAdd<PollutionSystem>();
            if (!LandValue)   LandValue   = GetOrAdd<LandValueSystem>();
            if (!Disasters)   Disasters   = GetOrAdd<DisasterManager>();
            if (!Themes)      Themes      = GetOrAdd<ThemeManager>();
            if (!GridRend)    GridRend    = GetOrAdd<GridRenderer>();
            if (!OverlayRend) OverlayRend = GetOrAdd<OverlayRenderer>();
            if (!UI)          UI          = GetOrAdd<UIManager>();
            if (!SaveLoad)    SaveLoad    = GetOrAdd<SaveLoadSystem>();
            if (!CamCtrl)     CamCtrl     = GetOrAdd<CameraController>();

            NewCity(0);
        }

        private T GetOrAdd<T>() where T : MonoBehaviour
        {
            // 1. Direct children of this root object (fast path for co-located systems)
            T comp = GetComponentInChildren<T>(true);
            // 2. Scene-wide search – catches UIManager on the Canvas,
            //    CameraController on Main Camera, EventSystem, etc.
            if (comp == null)
                comp = UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            // 3. Last resort: create a dedicated host object
            if (comp == null)
                comp = new GameObject(typeof(T).Name).AddComponent<T>();
            return comp;
        }

        // ── New city ──────────────────────────────────────────────────────────

        /// <summary>Generates a brand-new map and resets all city state.</summary>
        public void NewCity(int seed)
        {
            Seed      = seed == 0 ? UnityEngine.Random.Range(1, 99999) : seed;
            CityName  = "New City";
            Year      = 1;
            Month     = 1;
            TickCount = 0;

            Grid = new GridMap(Config.MAP_WIDTH, Config.MAP_HEIGHT);
            TerrainGenerator.Generate(Grid, Seed);

            Economy.Reset();
            Demand.Reset();
            Disasters.Reset();

            GridRend.Rebuild(Grid);
            CamCtrl.CentreOnMap(Grid);
            UI.Refresh();

            Notify("New city started. Build roads and zones to begin!");
            Debug.Log($"[GameManager] New city generated. Seed={Seed}");
        }

        // ── Simulation tick callback ───────────────────────────────────────────

        /// <summary>
        /// Called by SimulationEngine each tick.
        /// Runs all city systems in dependency order.
        /// </summary>
        public void OnTick()
        {
            TickCount++;

            // 1. Infrastructure networks (BFS from sources)
            Roads.UpdateRoadAccess(Grid);
            Power.Simulate(Grid);
            Water.Simulate(Grid);
            Sewer.Simulate(Grid);

            // 2. Environment
            Pollution.Simulate(Grid);
            LandValue.Simulate(Grid);

            // 3. Services (update coverage maps)
            Services.Simulate(Grid);

            // 4. Zone development (grows/shrinks based on conditions)
            Zones.Simulate(Grid, Demand);

            // 5. Economy (taxes, upkeep, balance)
            Economy.Simulate(Grid, this);

            // 6. Demand (RCI demand bars)
            Demand.Simulate(Grid, Economy);

            // 7. Traffic
            Traffic.Simulate(Grid);

            // 8. Disasters (random events)
            Disasters.Simulate(Grid, this);

            // 9. Advance calendar
            AdvanceCalendar();

            // 10. Notify UI
            OnSimTick?.Invoke();
            UI.Refresh();

            // 11. Dirty renderer only if needed
            GridRend.SetDirty();
            OverlayRend.SetDirty();
        }

        // ── Calendar ──────────────────────────────────────────────────────────

        private void AdvanceCalendar()
        {
            if (TickCount % Config.TICKS_PER_MONTH != 0) return;
            Month++;
            if (Month > Config.MONTHS_PER_YEAR)
            {
                Month = 1;
                Year++;
                Economy.YearEnd(this);
                OnYearEnd?.Invoke();
            }
        }

        public string DateString() =>
            $"{MonthName(Month)} {Year}";

        private static string MonthName(int m) => m switch
        {
            1=>"Jan", 2=>"Feb", 3=>"Mar", 4=>"Apr", 5=>"May", 6=>"Jun",
            7=>"Jul", 8=>"Aug", 9=>"Sep", 10=>"Oct", 11=>"Nov", _=>"Dec"
        };

        // ── Tool actions ──────────────────────────────────────────────────────

        /// <summary>
        /// Handles a left-click or drag on tile (x,y) with the active tool.
        /// Returns true if the action cost money (for UI feedback).
        /// </summary>
        public bool ApplyTool(int x, int y)
        {
            TileData tile = Grid.Get(x, y);
            if (tile == null) return false;

            bool changed = ApplyToolInner(tile);
            GridRend?.SetDirty();   // always refresh; zones return false but still modify tiles
            return changed;
        }

        private bool ApplyToolInner(TileData tile)
        {
            switch (ActiveTool)
            {
                case "select":
                    OnTileSelected?.Invoke(tile);
                    UI.ShowTileInfo(tile);
                    return false;

                case "zone_residential":  return TryZone(tile, ZoneType.Residential);
                case "zone_commercial":   return TryZone(tile, ZoneType.Commercial);
                case "zone_industrial":   return TryZone(tile, ZoneType.Industrial);
                case "dezone":            return TryDezone(tile);

                case "road_dirt":     return TryBuildRoad(tile, RoadType.Dirt,    Config.COST_ROAD_DIRT);
                case "road_street":   return TryBuildRoad(tile, RoadType.Street,  Config.COST_ROAD_STREET);
                case "road_avenue":   return TryBuildRoad(tile, RoadType.Avenue,  Config.COST_ROAD_AVENUE);
                case "road_highway":  return TryBuildRoad(tile, RoadType.Highway, Config.COST_ROAD_HIGHWAY);
                case "demolish_road": return TryDemolishRoad(tile);

                case "power_coal":   return TryPlaceBuilding(tile, BuildingType.PowerCoal);
                case "power_gas":    return TryPlaceBuilding(tile, BuildingType.PowerGas);
                case "power_solar":  return TryPlaceBuilding(tile, BuildingType.PowerSolar);
                case "power_wind":   return TryPlaceBuilding(tile, BuildingType.PowerWind);
                case "power_line":   return TryPlacePowerLine(tile);
                case "water_pump":   return TryPlaceBuilding(tile, BuildingType.WaterPump);
                case "water_tower":  return TryPlaceBuilding(tile, BuildingType.WaterTower);
                case "water_pipe":   return TryPlaceWaterPipe(tile);
                case "sewer_pipe":   return TryPlaceSewerPipe(tile);
                case "sewer_plant":  return TryPlaceBuilding(tile, BuildingType.SewerPlant);
                case "police":       return TryPlaceBuilding(tile, BuildingType.Police);
                case "fire":         return TryPlaceBuilding(tile, BuildingType.Fire);
                case "hospital":     return TryPlaceBuilding(tile, BuildingType.Hospital);
                case "school":       return TryPlaceBuilding(tile, BuildingType.School);
                case "park":         return TryPlaceBuilding(tile, BuildingType.Park);
                case "landfill":     return TryPlaceBuilding(tile, BuildingType.Landfill);
                case "bulldoze":     return TryBulldoze(tile);
            }
            return false;
        }

        // ── Placement helpers ────────────────────────────────────────────────

        private bool TryZone(TileData tile, ZoneType zone)
        {
            if (!tile.IsBuildable) { Notify("Cannot zone here."); return false; }
            tile.Zone = zone;
            Grid.MarkDirty(tile.X, tile.Y);
            return false; // zoning is free
        }

        private bool TryDezone(TileData tile)
        {
            if (tile.Zone == ZoneType.None) return false;
            tile.Zone = ZoneType.None;
            tile.Density = DensityLevel.Empty;
            tile.Occupants = 0;
            Grid.MarkDirty(tile.X, tile.Y);
            return false;
        }

        private bool TryBuildRoad(TileData tile, RoadType road, float cost)
        {
            if (tile.Terrain == TerrainType.Water)   { Notify("Cannot build road on water."); return false; }
            if (tile.Terrain == TerrainType.Mountain){ Notify("Cannot build road on mountains."); return false; }
            if (!Economy.CanAfford(cost))             { Notify("Not enough funds!"); return false; }
            if (tile.Road == road) return false;
            Economy.Spend(cost, "Road construction");
            tile.Road = road;
            Grid.MarkDirty(tile.X, tile.Y);
            return true;
        }

        private bool TryDemolishRoad(TileData tile)
        {
            if (tile.Road == RoadType.None) return false;
            if (!Economy.CanAfford(Config.COST_DEMOLISH)) { Notify("Not enough funds!"); return false; }
            Economy.Spend(Config.COST_DEMOLISH, "Road demolition");
            tile.Road = RoadType.None;
            Grid.MarkDirty(tile.X, tile.Y);
            return true;
        }

        private bool TryPlaceBuilding(TileData tile, BuildingType type)
        {
            BuildingDef def = BuildingDatabase.Get(type);
            if (def == null) return false;
            if (!tile.IsBuildable)               { Notify("Cannot build here."); return false; }
            if (tile.HasBuilding)                 { Notify("Tile already occupied."); return false; }
            if (!Economy.CanAfford(def.BuildCost)){ Notify("Not enough funds!"); return false; }

            // Multi-tile buildings: check all required tiles
            if (def.SizeX > 1 || def.SizeY > 1)
            {
                for (int dx = 0; dx < def.SizeX; dx++)
                    for (int dy = 0; dy < def.SizeY; dy++)
                    {
                        TileData t = Grid.Get(tile.X + dx, tile.Y + dy);
                        if (t == null || !t.IsBuildable || t.HasBuilding)
                        { Notify("Not enough space for this building."); return false; }
                    }
            }

            Economy.Spend(def.BuildCost, $"Build {def.Name}");

            // Place on all required tiles
            for (int dx = 0; dx < def.SizeX; dx++)
                for (int dy = 0; dy < def.SizeY; dy++)
                {
                    TileData t = Grid.Get(tile.X + dx, tile.Y + dy);
                    if (t == null) continue;
                    t.Building = type;
                    t.IsBuilt  = true;
                    // Underground infrastructure flags
                    if (type == BuildingType.WaterPump || type == BuildingType.WaterTower)
                        t.WaterPipe = true;
                    if (type == BuildingType.SewerPlant)
                        t.SewerPipe = true;
                    Grid.MarkDirty(t.X, t.Y);
                }
            return true;
        }

        private bool TryPlacePowerLine(TileData tile)
        {
            if (!Economy.CanAfford(Config.COST_POWER_LINE)) { Notify("Not enough funds!"); return false; }
            Economy.Spend(Config.COST_POWER_LINE, "Power line");
            tile.PowerLine = true;
            Grid.MarkDirty(tile.X, tile.Y);
            return true;
        }

        private bool TryPlaceWaterPipe(TileData tile)
        {
            if (!Economy.CanAfford(Config.COST_WATER_PIPE)) { Notify("Not enough funds!"); return false; }
            Economy.Spend(Config.COST_WATER_PIPE, "Water pipe");
            tile.WaterPipe = true;
            Grid.MarkDirty(tile.X, tile.Y);
            return true;
        }

        private bool TryPlaceSewerPipe(TileData tile)
        {
            if (!Economy.CanAfford(Config.COST_SEWER_PIPE)) { Notify("Not enough funds!"); return false; }
            Economy.Spend(Config.COST_SEWER_PIPE, "Sewer pipe");
            tile.SewerPipe = true;
            Grid.MarkDirty(tile.X, tile.Y);
            return true;
        }

        private bool TryBulldoze(TileData tile)
        {
            if (!tile.HasBuilding && tile.Road == RoadType.None && tile.Zone == ZoneType.None)
                return false;
            if (!Economy.CanAfford(Config.COST_DEMOLISH)) { Notify("Not enough funds!"); return false; }
            Economy.Spend(Config.COST_DEMOLISH, "Bulldoze");
            tile.Bulldoze();
            tile.Road = RoadType.None;
            tile.PowerLine = false;
            tile.WaterPipe = false;
            tile.SewerPipe = false;
            Grid.MarkDirty(tile.X, tile.Y);
            return true;
        }

        // ── Notifications ─────────────────────────────────────────────────────

        public void Notify(string message)
        {
            OnNotification?.Invoke(message);
            Debug.Log($"[Notify] {message}");
        }
    }
}
