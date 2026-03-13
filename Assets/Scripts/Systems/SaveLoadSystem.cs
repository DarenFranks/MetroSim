// =============================================================================
// SaveLoadSystem.cs  –  JSON serialisation / deserialisation of the full city.
//
// Save file layout (JSON):
//   {
//     "version": 1,
//     "cityName": "...",
//     "funds": 123456,
//     "year": 2024, "month": 3,
//     "activeTheme": "north_american",
//     "resTax": 0.08, "comTax": 0.09, "indTax": 0.07,
//     "mapWidth": 128, "mapHeight": 128,
//     "tiles": [ { per-tile fields }, ... ]
//   }
//
// Save files live in Application.persistentDataPath/Cities/
// The save/load slot is "autosave.json" unless changed.
// =============================================================================
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace MetroSim
{
    public class SaveLoadSystem : MonoBehaviour
    {
        public string SaveFileName = "autosave.json";

        private string SaveDir => Path.Combine(Application.persistentDataPath, "Cities");
        private string SavePath => Path.Combine(SaveDir, SaveFileName);

        // ── Public API ────────────────────────────────────────────────────────

        public void Save()
        {
            var gm = GameManager.Instance;
            if (gm?.Grid == null) return;

            try
            {
                Directory.CreateDirectory(SaveDir);

                var dto = BuildDTO(gm);
                string json = JsonUtility.ToJson(dto, prettyPrint: true);
                File.WriteAllText(SavePath, json);

                gm.Notify($"💾 City saved to {SaveFileName}");
                Debug.Log($"[SaveLoad] Saved to {SavePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveLoad] Save failed: {e}");
                gm.Notify("❌ Save failed – see console.");
            }
        }

        public void Load()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (!File.Exists(SavePath))
            {
                gm.Notify("❌ No save file found.");
                return;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                var dto = JsonUtility.FromJson<CityDTO>(json);
                ApplyDTO(dto, gm);

                gm.Notify($"📂 City loaded from {SaveFileName}");
                Debug.Log($"[SaveLoad] Loaded from {SavePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveLoad] Load failed: {e}");
                gm.Notify("❌ Load failed – save may be corrupt.");
            }
        }

        // ── DTO construction ──────────────────────────────────────────────────

        private CityDTO BuildDTO(GameManager gm)
        {
            var map  = gm.Grid;
            var eco  = gm.Economy;
            var dto  = new CityDTO
            {
                version     = 1,
                cityName    = gm.CityName,
                funds       = eco.Funds,
                year        = gm.Year,
                month       = gm.Month,
                activeTheme = gm.Themes?.ActiveThemeKey ?? "north_american",
                resTax      = eco.ResidentialTaxRate,
                comTax      = eco.CommercialTaxRate,
                indTax      = eco.IndustrialTaxRate,
                mapWidth    = map.Width,
                mapHeight   = map.Height,
                tiles       = new TileDTO[map.Width * map.Height]
            };

            int idx = 0;
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                TileData t = map.Get(x, y);
                dto.tiles[idx++] = new TileDTO
                {
                    terrain      = (int)t.Terrain,
                    zone         = (int)t.Zone,
                    density      = (int)t.Density,
                    road         = (int)t.Road,
                    building     = (int)t.Building,
                    isBuilt      = t.IsBuilt,
                    occupants    = t.Occupants,
                    happiness    = t.Happiness,
                    landValue    = t.LandValue,
                    heightValue  = t.HeightValue,
                    hasPower     = t.HasPower,
                    hasWater     = t.HasWater,
                    hasSewer     = t.HasSewer,
                    waterPipe    = t.WaterPipe,
                    sewerPipe    = t.SewerPipe,
                    powerLine    = t.PowerLine,
                    airPollution = t.AirPollution,
                    isOnFire     = t.IsOnFire,
                    isFlooded    = t.IsFlooded
                };
            }

            return dto;
        }

        // ── DTO application ───────────────────────────────────────────────────

        private void ApplyDTO(CityDTO dto, GameManager gm)
        {
            // Validate
            if (dto.version != 1)
                throw new Exception($"Unknown save version {dto.version}");
            if (dto.tiles == null || dto.tiles.Length != dto.mapWidth * dto.mapHeight)
                throw new Exception("Tile array size mismatch.");

            // Rebuild fresh grid at the same size
            gm.CityName = dto.cityName;
            gm.Year     = dto.year;
            gm.Month    = dto.month;

            var map = new GridMap(dto.mapWidth, dto.mapHeight);
            gm.Grid = map;

            // Apply tiles
            int idx = 0;
            for (int x = 0; x < dto.mapWidth; x++)
            for (int y = 0; y < dto.mapHeight; y++)
            {
                TileDTO  d = dto.tiles[idx++];
                TileData t = map.Get(x, y);

                t.Terrain      = (TerrainType)  d.terrain;
                t.Zone         = (ZoneType)     d.zone;
                t.Density      = (DensityLevel) d.density;
                t.Road         = (RoadType)     d.road;
                t.Building     = (BuildingType) d.building;
                t.IsBuilt      = d.isBuilt;
                t.Occupants    = d.occupants;
                t.Happiness    = d.happiness;
                t.LandValue    = d.landValue;
                t.HeightValue  = d.heightValue;
                t.HasPower     = d.hasPower;
                t.HasWater     = d.hasWater;
                t.HasSewer     = d.hasSewer;
                t.WaterPipe    = d.waterPipe;
                t.SewerPipe    = d.sewerPipe;
                t.PowerLine    = d.powerLine;
                t.AirPollution = d.airPollution;
                t.IsOnFire     = d.isOnFire;
                t.IsFlooded    = d.isFlooded;
            }

            // Economy
            gm.Economy.Funds               = dto.funds;
            gm.Economy.ResidentialTaxRate  = dto.resTax;
            gm.Economy.CommercialTaxRate   = dto.comTax;
            gm.Economy.IndustrialTaxRate   = dto.indTax;

            // Theme
            gm.Themes?.SetTheme(dto.activeTheme);

            // Reset subsystem state
            gm.Disasters?.Reset();

            // Force full re-render
            gm.GridRend?.Rebuild(map);
            gm.OverlayRend?.SetDirty();

            // Refresh UI
            gm.UI?.Refresh();
        }

        // ── DTOs (must be [Serializable] for JsonUtility) ─────────────────────

        [Serializable]
        private class CityDTO
        {
            public int    version;
            public string cityName;
            public float  funds;
            public int    year;
            public int    month;
            public string activeTheme;
            public float  resTax;
            public float  comTax;
            public float  indTax;
            public int    mapWidth;
            public int    mapHeight;
            public TileDTO[] tiles;
        }

        [Serializable]
        private struct TileDTO
        {
            public int   terrain;
            public int   zone;
            public int   density;
            public int   road;
            public int   building;
            public bool  isBuilt;
            public int   occupants;
            public float happiness;
            public float landValue;
            public float heightValue;
            public bool  hasPower;
            public bool  hasWater;
            public bool  hasSewer;
            public bool  waterPipe;
            public bool  sewerPipe;
            public bool  powerLine;
            public float airPollution;
            public bool  isOnFire;
            public bool  isFlooded;
        }
    }
}
