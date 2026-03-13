// =============================================================================
// CameraController.cs  –  Top-down orthographic camera with pan and zoom.
//
// Controls:
//   WASD / Arrow keys – pan
//   Middle mouse drag – pan
//   Scroll wheel      – zoom
//   Mouse click       – forward tile coords to GameManager.ApplyTool()
//   Mouse drag        – zone brush (hold LMB + drag to paint)
// =============================================================================
using UnityEngine;

namespace MetroSim
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        private Camera _cam;

        // Drag state
        private bool      _middleDragging = false;
        private Vector3   _dragOriginWorld;
        private bool      _leftDragging   = false;
        private Vector2Int _lastDragTile  = new Vector2Int(-1,-1);

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) { enabled = false; return; }  // not on a real camera – bail out
            _cam.orthographic = true;
            _cam.orthographicSize = 20f;
            _cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Centre the camera on the middle of the map.</summary>
        public void CentreOnMap(GridMap map)
        {
            float cx = map.Width  * Config.TILE_SIZE * 0.5f;
            float cy = map.Height * Config.TILE_SIZE * 0.5f;
            _cam.transform.position = new Vector3(cx, 50f, cy);
        }

        // ── Unity update ──────────────────────────────────────────────────────

        private void Update()
        {
            HandleKeyboardPan();
            HandleMouseZoom();
            HandleMiddleMousePan();
            HandleLeftMouseClick();
            ClampCamera();
        }

        // ── Keyboard pan ──────────────────────────────────────────────────────

        private void HandleKeyboardPan()
        {
            float speed = Config.CAM_PAN_SPEED * _cam.orthographicSize * 0.1f * Time.deltaTime;
            Vector3 move = Vector3.zero;

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    move.z += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  move.z -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  move.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;

            _cam.transform.position += move.normalized * speed;
        }

        // ── Scroll zoom ───────────────────────────────────────────────────────

        private void HandleMouseZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) < 0.001f) return;

            // Zoom toward mouse position
            Vector3 mouseWorld = ScreenToWorld(Input.mousePosition);

            _cam.orthographicSize = Mathf.Clamp(
                _cam.orthographicSize - scroll * Config.CAM_ZOOM_SPEED,
                Config.CAM_ZOOM_MIN, Config.CAM_ZOOM_MAX);

            // Offset camera so mouse world position stays fixed
            Vector3 newMouseWorld = ScreenToWorld(Input.mousePosition);
            _cam.transform.position += mouseWorld - newMouseWorld;
        }

        // ── Middle mouse drag ─────────────────────────────────────────────────

        private void HandleMiddleMousePan()
        {
            if (Input.GetMouseButtonDown(2))
            {
                _middleDragging   = true;
                _dragOriginWorld  = ScreenToWorld(Input.mousePosition);
            }
            if (Input.GetMouseButtonUp(2))
                _middleDragging = false;

            if (_middleDragging)
            {
                Vector3 cur = ScreenToWorld(Input.mousePosition);
                _cam.transform.position += _dragOriginWorld - cur;
                // Recalculate because camera moved
                _dragOriginWorld = ScreenToWorld(Input.mousePosition);
            }
        }

        // ── Left click / drag ─────────────────────────────────────────────────

        private void HandleLeftMouseClick()
        {
            // Ignore clicks that hit a UI element
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            if (Input.GetMouseButtonDown(0))
            {
                _leftDragging = true;
                _lastDragTile = new Vector2Int(-1,-1);
                ApplyToolAtMouse();
            }

            if (Input.GetMouseButton(0) && _leftDragging)
            {
                // Drag painting for zone / road tools
                var gm = GameManager.Instance;
                if (gm == null) return;
                string tool = gm.ActiveTool;
                bool isDragTool = tool.StartsWith("zone") || tool.StartsWith("road")
                               || tool == "dezone" || tool == "bulldoze"
                               || tool == "water_pipe" || tool == "sewer_pipe"
                               || tool == "power_line";
                if (isDragTool) ApplyToolAtMouse();
            }

            if (Input.GetMouseButtonUp(0))
                _leftDragging = false;
        }

        private void ApplyToolAtMouse()
        {
            var gm = GameManager.Instance;
            if (gm?.Grid == null) return;

            Vector3    world = ScreenToWorld(Input.mousePosition);
            Vector2Int tile  = gm.Grid.WorldToTile(world);

            // Avoid repeating on same tile during drag
            if (tile == _lastDragTile) return;
            _lastDragTile = tile;

            if (!gm.Grid.InBounds(tile.x, tile.y)) return;
            gm.ApplyTool(tile.x, tile.y);
        }

        // ── Camera bounds ─────────────────────────────────────────────────────

        private void ClampCamera()
        {
            var gm = GameManager.Instance;
            if (gm?.Grid == null) return;

            float maxX = gm.Grid.Width  * Config.TILE_SIZE;
            float maxZ = gm.Grid.Height * Config.TILE_SIZE;
            float margin = _cam.orthographicSize;

            Vector3 p = _cam.transform.position;
            p.x = Mathf.Clamp(p.x, -margin, maxX + margin);
            p.z = Mathf.Clamp(p.z, -margin, maxZ + margin);
            _cam.transform.position = p;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Vector3 ScreenToWorld(Vector3 screenPos)
        {
            Ray   ray = _cam.ScreenPointToRay(screenPos);
            float t;
            new Plane(Vector3.up, Vector3.zero).Raycast(ray, out t);
            return ray.GetPoint(t);
        }
    }
}
