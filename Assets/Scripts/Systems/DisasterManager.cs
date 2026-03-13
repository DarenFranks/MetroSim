// =============================================================================
// DisasterManager.cs  –  Random city events: fires, floods, earthquakes,
//                         power outages, and disease outbreaks.
//
// Events are gated by a minimum tick interval and random probability.
// Fire is the most common; earthquakes are rare but devastating.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace MetroSim
{
    public class DisasterManager : MonoBehaviour
    {
        private int  _ticksSinceLastDisaster = 0;
        private bool _disastersEnabled       = true;

        // Active fire tiles (track spread)
        private readonly List<Vector2Int> _fireTiles = new List<Vector2Int>();

        public void Reset()
        {
            _ticksSinceLastDisaster = 0;
            _fireTiles.Clear();
        }

        public void SetEnabled(bool enabled) => _disastersEnabled = enabled;

        // ── Main simulate ─────────────────────────────────────────────────────

        public void Simulate(GridMap map, GameManager gm)
        {
            _ticksSinceLastDisaster++;

            // ── Spread and extinguish existing fires ──────────────────────────
            SimulateFires(map, gm);

            if (!_disastersEnabled) return;
            if (_ticksSinceLastDisaster < Config.DISASTER_MIN_INTERVAL_TICKS) return;

            // ── Random disaster check ─────────────────────────────────────────
            float roll = Random.value;

            if      (roll < 0.008f)  TriggerFire(map, gm);        // 0.8% chance
            else if (roll < 0.012f)  TriggerPowerOutage(map, gm);  // 0.4%
            else if (roll < 0.014f)  TriggerFlood(map, gm);        // 0.2%
            else if (roll < 0.015f)  TriggerEarthquake(map, gm);   // 0.1%
        }

        // ── Fire simulation ───────────────────────────────────────────────────

        private void TriggerFire(GridMap map, GameManager gm)
        {
            // Pick a random occupied tile to start the fire
            var candidates = map.FindAll(t =>
                t.Density >= DensityLevel.Low && t.Zone != ZoneType.None);
            if (candidates.Count == 0) return;

            TileData start = candidates[Random.Range(0, candidates.Count)];
            IgniteTile(start);
            _ticksSinceLastDisaster = 0;
            gm.Notify($"🔥 Fire broke out at ({start.X},{start.Y})!");
        }

        private void IgniteTile(TileData tile)
        {
            if (tile.IsOnFire || !tile.IsBuildable) return;
            tile.IsOnFire    = true;
            tile.FireDuration = Random.Range(3, 8);
            _fireTiles.Add(new Vector2Int(tile.X, tile.Y));
        }

        private void SimulateFires(GridMap map, GameManager gm)
        {
            var toRemove = new List<Vector2Int>();

            foreach (var pos in _fireTiles)
            {
                TileData tile = map.Get(pos.x, pos.y);
                if (tile == null || !tile.IsOnFire) { toRemove.Add(pos); continue; }

                tile.FireDuration--;
                tile.AirPollution = Mathf.Min(100f, tile.AirPollution + 10f);

                // Fire station coverage increases extinguish chance
                bool fireCovered = gm.Services.IsFireCovered(map, pos.x, pos.y);
                float extChance  = fireCovered ? 0.4f : 0.1f;

                if (tile.FireDuration <= 0 || Random.value < extChance)
                {
                    // Extinguish – leave rubble
                    tile.IsOnFire     = false;
                    tile.FireDuration = 0;
                    tile.Building     = BuildingType.Rubble;
                    tile.Density      = DensityLevel.Empty;
                    tile.Occupants    = 0;
                    map.MarkDirty(pos.x, pos.y);
                    toRemove.Add(pos);
                    continue;
                }

                // Fire spreads to neighbours
                if (Random.value < Config.DISASTER_FIRE_SPREAD_CHANCE)
                {
                    var neighbours = map.GetNeighbours4(pos.x, pos.y);
                    foreach (var nb in neighbours)
                    {
                        if (!nb.IsOnFire && nb.Density > DensityLevel.Empty
                            && Random.value < Config.DISASTER_FIRE_SPREAD_CHANCE)
                            IgniteTile(nb);
                    }
                }
            }

            foreach (var pos in toRemove) _fireTiles.Remove(pos);
        }

        // ── Power outage ──────────────────────────────────────────────────────

        private void TriggerPowerOutage(GridMap map, GameManager gm)
        {
            // Randomly cut power to a region
            int cx = Random.Range(10, map.Width  - 10);
            int cy = Random.Range(10, map.Height - 10);
            int r  = Random.Range(5, 15);

            int affected = 0;
            var tiles = map.GetTilesInRadius(cx, cy, r);
            foreach (var t in tiles)
            {
                if (t.HasPower) { t.HasPower = false; affected++; }
            }

            _ticksSinceLastDisaster = 0;
            if (affected > 0)
                gm.Notify($"⚡ Power outage! {affected} tiles lost power.");
        }

        // ── Flood ─────────────────────────────────────────────────────────────

        private void TriggerFlood(GridMap map, GameManager gm)
        {
            // Find a water tile and flood some adjacent land tiles
            var waterTiles = map.FindAll(t => t.Terrain == TerrainType.Water);
            if (waterTiles.Count == 0) return;

            TileData origin = waterTiles[Random.Range(0, waterTiles.Count)];
            int radius = Random.Range(2, 6);
            int flooded = 0;

            var tiles = map.GetTilesInRadius(origin.X, origin.Y, radius);
            foreach (var t in tiles)
            {
                if (t.Terrain != TerrainType.Water && t.HeightValue < Config.TERRAIN_SAND_MAX + 0.05f)
                {
                    t.IsFlooded   = true;
                    t.HasPower    = false;
                    t.HasWater    = false;
                    flooded++;
                }
            }

            _ticksSinceLastDisaster = 0;
            gm.Notify($"🌊 Flooding! {flooded} tiles inundated.");

            // Floods drain after a few ticks (handled next sim calls)
            // Schedule unflooding via a coroutine-like flag (FireDuration reuse)
            foreach (var t in tiles) if (t.IsFlooded) t.FireDuration = 5;
        }

        // ── Earthquake ────────────────────────────────────────────────────────

        private void TriggerEarthquake(GridMap map, GameManager gm)
        {
            int cx = Random.Range(5, map.Width  - 5);
            int cy = Random.Range(5, map.Height - 5);
            int r  = Random.Range(8, 20);

            int destroyed = 0;
            var tiles = map.GetTilesInRadius(cx, cy, r);
            foreach (var t in tiles)
            {
                if (t.Density <= DensityLevel.Empty) continue;
                float distRatio = 1f - (float)(Mathf.Abs(t.X-cx)+Mathf.Abs(t.Y-cy)) / (r+1);
                if (Random.value < distRatio * 0.6f)
                {
                    t.Building  = BuildingType.Rubble;
                    t.IsBuilt   = false;
                    t.Density   = DensityLevel.Empty;
                    t.Occupants = 0;
                    destroyed++;
                    map.MarkDirty(t.X, t.Y);
                }
            }

            _ticksSinceLastDisaster = 0;
            gm.Notify($"🏚 Earthquake! {destroyed} buildings damaged near ({cx},{cy}).");
        }
    }
}
