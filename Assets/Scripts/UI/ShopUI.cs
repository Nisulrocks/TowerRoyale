using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;
using TR.Infrastructure;
using TR.Data;

namespace TR.UI
{
    
    public class ShopUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private ShopPackItemUI itemPrefab;
        [SerializeField] private TMP_Text softCurrencyText; 
        [Header("Card Points Offers")]
        [SerializeField] private ShopCardPointsItemUI cardPointsItemPrefab; 
        [SerializeField] private RectTransform offersSectionParent; 
        [SerializeField] private RectTransform offersSectionRoot; 
        [SerializeField] private RectTransform offersItemsRoot;  
        [SerializeField] private TMP_Text offersHeaderText;       
        [SerializeField] private TMP_Text offersCountdownText;    
        [SerializeField] private bool devEnableRefreshHotkey = false; 
        private TMP_Text _offersCountdown;
        private System.Collections.Generic.List<ShopCardPointsItemUI> _offerItems = new System.Collections.Generic.List<ShopCardPointsItemUI>();
        
        private Coroutine _softFlashCo;
        private Color _softBaseColor = Color.white;
        private Vector3 _softBaseScale = Vector3.one;

        [Header("Daily Entry")]
        [SerializeField] private bool showDailyEntry = true;
        [SerializeField] private string dailyPackId = "normal_pack";
        [SerializeField] private int dailyCooldownHours = 24;
        [SerializeField] private Color dailyDisabledColor = new Color(0.8f, 0.8f, 0.8f, 1f);

        [Header("Starter Pack")]
        [SerializeField] private string starterPackId = "starter_pack"; 

        [Header("Scene Names")]
        [SerializeField] private string packOpeningSceneName = "PackOpening";

        private void OnEnable()
        {
            Refresh();
        }

        private ShopPackItemUI _dailyItem;
        private long _cooldownSeconds;
        private float _nextUiTick;

        public void Refresh()
        {
            foreach (Transform child in listRoot) Destroy(child.gameObject);
            GameDB.EnsureLoaded();
            if (softCurrencyText)
            {
                softCurrencyText.text = $"Coins: {PlayerProfile.GetSoftCurrency()}";
                
                if (_softBaseColor == default(Color)) _softBaseColor = softCurrencyText.color;
                if (_softBaseScale == Vector3.zero) _softBaseScale = softCurrencyText.transform.localScale;
            }

            
            _dailyItem = null;
            _cooldownSeconds = Mathf.Max(1, dailyCooldownHours) * 3600L;
            if (showDailyEntry)
            {
                var def = GameDB.GetPackById(string.IsNullOrEmpty(dailyPackId) ? "normal_pack" : dailyPackId);
                if (def == null && GameDB.Packs != null && GameDB.Packs.Count > 0)
                    def = GameDB.Packs[0];
                if (def != null)
                {
                    _dailyItem = Instantiate(itemPrefab, listRoot);
                    _dailyItem.Bind(def, OnClaimDailyPack, 0);
                    UpdateDailyItemState(force: true);
                }
            }
            
            bool starterClaimed = PlayerProfile.Data.starterClaimed;
            if (!starterClaimed)
            {
                
                var starter = GameDB.GetPackById(string.IsNullOrEmpty(starterPackId) ? "starter_pack" : starterPackId);
                if (starter == null && GameDB.Packs != null && GameDB.Packs.Count > 0)
                    starter = GameDB.Packs[0];
                if (starter != null)
                {
                    var item = Instantiate(itemPrefab, listRoot);
                    
                    item.Bind(starter, OnOpenStarterPack, 0);
                    
                    var tmp = item.GetComponentInChildren<TMP_Text>();
                    
                }
            }

            
            var ids = PlayerProfile.Data.packIds;
            var counts = PlayerProfile.Data.packCounts;
            if (ids != null && counts != null)
            {
                for (int i = 0; i < ids.Count && i < counts.Count; i++)
                {
                    int count = counts[i];
                    if (count <= 0) continue;
                    var def = GameDB.GetPackById(ids[i]);
                    if (def == null) continue;
                    var ownedItem = Instantiate(itemPrefab, listRoot);
                    
                    ownedItem.Bind(def, OnOpenOwnedPack, 0);
                    
                    
                    var texts = ownedItem.GetComponentsInChildren<TMP_Text>();
                    if (texts != null && texts.Length > 0)
                    {
                        texts[0].text = $"{def.DisplayName} (x{count})";
                    }
                }
            }
            
            string starterId = string.IsNullOrEmpty(starterPackId) ? "starter_pack" : starterPackId;
            var orderedPacks = new System.Collections.Generic.List<PackDefinition>(GameDB.Packs);
            orderedPacks.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                int so = a.ShopOrder.CompareTo(b.ShopOrder);
                if (so != 0) return so;
                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
            });
            foreach (var pack in orderedPacks)
            {
                if (pack == null) continue;
                if (!string.IsNullOrEmpty(starterId) && pack.PackId == starterId) continue; 
                
                if (!pack.IsUnlockedForPlayer()) continue;
                var item = Instantiate(itemPrefab, listRoot);
                item.Bind(pack, OnOpenPack);
            }

            
            BuildCardPointsSection();
        }

        private void OnOpenPack(string packId)
        {
            
            SceneParams.Set("packId", packId);
            SceneParams.Set("openCount", 1);
            _ = SceneFader.Instance.LoadSceneWithFade(packOpeningSceneName);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextUiTick) return;
            _nextUiTick = Time.unscaledTime + 1f; 
            
            if (showDailyEntry && _dailyItem != null)
            {
                UpdateDailyItemState(force: false);
            }
            
            UpdateCardPointsCountdown();
            
            if (devEnableRefreshHotkey && Input.GetKeyDown(KeyCode.L))
            {
                OnClickDevRefreshOffers();
            }
        }

        private void UpdateCardPointsCountdown()
        {
            if (_offersCountdown == null) return;
            var remain = ShopService.GetTimeUntilNextRefresh();
            if (remain.TotalSeconds < 0) { _offersCountdown.text = "--:--:--"; return; }
            string hh = Mathf.FloorToInt((float)remain.TotalHours).ToString("00");
            string mm = Mathf.FloorToInt((float)remain.Minutes).ToString("00");
            string ss = Mathf.FloorToInt((float)remain.Seconds).ToString("00");
            _offersCountdown.text = $"Refresh in {hh}:{mm}:{ss}";
        }

        private void OnBuyCardPoints(int index)
        {
            if (ShopService.TryPurchaseCardPointsOffer(index))
            {
                
                Refresh();
            }
            else
            {
                
                var offers = ShopService.GetOrGenerateDailyCardPointOffers();
                if (offers != null && index >= 0 && index < offers.Count)
                {
                    var off = offers[index];
                    if (off != null && PlayerProfile.GetSoftCurrency() < off.cost)
                    {
                        if (_offerItems != null && index < _offerItems.Count && _offerItems[index] != null)
                            _offerItems[index].FlashInsufficientFunds();
                        FlashSoftCurrencyInsufficient();
                    }
                    else
                    {
                        Debug.Log("[Shop] Purchase card points failed");
                    }
                }
                else
                {
                    Debug.Log("[Shop] Purchase card points failed");
                }
            }
        }

        private void OnDisable()
        {
            StopSoftFlash();
            RestoreSoftCurrencyVisuals();
        }

        private void FlashSoftCurrencyInsufficient()
        {
            if (softCurrencyText == null) return;
            StopSoftFlash();
            _softFlashCo = StartCoroutine(SoftFlashCo());
        }

        private System.Collections.IEnumerator SoftFlashCo()
        {
            const float dur = 0.35f;
            float t = 0f;
            if (_softBaseScale == Vector3.zero) _softBaseScale = softCurrencyText.transform.localScale;
            if (_softBaseColor == default(Color)) _softBaseColor = softCurrencyText.color;
            Color pulse = new Color(1f, 0.3f, 0.3f, _softBaseColor.a);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                float ping = Mathf.PingPong(u * 2f, 1f);
                softCurrencyText.color = Color.Lerp(_softBaseColor, pulse, ping);
                softCurrencyText.transform.localScale = _softBaseScale * (1f + 0.06f * ping);
                yield return null;
            }
            RestoreSoftCurrencyVisuals();
            _softFlashCo = null;
        }

        private void StopSoftFlash()
        {
            if (_softFlashCo != null)
            {
                StopCoroutine(_softFlashCo);
                _softFlashCo = null;
            }
        }

        private void RestoreSoftCurrencyVisuals()
        {
            if (softCurrencyText == null) return;
            softCurrencyText.color = (_softBaseColor == default(Color)) ? softCurrencyText.color : _softBaseColor;
            softCurrencyText.transform.localScale = _softBaseScale == Vector3.zero ? softCurrencyText.transform.localScale : _softBaseScale;
        }

        
        public void OnClickDevRefreshOffers()
        {
            ShopService.ForceRefreshOffers();
            Refresh();
        }

        

        
        private void BuildCardPointsSection()
        {
            var offers = ShopService.GetOrGenerateDailyCardPointOffers();
            _offerItems.Clear();
            Transform section = offersSectionRoot != null ? (Transform)offersSectionRoot : null;
            Transform itemsParent = offersItemsRoot != null ? (Transform)offersItemsRoot : section;
            
            if (section == null)
            {
                Transform parent = offersSectionParent != null ? (Transform)offersSectionParent : listRoot;
                
                var existing = parent.Find("CardPointsSection");
                if (existing != null) DestroyImmediate(existing.gameObject);

                var sectionGO = new GameObject("CardPointsSection", typeof(RectTransform));
                sectionGO.transform.SetParent(parent, false);
                section = sectionGO.transform;

                
                var headerGO = new GameObject("Header", typeof(RectTransform));
                headerGO.transform.SetParent(section, false);
                var header = headerGO.AddComponent<TextMeshProUGUI>();
                header.text = "Card Points Offers";
                header.fontSize = 26;
                header.alignment = TextAlignmentOptions.Left;
                offersHeaderText = header;

                
                var cdGO = new GameObject("Countdown", typeof(RectTransform));
                cdGO.transform.SetParent(section, false);
                var cdtxt = cdGO.AddComponent<TextMeshProUGUI>();
                cdtxt.text = "";
                cdtxt.fontSize = 18;
                cdtxt.alignment = TextAlignmentOptions.Left;
                _offersCountdown = cdtxt;
                offersCountdownText = cdtxt;

                itemsParent = section; 
            }
            else
            {
                
                if (offersHeaderText != null) offersHeaderText.text = "Card Points Offers";
                _offersCountdown = offersCountdownText;
            }

            
            if (itemsParent != null)
            {
                
                var toDestroy = new System.Collections.Generic.List<GameObject>();
                foreach (Transform ch in itemsParent)
                {
                    
                    toDestroy.Add(ch.gameObject);
                }
                foreach (var go in toDestroy) DestroyImmediate(go);
            }

            
            if (offers != null && offers.Count > 0 && cardPointsItemPrefab != null && itemsParent != null)
            {
                for (int i = 0; i < offers.Count; i++)
                {
                    var off = offers[i];
                    if (off == null) continue;
                    var card = GameDB.GetCardById(off.cardId);
                    if (card == null) continue;
                    var ui = Instantiate(cardPointsItemPrefab, itemsParent);
                    int index = i;
                    ui.Bind(card, off.points, off.cost, off.sold, () => OnBuyCardPoints(index));
                    
                    while (_offerItems.Count <= i) _offerItems.Add(null);
                    _offerItems[i] = ui;
                }
            }

            UpdateCardPointsCountdown();
        }

        private void OnOpenOwnedPack(string packId)
        {
            
            if (!string.IsNullOrEmpty(packId))
            {
                if (PlayerProfile.Data.ConsumePack(packId))
                {
                    PlayerProfile.Save();
                    OnOpenPack(packId);
                }
                else
                {
                    Refresh();
                }
            }
        }

        private void OnOpenStarterPack(string packId)
        {
            
            PlayerProfile.Data.starterClaimed = true;
            PlayerProfile.Save();
            OnOpenPack(packId);
        }

        private void OnClaimDailyPack(string packId)
        {
            
            long last = PlayerProfile.GetLastDailyPackUnix();
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now - last < _cooldownSeconds)
            {
                UpdateDailyItemState(force: true);
                return;
            }
            var id = string.IsNullOrEmpty(packId) ? dailyPackId : packId;
            if (!string.IsNullOrEmpty(id))
            {
                PlayerProfile.Data.AddPacks(id, 1);
                PlayerProfile.SetLastDailyPackNow();
                PlayerProfile.Save();
                UpdateDailyItemState(force: true);
            }
        }

        private void UpdateDailyItemState(bool force)
        {
            if (_dailyItem == null) return;
            long last = PlayerProfile.GetLastDailyPackUnix();
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long remain = (last + _cooldownSeconds) - now;
            if (remain <= 0)
            {
                _dailyItem.SetButtonInteractable(true);
                _dailyItem.SetCostLabel("Free (Daily)", new Color(0.8f, 1f, 0.8f, 1f));
            }
            else
            {
                _dailyItem.SetButtonInteractable(false);
                string hh = Mathf.FloorToInt(remain / 3600f).ToString("00");
                string mm = Mathf.FloorToInt((remain % 3600) / 60f).ToString("00");
                string ss = Mathf.FloorToInt(remain % 60f).ToString("00");
                _dailyItem.SetCostLabel($"{hh}:{mm}:{ss}", dailyDisabledColor);
            }
        }
    }
}
