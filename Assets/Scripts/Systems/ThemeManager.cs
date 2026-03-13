// =============================================================================
// ThemeManager.cs  –  Cultural architecture theme system.
//
// Each theme provides a colour palette and name overrides for building types.
// The renderer queries ThemeManager to get the correct colour / label for
// each tile rather than using raw BuildingDef.BaseColor.
//
// New themes can be added by registering a ThemeData object.
// Future extension: swap in sprite sheets / mesh prefabs per theme.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace MetroSim
{
    // ── Per-building-type visual override ────────────────────────────────────
    public class BuildingThemeStyle
    {
        public Color  PrimaryColor   { get; set; }
        public Color  SecondaryColor { get; set; }
        public string LocalName      { get; set; }  // cultural name, e.g. "Maison" instead of "House"

        public BuildingThemeStyle(Color primary, Color secondary, string localName)
        { PrimaryColor=primary; SecondaryColor=secondary; LocalName=localName; }
    }

    // ── Theme data ────────────────────────────────────────────────────────────
    public class ThemeData
    {
        public string Name                                   { get; }
        public Color  TerrainWaterColor                     { get; set; }
        public Color  TerrainGrassColor                     { get; set; }
        public Color  TerrainForestColor                    { get; set; }
        public Color  TerrainSandColor                      { get; set; }
        public Color  TerrainHillColor                      { get; set; }
        public Color  TerrainMountainColor                  { get; set; }
        public Color  RoadColorDirt                         { get; set; }
        public Color  RoadColorStreet                       { get; set; }
        public Color  RoadColorAvenue                       { get; set; }
        public Color  RoadColorHighway                      { get; set; }
        public Color  ZoneResColor                          { get; set; }
        public Color  ZoneComColor                          { get; set; }
        public Color  ZoneIndColor                          { get; set; }
        // Per-building overrides (optional – fallback to BuildingDef.BaseColor)
        public Dictionary<BuildingType, BuildingThemeStyle> BuildingStyles { get; }
            = new Dictionary<BuildingType, BuildingThemeStyle>();

        public ThemeData(string name) { Name = name; }

        public Color GetBuildingColor(BuildingType bt)
        {
            if (BuildingStyles.TryGetValue(bt, out var s)) return s.PrimaryColor;
            return BuildingDatabase.Get(bt)?.BaseColor ?? Color.white;
        }

        public string GetBuildingName(BuildingType bt)
        {
            if (BuildingStyles.TryGetValue(bt, out var s) && !string.IsNullOrEmpty(s.LocalName))
                return s.LocalName;
            return BuildingDatabase.Get(bt)?.Name ?? bt.ToString();
        }
    }

    // ── Manager ───────────────────────────────────────────────────────────────
    public class ThemeManager : MonoBehaviour
    {
        private readonly Dictionary<string, ThemeData> _themes
            = new Dictionary<string, ThemeData>();

        public ThemeData ActiveTheme   { get; private set; }
        public string   ActiveThemeKey { get; private set; } = "north_american";

        private void Awake() { RegisterAllThemes(); SetTheme("north_american"); }

        public void SetTheme(string key)
        {
            if (_themes.TryGetValue(key, out var t)) { ActiveTheme = t; ActiveThemeKey = key; }
            else Debug.LogWarning($"[ThemeManager] Unknown theme: {key}");
            GameManager.Instance?.GridRend?.SetDirty();
        }

        public IEnumerable<string> ThemeKeys => _themes.Keys;

        // ── Theme registration ────────────────────────────────────────────────

        private void RegisterAllThemes()
        {
            _themes["north_american"] = NorthAmerican();
            _themes["european"]       = European();
            _themes["east_asian"]     = EastAsian();
            _themes["middle_eastern"] = MiddleEastern();
            _themes["futuristic"]     = Futuristic();
        }

        // ─────────────────────────────────────────────────────────────────────
        // NORTH AMERICAN  –  Red brick, asphalt grey, suburban greens
        // ─────────────────────────────────────────────────────────────────────
        private ThemeData NorthAmerican()
        {
            var t = new ThemeData("North American")
            {
                TerrainWaterColor    = new Color(0.25f,0.55f,0.90f),
                TerrainGrassColor    = new Color(0.40f,0.72f,0.30f),
                TerrainForestColor   = new Color(0.18f,0.52f,0.18f),
                TerrainSandColor     = new Color(0.90f,0.84f,0.62f),
                TerrainHillColor     = new Color(0.60f,0.55f,0.40f),
                TerrainMountainColor = new Color(0.70f,0.68f,0.65f),
                RoadColorDirt        = new Color(0.65f,0.50f,0.30f),
                RoadColorStreet      = new Color(0.40f,0.40f,0.40f),
                RoadColorAvenue      = new Color(0.30f,0.30f,0.30f),
                RoadColorHighway     = new Color(0.20f,0.20f,0.22f),
                ZoneResColor         = new Color(0.55f,0.85f,0.45f),
                ZoneComColor         = new Color(0.40f,0.65f,0.95f),
                ZoneIndColor         = new Color(0.90f,0.78f,0.35f),
            };
            t.BuildingStyles[BuildingType.PowerCoal]  = new BuildingThemeStyle(new Color(0.30f,0.28f,0.25f), Color.grey, "Coal Plant");
            t.BuildingStyles[BuildingType.Police]      = new BuildingThemeStyle(new Color(0.18f,0.25f,0.80f), Color.white, "Police Dept.");
            t.BuildingStyles[BuildingType.Fire]        = new BuildingThemeStyle(new Color(0.88f,0.18f,0.10f), Color.white, "Fire Dept.");
            t.BuildingStyles[BuildingType.School]      = new BuildingThemeStyle(new Color(0.95f,0.82f,0.10f), Color.white, "Elementary School");
            t.BuildingStyles[BuildingType.Hospital]    = new BuildingThemeStyle(new Color(0.95f,0.95f,0.95f), Color.red,   "Hospital");
            return t;
        }

        // ─────────────────────────────────────────────────────────────────────
        // EUROPEAN  –  Stone grey, terracotta roofs, cobblestone
        // ─────────────────────────────────────────────────────────────────────
        private ThemeData European()
        {
            var t = new ThemeData("European")
            {
                TerrainWaterColor    = new Color(0.28f,0.52f,0.78f),
                TerrainGrassColor    = new Color(0.35f,0.65f,0.25f),
                TerrainForestColor   = new Color(0.15f,0.45f,0.15f),
                TerrainSandColor     = new Color(0.85f,0.78f,0.55f),
                TerrainHillColor     = new Color(0.55f,0.52f,0.40f),
                TerrainMountainColor = new Color(0.68f,0.65f,0.60f),
                RoadColorDirt        = new Color(0.60f,0.50f,0.35f),
                RoadColorStreet      = new Color(0.55f,0.48f,0.42f),  // cobblestone grey-brown
                RoadColorAvenue      = new Color(0.42f,0.38f,0.35f),
                RoadColorHighway     = new Color(0.30f,0.28f,0.25f),
                ZoneResColor         = new Color(0.90f,0.65f,0.45f),  // terracotta
                ZoneComColor         = new Color(0.60f,0.72f,0.90f),
                ZoneIndColor         = new Color(0.75f,0.68f,0.50f),
            };
            t.BuildingStyles[BuildingType.Police]   = new BuildingThemeStyle(new Color(0.20f,0.20f,0.60f), Color.white, "Gendarmerie");
            t.BuildingStyles[BuildingType.Fire]     = new BuildingThemeStyle(new Color(0.80f,0.15f,0.10f), Color.white, "Pompiers");
            t.BuildingStyles[BuildingType.School]   = new BuildingThemeStyle(new Color(0.80f,0.72f,0.15f), Color.white, "École");
            t.BuildingStyles[BuildingType.Hospital] = new BuildingThemeStyle(new Color(0.92f,0.92f,0.92f), Color.red,   "Hôpital");
            t.BuildingStyles[BuildingType.Park]     = new BuildingThemeStyle(new Color(0.20f,0.68f,0.25f), Color.white, "Jardin Public");
            return t;
        }

        // ─────────────────────────────────────────────────────────────────────
        // EAST ASIAN  –  Warm reds, pagoda roofs, lush greens
        // ─────────────────────────────────────────────────────────────────────
        private ThemeData EastAsian()
        {
            var t = new ThemeData("East Asian")
            {
                TerrainWaterColor    = new Color(0.20f,0.58f,0.85f),
                TerrainGrassColor    = new Color(0.45f,0.75f,0.32f),
                TerrainForestColor   = new Color(0.12f,0.48f,0.18f),
                TerrainSandColor     = new Color(0.88f,0.82f,0.60f),
                TerrainHillColor     = new Color(0.52f,0.58f,0.40f),
                TerrainMountainColor = new Color(0.65f,0.62f,0.58f),
                RoadColorDirt        = new Color(0.62f,0.52f,0.32f),
                RoadColorStreet      = new Color(0.38f,0.38f,0.38f),
                RoadColorAvenue      = new Color(0.28f,0.28f,0.28f),
                RoadColorHighway     = new Color(0.18f,0.18f,0.20f),
                ZoneResColor         = new Color(0.90f,0.42f,0.32f),  // deep red
                ZoneComColor         = new Color(0.95f,0.80f,0.25f),  // golden
                ZoneIndColor         = new Color(0.65f,0.65f,0.65f),
            };
            t.BuildingStyles[BuildingType.Police]   = new BuildingThemeStyle(new Color(0.10f,0.20f,0.70f), Color.white, "公安局");
            t.BuildingStyles[BuildingType.Fire]     = new BuildingThemeStyle(new Color(0.85f,0.15f,0.10f), Color.white, "消防站");
            t.BuildingStyles[BuildingType.School]   = new BuildingThemeStyle(new Color(0.90f,0.75f,0.08f), Color.white, "学校");
            t.BuildingStyles[BuildingType.Hospital] = new BuildingThemeStyle(new Color(0.90f,0.90f,0.90f), Color.red,   "医院");
            t.BuildingStyles[BuildingType.Park]     = new BuildingThemeStyle(new Color(0.15f,0.65f,0.22f), Color.white, "公园");
            return t;
        }

        // ─────────────────────────────────────────────────────────────────────
        // MIDDLE EASTERN  –  Sand tones, domed roofs, warm whites
        // ─────────────────────────────────────────────────────────────────────
        private ThemeData MiddleEastern()
        {
            var t = new ThemeData("Middle Eastern")
            {
                TerrainWaterColor    = new Color(0.22f,0.55f,0.85f),
                TerrainGrassColor    = new Color(0.55f,0.72f,0.28f),
                TerrainForestColor   = new Color(0.20f,0.50f,0.15f),
                TerrainSandColor     = new Color(0.92f,0.86f,0.62f),
                TerrainHillColor     = new Color(0.80f,0.72f,0.48f),
                TerrainMountainColor = new Color(0.75f,0.68f,0.55f),
                RoadColorDirt        = new Color(0.72f,0.62f,0.42f),
                RoadColorStreet      = new Color(0.58f,0.52f,0.40f),
                RoadColorAvenue      = new Color(0.45f,0.40f,0.30f),
                RoadColorHighway     = new Color(0.32f,0.28f,0.22f),
                ZoneResColor         = new Color(0.95f,0.88f,0.70f),  // warm sand
                ZoneComColor         = new Color(0.78f,0.55f,0.25f),  // terracotta-gold
                ZoneIndColor         = new Color(0.68f,0.60f,0.42f),
            };
            t.BuildingStyles[BuildingType.Police]   = new BuildingThemeStyle(new Color(0.15f,0.22f,0.72f), Color.white, "مركز الشرطة");
            t.BuildingStyles[BuildingType.Hospital] = new BuildingThemeStyle(new Color(0.92f,0.88f,0.80f), Color.red,   "مستشفى");
            t.BuildingStyles[BuildingType.Park]     = new BuildingThemeStyle(new Color(0.18f,0.62f,0.20f), Color.white, "حديقة");
            return t;
        }

        // ─────────────────────────────────────────────────────────────────────
        // FUTURISTIC  –  Neon accents, dark steel, glowing blues
        // ─────────────────────────────────────────────────────────────────────
        private ThemeData Futuristic()
        {
            var t = new ThemeData("Futuristic")
            {
                TerrainWaterColor    = new Color(0.08f,0.42f,0.75f),
                TerrainGrassColor    = new Color(0.28f,0.60f,0.28f),
                TerrainForestColor   = new Color(0.12f,0.45f,0.20f),
                TerrainSandColor     = new Color(0.72f,0.68f,0.55f),
                TerrainHillColor     = new Color(0.38f,0.38f,0.42f),
                TerrainMountainColor = new Color(0.48f,0.48f,0.52f),
                RoadColorDirt        = new Color(0.35f,0.35f,0.38f),
                RoadColorStreet      = new Color(0.15f,0.15f,0.18f),
                RoadColorAvenue      = new Color(0.10f,0.10f,0.12f),
                RoadColorHighway     = new Color(0.05f,0.05f,0.08f),
                ZoneResColor         = new Color(0.12f,0.55f,0.88f),  // neon blue
                ZoneComColor         = new Color(0.15f,0.85f,0.65f),  // teal
                ZoneIndColor         = new Color(0.85f,0.42f,0.08f),  // orange neon
            };
            t.BuildingStyles[BuildingType.PowerSolar]  = new BuildingThemeStyle(new Color(0.08f,0.35f,0.90f), Color.cyan,  "Fusion Array");
            t.BuildingStyles[BuildingType.PowerWind]   = new BuildingThemeStyle(new Color(0.65f,0.90f,1.00f), Color.white, "Atmospheric Converter");
            t.BuildingStyles[BuildingType.Police]      = new BuildingThemeStyle(new Color(0.05f,0.12f,0.85f), Color.cyan,  "Enforcement Hub");
            t.BuildingStyles[BuildingType.Fire]        = new BuildingThemeStyle(new Color(0.90f,0.35f,0.05f), Color.white, "Crisis Response Unit");
            t.BuildingStyles[BuildingType.Hospital]    = new BuildingThemeStyle(new Color(0.85f,0.95f,1.00f), Color.cyan,  "MediCore Tower");
            t.BuildingStyles[BuildingType.School]      = new BuildingThemeStyle(new Color(0.20f,0.80f,0.72f), Color.white, "Neural Academy");
            t.BuildingStyles[BuildingType.Park]        = new BuildingThemeStyle(new Color(0.12f,0.75f,0.40f), Color.white, "BioDome");
            return t;
        }
    }
}
