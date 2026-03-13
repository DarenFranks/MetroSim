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
// Multiple named save slots are supported.
// "AutoSave" slot is written automatically on application quit.
// =============================================================================
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace MetroSim
{
    public class SaveLoadSystem : MonoBehaviour
    {
        // ── Save info (public so dialogs can read it) ─────────────────────────

        public struct SaveInfo
        {
            public string name;
            public string displayDate;
            public DateTime lastWrite;
        }

        // ── State ─────────────────────────────────────────────────────────────

        public string LastUsedSlot { get; private set; } = "QuickSave";

        private string SaveDir => Path.Combine(Application.persistentDataPath, "Cities");

        private string SlotPath(string slotName) =>
            Path.Combine(SaveDir, SanitiseName(slotName) + ".json");

        private static string SanitiseName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim().Length == 0 ? "save" : name.Trim();
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void OnApplicationQuit()
        {
            // Always write an AutoSave on exit
            SaveToSlot("AutoSave");
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Save to the last-used named slot.</summary>
        public void QuickSave() => SaveToSlot(LastUsedSlot);

        /// <summary>Save to a named slot.</summary>
        public void SaveToSlot(string slotName)
        {
            var gm = GameManager.Instance;
            if (gm?.Grid == null) return;

            try
            {
                Directory.CreateDirectory(SaveDir);
                string path = SlotPath(slotName);
                var dto  = BuildDTO(gm);
                string json = JsonUtility.ToJson(dto, prettyPrint: true);
                File.WriteAllText(path, json);

                LastUsedSlot = slotName;
                if (slotName != "AutoSave")
                    gm.Notify($"Saved \"{slotName}\"");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveLoad] SaveToSlot({slotName}) failed: {e}");
                GameManager.Instance?.Notify("Save failed – see console.");
            }
        }

        /// <summary>Load from a named slot.</summary>
        public void LoadFromSlot(string slotName)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            string path = SlotPath(slotName);
            if (!File.Exists(path))
            {
                gm.Notify($"No save found: \"{slotName}\"");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var dto = JsonUtility.FromJson<CityDTO>(json);
                ApplyDTO(dto, gm);

                LastUsedSlot = slotName;
                gm.Notify($"Loaded \"{slotName}\"");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveLoad] LoadFromSlot({slotName}) failed: {e}");
                GameManager.Instance?.Notify("Load failed – save may be corrupt.");
            }
        }

        /// <summary>Returns all save files sorted by name.</summary>
        public SaveInfo[] GetAllSaves()
        {
            if (!Directory.Exists(SaveDir)) return Array.Empty<SaveInfo>();

            var files = Directory.GetFiles(SaveDir, "*.json");
            var infos = new List<SaveInfo>(files.Length);
            foreach (var f in files)
            {
                var info = new SaveInfo
                {
                    name        = Path.GetFileNameWithoutExtension(f),
                    lastWrite   = File.GetLastWriteTime(f),
                    displayDate = File.GetLastWriteTime(f).ToString("MM/dd/yy  HH:mm"),
                };
                infos.Add(info);
            }
            infos.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return infos.ToArray();
        }

        /// <summary>Delete a named save slot.</summary>
        public void DeleteSlot(string slotName)
        {
            string path = SlotPath(slotName);
            if (File.Exists(path))
            {
                File.Delete(path);
                GameManager.Instance?.Notify($"Deleted \"{slotName}\"");
            }
        }

        // ── Legacy wrappers (keep existing call sites working) ────────────────

        public void Save() => SaveToSlot(LastUsedSlot);
        public void Load() => LoadFromSlot(LastUsedSlot);

        // ── DTO construction ──────────────────────────────────────────────────

        private CityDTO BuildDTO(GameManager gm)
        {
            var map = gm.Grid;
            var eco = gm.Economy;
            var dto = new CityDTO
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
            if (dto.version != 1)
                throw new Exception($"Unknown save version {dto.version}");
            if (dto.tiles == null || dto.tiles.Length != dto.mapWidth * dto.mapHeight)
                throw new Exception("Tile array size mismatch.");

            gm.CityName = dto.cityName;
            gm.Year     = dto.year;
            gm.Month    = dto.month;

            var map = new GridMap(dto.mapWidth, dto.mapHeight);
            gm.Grid = map;

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

            gm.Economy.Funds              = dto.funds;
            gm.Economy.ResidentialTaxRate = dto.resTax;
            gm.Economy.CommercialTaxRate  = dto.comTax;
            gm.Economy.IndustrialTaxRate  = dto.indTax;

            gm.Themes?.SetTheme(dto.activeTheme);
            gm.Disasters?.Reset();
            gm.GridRend?.Rebuild(map);
            gm.OverlayRend?.SetDirty();
            gm.UI?.Refresh();
        }

        // ── DTOs ──────────────────────────────────────────────────────────────

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
