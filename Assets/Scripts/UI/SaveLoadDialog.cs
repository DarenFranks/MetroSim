// =============================================================================
// SaveLoadDialog.cs  –  Modal dialog for saving and loading city files.
//
// Self-bootstraps via RuntimeInitializeOnLoadMethod – no scene setup needed.
// Usage:
//   SaveLoadDialog.Instance.ShowSave();
//   SaveLoadDialog.Instance.ShowLoad();
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MetroSim
{
    public class SaveLoadDialog : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static SaveLoadDialog Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (Instance != null) return;
            var go = new GameObject("[SaveLoadDialog]");
            DontDestroyOnLoad(go);
            go.AddComponent<SaveLoadDialog>();
        }

        // ── Colors ────────────────────────────────────────────────────────────
        private static readonly Color32 OVERLAY_BG  = new Color32(0,  0,  0,  180);
        private static readonly Color32 PANEL_BG    = new Color32(28, 32, 40, 255);
        private static readonly Color32 HEADER_BG   = new Color32(18, 22, 30, 255);
        private static readonly Color32 ROW_EVEN    = new Color32(32, 36, 46, 255);
        private static readonly Color32 ROW_ODD     = new Color32(38, 42, 52, 255);
        private static readonly Color32 ROW_HOVER   = new Color32(50, 80, 140, 255);
        private static readonly Color32 BTN_SAVE    = new Color32(30, 100, 200, 255);
        private static readonly Color32 BTN_CANCEL  = new Color32(80,  80,  90, 255);
        private static readonly Color32 BTN_DELETE  = new Color32(160, 40,  40, 255);
        private static readonly Color32 BTN_LOAD    = new Color32(40, 130,  60, 255);
        private static readonly Color32 SEP_COLOR   = new Color32(60, 64, 80, 255);

        // ── Internal UI references ────────────────────────────────────────────
        private GameObject _root;           // full-screen overlay
        private GameObject _savePanel;
        private GameObject _loadPanel;
        private TMP_InputField _saveNameInput;
        private Transform _saveList;        // parent for existing-save rows
        private Transform _loadList;        // parent for load-slot rows
        private bool _built = false;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start() => EnsureBuilt();

        // ── Public API ────────────────────────────────────────────────────────

        public void ShowSave()
        {
            EnsureBuilt();
            RefreshSaveList();
            // Pre-fill with current city name
            if (_saveNameInput != null)
                _saveNameInput.text = GameManager.Instance?.CityName ?? "My City";
            _root.SetActive(true);
            _savePanel.SetActive(true);
            _loadPanel.SetActive(false);
        }

        public void ShowLoad()
        {
            EnsureBuilt();
            RefreshLoadList();
            _root.SetActive(true);
            _savePanel.SetActive(false);
            _loadPanel.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        // ── Build UI ──────────────────────────────────────────────────────────
        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var cgo = new GameObject("DialogCanvas");
                DontDestroyOnLoad(cgo);
                var c = cgo.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 200;
                cgo.AddComponent<CanvasScaler>();
                cgo.AddComponent<GraphicRaycaster>();
                canvas = c;
            }

            // Full-screen clickable overlay
            _root = CreateRect("SaveLoadOverlay", canvas.transform);
            SetAnchors(_root, Vector2.zero, Vector2.one);
            var rootImg = _root.AddComponent<Image>();
            rootImg.color = OVERLAY_BG;
            _root.SetActive(false);

            // Bring to front
            _root.GetComponent<RectTransform>().SetAsLastSibling();

            // Build both panels
            _savePanel = BuildSavePanel(_root.transform);
            _loadPanel = BuildLoadPanel(_root.transform);
        }

        // ─────────────────────────────── SAVE PANEL ──────────────────────────

        private GameObject BuildSavePanel(Transform parent)
        {
            var panel = CreateCentredPanel(parent, "SavePanel", 420, 460);

            // Header
            CreateHeader(panel.transform, "Save City");

            // Body
            var body = CreateRect("Body", panel.transform);
            SetAnchors(body, new Vector2(0, 0), new Vector2(1, 1));
            body.GetComponent<RectTransform>().offsetMin = new Vector2(0, 56);   // above footer
            body.GetComponent<RectTransform>().offsetMax = new Vector2(0, -44);  // below header
            var bodyVL = body.AddComponent<VerticalLayoutGroup>();
            bodyVL.padding = new RectOffset(16, 16, 12, 8);
            bodyVL.spacing = 10;
            bodyVL.childControlWidth = true;
            bodyVL.childControlHeight = false;
            bodyVL.childForceExpandWidth = true;

            // Label
            var lbl = CreateLabel(body.transform, "Save name:");
            lbl.GetComponent<LayoutElement>().preferredHeight = 20;

            // Input field row
            var inputRow = CreateRect("InputRow", body.transform);
            inputRow.AddComponent<LayoutElement>().preferredHeight = 34;
            inputRow.AddComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            _saveNameInput = CreateInputField(inputRow.transform, "City name");

            // Divider label
            var divLbl = CreateLabel(body.transform, "Overwrite existing save:");
            divLbl.GetComponent<LayoutElement>().preferredHeight = 20;

            // Scroll view for existing saves
            var scroll = CreateScrollView(body.transform, 200);
            _saveList = scroll.GetComponentInChildren<VerticalLayoutGroup>()?.transform
                        ?? scroll.transform;

            // Footer
            CreateSaveFooter(panel.transform);

            return panel;
        }

        private void CreateSaveFooter(Transform parent)
        {
            var footer = CreateRect("Footer", parent);
            SetAnchors(footer, new Vector2(0, 0), new Vector2(1, 0));
            var footerRT = footer.GetComponent<RectTransform>();
            footerRT.pivot = new Vector2(0.5f, 0);
            footerRT.sizeDelta = new Vector2(0, 52);
            footerRT.anchoredPosition = Vector2.zero;

            var bg = footer.AddComponent<Image>();
            bg.color = HEADER_BG;

            var hl = footer.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(16, 16, 10, 10);
            hl.spacing = 10;
            hl.childAlignment = TextAnchor.MiddleRight;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = true;

            // Spacer
            var spacer = CreateRect("Spacer", footer.transform);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Cancel
            var cancelBtn = CreateButton(footer.transform, "Cancel", BTN_CANCEL, 90, 32);
            cancelBtn.onClick.AddListener(Hide);

            // Save
            var saveBtn = CreateButton(footer.transform, "Save", BTN_SAVE, 90, 32);
            saveBtn.onClick.AddListener(OnSaveConfirmed);
        }

        private void OnSaveConfirmed()
        {
            string name = _saveNameInput?.text?.Trim();
            if (string.IsNullOrEmpty(name)) name = GameManager.Instance?.CityName ?? "save";
            GameManager.Instance?.SaveLoad?.SaveToSlot(name);
            Hide();
        }

        private void RefreshSaveList()
        {
            if (_saveList == null) return;
            foreach (Transform child in _saveList) Destroy(child.gameObject);

            var saves = GameManager.Instance?.SaveLoad?.GetAllSaves();
            if (saves == null || saves.Length == 0)
            {
                CreateLabel(_saveList, "  No saves yet.").color = new Color(0.55f, 0.55f, 0.6f);
                return;
            }

            bool odd = false;
            foreach (var info in saves)
            {
                string slotName = info.name;
                var row = CreateRect("SaveRow_" + slotName, _saveList);
                var rowImg = row.AddComponent<Image>();
                rowImg.color = odd ? ROW_ODD : ROW_EVEN;
                odd = !odd;

                var le = row.AddComponent<LayoutElement>();
                le.preferredHeight = 28;

                var hl = row.AddComponent<HorizontalLayoutGroup>();
                hl.padding = new RectOffset(8, 6, 0, 0);
                hl.spacing = 6;
                hl.childForceExpandWidth = false;
                hl.childForceExpandHeight = true;
                hl.childAlignment = TextAnchor.MiddleLeft;

                // Name label
                var nameLbl = CreateLabel(row.transform, slotName);
                nameLbl.fontSize = 11;
                nameLbl.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 0);
                var nameLE = nameLbl.gameObject.GetComponent<LayoutElement>()
                             ?? nameLbl.gameObject.AddComponent<LayoutElement>();
                nameLE.preferredWidth = 140;

                // Date label
                var dateLbl = CreateLabel(row.transform, info.displayDate);
                dateLbl.fontSize = 10;
                dateLbl.color = new Color(0.65f, 0.65f, 0.7f);
                var dateLE = dateLbl.gameObject.GetComponent<LayoutElement>()
                             ?? dateLbl.gameObject.AddComponent<LayoutElement>();
                dateLE.flexibleWidth = 1;

                // Overwrite button
                var overBtn = CreateButton(row.transform, "Select", BTN_SAVE, 64, 22);
                overBtn.onClick.AddListener(() =>
                {
                    if (_saveNameInput != null) _saveNameInput.text = slotName;
                });
            }
        }

        // ─────────────────────────────── LOAD PANEL ──────────────────────────

        private GameObject BuildLoadPanel(Transform parent)
        {
            var panel = CreateCentredPanel(parent, "LoadPanel", 460, 460);

            CreateHeader(panel.transform, "Load City");

            var body = CreateRect("Body", panel.transform);
            SetAnchors(body, new Vector2(0, 0), new Vector2(1, 1));
            body.GetComponent<RectTransform>().offsetMin = new Vector2(0, 56);
            body.GetComponent<RectTransform>().offsetMax = new Vector2(0, -44);

            var scroll = CreateScrollView(body.transform, -1); // fill
            _loadList = scroll.GetComponentInChildren<VerticalLayoutGroup>()?.transform
                        ?? scroll.transform;

            // Footer
            var footer = CreateRect("Footer", panel.transform);
            SetAnchors(footer, new Vector2(0, 0), new Vector2(1, 0));
            var footerRT = footer.GetComponent<RectTransform>();
            footerRT.pivot = new Vector2(0.5f, 0);
            footerRT.sizeDelta = new Vector2(0, 52);
            footerRT.anchoredPosition = Vector2.zero;
            var footerBG = footer.AddComponent<Image>();
            footerBG.color = HEADER_BG;

            var hl = footer.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(16, 16, 10, 10);
            hl.spacing = 10;
            hl.childAlignment = TextAnchor.MiddleRight;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = true;

            var spacer = CreateRect("Spacer", footer.transform);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1;

            var cancelBtn = CreateButton(footer.transform, "Cancel", BTN_CANCEL, 90, 32);
            cancelBtn.onClick.AddListener(Hide);

            return panel;
        }

        private void RefreshLoadList()
        {
            if (_loadList == null) return;
            foreach (Transform child in _loadList) Destroy(child.gameObject);

            var saves = GameManager.Instance?.SaveLoad?.GetAllSaves();
            if (saves == null || saves.Length == 0)
            {
                CreateLabel(_loadList, "  No save files found.").color = new Color(0.55f, 0.55f, 0.6f);
                return;
            }

            bool odd = false;
            foreach (var info in saves)
            {
                string slotName = info.name;
                var row = CreateRect("LoadRow_" + slotName, _loadList);
                var rowImg = row.AddComponent<Image>();
                rowImg.color = odd ? ROW_ODD : ROW_EVEN;
                odd = !odd;

                var le = row.AddComponent<LayoutElement>();
                le.preferredHeight = 32;

                var hl = row.AddComponent<HorizontalLayoutGroup>();
                hl.padding = new RectOffset(8, 6, 2, 2);
                hl.spacing = 6;
                hl.childForceExpandWidth = false;
                hl.childForceExpandHeight = true;
                hl.childAlignment = TextAnchor.MiddleLeft;

                // Name label
                var nameLbl = CreateLabel(row.transform, slotName);
                nameLbl.fontSize = 12;
                var nameLE = nameLbl.gameObject.AddComponent<LayoutElement>();
                nameLE.preferredWidth = 160;

                // Date label
                var dateLbl = CreateLabel(row.transform, info.displayDate);
                dateLbl.fontSize = 10;
                dateLbl.color = new Color(0.65f, 0.65f, 0.7f);
                var dateLE = dateLbl.gameObject.AddComponent<LayoutElement>();
                dateLE.flexibleWidth = 1;

                // Load button
                var loadBtn = CreateButton(row.transform, "Load", BTN_LOAD, 60, 24);
                loadBtn.onClick.AddListener(() =>
                {
                    GameManager.Instance?.SaveLoad?.LoadFromSlot(slotName);
                    Hide();
                });

                // Delete button
                var delBtn = CreateButton(row.transform, "Del", BTN_DELETE, 40, 24);
                delBtn.onClick.AddListener(() =>
                {
                    GameManager.Instance?.SaveLoad?.DeleteSlot(slotName);
                    RefreshLoadList();
                });
            }
        }

        // ── Shared UI helpers ─────────────────────────────────────────────────

        private GameObject CreateCentredPanel(Transform parent, string name, float w, float h)
        {
            var panel = CreateRect(name, parent);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
            var bg = panel.AddComponent<Image>();
            bg.color = PANEL_BG;
            return panel;
        }

        private void CreateHeader(Transform parent, string title)
        {
            var header = CreateRect("Header", parent);
            SetAnchors(header, new Vector2(0, 1), new Vector2(1, 1));
            var headerRT = header.GetComponent<RectTransform>();
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.sizeDelta = new Vector2(0, 44);
            headerRT.anchoredPosition = Vector2.zero;
            var headerBG = header.AddComponent<Image>();
            headerBG.color = HEADER_BG;

            var lbl = CreateLabel(header.transform, title);
            lbl.fontSize = 16;
            lbl.fontStyle = FontStyles.Bold;
            var lblRT = lbl.rectTransform;
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = new Vector2(16, 0);
            lblRT.offsetMax = new Vector2(-16, 0);
            lbl.alignment = TextAlignmentOptions.MidlineLeft;

            // Separator line
            var sep = CreateRect("Sep", header.transform);
            SetAnchors(sep, new Vector2(0, 0), new Vector2(1, 0));
            sep.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 1);
            sep.AddComponent<Image>().color = SEP_COLOR;
        }

        private GameObject CreateScrollView(Transform parent, float height)
        {
            var sv = CreateRect("ScrollView", parent);
            if (height < 0)
            {
                SetAnchors(sv, Vector2.zero, Vector2.one);
            }
            else
            {
                sv.AddComponent<LayoutElement>().preferredHeight = height;
                var le = sv.GetComponent<LayoutElement>();
                le.flexibleHeight = 0;
            }

            var scrollBG = sv.AddComponent<Image>();
            scrollBG.color = new Color32(22, 25, 32, 255);

            var scrollRect = sv.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            // Viewport
            var viewport = CreateRect("Viewport", sv.transform);
            SetAnchors(viewport, Vector2.zero, Vector2.one);
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();

            // Content
            var content = CreateRect("Content", viewport.transform);
            SetAnchors(content, new Vector2(0, 1), new Vector2(1, 1));
            var contentRT = content.GetComponent<RectTransform>();
            contentRT.pivot = new Vector2(0, 1);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0, 0);

            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vl = content.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(2, 2, 2, 2);
            vl.spacing = 1;
            vl.childControlWidth = true;
            vl.childControlHeight = false;
            vl.childForceExpandWidth = true;

            scrollRect.content = contentRT;

            return sv;
        }

        private TMP_InputField CreateInputField(Transform parent, string placeholder)
        {
            var go = CreateRect("InputField", parent);
            var bg = go.AddComponent<Image>();
            bg.color = new Color32(20, 22, 28, 255);

            var field = go.AddComponent<TMP_InputField>();

            // Text area
            var textAreaGO = CreateRect("Text Area", go.transform);
            SetAnchors(textAreaGO, Vector2.zero, Vector2.one);
            textAreaGO.AddComponent<RectMask2D>();
            var textAreaRT = textAreaGO.GetComponent<RectTransform>();
            textAreaRT.offsetMin = new Vector2(8, 4);
            textAreaRT.offsetMax = new Vector2(-8, -4);

            // Placeholder
            var phGO = CreateRect("Placeholder", textAreaGO.transform);
            SetAnchors(phGO, Vector2.zero, Vector2.one);
            var phText = phGO.AddComponent<TextMeshProUGUI>();
            phText.text = placeholder;
            phText.fontSize = 12;
            phText.color = new Color(0.5f, 0.5f, 0.55f);
            phText.fontStyle = FontStyles.Italic;

            // Text
            var txtGO = CreateRect("Text", textAreaGO.transform);
            SetAnchors(txtGO, Vector2.zero, Vector2.one);
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.fontSize = 12;
            txt.color = Color.white;
            txt.enableWordWrapping = false;

            field.textViewport = textAreaRT;
            field.textComponent = txt;
            field.placeholder = phText;

            return field;
        }

        private Button CreateButton(Transform parent, string label, Color32 color, float w, float h)
        {
            var go = CreateRect("Btn_" + label, parent);
            var img = go.AddComponent<Image>();
            img.color = color;

            var btn = go.AddComponent<Button>();
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;

            var colors = btn.colors;
            var hoverCol = new Color(color.r / 255f + 0.12f, color.g / 255f + 0.12f, color.b / 255f + 0.12f);
            colors.normalColor      = color;
            colors.highlightedColor = hoverCol;
            colors.pressedColor     = new Color(color.r / 255f - 0.1f, color.g / 255f - 0.1f, color.b / 255f - 0.1f);
            colors.selectedColor    = color;
            colors.fadeDuration     = 0.05f;
            btn.colors = colors;

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth  = w;
            le.preferredHeight = h;
            le.minWidth  = w;
            le.minHeight = h;
            le.flexibleWidth = 0;

            var lbl = CreateLabel(go.transform, label);
            lbl.fontSize  = 11;
            lbl.alignment = TextAlignmentOptions.Center;
            var lblRT = lbl.rectTransform;
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = new Vector2(2, 0);
            lblRT.offsetMax = new Vector2(-2, 0);

            return btn;
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string text)
        {
            var go = CreateRect("Label", parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = 12;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.enableWordWrapping = false;
            return tmp;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void SetAnchors(GameObject go, Vector2 min, Vector2 max)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin  = min;
            rt.anchorMax  = max;
            rt.pivot      = new Vector2(0.5f, 0.5f);
            rt.offsetMin  = Vector2.zero;
            rt.offsetMax  = Vector2.zero;
        }
    }
}
