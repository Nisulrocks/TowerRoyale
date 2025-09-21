using System.Collections;
using UnityEngine;
using TMPro;

namespace TR.Tutorial
{
    // Minimal dialogue panel with a typewriter effect.
    public class TutorialDialogueUI : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private TextMeshProUGUI text;
        [SerializeField] private float defaultCharDelay = 0.03f;
        [SerializeField] private Vector2 anchoredPosition = new Vector2(0f, 40f);
        [SerializeField] private Vector2 size = new Vector2(720f, 140f);

        private Coroutine _typing;

        private void Awake()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            // Only apply default layout if the prefab hasn't positioned/sized it yet
            if (rt.sizeDelta == Vector2.zero)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = anchoredPosition;
                rt.sizeDelta = size;
            }

            // If a prefab has a TextMeshProUGUI assigned, respect it; otherwise try to find one; if none, create minimal one
            if (text == null)
            {
                text = GetComponentInChildren<TextMeshProUGUI>(true);
            }
            if (text == null)
            {
                var textGO = new GameObject("DialogueText", typeof(RectTransform));
                textGO.transform.SetParent(transform, false);
                text = textGO.AddComponent<TextMeshProUGUI>();
                text.raycastTarget = false;
                text.alignment = TextAlignmentOptions.Midline;
                text.fontSize = 26f;
                text.textWrappingMode = TextWrappingModes.Normal;
                var tr = text.rectTransform;
                tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
                tr.pivot = new Vector2(0.5f, 0.5f);
                tr.sizeDelta = size;
            }
            gameObject.name = "TutorialDialogueUI";

            Hide();
        }

        public void Show(string content, float charDelay)
        {
            gameObject.SetActive(true);
            if (_typing != null) StopCoroutine(_typing);
            _typing = StartCoroutine(Typewriter(content ?? string.Empty, charDelay > 0f ? charDelay : defaultCharDelay));
        }

        public void Hide()
        {
            if (_typing != null)
            {
                StopCoroutine(_typing);
                _typing = null;
            }
            if (text != null) text.text = string.Empty;
            gameObject.SetActive(false);
        }

        private IEnumerator Typewriter(string content, float delay)
        {
            text.text = string.Empty;
            for (int i = 0; i < content.Length; i++)
            {
                text.text = content.Substring(0, i + 1);
                yield return new WaitForSecondsRealtime(delay);
            }
            _typing = null;
        }
    }
}
