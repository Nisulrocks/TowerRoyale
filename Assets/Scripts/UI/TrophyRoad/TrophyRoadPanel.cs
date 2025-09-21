using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;
using TR.Data.Progression;

namespace TR.UI.TrophyRoad
{
    public class TrophyRoadPanel : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] private GameObject root; // enable/disable to show/hide

        [Header("Header UI")]
        [SerializeField] private TMP_Text trophiesText;
        [SerializeField] private TMP_Text nextMilestoneText;
        [SerializeField] private Slider progressSlider; // place this as a child of 'content' so it scrolls and stretches across the road

        [Header("Scroll Strip")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content; // horizontal content area (its width scales with max trophies)
        [SerializeField] private TrophyRoadNode nodePrefab;
        [SerializeField] private float contentPadding = 64f; // padding left/right
        [SerializeField] private float pixelsPerTrophy = 2f; // how wide the road is per trophy point
        [SerializeField] private float minContentWidth = 1500f; // ensure reasonable width even at low caps
        [Header("Unified Sizing")]
        [Tooltip("Global scale that affects both slider height and node size.")]
        [SerializeField] private float sizeScale = 1f;
        [Tooltip("Base slider height in pixels (before scaling).")]
        [SerializeField] private float baseSliderHeight = 24f;
        [Tooltip("Base node width in pixels (before scaling).")]
        [SerializeField] private float baseNodeWidth = 110f;
        [Tooltip("Base node height in pixels (before scaling).")]
        [SerializeField] private float baseNodeHeight = 80f;
        [SerializeField] private float nodeVerticalOffset = 0f; // offset from slider center

        private readonly List<TrophyRoadNode> _nodes = new();
        private readonly List<RectTransform> _markers = new();
        private readonly List<TMPro.TMP_Text> _markerLabels = new();
        private TrophyRoadDefinition _road;

        private void Awake()
        {
            if (root == null) root = gameObject;
            EnsureScrollBindings();
        }

        public void Show()
        {
            if (_road == null) _road = GameDB.GetTrophyRoad();
            if (_road == null)
            {
                Debug.LogWarning("[TrophyRoadPanel] No Trophy Road configured in Resources/Progression");
                return;
            }
            if (root != null && !root.activeSelf) root.SetActive(true);
            EnsureScrollBindings();
            BuildOrRefresh();
            AutoScrollToNext();
        }

        public void Hide()
        {
            if (root != null && root.activeSelf) root.SetActive(false);
        }

        public void ForceRefresh() => BuildOrRefresh();

        // Hook this to a UI Button on the panel header to close the Trophy Road
        public void OnClickClose()
        {
            Hide();
        }

        private void Update()
        {
            // Allow closing with Escape while the panel is visible
            if (root != null && root.activeInHierarchy)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) Hide();
            }
        }

        private void BuildOrRefresh()
        {
            if (_road == null) _road = GameDB.GetTrophyRoad();
            if (_road == null) return;

            int trophies = PlayerProfile.GetTrophies();
            if (trophiesText) trophiesText.text = $"Trophies: {trophies}";

            // Compute content width based on max trophies
            int max = Mathf.Max(1, _road.MaxTrophies);
            float targetWidth = Mathf.Max(minContentWidth, contentPadding * 2f + pixelsPerTrophy * max);
            if (content)
            {
                var size = content.sizeDelta;
                size.x = targetWidth;
                content.sizeDelta = size;
                // Ensure content starts at the left (so the start of the slider is visible)
                content.pivot = new Vector2(0f, content.pivot.y);
                content.anchoredPosition = new Vector2(0f, content.anchoredPosition.y);
            }

            // Configure progress slider to span the whole content and reflect progress
            if (progressSlider)
            {
                progressSlider.maxValue = max;
                progressSlider.value = Mathf.Clamp(trophies, 0, max);
                var srt = progressSlider.transform as RectTransform;
                if (srt != null)
                {
                    // Stretch horizontally within content and give it a fixed height
                    srt.anchorMin = new Vector2(0f, srt.anchorMin.y);
                    srt.anchorMax = new Vector2(1f, srt.anchorMax.y);
                    srt.offsetMin = new Vector2(contentPadding, srt.offsetMin.y);
                    srt.offsetMax = new Vector2(-contentPadding, srt.offsetMax.y);
                    // Apply unified size scaling to slider height
                    float h = Mathf.Max(8f, baseSliderHeight * Mathf.Max(0.25f, sizeScale));
                    srt.sizeDelta = new Vector2(srt.sizeDelta.x, h);
                }
            }

            // Next milestone hint
            if (nextMilestoneText)
            {
                int nextIdx = TrophyRoadService.GetNextMilestoneIndex();
                if (nextIdx >= 0)
                {
                    var ms = _road.Milestones[nextIdx];
                    int need = Mathf.Max(0, ms.trophyRequired - trophies);
                    nextMilestoneText.text = need > 0 ? $"Next reward at {ms.trophyRequired} (+{need})" : $"Reward available at {ms.trophyRequired}!";
                }
                else
                {
                    nextMilestoneText.text = "All rewards claimed";
                }
            }

            // Build nodes if needed
            if (_nodes.Count == 0)
            {
                BuildNodes();
            }
            else
            {
                RefreshNodes();
            }
        }

        private void BuildNodes()
        {
            ClearNodes();
            if (_road == null || content == null || nodePrefab == null) return;
            var milestones = _road.Milestones;
            if (milestones == null || milestones.Count == 0) return;

            float width = content.rect.width;
            float usable = Mathf.Max(0f, width - contentPadding * 2f);
            int max = Mathf.Max(1, _road.MaxTrophies);

            var sliderRT = progressSlider != null ? (progressSlider.transform as RectTransform) : null;
            float sliderCenterY = sliderRT != null ? sliderRT.anchoredPosition.y + (sliderRT.sizeDelta.y * (sliderRT.pivot.y - 0.5f)) : 0f;
            float nodeW = Mathf.Max(20f, baseNodeWidth * Mathf.Max(0.25f, sizeScale));
            float nodeH = Mathf.Max(20f, baseNodeHeight * Mathf.Max(0.25f, sizeScale));

            for (int i = 0; i < milestones.Count; i++)
            {
                var ms = milestones[i];
                if (ms == null) continue;
                // Create node and marker line at the exact milestone position
                var node = Instantiate(nodePrefab, content);
                node.name = $"Node_{ms.trophyRequired}_{i}";
                _nodes.Add(node);
                RectTransform markerRT = CreateOrUpdateMarker(i, content);
                var label = CreateOrUpdateMarkerLabel(i, content);

                float t = Mathf.Clamp01((float)ms.trophyRequired / max);
                // Position marker and node together
                var rt = node.transform as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
                    rt.anchorMax = new Vector2(0f, rt.anchorMax.y);
                    rt.pivot = new Vector2(0.5f, rt.pivot.y);
                    float xCenter = contentPadding + usable * t;
                    // Size to fit nicely within the slider track area (scaled)
                    rt.sizeDelta = new Vector2(nodeW, nodeH);
                    // Center vertically on slider plus optional offset
                    float y = sliderCenterY + nodeVerticalOffset;
                    rt.anchoredPosition = new Vector2(xCenter, y);

                    if (markerRT != null)
                    {
                        // Marker centered at same X, spanning slider height
                        markerRT.anchorMin = new Vector2(0f, 0.5f);
                        markerRT.anchorMax = new Vector2(0f, 0.5f);
                        markerRT.pivot = new Vector2(0.5f, 0.5f);
                        float mh = sliderRT != null ? Mathf.Max(8f, sliderRT.sizeDelta.y) : nodeH;
                        markerRT.sizeDelta = new Vector2(markerWidth, mh);
                        markerRT.anchoredPosition = new Vector2(xCenter, sliderCenterY);
                        // Ensure marker is behind node in hierarchy
                        markerRT.SetSiblingIndex(Mathf.Max(0, rt.GetSiblingIndex() - 1));
                    }

                    if (label != null)
                    {
                        label.text = ms.trophyRequired.ToString();
                        var lrt = label.transform as RectTransform;
                        if (lrt != null)
                        {
                            lrt.anchorMin = new Vector2(0f, 0.5f);
                            lrt.anchorMax = new Vector2(0f, 0.5f);
                            lrt.pivot = new Vector2(0.5f, 0.5f);
                            float mh = sliderRT != null ? Mathf.Max(8f, sliderRT.sizeDelta.y) : nodeH;
                            float yLabel = sliderCenterY + (mh * 0.5f) + markerLabelVerticalOffset;
                            lrt.anchoredPosition = new Vector2(xCenter, yLabel);
                        }
                    }
                }

                node.SetData(i, ms);
            }
        }

        private void RefreshNodes()
        {
            // Recompute positions in case sizes/settings changed
            float width = content != null ? content.rect.width : 0f;
            float usable = Mathf.Max(0f, width - contentPadding * 2f);
            int max = Mathf.Max(1, _road != null ? _road.MaxTrophies : 1);

            var sliderRT = progressSlider != null ? (progressSlider.transform as RectTransform) : null;
            float sliderCenterY = sliderRT != null ? sliderRT.anchoredPosition.y + (sliderRT.sizeDelta.y * (sliderRT.pivot.y - 0.5f)) : 0f;
            float nodeW = Mathf.Max(20f, baseNodeWidth * Mathf.Max(0.25f, sizeScale));
            float nodeH = Mathf.Max(20f, baseNodeHeight * Mathf.Max(0.25f, sizeScale));

            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (node != null)
                {
                    // State
                    node.RefreshState();
                    // Position
                    var ms = _road.Milestones[i];
                    if (ms != null && content != null)
                    {
                        float t = Mathf.Clamp01((float)ms.trophyRequired / max);
                        var rt = node.transform as RectTransform;
                        if (rt != null)
                        {
                            rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
                            rt.anchorMax = new Vector2(0f, rt.anchorMax.y);
                            rt.pivot = new Vector2(0.5f, rt.pivot.y);
                            float xCenter = contentPadding + usable * t;
                            // Keep size and vertical alignment consistent with slider (scaled)
                            rt.sizeDelta = new Vector2(nodeW, nodeH);
                            float y = sliderCenterY + nodeVerticalOffset;
                            rt.anchoredPosition = new Vector2(xCenter, y);

                            // Update marker to stay under node
                            if (i < _markers.Count && _markers[i] != null)
                            {
                                var markerRT = _markers[i];
                                markerRT.anchorMin = new Vector2(0f, 0.5f);
                                markerRT.anchorMax = new Vector2(0f, 0.5f);
                                markerRT.pivot = new Vector2(0.5f, 0.5f);
                                float mh = sliderRT != null ? Mathf.Max(8f, sliderRT.sizeDelta.y) : nodeH;
                                markerRT.sizeDelta = new Vector2(markerWidth, mh);
                                markerRT.anchoredPosition = new Vector2(xCenter, sliderCenterY);
                            }

                            // Update label position/text
                            if (i < _markerLabels.Count && _markerLabels[i] != null)
                            {
                                var label = _markerLabels[i];
                                label.text = ms.trophyRequired.ToString();
                                var lrt = label.transform as RectTransform;
                                if (lrt != null)
                                {
                                    lrt.anchorMin = new Vector2(0f, 0.5f);
                                    lrt.anchorMax = new Vector2(0f, 0.5f);
                                    lrt.pivot = new Vector2(0.5f, 0.5f);
                                    float mh = sliderRT != null ? Mathf.Max(8f, sliderRT.sizeDelta.y) : nodeH;
                                    float yLabel = sliderCenterY + (mh * 0.5f) + markerLabelVerticalOffset;
                                    lrt.anchoredPosition = new Vector2(xCenter, yLabel);
                                }
                            }
                        }
                    }
                }
            }
            // Update header
            int trophies = PlayerProfile.GetTrophies();
            if (trophiesText) trophiesText.text = $"Trophies: {trophies}";
            if (progressSlider)
            {
                progressSlider.maxValue = max;
                progressSlider.value = Mathf.Clamp(trophies, 0, max);
            }
        }

        private void AutoScrollToNext()
        {
            if (scrollRect == null || content == null || _road == null) return;
            int nextIdx = TrophyRoadService.GetNextMilestoneIndex();
            if (nextIdx < 0 || nextIdx >= _road.Milestones.Count) return;
            var ms = _road.Milestones[nextIdx];

            // Compute normalized scroll based on target x
            float width = content.rect.width;
            float usable = Mathf.Max(0f, width - contentPadding * 2f);
            int max = Mathf.Max(1, _road.MaxTrophies);
            float t = Mathf.Clamp01((float)ms.trophyRequired / max);
            // position in content pixels
            float targetX = contentPadding + usable * t;
            // Use assigned viewport if present; else fallback to ScrollRect's own RectTransform
            RectTransform viewportRT = scrollRect.viewport != null ? scrollRect.viewport : (scrollRect.transform as RectTransform);
            float viewportWidth = viewportRT != null ? viewportRT.rect.width : 0f;
            float maxScrollX = Mathf.Max(0f, width - viewportWidth);
            float clampedX = Mathf.Clamp(targetX - viewportWidth * 0.5f, 0f, maxScrollX);
            float normalized = maxScrollX <= 1e-3f ? 0f : clampedX / maxScrollX;
            scrollRect.horizontalNormalizedPosition = normalized;
        }

        private void EnsureScrollBindings()
        {
            if (scrollRect == null) return;
            // Assign content if not set
            if (scrollRect.content == null && content != null)
            {
                scrollRect.content = content;
            }
            // Assign viewport if missing: try find child named "Viewport", else use ScrollRect's RectTransform
            if (scrollRect.viewport == null)
            {
                RectTransform found = null;
                var t = scrollRect.transform.Find("Viewport");
                if (t != null) found = t as RectTransform;
                if (found == null) found = scrollRect.transform as RectTransform;
                scrollRect.viewport = found;
            }
        }

        private void ClearNodes()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] != null) Destroy(_nodes[i].gameObject);
            }
            _nodes.Clear();
            if (_markers.Count > 0)
            {
                for (int i = 0; i < _markers.Count; i++)
                {
                    if (_markers[i] != null) Destroy(_markers[i].gameObject);
                }
                _markers.Clear();
            }
            if (_markerLabels.Count > 0)
            {
                for (int i = 0; i < _markerLabels.Count; i++)
                {
                    if (_markerLabels[i] != null) Destroy(_markerLabels[i].gameObject);
                }
                _markerLabels.Clear();
            }
        }

        [Header("Markers")]
        [SerializeField] private bool showMarkers = true;
        [SerializeField] private Color markerColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private float markerWidth = 3f;
        [Header("Marker Labels")]
        [SerializeField] private bool showMarkerLabels = true;
        [SerializeField] private float markerLabelVerticalOffset = 16f;
        [SerializeField] private Color markerLabelColor = Color.white;
        [SerializeField] private int markerLabelFontSize = 18;

        private RectTransform CreateOrUpdateMarker(int index, RectTransform parent)
        {
            if (!showMarkers || parent == null) return null;
            while (_markers.Count <= index) _markers.Add(null);
            var existing = _markers[index];
            if (existing == null)
            {
                var go = new GameObject($"Marker_{index}", typeof(UnityEngine.UI.Image));
                go.transform.SetParent(parent, false);
                var img = go.GetComponent<UnityEngine.UI.Image>();
                img.color = markerColor;
                existing = go.GetComponent<RectTransform>();
                _markers[index] = existing;
            }
            else
            {
                var img = existing.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.color = markerColor;
            }
            return existing;
        }

        private TMPro.TMP_Text CreateOrUpdateMarkerLabel(int index, RectTransform parent)
        {
            if (!showMarkerLabels || parent == null) return null;
            while (_markerLabels.Count <= index) _markerLabels.Add(null);
            var existing = _markerLabels[index];
            if (existing == null)
            {
                var go = new GameObject($"MarkerLabel_{index}", typeof(TMPro.TextMeshProUGUI));
                go.transform.SetParent(parent, false);
                var txt = go.GetComponent<TMPro.TextMeshProUGUI>();
                txt.alignment = TMPro.TextAlignmentOptions.Center;
                txt.color = markerLabelColor;
                txt.fontSize = markerLabelFontSize;
                existing = txt;
                _markerLabels[index] = existing;
            }
            else
            {
                existing.color = markerLabelColor;
                existing.fontSize = markerLabelFontSize;
                existing.alignment = TMPro.TextAlignmentOptions.Center;
            }
            return existing;
        }

        private void OnDisable()
        {
            // optional cleanup or save
        }
    }
}
