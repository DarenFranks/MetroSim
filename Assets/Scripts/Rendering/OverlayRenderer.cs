// =============================================================================
// OverlayRenderer.cs  –  Renders semi-transparent overlay visualisations.
//
// Supported overlays:
//   Power     – green = has power, red = no power
//   Water     – blue = has water, orange = no water
//   Sewer     – teal = connected, red = not connected
//   Pollution – gradient from clear to red based on air pollution
//   LandValue – gradient green (high) → red (low)
//   Traffic   – heat map based on TrafficDensity
//
// Uses a single full-map quad mesh with per-vertex colours.
// =============================================================================
using UnityEngine;

namespace MetroSim
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class OverlayRenderer : MonoBehaviour
    {
        private MeshFilter   _filter;
        private MeshRenderer _renderer;
        private Mesh         _mesh;
        private bool         _dirty = false;
        private OverlayType  _currentOverlay = OverlayType.None;

        // Slightly above the terrain mesh (Y offset)
        private const float Y_OFFSET = 0.05f;
        private const float ALPHA    = 0.55f;

        private void Awake()
        {
            _filter   = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();

            var mat = new Material(Shader.Find("Sprites/Default") ??
                                   Shader.Find("Unlit/Color"));
            mat.color = Color.white;
            _renderer.sharedMaterial = mat;
            _renderer.enabled = false;
        }

        public void SetDirty() => _dirty = true;

        public void SetOverlay(OverlayType type)
        {
            _currentOverlay = type;
            _renderer.enabled = (type != OverlayType.None);
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (!_dirty || _currentOverlay == OverlayType.None) return;
            _dirty = false;
            RebuildMesh();
        }

        // ── Mesh build ────────────────────────────────────────────────────────

        private void RebuildMesh()
        {
            GridMap map = GameManager.Instance?.Grid;
            if (map == null) return;

            int   W  = map.Width;
            int   H  = map.Height;
            int   n  = W * H;
            float s  = Config.TILE_SIZE;

            var verts  = new Vector3[n * 4];
            var colors = new Color  [n * 4];
            var tris   = new int    [n * 6];
            var uvs    = new Vector2[n * 4];

            int vi = 0, ti = 0;
            for (int x = 0; x < W; x++)
            {
                for (int y = 0; y < H; y++)
                {
                    TileData tile = map.Get(x, y);
                    Color    col  = GetOverlayColor(tile);

                    float wx = x*s, wy = y*s;
                    verts[vi]   = new Vector3(wx,     Y_OFFSET, wy    );
                    verts[vi+1] = new Vector3(wx,     Y_OFFSET, wy + s);
                    verts[vi+2] = new Vector3(wx + s, Y_OFFSET, wy + s);
                    verts[vi+3] = new Vector3(wx + s, Y_OFFSET, wy    );

                    colors[vi]=colors[vi+1]=colors[vi+2]=colors[vi+3] = col;

                    uvs[vi]=new Vector2(0,0); uvs[vi+1]=new Vector2(0,1);
                    uvs[vi+2]=new Vector2(1,1); uvs[vi+3]=new Vector2(1,0);

                    tris[ti]=vi; tris[ti+1]=vi+1; tris[ti+2]=vi+2;
                    tris[ti+3]=vi; tris[ti+4]=vi+2; tris[ti+5]=vi+3;
                    vi+=4; ti+=6;
                }
            }

            if (_mesh == null) _mesh = new Mesh { name = "OverlayMesh" };
            _mesh.Clear();
            _mesh.vertices  = verts;
            _mesh.colors    = colors;
            _mesh.triangles = tris;
            _mesh.uv        = uvs;
            _mesh.RecalculateNormals();
            _filter.mesh = _mesh;
        }

        // ── Colour per overlay ────────────────────────────────────────────────

        private Color GetOverlayColor(TileData t)
        {
            // Tiles that are not buildable or empty show transparent in overlays
            bool relevant = t.IsBuildable && (t.Zone != ZoneType.None || t.HasBuilding || t.IsRoad);

            return _currentOverlay switch
            {
                OverlayType.Power     => PowerColor(t, relevant),
                OverlayType.Water     => WaterColor(t, relevant),
                OverlayType.Sewer     => SewerColor(t, relevant),
                OverlayType.Pollution => PollutionColor(t),
                OverlayType.LandValue => LandValueColor(t),
                OverlayType.Traffic   => TrafficColor(t),
                _                     => Color.clear
            };
        }

        // -- Power: green = connected, red = no power (only for occupied tiles)
        private Color PowerColor(TileData t, bool relevant)
        {
            if (!relevant) return Color.clear;
            return t.HasPower
                ? new Color(0.1f, 0.9f, 0.2f, ALPHA)
                : new Color(0.9f, 0.1f, 0.1f, ALPHA);
        }

        // -- Water: blue = connected, orange = no water
        private Color WaterColor(TileData t, bool relevant)
        {
            if (!relevant) return Color.clear;
            if (t.WaterPipe || (t.HasBuilding && (t.Building == BuildingType.WaterPump
                                               || t.Building == BuildingType.WaterTower)))
                return new Color(0.2f, 0.5f, 1.0f, ALPHA);
            return t.HasWater
                ? new Color(0.3f, 0.7f, 1.0f, ALPHA * 0.6f)
                : new Color(0.9f, 0.5f, 0.1f, ALPHA);
        }

        // -- Sewer: teal = connected, brown = not
        private Color SewerColor(TileData t, bool relevant)
        {
            if (!relevant) return Color.clear;
            if (t.SewerPipe)
                return new Color(0.2f, 0.7f, 0.6f, ALPHA);
            return t.HasSewer
                ? new Color(0.3f, 0.65f, 0.55f, ALPHA * 0.6f)
                : new Color(0.6f, 0.35f, 0.1f, ALPHA);
        }

        // -- Pollution: clear (0) → yellow → red (100)
        private Color PollutionColor(TileData t)
        {
            float p = Mathf.Clamp01(t.AirPollution / 100f);
            if (p < 0.01f) return Color.clear;
            Color c = p < 0.5f
                ? Color.Lerp(new Color(1f,1f,0f), new Color(1f,0.5f,0f), p*2f)
                : Color.Lerp(new Color(1f,0.5f,0f), new Color(0.9f,0.05f,0.05f), (p-0.5f)*2f);
            c.a = p * ALPHA;
            return c;
        }

        // -- Land Value: red (low 0) → yellow (mid) → green (high 200)
        private Color LandValueColor(TileData t)
        {
            if (!t.IsBuildable) return Color.clear;
            float v = Mathf.Clamp01(t.LandValue / 200f);
            Color c = v < 0.5f
                ? Color.Lerp(new Color(0.9f,0.1f,0.1f), new Color(1f,0.9f,0.1f), v*2f)
                : Color.Lerp(new Color(1f,0.9f,0.1f), new Color(0.1f,0.85f,0.2f), (v-0.5f)*2f);
            c.a = 0.45f;
            return c;
        }

        // -- Traffic: clear → yellow → red based on TrafficDensity
        private Color TrafficColor(TileData t)
        {
            if (!t.IsRoad) return Color.clear;
            float d = t.TrafficDensity;
            if (d < 0.01f) return Color.clear;
            Color c = d < 0.5f
                ? Color.Lerp(new Color(0.3f,0.9f,0.3f), new Color(1f,0.9f,0.1f), d*2f)
                : Color.Lerp(new Color(1f,0.9f,0.1f), new Color(0.9f,0.1f,0.1f), (d-0.5f)*2f);
            c.a = d * ALPHA;
            return c;
        }
    }
}
