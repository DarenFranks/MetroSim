// =============================================================================
// GridMap.cs  –  The world grid: a 2D array of TileData with spatial helpers.
// All systems query / mutate the grid through this class.
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MetroSim
{
    public class GridMap
    {
        // ── Storage ───────────────────────────────────────────────────────────
        private readonly TileData[,] _tiles;
        public  int Width  { get; }
        public  int Height { get; }

        // ── Events ────────────────────────────────────────────────────────────
        /// <summary>Fired when any tile's visual state changes (triggers a renderer dirty flag).</summary>
        public event Action<int, int> OnTileChanged;

        // ── Constructor ───────────────────────────────────────────────────────
        public GridMap(int width, int height)
        {
            Width  = width;
            Height = height;
            _tiles = new TileData[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    _tiles[x, y] = new TileData(x, y);
        }

        // ── Accessors ─────────────────────────────────────────────────────────

        /// <summary>Returns the tile at (x,y) or null if out of bounds.</summary>
        public TileData Get(int x, int y)
        {
            if (!InBounds(x, y)) return null;
            return _tiles[x, y];
        }

        /// <summary>Sets a tile reference (used by terrain generator).</summary>
        public void Set(int x, int y, TileData tile)
        {
            if (!InBounds(x, y)) return;
            _tiles[x, y] = tile;
        }

        /// <summary>Notifies listeners that a tile's visual state has changed.</summary>
        public void MarkDirty(int x, int y) => OnTileChanged?.Invoke(x, y);

        public bool InBounds(int x, int y) =>
            x >= 0 && x < Width && y >= 0 && y < Height;

        // ── Neighbourhood queries ─────────────────────────────────────────────

        /// <summary>Returns up to 4 cardinal neighbours (may be fewer at edges).</summary>
        public List<TileData> GetNeighbours4(int x, int y)
        {
            var list = new List<TileData>(4);
            TileData t;
            if ((t = Get(x - 1, y)) != null) list.Add(t);
            if ((t = Get(x + 1, y)) != null) list.Add(t);
            if ((t = Get(x, y - 1)) != null) list.Add(t);
            if ((t = Get(x, y + 1)) != null) list.Add(t);
            return list;
        }

        /// <summary>Returns up to 8 cardinal + diagonal neighbours.</summary>
        public List<TileData> GetNeighbours8(int x, int y)
        {
            var list = new List<TileData>(8);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    TileData t = Get(x + dx, y + dy);
                    if (t != null) list.Add(t);
                }
            return list;
        }

        /// <summary>
        /// Returns all tiles within a Manhattan-distance radius.
        /// (Cheaper than circular radius for coverage checks.)
        /// </summary>
        public List<TileData> GetTilesInRadius(int cx, int cy, int radius)
        {
            var list = new List<TileData>();
            int xMin = Mathf.Max(0, cx - radius);
            int xMax = Mathf.Min(Width  - 1, cx + radius);
            int yMin = Mathf.Max(0, cy - radius);
            int yMax = Mathf.Min(Height - 1, cy + radius);
            for (int x = xMin; x <= xMax; x++)
                for (int y = yMin; y <= yMax; y++)
                    if (Mathf.Abs(x - cx) + Mathf.Abs(y - cy) <= radius)
                        list.Add(_tiles[x, y]);
            return list;
        }

        /// <summary>Returns tiles in a true circular radius.</summary>
        public List<TileData> GetTilesInCircle(int cx, int cy, int radius)
        {
            var list = new List<TileData>();
            int r2 = radius * radius;
            int xMin = Mathf.Max(0, cx - radius);
            int xMax = Mathf.Min(Width  - 1, cx + radius);
            int yMin = Mathf.Max(0, cy - radius);
            int yMax = Mathf.Min(Height - 1, cy + radius);
            for (int x = xMin; x <= xMax; x++)
                for (int y = yMin; y <= yMax; y++)
                {
                    int dx = x - cx, dy = y - cy;
                    if (dx*dx + dy*dy <= r2)
                        list.Add(_tiles[x, y]);
                }
            return list;
        }

        // ── Bulk iterators ────────────────────────────────────────────────────

        /// <summary>Iterates every tile in the grid.</summary>
        public void ForEach(Action<TileData> action)
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    action(_tiles[x, y]);
        }

        /// <summary>Returns a flat list of all tiles matching a predicate.</summary>
        public List<TileData> FindAll(Func<TileData, bool> predicate)
        {
            var list = new List<TileData>();
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (predicate(_tiles[x, y])) list.Add(_tiles[x, y]);
            return list;
        }

        // ── World-space conversions ───────────────────────────────────────────

        /// <summary>Converts tile grid coords to world Vector3 (centre of tile).</summary>
        public Vector3 TileToWorld(int x, int y) =>
            new Vector3((x + 0.5f) * Config.TILE_SIZE, 0f, (y + 0.5f) * Config.TILE_SIZE);

        /// <summary>Converts a world position to tile coords (may be out of bounds).</summary>
        public Vector2Int WorldToTile(Vector3 world) =>
            new Vector2Int(
                Mathf.FloorToInt(world.x / Config.TILE_SIZE),
                Mathf.FloorToInt(world.z / Config.TILE_SIZE));

        // ── Road-access check ─────────────────────────────────────────────────

        /// <summary>
        /// Returns true if any of the 4 cardinal neighbours is a road tile.
        /// Used during zone development and building placement.
        /// </summary>
        public bool HasAdjacentRoad(int x, int y)
        {
            foreach (var n in GetNeighbours4(x, y))
                if (n.IsRoad) return true;
            return false;
        }

        // ── Serialization helpers ─────────────────────────────────────────────

        /// <summary>
        /// Returns a flat copy of all tiles for serialisation (row-major).
        /// </summary>
        public TileData[] GetFlatArray()
        {
            var arr = new TileData[Width * Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    arr[y * Width + x] = _tiles[x, y];
            return arr;
        }

        /// <summary>Restores grid from a flat array (matching order to GetFlatArray).</summary>
        public void LoadFlatArray(TileData[] arr)
        {
            if (arr.Length != Width * Height)
            {
                Debug.LogError("GridMap.LoadFlatArray: size mismatch.");
                return;
            }
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    _tiles[x, y] = arr[y * Width + x];
        }
    }
}
