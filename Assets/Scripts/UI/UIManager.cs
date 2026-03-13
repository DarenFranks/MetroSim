// =============================================================================
// UIManager.cs  –  Drives all UGUI panels: stats, demand bars, build menu,
//                   overlay selector, budget, tile info, speed controls.
//
// UI structure (expected in scene):
//   Canvas
//     TopBar
//       CityNameText, PopulationText, DateText, FundsText, IncomeText
//       DemandBars: ResBar, ComBar, IndBar (sliders)
//       SpeedButtons: PauseBtn, Speed1Btn, Speed2Btn, Speed3Btn
//     LeftPanel
//       ToolButtons (each has a "tool" tag = tool key)
//     RightPanel
//       OverlayButtons (each has an "overlay" tag)
//       StatsPanel: stat TextMeshPro labels
//       BudgetPanel: tax sliders
//       TileInfoPanel
//       ThemeDropdown
//       SaveBtn, LoadBtn, NewBtn
//     NotificationArea
//
// If any UI element is not found, it is silently skipped (no null-ref crash).
// =============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MetroSim
{
    public class UIManager : MonoBehaviour
    {
        // ── UI references (assigned by SceneSetup or inspector) ───────────────
        [Header("Top Bar")]
        public TextMeshProUGUI CityNameText;
        public TextMeshProUGUI PopulationText;
        public TextMeshProUGUI DateText;
        public TextMeshProUGUI FundsText;
        public TextMeshProUGUI IncomeText;

        [Header("Demand Bars (0-1 fill amounts)")]
        public Slider ResBar;
        public Slider ComBar;
        public Slider IndBar;

        [Header("Stats Panel")]
        public TextMeshProUGUI StatPop;
        public TextMeshProUGUI StatFunds;
        public TextMeshProUGUI StatIncome;
        public TextMeshProUGUI StatExpenses;
        public TextMeshProUGUI StatPower;
        public TextMeshProUGUI StatWater;
        public TextMeshProUGUI StatSewer;
        public TextMeshProUGUI StatHappiness;

        [Header("Budget Sliders")]
        public Slider ResTaxSlider;
        public Slider ComTaxSlider;
        public Slider IndTaxSlider;
        public TextMeshProUGUI ResTaxLabel;
        public TextMeshProUGUI ComTaxLabel;
        public TextMeshProUGUI IndTaxLabel;

        [Header("Tile Info")]
        public GameObject     TileInfoPanel;
        public TextMeshProUGUI TileInfoText;

        [Header("Theme")]
        public TMP_Dropdown ThemeDropdown;

        [Header("Notifications")]
        public NotificationUI Notifications;

        // ── Init ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Wire up tax sliders
            if (ResTaxSlider) ResTaxSlider.onValueChanged.AddListener(v => OnTaxChanged());
            if (ComTaxSlider) ComTaxSlider.onValueChanged.AddListener(v => OnTaxChanged());
            if (IndTaxSlider) IndTaxSlider.onValueChanged.AddListener(v => OnTaxChanged());

            // Wire theme dropdown
            if (ThemeDropdown) ThemeDropdown.onValueChanged.AddListener(OnThemeChanged);

            // Subscribe to game events
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnNotification += msg => Notifications?.Show(msg);
                gm.OnSimTick      += Refresh;
            }
        }

        // ── Runtime button wiring ──────────────────────────────────────────────
        // SceneSetup's btn.onClick.AddListener() calls are runtime-only and are
        // NOT serialized to the scene file. We re-wire every button here in
        // Start() so they work correctly the moment Play begins.

        private void Start()
        {
            WireAllButtons();

            // Also hook up game events here in case Awake ran before GameManager
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnNotification -= msg => Notifications?.Show(msg); // avoid duplicate
                gm.OnNotification += msg => Notifications?.Show(msg);
                gm.OnSimTick      -= Refresh;
                gm.OnSimTick      += Refresh;
            }
        }

        private void WireAllButtons()
        {
            var allButtons = GetComponentsInChildren<Button>(true);
            foreach (var btn in allButtons)
            {
                string n = btn.gameObject.name;

                // Tool buttons: named "{toolKey}_btn"
                if (n.EndsWith("_btn") && !n.EndsWith("_ovr_btn"))
                {
                    string toolKey = n.Substring(0, n.Length - 4);
                    btn.onClick.AddListener(() => OnToolButtonClicked(toolKey));
                }
                // Overlay buttons: named "{overlayKey}_ovr_btn"
                else if (n.EndsWith("_ovr_btn"))
                {
                    string ovKey = n.Substring(0, n.Length - 8);
                    btn.onClick.AddListener(() => OnOverlayButtonClicked(ovKey));
                }
                else
                {
                    switch (n)
                    {
                        case "Pause":   btn.onClick.AddListener(SetSpeedPause);  break;
                        case "Speed1":  btn.onClick.AddListener(SetSpeed1);      break;
                        case "Speed2":  btn.onClick.AddListener(SetSpeed2);      break;
                        case "Speed3":  btn.onClick.AddListener(SetSpeed3);      break;
                        case "SaveBtn": btn.onClick.AddListener(OnSaveClicked);  break;
                        case "LoadBtn": btn.onClick.AddListener(OnLoadClicked);  break;
                        case "NewBtn":  btn.onClick.AddListener(OnNewClicked);   break;
                    }
                }
            }
        }

        // ── Refresh (called every tick) ────────────────────────────────────────

        public void Refresh()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            var eco  = gm.Economy;
            var pw   = gm.Power;
            var wt   = gm.Water;
            var sw   = gm.Sewer;
            var zm   = gm.Zones;
            var dem  = gm.Demand;

            // Top bar
            SetText(CityNameText, gm.CityName);
            SetText(PopulationText, $"Pop: {zm.TotalPopulation:N0}");
            SetText(DateText, gm.DateString());
            SetText(FundsText, $"${eco.Funds:N0}");

            float net = eco.NetIncome;
            if (IncomeText)
            {
                IncomeText.text  = net >= 0 ? $"+${net:N0}/yr" : $"-${Mathf.Abs(net):N0}/yr";
                IncomeText.color = net >= 0 ? Color.green : Color.red;
            }

            // Demand bars (slider value = 0-1 mapped from -1..+1 range)
            SetSlider(ResBar, (dem.ResDisplay + 1f) * 0.5f);
            SetSlider(ComBar, (dem.ComDisplay + 1f) * 0.5f);
            SetSlider(IndBar, (dem.IndDisplay + 1f) * 0.5f);

            // Stats panel
            SetText(StatPop,      $"{zm.TotalPopulation:N0}");
            SetText(StatFunds,    $"${eco.Funds:N0}");
            SetText(StatIncome,   $"${eco.AnnualIncome:N0}/yr");
            SetText(StatExpenses, $"${eco.AnnualExpenses:N0}/yr");
            SetText(StatPower,    $"{pw.TotalDemand:N0}/{pw.TotalGeneration:N0} MW");
            SetText(StatWater,    $"{wt.TotalDemand:N0}/{wt.TotalProduction:N0} m³");
            SetText(StatSewer,    $"{sw.TotalLoad:N0}/{sw.TotalCapacity:N0} m³");
            SetText(StatHappiness,$"{zm.TotalHappiness}%");

            // Colour warnings
            if (StatPower)  StatPower.color  = pw.IsInShortage    ? Color.red : Color.white;
            if (StatWater)  StatWater.color  = wt.IsInShortage    ? Color.red : Color.white;
            if (StatSewer)  StatSewer.color  = sw.IsOverloaded    ? Color.red : Color.white;

            // Budget labels
            if (ResTaxLabel && ResTaxSlider) ResTaxLabel.text = $"{ResTaxSlider.value:F0}%";
            if (ComTaxLabel && ComTaxSlider) ComTaxLabel.text = $"{ComTaxSlider.value:F0}%";
            if (IndTaxLabel && IndTaxSlider) IndTaxLabel.text = $"{IndTaxSlider.value:F0}%";
        }

        // ── Tile info ─────────────────────────────────────────────────────────

        public void ShowTileInfo(TileData tile)
        {
            if (TileInfoPanel == null || TileInfoText == null) return;
            TileInfoPanel.SetActive(true);

            var tm  = GameManager.Instance?.Themes?.ActiveTheme;
            string buildingName = tile.HasBuilding
                ? (tm?.GetBuildingName(tile.Building) ?? tile.Building.ToString())
                : "None";

            TileInfoText.text =
                $"Tile ({tile.X}, {tile.Y})\n" +
                $"Terrain: {tile.Terrain}\n" +
                $"Zone: {tile.Zone}  Density: {tile.Density}\n" +
                $"Building: {buildingName}\n" +
                $"Occupants: {tile.Occupants}\n" +
                $"Happiness: {tile.Happiness:F0}%\n" +
                $"Land Value: ${tile.LandValue:F0}\n" +
                $"Power: {(tile.HasPower?"✓":"✗")}  " +
                $"Water: {(tile.HasWater?"✓":"✗")}  " +
                $"Sewer: {(tile.HasSewer?"✓":"✗")}\n" +
                $"Air Pollution: {tile.AirPollution:F0}\n" +
                $"Traffic: {tile.TrafficDensity:P0}\n" +
                $"Road: {tile.Road}  RoadAccess: {tile.HasRoadAccess}";
        }

        public void HideTileInfo() => TileInfoPanel?.SetActive(false);

        // ── Tool button callbacks (called from buttons in scene) ──────────────

        public void OnToolButtonClicked(string tool)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            gm.ActiveTool = tool;
        }

        // ── Overlay callbacks ─────────────────────────────────────────────────

        public void OnOverlayButtonClicked(string overlayName)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (System.Enum.TryParse<OverlayType>(overlayName, true, out var ot))
            {
                gm.ActiveOverlay = ot;
                gm.OverlayRend?.SetOverlay(ot);
            }
        }

        // ── Speed callbacks ────────────────────────────────────────────────────

        public void SetSpeedPause()  => GameManager.Instance?.SimEngine?.Pause();
        public void SetSpeed1()      => GameManager.Instance?.SimEngine?.SetNormal();
        public void SetSpeed2()      => GameManager.Instance?.SimEngine?.SetFast();
        public void SetSpeed3()      => GameManager.Instance?.SimEngine?.SetUltraFast();

        // ── Save / Load / New callbacks ───────────────────────────────────────

        public void OnSaveClicked()  => SaveLoadDialog.Instance?.ShowSave();
        public void OnLoadClicked()  => SaveLoadDialog.Instance?.ShowLoad();
        public void OnNewClicked()   => GameManager.Instance?.NewCity(0);

        // ── Private ───────────────────────────────────────────────────────────

        private void OnTaxChanged()
        {
            var eco = GameManager.Instance?.Economy;
            if (eco == null) return;
            if (ResTaxSlider) eco.ResidentialTaxRate = ResTaxSlider.value / 100f;
            if (ComTaxSlider) eco.CommercialTaxRate  = ComTaxSlider.value / 100f;
            if (IndTaxSlider) eco.IndustrialTaxRate  = IndTaxSlider.value / 100f;
        }

        private void OnThemeChanged(int idx)
        {
            if (ThemeDropdown == null) return;
            string key = ThemeDropdown.options[idx].text.ToLower().Replace(" ","_");
            GameManager.Instance?.Themes?.SetTheme(key);
        }

        private void SetText(TextMeshProUGUI t, string v) { if (t) t.text = v; }
        private void SetSlider(Slider s, float v)         { if (s) s.value = v; }
    }
}
