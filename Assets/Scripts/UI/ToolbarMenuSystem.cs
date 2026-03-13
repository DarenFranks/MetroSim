// =============================================================================
// ToolbarMenuSystem.cs  –  Bottom toolbar with grouped dropdown build menus.
//
// Self-bootstraps via RuntimeInitializeOnLoadMethod – no scene setup needed.
// Layout (left → right):
//   [Select] [Bulldoze]  |  [Zones▾] [Roads▾] [Power▾] [Water▾] [Sewer▾]
//                           [Services▾]  |  [View▾]  |  [Game▾]
//                           (spacer)  [⏸] [▶] [▶▶] [▶▶▶]
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MetroSim
{
    public class ToolbarMenuSystem : MonoBehaviour
    {
        // ── Singleton / auto-spawn ────────────────────────────────────────────
        public static ToolbarMenuSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (Instance != null) return;
            var go = new GameObject("[ToolbarMenuSystem]");
            DontDestroyOnLoad(go);
            go.AddComponent<ToolbarMenuSystem>();
        }

        // ── Colors ────────────────────────────────────────────────────────────
        private static readonly Color32 TOOLBAR_BG   = new Color32(24,  27,  35, 245);
        private static readonly Color32 BTN_NORMAL   = new Color32(40,  44,  56, 255);
        private static readonly Color32 BTN_HOVER    = new Color32(58,  65,  85, 255);
        private static readonly Color32 BTN_ACTIVE   = new Color32(30,  90, 180, 255);
        private static readonly Color32 BTN_SELECTED = new Color32(22,  70, 145, 255);
        private static readonly Color32 DROPDOWN_BG  = new Color32(30,  34,  44, 252);
        private static readonly Color32 ITEM_HOVER   = new Color32(45,  85, 155, 230);
        private static readonly Color32 SEP_COLOR    = new Color32(55,  60,  75, 200);
        private static readonly Color32 SPEED_BG     = new Color32(32,  50,  32, 255);

        // ── Layout constants ──────────────────────────────────────────────────
        private const float TOOLBAR_H   = 36f;
        private const float ITEM_H      = 28f;
        private const float DROPDOWN_W  = 170f;
        private const float FONT_SIZE   = 11.5f;

        // ── Menu data ─────────────────────────────────────────────────────────
        private readonly struct MenuItem
        {
            public readonly string label;
            public readonly string key;     // null = separator
            public readonly bool   isOverlay;
            public readonly bool   isGame;
            public MenuItem(string l, string k, bool isOverlay = false, bool isGame = false)
            { label = l; key = k; this.isOverlay = isOverlay; this.isGame = isGame; }
        }

        private (string cat, float width, MenuItem[] items)[] _menus;

        // ── Runtime state ─────────────────────────────────────────────────────
        private readonly Dictionary<string, GameObject> _dropdowns     = new();
        private readonly Dictionary<string, Button>     _catButtons    = new();
        private readonly Dictionary<string, Button>     _directButtons = new();
        private string _openCat   = null;
        private string _activeTool = "select";
        private Canvas _canvas;
        private bool _built = false;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start() => EnsureBuild();

        private void Update()
        {
            if (_openCat == null) return;
            // Close dropdown on click outside
            if (Input.GetMouseButtonDown(0) && !IsPointerOverOpenDropdown())
                CloseAll();
        }

        // ── Build ─────────────────────────────────────────────────────────────
        private void EnsureBuild()
        {
            if (_built) return;
            _built = true;

            DefineMenus();

            _canvas = FindAnyObjectByType<Canvas>();
            if (_canvas == null) return;

            BuildToolbar();

            // Hide old left-panel tool buttons (replaced by this toolbar)
            var leftPanel = GameObject.Find("LeftPanel");
            if (leftPanel != null) leftPanel.SetActive(false);
        }

        private void DefineMenus()
        {
            _menus = new (string, float, MenuItem[])[]
            {
                ("Zones", 68, new[]
                {
                    new MenuItem("Residential Zone",  "zone_residential"),
                    new MenuItem("Commercial Zone",   "zone_commercial"),
                    new MenuItem("Industrial Zone",   "zone_industrial"),
                    new MenuItem("─────────────",    null),
                    new MenuItem("De-zone",           "dezone"),
                }),
                ("Roads", 68, new[]
                {
                    new MenuItem("Dirt Road   ($10)", "road_dirt"),
                    new MenuItem("Street      ($50)", "road_street"),
                    new MenuItem("Avenue     ($100)", "road_avenue"),
                    new MenuItem("Highway    ($500)", "road_highway"),
                    new MenuItem("─────────────",    null),
                    new MenuItem("Demolish Road",     "demolish_road"),
                }),
                ("Power", 68, new[]
                {
                    new MenuItem("Coal Plant",   "power_coal"),
                    new MenuItem("Gas Plant",    "power_gas"),
                    new MenuItem("Solar Farm",   "power_solar"),
                    new MenuItem("Wind Farm",    "power_wind"),
                    new MenuItem("─────────────", null),
                    new MenuItem("Power Line",   "power_line"),
                }),
                ("Water", 68, new[]
                {
                    new MenuItem("Pump Station", "water_pump"),
                    new MenuItem("Water Tower",  "water_tower"),
                    new MenuItem("Water Pipe",   "water_pipe"),
                }),
                ("Sewer", 68, new[]
                {
                    new MenuItem("Sewer Pipe",   "sewer_pipe"),
                    new MenuItem("Sewage Plant", "sewer_plant"),
                }),
                ("Services", 80, new[]
                {
                    new MenuItem("Police Station", "police"),
                    new MenuItem("Fire Station",   "fire"),
                    new MenuItem("Hospital",       "hospital"),
                    new MenuItem("School",         "school"),
                    new MenuItem("─────────────",  null),
                    new MenuItem("Park",           "park"),
                    new MenuItem("Landfill",       "landfill"),
                }),
                ("View", 58, new[]
                {
                    new MenuItem("No Overlay",  "overlay_None",      isOverlay: true),
                    new MenuItem("Power Grid",  "overlay_Power",     isOverlay: true),
                    new MenuItem("Water",       "overlay_Water",     isOverlay: true),
                    new MenuItem("Sewer",       "overlay_Sewer",     isOverlay: true),
                    new MenuItem("Pollution",   "overlay_Pollution", isOverlay: true),
                    new MenuItem("Land Value",  "overlay_LandValue", isOverlay: true),
                    new MenuItem("Traffic",     "overlay_Traffic",   isOverlay: true),
                }),
                ("Game", 58, new[]
                {
                    new MenuItem("New City",     "game_new",      isGame: true),
                    new MenuItem("─────────────", null),
                    new MenuItem("Quick Save",   "game_quicksave",isGame: true),
                    new MenuItem("Save As...",   "game_saveas",   isGame: true),
                    new MenuItem("Load...",      "game_load",     isGame: true),
                }),
            };
        }

        private void BuildToolbar()
        {
            // ── Toolbar bar ──────────────────────────────────────────────────
            var bar = new GameObject("ToolbarBar");
            bar.transform.SetParent(_canvas.transform, false);
            var barRT = bar.AddComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0, 0);
            barRT.anchorMax = new Vector2(1, 0);
            barRT.pivot     = new Vector2(0.5f, 0);
            barRT.offsetMin = Vector2.zero;
            barRT.offsetMax = new Vector2(0, TOOLBAR_H);
            var barBG = bar.AddComponent<Image>();
            barBG.color = TOOLBAR_BG;
            // Ensure toolbar renders on top
            bar.transform.SetAsLastSibling();

            var hl = bar.AddComponent<HorizontalLayoutGroup>();
            hl.padding              = new RectOffset(4, 4, 3, 3);
            hl.spacing              = 2;
            hl.childAlignment       = TextAnchor.MiddleLeft;
            hl.childControlHeight   = true;
            hl.childControlWidth    = false;
            hl.childForceExpandHeight = true;
            hl.childForceExpandWidth  = false;

            // Quick-access tools
            AddDirectButton(bar.transform, "Select",   "select",   60);
            AddDirectButton(bar.transform, "Bulldoze", "bulldoze", 70);
            AddSepLine(bar.transform);

            // Category dropdowns
            bool afterServices = false;
            bool afterView     = false;
            foreach (var (cat, w, items) in _menus)
            {
                AddCategoryButton(bar.transform, cat, w, items);
                if (cat == "Services") { AddSepLine(bar.transform); afterServices = true; }
                if (cat == "View")     { AddSepLine(bar.transform); afterView     = true; }
            }

            // Spacer pushes speed controls to the right
            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(bar.transform, false);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Speed controls
            AddSpeedButtons(bar.transform);

            // ── Select as default active tool ────────────────────────────────
            SetActiveTool("select");
        }

        // ── Button factories ──────────────────────────────────────────────────

        private void AddDirectButton(Transform parent, string label, string toolKey, float w)
        {
            var btn = MakeButton(parent, label, w);
            btn.onClick.AddListener(() => SetActiveTool(toolKey));
            _directButtons[toolKey] = btn;
        }

        private void AddCategoryButton(Transform parent, string cat, float w, MenuItem[] items)
        {
            var btn = MakeButton(parent, cat + " \u25be", w);  // ▾
            var dropdown = BuildDropdown(cat, items);
            _dropdowns[cat] = dropdown;
            _catButtons[cat] = btn;
            btn.onClick.AddListener(() => ToggleDropdown(cat, btn));
        }

        private void AddSpeedButtons(Transform parent)
        {
            (string label, Action action)[] speeds =
            {
                ("⏸",   () => GameManager.Instance?.SimEngine?.Pause()),
                ("▶",   () => GameManager.Instance?.SimEngine?.SetNormal()),
                ("▶▶",  () => GameManager.Instance?.SimEngine?.SetFast()),
                ("▶▶▶", () => GameManager.Instance?.SimEngine?.SetUltraFast()),
            };
            foreach (var (lbl, act) in speeds)
            {
                var btn = MakeButton(parent, lbl, 32, SPEED_BG);
                Action captured = act;
                btn.onClick.AddListener(() => captured());
            }
        }

        private void AddSepLine(Transform parent)
        {
            var go = new GameObject("Sep");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = SEP_COLOR;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth  = 1;
            le.preferredHeight = 24;
            le.flexibleHeight  = 0;
            le.flexibleWidth   = 0;
        }

        private Button MakeButton(Transform parent, string label, float w, Color32? bg = null)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, 0);

            var img = go.AddComponent<Image>();
            img.color = bg ?? BTN_NORMAL;

            var btn = go.AddComponent<Button>();
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
            var c = btn.colors;
            c.normalColor      = bg ?? BTN_NORMAL;
            c.highlightedColor = BTN_HOVER;
            c.pressedColor     = BTN_ACTIVE;
            c.selectedColor    = bg ?? BTN_NORMAL;
            c.fadeDuration     = 0.07f;
            btn.colors = c;

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.minWidth       = w;
            le.flexibleWidth  = 0;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = FONT_SIZE;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.enableWordWrapping = false;
            var trt = tmp.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(3, 0);
            trt.offsetMax = new Vector2(-3, 0);

            return btn;
        }

        // ── Dropdown panel factory ────────────────────────────────────────────

        private GameObject BuildDropdown(string cat, MenuItem[] items)
        {
            // Count non-separator items for height
            int realItems = 0;
            int seps = 0;
            foreach (var m in items) { if (m.key == null) seps++; else realItems++; }

            float panelH = realItems * ITEM_H + seps * 6f + 4f;

            var panel = new GameObject($"Drop_{cat}");
            panel.transform.SetParent(_canvas.transform, false);

            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(DROPDOWN_W, panelH);

            var bg = panel.AddComponent<Image>();
            bg.color = DROPDOWN_BG;

            var vl = panel.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(3, 3, 3, 3);
            vl.spacing = 1;
            vl.childControlWidth    = true;
            vl.childControlHeight   = false;
            vl.childForceExpandWidth = true;

            foreach (var item in items)
            {
                if (item.key == null)
                {
                    // Visual separator
                    var sep = new GameObject("Sep");
                    sep.transform.SetParent(panel.transform, false);
                    sep.AddComponent<Image>().color = SEP_COLOR;
                    var le2 = sep.AddComponent<LayoutElement>();
                    le2.preferredHeight = 5;
                    continue;
                }

                var row = new GameObject("Row_" + item.key);
                row.transform.SetParent(panel.transform, false);

                var rowRT = row.AddComponent<RectTransform>();
                rowRT.sizeDelta = new Vector2(0, ITEM_H);

                var rowImg = row.AddComponent<Image>();
                rowImg.color = Color.clear;

                var rowBtn = row.AddComponent<Button>();
                var nav = rowBtn.navigation; nav.mode = Navigation.Mode.None; rowBtn.navigation = nav;
                var rc = rowBtn.colors;
                rc.normalColor      = Color.clear;
                rc.highlightedColor = ITEM_HOVER;
                rc.pressedColor     = BTN_ACTIVE;
                rc.selectedColor    = Color.clear;
                rc.fadeDuration     = 0.05f;
                rowBtn.colors = rc;

                var le = row.AddComponent<LayoutElement>();
                le.preferredHeight = ITEM_H;

                var textGO = new GameObject("Text");
                textGO.transform.SetParent(row.transform, false);
                var tmp = textGO.AddComponent<TextMeshProUGUI>();
                tmp.text     = item.label;
                tmp.fontSize = FONT_SIZE;
                tmp.color    = Color.white;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                tmp.enableWordWrapping = false;
                var trt = tmp.rectTransform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(12, 0);
                trt.offsetMax = new Vector2(-6, 0);

                // Wire callback
                string key     = item.key;
                bool isOverlay = item.isOverlay;
                bool isGame    = item.isGame;
                rowBtn.onClick.AddListener(() =>
                {
                    CloseAll();
                    if (isOverlay) HandleOverlay(key.Substring("overlay_".Length));
                    else if (isGame) HandleGameAction(key);
                    else SetActiveTool(key);
                });
            }

            panel.SetActive(false);
            return panel;
        }

        // ── Dropdown logic ────────────────────────────────────────────────────

        private void ToggleDropdown(string cat, Button triggerBtn)
        {
            if (_openCat == cat) { CloseAll(); return; }
            CloseAll();
            if (!_dropdowns.TryGetValue(cat, out var panel)) return;

            // Position panel above the trigger button
            PositionDropdownAbove(panel, triggerBtn);
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            _openCat = cat;
        }

        private void PositionDropdownAbove(GameObject panel, Button trigger)
        {
            var canvasRT  = _canvas.GetComponent<RectTransform>();
            var triggerRT = trigger.GetComponent<RectTransform>();
            var panelRT   = panel.GetComponent<RectTransform>();

            // Get button's bottom-left corner
            var corners = new Vector3[4];
            triggerRT.GetWorldCorners(corners);
            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : _canvas.worldCamera;

            Vector2 screenBL = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);

            // ScreenPointToLocalPointInRectangle returns canvas-local coords
            // where (0,0) = canvas CENTER.
            // Panel anchor is (0,0) = canvas BOTTOM-LEFT.
            // anchoredPosition = localPos - anchorInLocalSpace
            //                  = localPos - (-halfW, -halfH)
            //                  = localPos + (halfW, halfH)
            Vector2 localBL;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, screenBL, cam, out localBL);

            Vector2 halfSize = canvasRT.rect.size * 0.5f;
            float xPos = localBL.x + halfSize.x;

            // Clamp so the panel doesn't run past the right edge
            xPos = Mathf.Clamp(xPos, 0f, canvasRT.rect.width - panelRT.sizeDelta.x);

            panelRT.anchoredPosition = new Vector2(xPos, TOOLBAR_H);
        }

        private void CloseAll()
        {
            foreach (var kv in _dropdowns) kv.Value.SetActive(false);
            _openCat = null;
        }

        private bool IsPointerOverOpenDropdown()
        {
            if (_openCat == null) return false;
            if (!_dropdowns.TryGetValue(_openCat, out var panel)) return false;
            var rt  = panel.GetComponent<RectTransform>();
            var cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, cam);
        }

        // ── Action handlers ───────────────────────────────────────────────────

        private void SetActiveTool(string toolKey)
        {
            _activeTool = toolKey;
            var gm = GameManager.Instance;
            if (gm != null) gm.ActiveTool = toolKey;
            UpdateHighlights();
        }

        private void HandleOverlay(string overlayName)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (Enum.TryParse<OverlayType>(overlayName, true, out var ot))
            {
                gm.ActiveOverlay = ot;
                gm.OverlayRend?.SetOverlay(ot);
            }
        }

        private void HandleGameAction(string key)
        {
            switch (key)
            {
                case "game_new":
                    GameManager.Instance?.NewCity(0);
                    break;
                case "game_quicksave":
                    GameManager.Instance?.SaveLoad?.QuickSave();
                    break;
                case "game_saveas":
                    SaveLoadDialog.Instance?.ShowSave();
                    break;
                case "game_load":
                    SaveLoadDialog.Instance?.ShowLoad();
                    break;
            }
        }

        private void UpdateHighlights()
        {
            foreach (var kv in _directButtons)
            {
                var img = kv.Value.GetComponent<Image>();
                if (img == null) continue;
                img.color = (kv.Key == _activeTool) ? BTN_SELECTED : BTN_NORMAL;
            }
        }
    }
}
