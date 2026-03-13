// =============================================================================
// SceneSetup.cs  –  Unity Editor utility: MetroSim → Setup Scene
//
// Builds the entire scene from scratch in one click:
//   • Main Camera (orthographic, top-down)
//   • Lighting (directional)
//   • Managers root with all MonoBehaviour systems
//   • Full UGUI Canvas (TopBar, LeftPanel, RightPanel, NotificationArea)
//   • Wires every public reference on UIManager
//   • Creates OverlayRenderer GameObject
//   • Creates GridRenderer root
//
// Run once on a fresh empty scene. Safe to re-run – it destroys the previous
// "MetroSim Root" and "UI Canvas" objects before rebuilding.
// =============================================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace MetroSim
{
    public static class SceneSetup
    {
        private const string ROOT_NAME   = "MetroSim Root";
        private const string CANVAS_NAME = "UI Canvas";

        // ── Menu entry ────────────────────────────────────────────────────────

        [MenuItem("MetroSim/Setup Scene")]
        public static void Run()
        {
            // ── Clean up previous setup ───────────────────────────────────────
            DestroyExisting(ROOT_NAME);
            DestroyExisting(CANVAS_NAME);
            DestroyExisting("Main Camera");
            DestroyExisting("Directional Light");

            // ── Camera ────────────────────────────────────────────────────────
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 20f;
            cam.backgroundColor  = new Color(0.12f, 0.15f, 0.18f);
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.transform.position = new Vector3(64f, 60f, 64f);
            camGO.AddComponent<AudioListener>();

            // ── Directional light ─────────────────────────────────────────────
            var lightGO = new GameObject("Directional Light");
            var light   = lightGO.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.intensity = 1.1f;
            light.color     = new Color(1f, 0.97f, 0.90f);
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // ── Manager root ──────────────────────────────────────────────────
            var root = new GameObject(ROOT_NAME);

            // ── Add all MonoBehaviour systems ─────────────────────────────────
            var gm        = root.AddComponent<GameManager>();
            var simEngine = root.AddComponent<SimulationEngine>();
            var zoneMan   = root.AddComponent<ZoneManager>();
            var roadNet   = root.AddComponent<RoadNetwork>();
            var powerNet  = root.AddComponent<PowerNetwork>();
            var waterNet  = root.AddComponent<WaterNetwork>();
            var sewerNet  = root.AddComponent<SewerNetwork>();
            var traffic   = root.AddComponent<TrafficManager>();
            var economy   = root.AddComponent<EconomySystem>();
            var demand    = root.AddComponent<DemandSystem>();
            var services  = root.AddComponent<ServiceManager>();
            var pollution = root.AddComponent<PollutionSystem>();
            var landValue = root.AddComponent<LandValueSystem>();
            var disasters = root.AddComponent<DisasterManager>();
            var themes    = root.AddComponent<ThemeManager>();
            var saveLoad  = root.AddComponent<SaveLoadSystem>();

            // ── Camera controller ─────────────────────────────────────────────
            var camCtrl = camGO.AddComponent<CameraController>();

            // ── Grid renderer child ───────────────────────────────────────────
            var gridRendGO = new GameObject("GridRenderer");
            gridRendGO.transform.SetParent(root.transform);
            var gridRend = gridRendGO.AddComponent<GridRenderer>();

            // ── Overlay renderer child ────────────────────────────────────────
            var overlayGO = new GameObject("OverlayRenderer");
            overlayGO.transform.SetParent(root.transform);
            var overlayRend = overlayGO.AddComponent<OverlayRenderer>();

            // ── Wire GameManager subsystem references ─────────────────────────
            SetField(gm, "SimEngine",   simEngine);
            SetField(gm, "Zones",       zoneMan);
            SetField(gm, "Roads",       roadNet);
            SetField(gm, "Power",       powerNet);
            SetField(gm, "Water",       waterNet);
            SetField(gm, "Sewer",       sewerNet);
            SetField(gm, "Traffic",     traffic);
            SetField(gm, "Economy",     economy);
            SetField(gm, "Demand",      demand);
            SetField(gm, "Services",    services);
            SetField(gm, "Pollution",   pollution);
            SetField(gm, "LandValue",   landValue);
            SetField(gm, "Disasters",   disasters);
            SetField(gm, "Themes",      themes);
            SetField(gm, "GridRend",    gridRend);
            SetField(gm, "OverlayRend", overlayRend);
            SetField(gm, "SaveLoad",    saveLoad);
            SetField(gm, "CamCtrl",     camCtrl);

            // ── Build UI ──────────────────────────────────────────────────────
            var uiManager = BuildUI(root, gm, camCtrl);
            SetField(gm, "UI", uiManager);

            // ── Mark scene dirty ──────────────────────────────────────────────
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("[SceneSetup] Scene built successfully. Press Play to start.");
            EditorUtility.DisplayDialog("MetroSim", "Scene setup complete!\n\nPress Play to start the simulation.", "OK");
        }

        // ── UI builder ────────────────────────────────────────────────────────

        private static UIManager BuildUI(GameObject root, GameManager gm, CameraController camCtrl)
        {
            // ── Canvas ────────────────────────────────────────────────────────
            var canvasGO = new GameObject(CANVAS_NAME);
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            // Add UIManager
            var uiManager = canvasGO.AddComponent<UIManager>();

            // ── Event system ──────────────────────────────────────────────────
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                // Use InputSystemUIInputModule if Input System package is present (Unity 6),
                // otherwise fall back to the legacy StandaloneInputModule.
                var inputSysType = System.Type.GetType(
                    "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputSysType != null)
                    esGO.AddComponent(inputSysType);
                else
                    esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // ── Top Bar ───────────────────────────────────────────────────────
            var topBar = MakePanel(canvasGO, "TopBar",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -60f), new Vector2(0f, 0f),
                new Color(0.1f, 0.1f, 0.15f, 0.9f));

            uiManager.CityNameText   = MakeLabel(topBar, "CityNameText",   new Vector2(10, -10), new Vector2(200, 40), 18, TextAlignmentOptions.Left);
            uiManager.PopulationText = MakeLabel(topBar, "PopulationText", new Vector2(220, -10), new Vector2(180, 40), 15);
            uiManager.DateText       = MakeLabel(topBar, "DateText",       new Vector2(410, -10), new Vector2(160, 40), 15);
            uiManager.FundsText      = MakeLabel(topBar, "FundsText",      new Vector2(580, -10), new Vector2(160, 40), 15);
            uiManager.IncomeText     = MakeLabel(topBar, "IncomeText",     new Vector2(750, -10), new Vector2(180, 40), 15);

            // Demand bars
            uiManager.ResBar = MakeSlider(topBar, "ResBar", new Vector2(960, -10), new Color(0.2f, 0.8f, 0.3f));
            uiManager.ComBar = MakeSlider(topBar, "ComBar", new Vector2(1130, -10), new Color(0.2f, 0.4f, 0.9f));
            uiManager.IndBar = MakeSlider(topBar, "IndBar", new Vector2(1300, -10), new Color(0.9f, 0.7f, 0.1f));

            // Speed buttons
            MakeSpeedButton(topBar, "Pause",   new Vector2(1480, -10), "⏸", uiManager, "SetSpeedPause");
            MakeSpeedButton(topBar, "Speed1",  new Vector2(1540, -10), "▶",  uiManager, "SetSpeed1");
            MakeSpeedButton(topBar, "Speed2",  new Vector2(1600, -10), "▶▶", uiManager, "SetSpeed2");
            MakeSpeedButton(topBar, "Speed3",  new Vector2(1660, -10), "▶▶▶",uiManager, "SetSpeed3");

            // ── Left Panel – Build Menu ───────────────────────────────────────
            var leftPanel = MakePanel(canvasGO, "LeftPanel",
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 60f), new Vector2(180f, 0f),
                new Color(0.08f, 0.08f, 0.12f, 0.92f));

            AddScrollableToolButtons(leftPanel, uiManager);

            // ── Right Panel – Overlays + Stats + Budget ───────────────────────
            var rightPanel = MakePanel(canvasGO, "RightPanel",
                new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-220f, 60f), new Vector2(0f, 0f),
                new Color(0.08f, 0.08f, 0.12f, 0.92f));

            AddOverlayButtons(rightPanel, uiManager);
            AddStatsPanel(rightPanel, uiManager);
            AddBudgetPanel(rightPanel, uiManager);
            AddTileInfoPanel(rightPanel, uiManager);

            // Save/Load/New buttons
            MakeActionButton(rightPanel, "SaveBtn",  new Vector2(10, -600), "💾 Save",  uiManager, "OnSaveClicked");
            MakeActionButton(rightPanel, "LoadBtn",  new Vector2(75, -600), "📂 Load",  uiManager, "OnLoadClicked");
            MakeActionButton(rightPanel, "NewBtn",   new Vector2(140, -600), "🏗 New",  uiManager, "OnNewClicked");

            // ── Notification Area ─────────────────────────────────────────────
            var notifGO = new GameObject("NotificationArea");
            notifGO.transform.SetParent(canvasGO.transform, false);
            var notifRT = notifGO.AddComponent<RectTransform>();
            notifRT.anchorMin   = new Vector2(0.5f, 0f);
            notifRT.anchorMax   = new Vector2(0.5f, 0f);
            notifRT.pivot       = new Vector2(0.5f, 0f);
            notifRT.anchoredPosition = new Vector2(0f, 10f);
            notifRT.sizeDelta   = new Vector2(500f, 180f);
            var notif = notifGO.AddComponent<NotificationUI>();
            uiManager.Notifications = notif;

            return uiManager;
        }

        // ── Panel helpers ─────────────────────────────────────────────────────

        private static GameObject MakePanel(GameObject parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color bg)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var img = go.AddComponent<Image>();
            img.color = bg;
            return go;
        }

        private static TextMeshProUGUI MakeLabel(GameObject parent, string name,
            Vector2 pos, Vector2 size, float fontSize = 14f,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(0f, 1f);
            rt.pivot            = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta        = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize  = fontSize;
            tmp.color     = Color.white;
            tmp.alignment = align;
            tmp.text      = name;
            return tmp;
        }

        private static Slider MakeSlider(GameObject parent, string name,
            Vector2 pos, Color fillColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(0f, 1f);
            rt.pivot            = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta        = new Vector2(140f, 22f);

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var bgRT = bg.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Fill area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var faRT = fillArea.AddComponent<RectTransform>();
            faRT.anchorMin = new Vector2(0f, 0.1f); faRT.anchorMax = new Vector2(1f, 0.9f);
            faRT.offsetMin = new Vector2(5f, 0f); faRT.offsetMax = new Vector2(-5f, 0f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRT = fill.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = fillColor;

            // Slider component
            var slider = go.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value    = 0.5f;
            slider.fillRect = fillRT;

            // Label
            var labelGO = new GameObject("ValueLabel");
            labelGO.transform.SetParent(go.transform, false);
            var lRT = labelGO.AddComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
            lRT.offsetMin = Vector2.zero; lRT.offsetMax = Vector2.zero;
            var lbl = labelGO.AddComponent<TextMeshProUGUI>();
            lbl.text = name; lbl.fontSize = 11f; lbl.color = Color.white;
            lbl.alignment = TextAlignmentOptions.Center;

            return slider;
        }

        private static void MakeSpeedButton(GameObject parent, string name,
            Vector2 pos, string label, UIManager uiManager, string method)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(50f, 40f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.35f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var lbl = MakeLabel(go, "Label", Vector2.zero, new Vector2(50f, 40f), 13f);
            lbl.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            lbl.GetComponent<RectTransform>().anchorMax = Vector2.one;
            lbl.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            lbl.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            lbl.text = label;

            // Wire button
            btn.onClick.AddListener(() =>
            {
                var m = uiManager.GetType().GetMethod(method);
                m?.Invoke(uiManager, null);
            });
        }

        private static void MakeActionButton(GameObject parent, string name,
            Vector2 pos, string label, UIManager uiManager, string method)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(60f, 34f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.4f, 0.6f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var lbl = new GameObject("Label");
            lbl.transform.SetParent(go.transform, false);
            var lRT = lbl.AddComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
            lRT.offsetMin = new Vector2(2f,2f); lRT.offsetMax = new Vector2(-2f,-2f);
            var tmp = lbl.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 11f; tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            btn.onClick.AddListener(() =>
            {
                var m = uiManager.GetType().GetMethod(method);
                m?.Invoke(uiManager, null);
            });
        }

        // ── Tool buttons ──────────────────────────────────────────────────────

        private static readonly (string tool, string label)[] Tools =
        {
            ("zone_residential",      "🏠 Residential"),
            ("zone_commercial",      "🏢 Commercial"),
            ("zone_industrial",      "🏭 Industrial"),
            ("dezone",        "⬜ Dezone"),
            ("road_dirt",     "🛤 Dirt Road"),
            ("road_street",   "🛣 Street"),
            ("road_avenue",   "🛣 Avenue"),
            ("road_highway",  "🛣 Highway"),
            ("power_line",    "⚡ Power Line"),
            ("water_pipe",    "💧 Water Pipe"),
            ("sewer_pipe",    "🚿 Sewer Pipe"),
            ("power_coal",    "🏭 Coal Plant"),
            ("power_gas",     "⚡ Gas Plant"),
            ("power_solar",    "☀ Solar Farm"),
            ("power_wind",     "🌀 Wind Farm"),
            ("water_pump",    "💧 Water Pump"),
            ("water_tower",   "🗼 Water Tower"),
            ("sewer_plant",   "🏗 Sewer Plant"),
            ("police",        "🚔 Police"),
            ("fire",          "🚒 Fire Station"),
            ("hospital",      "🏥 Hospital"),
            ("school",        "🏫 School"),
            ("park",          "🌳 Park"),
            ("landfill",      "🗑 Landfill"),
            ("bulldoze",      "💥 Bulldoze"),
        };

        private static void AddScrollableToolButtons(GameObject panel, UIManager uiManager)
        {
            // Scroll view
            var svGO = new GameObject("ScrollView");
            svGO.transform.SetParent(panel.transform, false);
            var svRT = svGO.AddComponent<RectTransform>();
            svRT.anchorMin = Vector2.zero; svRT.anchorMax = Vector2.one;
            svRT.offsetMin = new Vector2(4f, 4f); svRT.offsetMax = new Vector2(-4f, -4f);
            var sv = svGO.AddComponent<ScrollRect>();
            sv.horizontal = false;

            // Viewport
            var vpGO = new GameObject("Viewport");
            vpGO.transform.SetParent(svGO.transform, false);
            var vpRT = vpGO.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;
            var mask = vpGO.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            vpGO.AddComponent<Image>().color = new Color(0,0,0,0.01f);
            sv.viewport = vpRT;

            // Content
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(vpGO.transform, false);
            var contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f); contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.offsetMin = Vector2.zero; contentRT.offsetMax = Vector2.zero;
            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = false; vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 2f; vlg.padding = new RectOffset(2, 2, 2, 2);
            var csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sv.content = contentRT;

            foreach (var (tool, label) in Tools)
            {
                var btnGO = new GameObject(tool + "_btn");
                btnGO.transform.SetParent(contentGO.transform, false);
                var le = btnGO.AddComponent<LayoutElement>();
                le.preferredHeight = 32f; le.flexibleWidth = 1f;
                var img = btnGO.AddComponent<Image>();
                img.color = new Color(0.18f, 0.22f, 0.3f);
                var btn = btnGO.AddComponent<Button>();
                btn.targetGraphic = img;
                var lblGO = new GameObject("Label");
                lblGO.transform.SetParent(btnGO.transform, false);
                var lRT = lblGO.AddComponent<RectTransform>();
                lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
                lRT.offsetMin = new Vector2(4f, 0f); lRT.offsetMax = new Vector2(-4f, 0f);
                var tmp = lblGO.AddComponent<TextMeshProUGUI>();
                tmp.text = label; tmp.fontSize = 12f; tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                var toolKey = tool; // capture
                btn.onClick.AddListener(() => uiManager.OnToolButtonClicked(toolKey));
            }
        }

        // ── Overlay buttons ───────────────────────────────────────────────────

        private static readonly (string overlay, string label, Color col)[] Overlays =
        {
            ("None",      "□ No Overlay",  new Color(0.3f, 0.3f, 0.3f)),
            ("Power",     "⚡ Power",       new Color(0.2f, 0.6f, 0.2f)),
            ("Water",     "💧 Water",       new Color(0.1f, 0.4f, 0.8f)),
            ("Sewer",     "🚿 Sewer",       new Color(0.2f, 0.5f, 0.4f)),
            ("Pollution", "💨 Pollution",   new Color(0.5f, 0.3f, 0.1f)),
            ("LandValue", "💰 Land Value",  new Color(0.1f, 0.5f, 0.1f)),
            ("Traffic",   "🚗 Traffic",     new Color(0.5f, 0.1f, 0.1f)),
        };

        private static void AddOverlayButtons(GameObject panel, UIManager uiManager)
        {
            float y = -10f;
            var header = MakeLabel(panel, "OverlayHeader", new Vector2(5f, y), new Vector2(210f, 22f), 13f, TextAlignmentOptions.Left);
            header.text = "── Overlays ──";
            y -= 28f;

            foreach (var (ov, label, col) in Overlays)
            {
                var btnGO = new GameObject(ov + "_ovr_btn");
                btnGO.transform.SetParent(panel.transform, false);
                var rt = btnGO.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(0f, 28f);
                rt.offsetMin = new Vector2(6f, y - 28f); rt.offsetMax = new Vector2(-6f, y);
                var img = btnGO.AddComponent<Image>(); img.color = col;
                var btn = btnGO.AddComponent<Button>(); btn.targetGraphic = img;
                var lblGO = new GameObject("L"); lblGO.transform.SetParent(btnGO.transform, false);
                var lRT = lblGO.AddComponent<RectTransform>();
                lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
                lRT.offsetMin = new Vector2(4f, 0f); lRT.offsetMax = new Vector2(-4f, 0f);
                var tmp = lblGO.AddComponent<TextMeshProUGUI>();
                tmp.text = label; tmp.fontSize = 12f; tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                var ovKey = ov;
                btn.onClick.AddListener(() => uiManager.OnOverlayButtonClicked(ovKey));
                y -= 30f;
            }
        }

        // ── Stats panel ───────────────────────────────────────────────────────

        private static void AddStatsPanel(GameObject panel, UIManager uiManager)
        {
            float y = -230f;
            var header = MakeLabel(panel, "StatsHeader", new Vector2(5f, y), new Vector2(210f, 22f), 13f, TextAlignmentOptions.Left);
            header.text = "── City Stats ──"; y -= 24f;

            uiManager.StatPop      = MakeStatRow(panel, "StatPop",      ref y, "Population:");
            uiManager.StatFunds    = MakeStatRow(panel, "StatFunds",    ref y, "Funds:");
            uiManager.StatIncome   = MakeStatRow(panel, "StatIncome",   ref y, "Income:");
            uiManager.StatExpenses = MakeStatRow(panel, "StatExpenses", ref y, "Expenses:");
            uiManager.StatPower    = MakeStatRow(panel, "StatPower",    ref y, "Power (MW):");
            uiManager.StatWater    = MakeStatRow(panel, "StatWater",    ref y, "Water (m³):");
            uiManager.StatSewer    = MakeStatRow(panel, "StatSewer",    ref y, "Sewer (m³):");
            uiManager.StatHappiness = MakeStatRow(panel, "StatHappiness", ref y, "Happiness:");
        }

        private static TextMeshProUGUI MakeStatRow(GameObject parent, string name,
            ref float y, string labelText)
        {
            // Row label (left)
            var lbl = MakeLabel(parent, name + "_lbl", new Vector2(6f, y), new Vector2(110f, 20f), 11f, TextAlignmentOptions.Left);
            lbl.text = labelText;
            // Row value (right)
            var val = MakeLabel(parent, name, new Vector2(120f, y), new Vector2(95f, 20f), 11f, TextAlignmentOptions.Right);
            val.text = "—";
            y -= 22f;
            return val;
        }

        // ── Budget panel ──────────────────────────────────────────────────────

        private static void AddBudgetPanel(GameObject parent, UIManager uiManager)
        {
            float y = -430f;
            var header = MakeLabel(parent, "BudgetHeader", new Vector2(5f, y), new Vector2(210f, 22f), 13f, TextAlignmentOptions.Left);
            header.text = "── Tax Rates ──"; y -= 26f;

            (uiManager.ResTaxSlider, uiManager.ResTaxLabel) = MakeTaxRow(parent, "ResTax", ref y, "Residential", new Color(0.2f, 0.8f, 0.3f));
            (uiManager.ComTaxSlider, uiManager.ComTaxLabel) = MakeTaxRow(parent, "ComTax", ref y, "Commercial",  new Color(0.2f, 0.4f, 0.9f));
            (uiManager.IndTaxSlider, uiManager.IndTaxLabel) = MakeTaxRow(parent, "IndTax", ref y, "Industrial",  new Color(0.9f, 0.7f, 0.1f));
        }

        private static (Slider slider, TextMeshProUGUI label) MakeTaxRow(
            GameObject parent, string name, ref float y, string rowLabel, Color fillColor)
        {
            MakeLabel(parent, name + "_lbl", new Vector2(6f, y), new Vector2(100f, 18f), 11f, TextAlignmentOptions.Left).text = rowLabel;
            var valLbl = MakeLabel(parent, name + "_val", new Vector2(108f, y), new Vector2(50f, 18f), 11f);
            valLbl.text = "8%";
            y -= 20f;

            // Build a compact slider directly on the panel
            var sliderGO = new GameObject(name + "_Slider");
            sliderGO.transform.SetParent(parent.transform, false);
            var rt = sliderGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.offsetMin = new Vector2(6f, y - 18f); rt.offsetMax = new Vector2(-6f, y);
            rt.sizeDelta = new Vector2(0f, 18f);

            // BG
            var bg = new GameObject("BG"); bg.transform.SetParent(sliderGO.transform, false);
            var bgRT = bg.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
            bg.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);

            // Fill area
            var fa = new GameObject("FA"); fa.transform.SetParent(sliderGO.transform, false);
            var faRT = fa.AddComponent<RectTransform>();
            faRT.anchorMin = new Vector2(0f, 0.1f); faRT.anchorMax = new Vector2(1f, 0.9f);
            faRT.offsetMin = new Vector2(2f, 0f); faRT.offsetMax = new Vector2(-2f, 0f);
            var fill = new GameObject("Fill"); fill.transform.SetParent(fa.transform, false);
            var fillRT = fill.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
            fill.AddComponent<Image>().color = fillColor;

            var slider = sliderGO.AddComponent<Slider>();
            slider.minValue  = 0f;
            slider.maxValue  = 30f;
            slider.value     = 8f;
            slider.wholeNumbers = true;
            slider.fillRect  = fillRT;

            y -= 22f;
            return (slider, valLbl);
        }

        // ── Tile info panel ───────────────────────────────────────────────────

        private static void AddTileInfoPanel(GameObject parent, UIManager uiManager)
        {
            var panelGO = new GameObject("TileInfoPanel");
            panelGO.transform.SetParent(parent.transform, false);
            panelGO.SetActive(false);   // hidden by default
            var rt = panelGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 40f);
            rt.sizeDelta = new Vector2(0f, 200f);
            panelGO.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.1f, 0.95f);

            var lblGO = new GameObject("TileInfoText");
            lblGO.transform.SetParent(panelGO.transform, false);
            var lRT = lblGO.AddComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
            lRT.offsetMin = new Vector2(6f, 4f); lRT.offsetMax = new Vector2(-6f, -4f);
            var tmp = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 11f; tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.TopLeft;

            uiManager.TileInfoPanel = panelGO;
            uiManager.TileInfoText  = tmp;
        }

        // ── Utility helpers ───────────────────────────────────────────────────

        private static void DestroyExisting(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        /// <summary>Sets a public field by name using reflection.</summary>
        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(target, value);
            else
                Debug.LogWarning($"[SceneSetup] Field '{fieldName}' not found on {target.GetType().Name}");
        }
    }
}
#endif
