using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Data;
using TR.Systems;

namespace TR.UI
{
    public class CollectionUpgradeBadge : MonoBehaviour
    {
        [Tooltip("The tab button that will display the badge. If null, the object will try to find 'Collections Tab' by name.")]
        [SerializeField] private Button targetButton;
        [Tooltip("Fallback name to search for if no button is assigned.")]
        [SerializeField] private string fallbackTabName = "Collections Tab";
        [Tooltip("How often to recalculate the upgrade count (seconds).")]
        [SerializeField] private float refreshInterval = 1f;
        [Tooltip("Size of the badge circle.")]
        [SerializeField] private Vector2 badgeSize = new Vector2(28f, 28f);
        [Tooltip("Offset from the top-right corner of the tab button.")]
        [SerializeField] private Vector2 badgeOffset = new Vector2(-8f, -8f);
        [Tooltip("Background color of the badge.")]
        [SerializeField] private Color badgeColor = new Color(0.9f, 0.2f, 0.2f, 1f);
        [Tooltip("Text color of the badge number.")]
        [SerializeField] private Color badgeTextColor = Color.white;

        private GameObject _badgeRoot;
        private TMP_Text _badgeText;
        private Coroutine _refreshCo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            var existing = FindFirstObjectByType<CollectionUpgradeBadge>();
            if (existing != null) return;

            var go = GameObject.Find("CollectionUpgradeBadge");
            if (go == null) go = new GameObject("CollectionUpgradeBadge");
            go.AddComponent<CollectionUpgradeBadge>();
        }

        private void Awake()
        {
            if (targetButton == null && !string.IsNullOrEmpty(fallbackTabName))
            {
                var tabGO = GameObject.Find(fallbackTabName);
                if (tabGO != null) targetButton = tabGO.GetComponent<Button>();
            }

            if (targetButton == null)
            {
                Debug.LogWarning("[CollectionUpgradeBadge] No collection tab button found. Destroying badge manager.");
                Destroy(gameObject);
                return;
            }

            CreateBadge();
        }

        private void OnEnable()
        {
            Refresh();
            if (_refreshCo != null) StopCoroutine(_refreshCo);
            _refreshCo = StartCoroutine(RefreshLoop());
        }

        private void OnDisable()
        {
            if (_refreshCo != null)
            {
                StopCoroutine(_refreshCo);
                _refreshCo = null;
            }
        }

        private IEnumerator RefreshLoop()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(refreshInterval);
                Refresh();
            }
        }

        public void Refresh()
        {
            if (_badgeRoot == null || _badgeText == null) return;

            int count = CountUpgradeableCards();
            _badgeRoot.SetActive(count > 0);
            if (count > 0)
            {
                _badgeText.text = count > 99 ? "99+" : count.ToString();
            }
        }

        private int CountUpgradeableCards()
        {
            GameDB.EnsureLoaded();
            int soft = PlayerProfile.GetSoftCurrency();
            int count = 0;

            var cards = GameDB.Cards;
            if (cards == null) return 0;

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null || card.Rarity == null) continue;

                var cp = PlayerProfile.GetOrCreateCard(card.CardId);
                if (cp == null || cp.ownedCount <= 0) continue;

                int level = Mathf.Max(1, cp.level);
                int maxLevel = card.Rarity.MaxLevel;
                if (level >= maxLevel) continue;

                int nextLevel = level + 1;
                int pointsRequired = card.Rarity.GetPointsRequiredForLevel(nextLevel);
                int cost = card.Rarity.GetUpgradeCostForLevel(nextLevel);

                if (cp.points >= pointsRequired && soft >= cost)
                    count++;
            }

            return count;
        }

        private void CreateBadge()
        {
            _badgeRoot = new GameObject("UpgradeBadge", typeof(RectTransform), typeof(Image));
            _badgeRoot.transform.SetParent(targetButton.transform, false);
            _badgeRoot.transform.SetAsLastSibling();

            var rt = _badgeRoot.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.one;
            rt.anchorMax = Vector2.one;
            rt.pivot = Vector2.one;
            rt.anchoredPosition = badgeOffset;
            rt.sizeDelta = badgeSize;

            var img = _badgeRoot.GetComponent<Image>();
            img.color = badgeColor;
            img.raycastTarget = false;
            img.sprite = CreateCircleSprite(Color.white);

            var textGO = new GameObject("BadgeText", typeof(RectTransform));
            textGO.transform.SetParent(_badgeRoot.transform, false);
            StretchFull(textGO.GetComponent<RectTransform>());

            _badgeText = textGO.AddComponent<TextMeshProUGUI>();
            _badgeText.alignment = TextAlignmentOptions.Center;
            _badgeText.fontSize = 14;
            _badgeText.color = badgeTextColor;
            _badgeText.raycastTarget = false;
            _badgeText.text = "0";

            _badgeRoot.SetActive(false);
        }

        private static void StretchFull(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private Sprite CreateCircleSprite(Color fill)
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color clear = new Color(0f, 0f, 0f, 0f);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    tex.SetPixel(x, y, d <= radius ? fill : clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
