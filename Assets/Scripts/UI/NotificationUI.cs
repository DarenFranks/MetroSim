// =============================================================================
// NotificationUI.cs  –  Toast-style notification queue.
//
// Shows a stack of short-lived text banners at the bottom of the screen.
// New messages push older ones up; each fades out after DISPLAY_SECONDS.
// UIManager holds a reference and calls Show(msg) on game events.
// =============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MetroSim
{
    public class NotificationUI : MonoBehaviour
    {
        [Header("Settings")]
        public int   MaxVisible     = 4;
        public float DisplaySeconds = 4f;
        public float FadeSeconds    = 0.5f;

        // Prefab: a simple RectTransform with a TextMeshProUGUI child and an
        // optional Image background.  Created at runtime if not assigned.
        [Header("Prefab (optional – auto-created if null)")]
        public GameObject ToastPrefab;

        // ── Runtime state ─────────────────────────────────────────────────────
        private readonly Queue<string>         _queue    = new Queue<string>();
        private readonly List<ActiveToast>     _active   = new List<ActiveToast>();
        private VerticalLayoutGroup            _layout;

        private struct ActiveToast
        {
            public GameObject   Root;
            public TextMeshProUGUI Label;
            public CanvasGroup  Group;
            public float        Remaining;   // seconds until fade starts
        }

        // ── Init ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Ensure we have a VerticalLayoutGroup so toasts stack nicely
            _layout = GetComponent<VerticalLayoutGroup>();
            if (_layout == null)
            {
                _layout = gameObject.AddComponent<VerticalLayoutGroup>();
                _layout.childControlHeight  = false;
                _layout.childControlWidth   = true;
                _layout.childForceExpandWidth  = true;
                _layout.childForceExpandHeight = false;
                _layout.spacing = 4f;
                _layout.padding = new RectOffset(4, 4, 4, 4);
                _layout.childAlignment = TextAnchor.LowerCenter;
                _layout.reverseArrangement = false;
            }

            // Build prefab at runtime if none provided
            if (ToastPrefab == null)
                ToastPrefab = BuildDefaultPrefab();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Queue a new notification message.</summary>
        public void Show(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            // If already at max, expire the oldest visible one immediately
            if (_active.Count >= MaxVisible && _active.Count > 0)
            {
                var oldest = _active[0];
                oldest.Remaining = 0f;
                _active[0] = oldest;
            }

            SpawnToast(message);
        }

        // ── Update ────────────────────────────────────────────────────────────

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var t = _active[i];
                t.Remaining -= dt;

                if (t.Remaining <= -FadeSeconds)
                {
                    // Fully expired – destroy
                    if (t.Root != null) Destroy(t.Root);
                    _active.RemoveAt(i);
                }
                else if (t.Remaining <= 0f)
                {
                    // Fading out
                    float alpha = Mathf.Clamp01((t.Remaining + FadeSeconds) / FadeSeconds);
                    if (t.Group != null) t.Group.alpha = alpha;
                    _active[i] = t;
                }
                else
                {
                    _active[i] = t;
                }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SpawnToast(string message)
        {
            GameObject go = Instantiate(ToastPrefab, transform);

            var group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            group.alpha = 1f;

            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = message;

            var toast = new ActiveToast
            {
                Root      = go,
                Label     = label,
                Group     = group,
                Remaining = DisplaySeconds
            };
            _active.Add(toast);
        }

        private GameObject BuildDefaultPrefab()
        {
            var go = new GameObject("Toast");
            go.SetActive(false);    // prefab stays inactive

            // RectTransform sizing
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 36f);

            // Dark semi-transparent background
            var img = go.AddComponent<Image>();
            img.color = new Color(0.05f, 0.05f, 0.05f, 0.82f);

            // Label child
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);

            var lrt = labelGO.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(6f, 2f);
            lrt.offsetMax = new Vector2(-6f, -2f);

            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.fontSize  = 13f;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            // CanvasGroup for alpha fade
            go.AddComponent<CanvasGroup>();

            // LayoutElement so VerticalLayoutGroup respects the fixed height
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 36f;
            le.flexibleWidth   = 1f;

            return go;
        }
    }
}
