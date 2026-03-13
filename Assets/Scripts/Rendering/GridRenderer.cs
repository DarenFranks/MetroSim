// =============================================================================
// GridRenderer.cs  –  Renders the world grid using a Unity mesh.
//
// Strategy:
//   The map is divided into CHUNK_SIZE×CHUNK_SIZE tile chunks.
//   Each chunk owns a GameObject with a MeshFilter+MeshRenderer.
//   When SetDirty() is called, all chunks rebuild their meshes on the
//   next LateUpdate().  Each tile quad is coloured via vertex colours,
//   so no textures are required.
//
// Tile visual priority (highest first):
//   1. On fire / flooded
//   2. Building placed
//   3. Road
//   4. Power line / water pipe / sewer pipe overlay
//   5. Zone (empty or developed)
//   6. Terrain
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace MetroSim
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GridRenderer : MonoBehaviour
    {
        // ── Chunk data ────────────────────────────────────────────────────────
        private class Chunk
        {
            public GameObject Go;
            public MeshFilter  Filter;
            public Mesh        Mesh;
            public bool        Dirty = true;
        }

        private Chunk[,] _chunks;
        private GridMap  _map;
        private bool     _globalDirty = true;

        // Shared material (vertex colour shader)
        private Material _mat;

        // ── Init ──────────────────────────────────────────────────────────────

        public void Rebuild(GridMap map)
        {
            _map = map;

            // Destroy old chunks
            if (_chunks != null)
            {
                foreach (var c in _chunks)
                    if (c?.Go != null) Destroy(c.Go);
            }

            int cw = Mathf.CeilToInt((float)map.Width  / Config.CHUNK_SIZE);
            int ch = Mathf.CeilToInt((float)map.Height / Config.CHUNK_SIZE);
            _chunks = new Chunk[cw, ch];

            // Create vertex-colour material if missing
            if (_mat == null)
            {
                _mat = new Material(Shader.Find("Sprites/Default") ??
                                    Shader.Find("Unlit/Color"));
                _mat.enableInstancing = false;
            }

            for (int cx = 0; cx < cw; cx++)
                for (int cy = 0; cy < ch; cy++)
                    _chunks[cx, cy] = CreateChunk(cx, cy);

            _globalDirty = true;
        }

        public void SetDirty()
        {
            _globalDirty = true;
            if (_chunks == null) return;
            int cw = _chunks.GetLength(0);
            int ch = _chunks.GetLength(1);
            for (int cx = 0; cx < cw; cx++)
                for (int cy = 0; cy < ch; cy++)
                    if (_chunks[cx, cy] != null) _chunks[cx, cy].Dirty = true;
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (!_globalDirty || _map == null || _chunks == null) return;
            _globalDirty = false;

            var theme = GameManager.Instance?.Themes?.ActiveTheme;
            int cw = _chunks.GetLength(0);
            int ch = _chunks.GetLength(1);
            for (int cx = 0; cx < cw; cx++)
                for (int cy = 0; cy < ch; cy++)
                    if (_chunks[cx, cy].Dirty)
                        RebuildChunk(_chunks[cx, cy], cx, cy, theme);
        }

        // ── Chunk creation ────────────────────────────────────────────────────

        private Chunk CreateChunk(int cx, int cy)
        {
            var go     = new GameObject($"Chunk_{cx}_{cy}");
            go.transform.SetParent(transform);

            var filter   = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _mat;
            renderer.receiveShadows = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var mesh = new Mesh { name = $"ChunkMesh_{cx}_{cy}" };
            filter.mesh = mesh;

            return new Chunk { Go = go, Filter = filter, Mesh = mesh, Dirty = true };
        }

        // ── Mesh rebuild ──────────────────────────────────────────────────────

        private void RebuildChunk(Chunk chunk, int cx, int cy, ThemeData theme)
        {
            int startX = cx * Config.CHUNK_SIZE;
            int startY = cy * Config.CHUNK_SIZE;
            int endX   = Mathf.Min(startX + Config.CHUNK_SIZE, _map.Width);
            int endY   = Mathf.Min(startY + Config.CHUNK_SIZE, _map.Height);

            int tileCount = (endX - startX) * (endY - startY);

            var verts   = new Vector3[tileCount * 4];
            var colors  = new Color  [tileCount * 4];
            var tris    = new int    [tileCount * 6];
            var uvs     = new Vector2[tileCount * 4];

            int vi = 0, ti = 0;
            float s = Config.TILE_SIZE;

            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    TileData tile = _map.Get(x, y);
                    Color    col  = GetTileColor(tile, theme);

                    float wx = x * s, wy = y * s;

                    verts[vi]   = new Vector3(wx,     0f, wy    );
                    verts[vi+1] = new Vector3(wx,     0f, wy + s);
                    verts[vi+2] = new Vector3(wx + s, 0f, wy + s);
                    verts[vi+3] = new Vector3(wx + s, 0f, wy    );

                    colors[vi] = colors[vi+1] = colors[vi+2] = colors[vi+3] = col;

                    uvs[vi]   = new Vector2(0,0);
                    uvs[vi+1] = new Vector2(0,1);
                    uvs[vi+2] = new Vector2(1,1);
                    uvs[vi+3] = new Vector2(1,0);

                    tris[ti]   = vi;   tris[ti+1] = vi+1; tris[ti+2] = vi+2;
                    tris[ti+3] = vi;   tris[ti+4] = vi+2; tris[ti+5] = vi+3;

                    vi += 4; ti += 6;
                }
            }

            chunk.Mesh.Clear();
            chunk.Mesh.vertices  = verts;
            chunk.Mesh.colors    = colors;
            chunk.Mesh.triangles = tris;
            chunk.Mesh.uv        = uvs;
            chunk.Mesh.RecalculateNormals();
            chunk.Dirty = false;
        }

        // ── Tile colour selection ─────────────────────────────────────────────

        private Color GetTileColor(TileData tile, ThemeData theme)
        {
            // 1. Disaster states
            if (tile.IsOnFire)  return new Color(1f, 0.3f, 0f);
            if (tile.IsFlooded) return new Color(0.3f, 0.5f, 0.9f);

            // 2. Rubble
            if (tile.HasBuilding && tile.Building == BuildingType.Rubble)
                return new Color(0.4f, 0.38f, 0.35f);

            // 3. Placed building
            if (tile.HasBuilding)
                return theme?.GetBuildingColor(tile.Building)
                    ?? BuildingDatabase.Get(tile.Building)?.BaseColor
                    ?? Color.white;

            // 4. Power line (visual stripe – shown even under roads)
            if (tile.PowerLine && !tile.IsRoad)
                return new Color(1f, 0.92f, 0.1f);

            // 5. Road
            if (tile.IsRoad)
            {
                if (theme == null) return Color.grey;
                return tile.Road switch
                {
                    RoadType.Dirt    => theme.RoadColorDirt,
                    RoadType.Street  => theme.RoadColorStreet,
                    RoadType.Avenue  => theme.RoadColorAvenue,
                    RoadType.Highway => theme.RoadColorHighway,
                    _                => Color.grey
                };
            }

            // 6. Zone (developed or empty)
            if (tile.Zone != ZoneType.None)
                return DevelopedZoneColor(tile, theme);

            // 7. Terrain
            return TerrainColor(tile.Terrain, theme);
        }

        private Color DevelopedZoneColor(TileData tile, ThemeData theme)
        {
            Color zoneBase = tile.Zone switch
            {
                ZoneType.Residential => theme?.ZoneResColor ?? new Color(0.5f, 0.85f, 0.45f),
                ZoneType.Commercial  => theme?.ZoneComColor ?? new Color(0.4f, 0.65f, 0.95f),
                ZoneType.Industrial  => theme?.ZoneIndColor ?? new Color(0.85f, 0.75f, 0.3f),
                _                    => Color.white
            };

            if (tile.Density == DensityLevel.Empty)
                return Color.Lerp(zoneBase, TerrainColor(tile.Terrain, theme), 0.6f);

            // Darken slightly with density
            float f = tile.Density switch
            {
                DensityLevel.Low    => 0.9f,
                DensityLevel.Medium => 0.75f,
                DensityLevel.High   => 0.58f,
                _                   => 1f
            };
            return zoneBase * f;
        }

        private Color TerrainColor(TerrainType terrain, ThemeData theme)
        {
            if (theme == null)
            {
                return terrain switch
                {
                    TerrainType.Water    => new Color(0.25f,0.55f,0.90f),
                    TerrainType.Sand     => new Color(0.90f,0.84f,0.62f),
                    TerrainType.Grass    => new Color(0.40f,0.72f,0.30f),
                    TerrainType.Forest   => new Color(0.18f,0.52f,0.18f),
                    TerrainType.Hill     => new Color(0.60f,0.55f,0.40f),
                    TerrainType.Mountain => new Color(0.70f,0.68f,0.65f),
                    _                    => Color.white
                };
            }
            return terrain switch
            {
                TerrainType.Water    => theme.TerrainWaterColor,
                TerrainType.Sand     => theme.TerrainSandColor,
                TerrainType.Grass    => theme.TerrainGrassColor,
                TerrainType.Forest   => theme.TerrainForestColor,
                TerrainType.Hill     => theme.TerrainHillColor,
                TerrainType.Mountain => theme.TerrainMountainColor,
                _                    => Color.white
            };
        }
    }
}
