// =============================================================================
// TerrainGenerator.cs  –  Procedural terrain using layered Perlin noise.
// Generates a heightmap then assigns terrain types and basic land values.
// =============================================================================
using UnityEngine;

namespace MetroSim
{
    public static class TerrainGenerator
    {
        // ── Noise parameters ─────────────────────────────────────────────────
        // Multiple octaves give natural-looking hills and coastlines.
        private const float BASE_SCALE    = 0.035f;  // low-frequency continent shape
        private const float DETAIL_SCALE  = 0.09f;   // mid-frequency hills
        private const float MICRO_SCALE   = 0.22f;   // high-frequency texture
        private const float BASE_WEIGHT   = 0.60f;
        private const float DETAIL_WEIGHT = 0.28f;
        private const float MICRO_WEIGHT  = 0.12f;

        /// <summary>
        /// Fills every tile in <paramref name="map"/> with terrain data.
        /// Call once when a new city is created.
        /// </summary>
        /// <param name="map">The target GridMap to populate.</param>
        /// <param name="seed">Random seed; 0 = use Unity's random.</param>
        public static void Generate(GridMap map, int seed = 0)
        {
            // Deterministic random offset so each seed looks different
            if (seed == 0) seed = Random.Range(1, 99999);
            float ox = seed * 1.3756f;  // X noise offset
            float oy = seed * 2.9871f;  // Y noise offset

            int   W = map.Width;
            int   H = map.Height;

            // ── Pass 1: sample heightmap ──────────────────────────────────────
            float[,] heightmap = new float[W, H];
            for (int x = 0; x < W; x++)
            {
                for (int y = 0; y < H; y++)
                {
                    float nx = x + ox;
                    float ny = y + oy;

                    float h  = Mathf.PerlinNoise(nx * BASE_SCALE,   ny * BASE_SCALE)   * BASE_WEIGHT
                             + Mathf.PerlinNoise(nx * DETAIL_SCALE, ny * DETAIL_SCALE) * DETAIL_WEIGHT
                             + Mathf.PerlinNoise(nx * MICRO_SCALE,  ny * MICRO_SCALE)  * MICRO_WEIGHT;

                    // Edge falloff – push map borders toward water to make islands/peninsulas
                    float edgeDist = EdgeFalloff(x, y, W, H);
                    h *= edgeDist;

                    heightmap[x, y] = Mathf.Clamp01(h);
                }
            }

            // ── Pass 2: assign terrain types ─────────────────────────────────
            for (int x = 0; x < W; x++)
            {
                for (int y = 0; y < H; y++)
                {
                    TileData tile  = map.Get(x, y);
                    float    h     = heightmap[x, y];
                    tile.HeightValue = h;

                    if      (h < Config.TERRAIN_WATER_MAX)  tile.Terrain = TerrainType.Water;
                    else if (h < Config.TERRAIN_SAND_MAX)   tile.Terrain = TerrainType.Sand;
                    else if (h < Config.TERRAIN_GRASS_MAX)  tile.Terrain = TerrainType.Grass;
                    else if (h < Config.TERRAIN_FOREST_MAX) tile.Terrain = TerrainType.Forest;
                    else if (h < Config.TERRAIN_HILL_MAX)   tile.Terrain = TerrainType.Hill;
                    else                                     tile.Terrain = TerrainType.Mountain;

                    // Base land value from terrain (forests/water = low, hills moderate, grass = good)
                    tile.LandValue = TerrainBaseLandValue(tile.Terrain);
                }
            }

            // ── Pass 3: carve rivers ──────────────────────────────────────────
            CarveRivers(map, heightmap, seed);

            Debug.Log($"[TerrainGenerator] Generated {W}×{H} map with seed {seed}.");
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Returns a 0–1 multiplier that is 1.0 in the centre and fades to 0
        /// near the map edges, creating a natural coastline.
        /// </summary>
        private static float EdgeFalloff(int x, int y, int W, int H)
        {
            float fx = Mathf.Min(x, W - 1 - x) / (W * 0.15f);
            float fy = Mathf.Min(y, H - 1 - y) / (H * 0.15f);
            return Mathf.Clamp01(fx) * Mathf.Clamp01(fy);
        }

        /// <summary>
        /// Returns a base land-value score for each terrain type.
        /// Flat grass is most desirable; water and mountains cannot be built on.
        /// </summary>
        private static float TerrainBaseLandValue(TerrainType t)
        {
            return t switch
            {
                TerrainType.Water    => 0f,
                TerrainType.Sand     => 30f,
                TerrainType.Grass    => Config.LAND_VALUE_BASE,
                TerrainType.Forest   => 40f,
                TerrainType.Hill     => 35f,
                TerrainType.Mountain => 0f,
                _                    => Config.LAND_VALUE_BASE
            };
        }

        /// <summary>
        /// Traces a small number of rivers from high ground to water
        /// by following a steepest-descent path on the heightmap.
        /// River tiles are converted to Water terrain.
        /// </summary>
        private static void CarveRivers(GridMap map, float[,] heightmap, int seed)
        {
            System.Random rng = new System.Random(seed + 7);
            int numRivers = rng.Next(2, 5);

            int W = map.Width;
            int H = map.Height;

            for (int r = 0; r < numRivers; r++)
            {
                // Pick a random high-ground starting tile (hill or forest)
                int startX = -1, startY = -1;
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    int tx = rng.Next(10, W - 10);
                    int ty = rng.Next(10, H - 10);
                    if (heightmap[tx, ty] > Config.TERRAIN_FOREST_MAX)
                    {
                        startX = tx; startY = ty;
                        break;
                    }
                }
                if (startX < 0) continue;

                // Walk downhill until we reach water or the edge
                int cx = startX, cy = startY;
                int maxSteps = W + H;
                for (int step = 0; step < maxSteps; step++)
                {
                    TileData tile = map.Get(cx, cy);
                    if (tile == null || tile.Terrain == TerrainType.Water) break;

                    // Carve this tile into water (river)
                    tile.Terrain     = TerrainType.Water;
                    tile.HeightValue = 0f;

                    // Move to lowest cardinal neighbour
                    float   lowest   = heightmap[cx, cy];
                    int     nx = cx, ny = cy;
                    int[]   dx = {-1, 1, 0, 0};
                    int[]   dy = { 0, 0,-1, 1};
                    foreach (var dir in new[] {0,1,2,3})
                    {
                        int ax = cx + dx[dir], ay = cy + dy[dir];
                        if (!map.InBounds(ax, ay)) continue;
                        if (heightmap[ax, ay] < lowest)
                        {
                            lowest = heightmap[ax, ay];
                            nx = ax; ny = ay;
                        }
                    }
                    if (nx == cx && ny == cy) break; // local minimum, stop
                    cx = nx; cy = ny;
                }
            }
        }
    }
}
