using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TR.Infrastructure;
using UnityEngine.UI;
using TMPro;
using TR.Systems;
using TR.Data;

namespace TR.UI
{
    
    public class PackOpeningSceneController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private string lobbySceneName = "Lobby";
        [SerializeField] private float packPopDuration = 0.25f;
        [SerializeField] private float packDropDuration = 0.6f;
        [SerializeField] private float cardRiseDuration = 0.4f;
        [SerializeField] private float cardInterval = 0.2f;
        [SerializeField] private float revealSpacing = 160f;     
        [SerializeField] private float finalOverlapSpacing = 80f; 
        [SerializeField] private float compressDuration = 0.35f;  
        [Header("Reveal FX")]
        [SerializeField] private float flipDuration = 0.45f;
        [SerializeField] private float flipOvershootScale = 1.08f;
        [SerializeField] private float flipInterval = 0.15f;
        [Header("Back Face")]
        [Tooltip("Optional prefab for the card back face (will be instantiated under each card). If null, a sprite or color will be used.")]
        [SerializeField] private GameObject backFacePrefab;

        [SerializeField] private Sprite cardBackSprite;
        [SerializeField] private Color backFaceColor = new Color(0.36f, 0.22f, 0.56f, 1f); 
        [SerializeField] private string sfxFlipKey = "card_flip";
        [SerializeField] private string sfxRarityCommon = "rarity_common";
        [SerializeField] private string sfxRarityRare = "rarity_rare";
        [SerializeField] private string sfxRarityEpic = "rarity_epic";
        [SerializeField] private string sfxRarityLegendary = "rarity_legendary";

        [Header("Refs")]
        [SerializeField] private RectTransform packRect;      
        [SerializeField] private CanvasGroup packCanvasGroup; 
        [SerializeField] private RectTransform cardsRoot;     
        [SerializeField] private CardItemUI cardPrefab;       
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private bool allowKeyboardContinue = true;

        [Header("Result Label (NEW!/points)")]
        [Tooltip("Offset applied to the result label under each card (x,y) in anchored pixels")]
        [SerializeField] private Vector2 resultLabelOffset = new Vector2(0f, -28f);

        [SerializeField] private Color resultLabelNewColor = new Color(0.2f, 1f, 0.4f);
        [Tooltip("Text color used when awarding duplicate points (e.g., +15 pts)")]
        [SerializeField] private Color resultLabelPointsColor = new Color(1f, 0.9f, 0.3f);

        [SerializeField] private float resultLabelFontSizeNew = 22f;

        [SerializeField] private float resultLabelFontSizePoints = 22f;
        [Header("Upgrade Label (separate)")]
        [Tooltip("Offset applied to the 'Upgrade Available' label under each card (x,y) in anchored pixels")] 
        [SerializeField] private Vector2 upgradeLabelOffset = new Vector2(0f, -52f);

        [SerializeField] private Color upgradeLabelColor = new Color(0.6f, 1f, 0.6f);

        [SerializeField] private float upgradeLabelFontSize = 20f;

        [SerializeField] private string upgradeAvailableText = "Upgrade Available";

        [SerializeField] private float upgradeLabelFadeOutDuration = 0.6f;

        [Header("Hover Spread")]
        [SerializeField] private float hoverSpread = 60f;         
        [SerializeField] private float hoverAnimDuration = 0.2f;  
        [SerializeField] private float hoverScale = 1.07f;        

        private List<CollectionService.AwardResult> _results;
        private readonly List<RectTransform> _spawned = new();
        private readonly List<CanvasGroup> _frontGroups = new();
        private readonly List<RectTransform> _backFaces = new();
        private Vector2[] _finalPositions; 
        private readonly List<RectTransform> _resultLabels = new();
        private readonly List<RectTransform> _upgradeLabelRects = new();
        private Coroutine _hoverTween;
        private int _currentHover = -1;
        private TR.Data.PackDefinition _currentPack;
        private bool _whooshPlayed;

        private void Start()
        {
            GameDB.EnsureLoaded();
            continueButton.interactable = false;
            if (packRect != null)
            {
                packCanvasGroup = packRect.GetComponent<CanvasGroup>() ?? packRect.gameObject.AddComponent<CanvasGroup>();
            }
            if (continueButton)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(OnContinue);
            }

            var packId = SceneParams.Get<string>("packId", null);
            var openCount = Mathf.Max(1, SceneParams.Get("openCount", 1));
            var pack = GameDB.GetPackById(packId);
            _currentPack = pack;
            if (headerText) headerText.text = pack != null ? pack.DisplayName : "Pack";

            
            if (packRect == null)
            {
                var anchorGO = new GameObject("PackAnchor", typeof(RectTransform));
                var anchorRT = anchorGO.GetComponent<RectTransform>();
                var parent = cardsRoot != null ? (Transform)cardsRoot.parent : transform;
                anchorRT.SetParent(parent, false);
                anchorRT.anchorMin = new Vector2(0.5f, 0.5f);
                anchorRT.anchorMax = new Vector2(0.5f, 0.5f);
                anchorRT.pivot = new Vector2(0.5f, 0.5f);
                anchorRT.anchoredPosition = Vector2.zero;
                anchorRT.localScale = Vector3.one;
                packRect = anchorRT;
                packCanvasGroup = packRect.gameObject.AddComponent<CanvasGroup>();
            }

            
            if (packRect != null && pack != null)
            {
                
                var toDestroy = new System.Collections.Generic.List<GameObject>();
                for (int i = 0; i < packRect.childCount; i++) toDestroy.Add(packRect.GetChild(i).gameObject);
                for (int i = 0; i < toDestroy.Count; i++) Destroy(toDestroy[i]);

                RectTransform visualRT = null;
                if (pack.PackArtPrefab != null)
                {
                    var inst = Instantiate(pack.PackArtPrefab, packRect);
                    visualRT = (inst.transform as RectTransform) ?? inst.AddComponent<RectTransform>();
                    visualRT.anchorMin = new Vector2(0.5f, 0.5f);
                    visualRT.anchorMax = new Vector2(0.5f, 0.5f);
                    visualRT.anchoredPosition = Vector2.zero;
                    visualRT.localScale = Vector3.one;
                }
                else if (pack.PackArtSprite != null)
                {
                    var go = new GameObject("PackArtSprite", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                    var rt = go.GetComponent<RectTransform>();
                    rt.SetParent(packRect, false);
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.localScale = Vector3.one;
                    var img = go.GetComponent<UnityEngine.UI.Image>();
                    img.sprite = pack.PackArtSprite;
                    img.preserveAspect = true;
                    img.color = Color.white;
                    visualRT = rt;
                }

                if (visualRT != null)
                {
                    
                    packRect = visualRT;
                    packCanvasGroup = packRect.GetComponent<CanvasGroup>() ?? packRect.gameObject.AddComponent<CanvasGroup>();
                }
            }

            
            if (packRect != null && packCanvasGroup == null)
                packCanvasGroup = packRect.gameObject.AddComponent<CanvasGroup>();
            if (packCanvasGroup != null) packCanvasGroup.alpha = 1f;

            _results = new List<CollectionService.AwardResult>();
            if (pack != null)
            {
                
                var rolled = new List<CardDefinition>();
                for (int i = 0; i < openCount; i++)
                    rolled.AddRange(PackService.OpenPack(pack));

                
                _results = CollectionService.AwardCardsDetailed(rolled);
            }

            StartCoroutine(RunSequence());
        }

        private void Update()
        {
            if (!allowKeyboardContinue) return;
            if (continueButton && continueButton.interactable)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))
                {
                    OnContinue();
                }
            }
        }

        private IEnumerator RunSequence()
        {
            
            Vector2 startPos = packRect.anchoredPosition;
            Vector2 endPos = startPos + new Vector2(0f, -200f);
            
            var startScale = Vector3.one * 0.85f;
            var overScale = Vector3.one * 1.05f;
            var finalScale = Vector3.one;
            packRect.localScale = startScale;
            if (packCanvasGroup) packCanvasGroup.alpha = 1f;
            float t = 0f;
            
            TryPlaySfx(_currentPack != null ? _currentPack.SealCrackKey : null);
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, packPopDuration);
                float e = EaseOutCubic(t);
                
                if (e < 0.7f)
                {
                    float e1 = e / 0.7f;
                    packRect.localScale = Vector3.Lerp(startScale, overScale, e1);
                }
                else
                {
                    float e2 = (e - 0.7f) / 0.3f;
                    packRect.localScale = Vector3.Lerp(overScale, finalScale, e2);
                }
                yield return null;
            }

            
            t = 0f;
            _whooshPlayed = false;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, packDropDuration);
                float e = EaseOutCubic(t);
                packRect.anchoredPosition = Vector2.Lerp(startPos, endPos, e);
                if (packCanvasGroup) packCanvasGroup.alpha = 1f - e;
                if (!_whooshPlayed)
                {
                    _whooshPlayed = true;
                    
                    TryPlaySfx(_currentPack != null ? _currentPack.OpenWhooshKey : null);
                }
                yield return null;
            }

            
            _spawned.Clear();
            _frontGroups.Clear();
            _backFaces.Clear();
            _resultLabels.Clear();
            _upgradeLabelRects.Clear();
            float startX = -revealSpacing * (Mathf.Max(0, _results.Count - 1) * 0.5f);
            for (int i = 0; i < _results.Count; i++)
            {
                var res = _results[i];
                var card = res.card;
                var ui = Instantiate(cardPrefab, cardsRoot);
                ui.Bind(card, 0);
                
                var frontGO = new GameObject("FrontFace", typeof(RectTransform));
                var frontRT = frontGO.GetComponent<RectTransform>();
                frontRT.SetParent(ui.transform, false);
                frontRT.anchorMin = new Vector2(0f, 0f);
                frontRT.anchorMax = new Vector2(1f, 1f);
                frontRT.offsetMin = Vector2.zero;
                frontRT.offsetMax = Vector2.zero;
                
                var tmpChildren = new System.Collections.Generic.List<Transform>();
                for (int ci = 0; ci < ui.transform.childCount; ci++) tmpChildren.Add(ui.transform.GetChild(ci));
                for (int ci = 0; ci < tmpChildren.Count; ci++)
                {
                    
                    if (tmpChildren[ci] == frontRT) continue;
                    tmpChildren[ci].SetParent(frontRT, false);
                }
                
                var cg = frontGO.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                _frontGroups.Add(cg);
                
                RectTransform backRT = null;
                if (backFacePrefab != null)
                {
                    var inst = Instantiate(backFacePrefab, ui.transform);
                    backRT = (inst.transform as RectTransform) ?? inst.AddComponent<RectTransform>();
                    backRT.anchorMin = new Vector2(0f, 0f);
                    backRT.anchorMax = new Vector2(1f, 1f);
                    backRT.offsetMin = Vector2.zero;
                    backRT.offsetMax = Vector2.zero;
                }
                else
                {
                    var backGo = new GameObject("BackFace", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                    backRT = backGo.GetComponent<RectTransform>();
                    backRT.SetParent(ui.transform, false);
                    backRT.anchorMin = new Vector2(0f, 0f);
                    backRT.anchorMax = new Vector2(1f, 1f);
                    backRT.offsetMin = Vector2.zero;
                    backRT.offsetMax = Vector2.zero;
                    var backImg = backGo.GetComponent<UnityEngine.UI.Image>();
                    if (cardBackSprite != null)
                    {
                        backImg.sprite = cardBackSprite;
                        backImg.color = Color.white;
                        backImg.preserveAspect = true;
                    }
                    else
                    {
                        backImg.color = backFaceColor;
                    }
                }
                
                backRT.SetAsLastSibling();
                var backCg = backRT.GetComponent<CanvasGroup>();
                if (backCg == null) backCg = backRT.gameObject.AddComponent<CanvasGroup>();
                backCg.alpha = 1f;
                _backFaces.Add(backRT);

                var rt = (RectTransform)ui.transform;
                rt.anchoredPosition = new Vector2(startX + i * revealSpacing, endPos.y);

                
                float t2 = 0f;
                Vector2 target = new Vector2(rt.anchoredPosition.x, endPos.y + 220f);
                while (t2 < 1f)
                {
                    t2 += Time.deltaTime / Mathf.Max(0.01f, cardRiseDuration);
                    float e = EaseOutCubic(t2);
                    rt.anchoredPosition = Vector2.Lerp(new Vector2(rt.anchoredPosition.x, endPos.y), target, e);
                    yield return null;
                }

                
                var hover = ui.gameObject.AddComponent<PackOpenedCardHover>();
                hover.Init(this, i);

                _spawned.Add(rt);
                yield return new WaitForSeconds(cardInterval);
            }

            
            for (int i = 0; i < _spawned.Count; i++)
            {
                yield return StartCoroutine(FlipReveal(_spawned[i], _frontGroups[i], _backFaces[i], _results[i]));
                
                CreateResultLabel(_spawned[i], _results[i]);
                yield return new WaitForSeconds(flipInterval);
            }

            
            if (_spawned.Count > 0)
            {
                float startX2 = -finalOverlapSpacing * (Mathf.Max(0, _spawned.Count - 1) * 0.5f);
                
                Vector2[] from = new Vector2[_spawned.Count];
                Vector2[] to = new Vector2[_spawned.Count];
                for (int i = 0; i < _spawned.Count; i++)
                {
                    from[i] = _spawned[i].anchoredPosition;
                    to[i] = new Vector2(startX2 + i * finalOverlapSpacing, from[i].y);
                }
                float t3 = 0f;
                while (t3 < 1f)
                {
                    t3 += Time.deltaTime / Mathf.Max(0.01f, compressDuration);
                    float e = EaseOutCubic(t3);
                    for (int i = 0; i < _spawned.Count; i++)
                    {
                        _spawned[i].anchoredPosition = Vector2.Lerp(from[i], to[i], e);
                    }
                    yield return null;
                }

                
                _finalPositions = new Vector2[_spawned.Count];
                for (int i = 0; i < _spawned.Count; i++)
                    _finalPositions[i] = _spawned[i].anchoredPosition;
            }

            
            if (_upgradeLabelRects != null && _upgradeLabelRects.Count > 0)
            {
                StartCoroutine(FadeOutUpgradeLabels());
            }

            continueButton.interactable = true;
        }

        public void OnContinue()
        {
            SceneParams.ClearAll();
            _ = SceneFader.Instance.LoadSceneWithFade(lobbySceneName);
        }

        private float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);

        private void CreateResultLabel(RectTransform cardRect, CollectionService.AwardResult res)
        {
            var go = new GameObject("ResultLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(cardRect, false);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = resultLabelOffset;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            string msg;
            if (res.isNew)
            {
                msg = "NEW!";
                tmp.color = resultLabelNewColor;
            }
            else
            {
                msg = $"+{res.pointsAwarded} pts";
                tmp.color = resultLabelPointsColor;
            }

            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = res.isNew ? resultLabelFontSizeNew : resultLabelFontSizePoints;
            tmp.text = msg;
            _resultLabels.Add(rt);

            
            var cp = PlayerProfile.GetOrCreateCard(res.card.CardId);
            var rarity = res.card.Rarity;
            bool showUpgrade = false;
            if (rarity != null)
            {
                int currentLevel = Mathf.Max(1, cp.level);
                if (currentLevel < rarity.MaxLevel)
                {
                    int nextLevel = currentLevel + 1;
                    int needed = rarity.GetPointsRequiredForLevel(nextLevel);
                    showUpgrade = cp.points >= needed;
                }
            }
            if (showUpgrade)
            {
                var goUp = new GameObject("UpgradeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                var rtUp = goUp.GetComponent<RectTransform>();
                rtUp.SetParent(cardRect, false);
                rtUp.anchorMin = new Vector2(0.5f, 1f);
                rtUp.anchorMax = new Vector2(0.5f, 1f);
                rtUp.pivot = new Vector2(0.5f, 0f);
                rtUp.anchoredPosition = upgradeLabelOffset;
                var tmpUp = goUp.GetComponent<TextMeshProUGUI>();
                tmpUp.text = upgradeAvailableText;
                tmpUp.color = upgradeLabelColor;
                tmpUp.fontSize = upgradeLabelFontSize;
                tmpUp.alignment = TextAlignmentOptions.Center;
                
                var cgUp = goUp.GetComponent<CanvasGroup>();
                if (cgUp == null) cgUp = goUp.AddComponent<CanvasGroup>();
                cgUp.alpha = 1f;
                _upgradeLabelRects.Add(rtUp);
            }
        }

        
        public IReadOnlyList<RectTransform> ResultLabelRects => _resultLabels;
        public IReadOnlyList<RectTransform> UpgradeLabelRects => _upgradeLabelRects;

        private IEnumerator FadeOutUpgradeLabels()
        {
            float dur = Mathf.Max(0f, upgradeLabelFadeOutDuration);
            if (dur <= 0f)
            {
                
                for (int i = 0; i < _upgradeLabelRects.Count; i++)
                {
                    var rt = _upgradeLabelRects[i]; if (rt == null) continue;
                    var cg = rt.GetComponent<CanvasGroup>() ?? rt.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                }
                yield break;
            }
            float t = 0f;
            
            var groups = new System.Collections.Generic.List<CanvasGroup>(_upgradeLabelRects.Count);
            for (int i = 0; i < _upgradeLabelRects.Count; i++)
            {
                var rt = _upgradeLabelRects[i]; if (rt == null) { groups.Add(null); continue; }
                var cg = rt.GetComponent<CanvasGroup>();
                if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
                groups.Add(cg);
            }
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, dur);
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                for (int i = 0; i < groups.Count; i++)
                {
                    var cg = groups[i]; if (cg == null) continue;
                    cg.alpha = a;
                }
                yield return null;
            }
            for (int i = 0; i < groups.Count; i++)
            {
                var cg = groups[i]; if (cg == null) continue;
                cg.alpha = 0f;
            }
        }

        private IEnumerator FlipReveal(RectTransform card, CanvasGroup front, RectTransform back, CollectionService.AwardResult res)
        {
            
            float t = 0f;
            Vector3 baseScale = card.localScale;
            
            TryPlaySfx(sfxFlipKey);
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, flipDuration);
                float e = EaseOutCubic(t);
                
                float sx;
                if (e < 0.5f)
                {
                    float p = e / 0.5f;
                    sx = Mathf.Lerp(1f, 0f, p);
                }
                else
                {
                    float p = (e - 0.5f) / 0.5f;
                    sx = Mathf.Lerp(0f, 1f, p);
                }
                
                float pulse = 1f + (flipOvershootScale - 1f) * Mathf.Sin(Mathf.PI * e);
                card.localScale = new Vector3(Mathf.Max(0.0001f, sx) * pulse, baseScale.y * pulse, baseScale.z);
                
                if (e >= 0.5f && back.gameObject.activeSelf)
                {
                    back.gameObject.SetActive(false);
                    if (front) front.alpha = 1f;
                    
                    PulseRarityColor(card, res.card?.Rarity);
                    TryPlayRarityHit(res.card?.Rarity);
                    
                    if (res.card?.Rarity != null && res.card.Rarity.ConfettiOnReveal)
                    {
                        StartCoroutine(ConfettiBurst(card));
                        StartCoroutine(ShakeTransform(card, 0.2f, 6f));
                    }
                }
                yield return null;
            }
            
            card.localScale = baseScale;
            if (front) front.alpha = 1f;
            if (back) back.gameObject.SetActive(false);
        }

        private void PulseRarityColor(RectTransform card, RarityDefinition rarity)
        {
            if (rarity == null) return;
            
            var go = new GameObject("RarityPulse", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(card, false);
            rt.SetAsFirstSibling(); 
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            
            const float padX = 18f;
            const float padY = 24f;
            rt.offsetMin = new Vector2(-padX, -padY);
            rt.offsetMax = new Vector2(+padX, +padY);
            var img = go.GetComponent<UnityEngine.UI.Image>();
            var col = rarity.Color; col.a = 0.0f; img.color = col;
            img.raycastTarget = false;
            
            StartCoroutine(PulseImageAlphaLoop(img, 0.2f, 0.55f, 1.8f));
        }

        private IEnumerator PulseImageAlpha(UnityEngine.UI.Image img, float peakAlpha, float duration)
        {
            if (img == null) yield break;
            float t = 0f;
            Color baseCol = img.color;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, duration);
                float e = EaseOutCubic(t);
                float a = Mathf.Sin(e * Mathf.PI); 
                var c = baseCol; c.a = peakAlpha * a; img.color = c;
                yield return null;
            }
            Destroy(img.gameObject);
        }

        private IEnumerator PulseImageAlphaLoop(UnityEngine.UI.Image img, float minAlpha, float maxAlpha, float speed)
        {
            if (img == null) yield break;
            minAlpha = Mathf.Clamp01(minAlpha);
            maxAlpha = Mathf.Clamp01(Mathf.Max(minAlpha, maxAlpha));
            float t = 0f;
            var baseCol = img.color;
            while (img != null && img.gameObject != null)
            {
                t += Time.deltaTime * Mathf.Max(0.01f, speed);
                float s = 0.5f * (Mathf.Sin(t * Mathf.PI * 2f) + 1f); 
                float a = Mathf.Lerp(minAlpha, maxAlpha, s);
                var c = baseCol; c.a = a; img.color = c;
                yield return null;
            }
        }

        private void TryPlaySfx(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            try { TR.Audio.SFXManager.Instance.Play(key); } catch { }
        }

        private void TryPlayRarityHit(RarityDefinition rarity)
        {
            if (rarity == null) return;
            
            string key = sfxRarityCommon;
            var id = rarity.RarityId;
            if (!string.IsNullOrEmpty(id))
            {
                var lid = id.Trim().ToLowerInvariant();
                if (lid == "legendary") key = sfxRarityLegendary;
                else if (lid == "epic") key = sfxRarityEpic;
                else if (lid == "rare") key = sfxRarityRare;
                else key = sfxRarityCommon;
            }
            TryPlaySfx(key);
        }

        private bool IsLegendary(RarityDefinition rarity)
        {
            if (rarity == null || string.IsNullOrEmpty(rarity.RarityId)) return false;
            var lid = rarity.RarityId.Trim().ToLowerInvariant();
            return lid == "legendary";
        }

        private IEnumerator ShakeTransform(RectTransform target, float duration, float amplitude)
        {
            if (target == null) yield break;
            float t = 0f;
            Vector2 basePos = target.anchoredPosition;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / duration);
                float dx = (Random.value * 2f - 1f) * amplitude * k;
                float dy = (Random.value * 2f - 1f) * amplitude * k;
                target.anchoredPosition = basePos + new Vector2(dx, dy);
                yield return null;
            }
            target.anchoredPosition = basePos;
        }

        private IEnumerator ConfettiBurst(RectTransform card)
        {
            if (card == null) yield break;
            
            var parent = card.parent as RectTransform;
            if (parent == null) yield break;

            
            var containerGO = new GameObject("ConfettiBurst", typeof(RectTransform));
            var container = containerGO.GetComponent<RectTransform>();
            container.SetParent(parent, false);
            container.SetAsLastSibling();
            container.anchorMin = new Vector2(0.5f, 0.5f);
            container.anchorMax = new Vector2(0.5f, 0.5f);
            container.pivot = new Vector2(0.5f, 0.5f);
            container.anchoredPosition = card.anchoredPosition;

            int count = 28;
            float life = 0.8f;
            float gravity = 560f; 
            float startSpeed = 320f; 
            float startSpeedJitter = 120f;
            float startScale = 0.9f;
            float endScale = 0.6f;

            var imgs = new System.Collections.Generic.List<UnityEngine.UI.Image>(count);
            var vel = new System.Collections.Generic.List<Vector2>(count);
            var angVel = new System.Collections.Generic.List<float>(count);

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Confetti", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(container, false);
                rt.sizeDelta = new Vector2(Random.Range(6f, 12f), Random.Range(6f, 12f));
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one * startScale;
                var img = go.GetComponent<UnityEngine.UI.Image>();
                img.raycastTarget = false;
                
                img.color = Color.HSVToRGB(Random.value, Random.Range(0.3f, 0.8f), 1f);
                imgs.Add(img);
                
                float ang = Mathf.Deg2Rad * Random.Range(60f, 120f);
                float spd = startSpeed + Random.Range(-startSpeedJitter, startSpeedJitter);
                vel.Add(new Vector2(Mathf.Cos(ang) * spd, Mathf.Sin(ang) * spd));
                angVel.Add(Random.Range(-720f, 720f));
            }

            float t = 0f;
            while (t < life)
            {
                float dt = Time.deltaTime;
                t += dt;
                float u = Mathf.Clamp01(t / life);
                for (int i = 0; i < imgs.Count; i++)
                {
                    var img = imgs[i]; if (img == null) continue;
                    var rt = img.rectTransform;
                    
                    var v = vel[i];
                    v.y -= gravity * dt;
                    vel[i] = v;
                    rt.anchoredPosition += v * dt;
                    
                    rt.localRotation = Quaternion.Euler(0, 0, rt.localEulerAngles.z + angVel[i] * dt);
                    
                    var c = img.color; c.a = 1f - u; img.color = c;
                    float sc = Mathf.Lerp(startScale, endScale, u);
                    rt.localScale = new Vector3(sc, sc, 1f);
                }
                yield return null;
            }
            if (container != null) Destroy(container.gameObject);
        }

        
        public void StartHover(int index)
        {
            if (_spawned.Count == 0 || _finalPositions == null) return;
            _currentHover = index;
            
            var targets = new Vector2[_spawned.Count];
            for (int i = 0; i < _spawned.Count; i++)
            {
                float dir = Mathf.Sign(i - index);
                float dist = Mathf.Abs(i - index);
                float offset = (i == index) ? 0f : dir * (hoverSpread / Mathf.Max(1f, dist));
                targets[i] = _finalPositions[i] + new Vector2(offset, 0f);
            }
            if (_hoverTween != null) StopCoroutine(_hoverTween);
            _hoverTween = StartCoroutine(AnimateHover(targets, index));
        }

        public void EndHover(int index)
        {
            if (_spawned.Count == 0 || _finalPositions == null) return;
            _currentHover = -1;
            var targets = new Vector2[_spawned.Count];
            for (int i = 0; i < _spawned.Count; i++) targets[i] = _finalPositions[i];
            if (_hoverTween != null) StopCoroutine(_hoverTween);
            _hoverTween = StartCoroutine(AnimateHover(targets, -1));
        }

        private IEnumerator AnimateHover(Vector2[] targets, int hoveredIndex)
        {
            Vector2[] from = new Vector2[_spawned.Count];
            Vector3[] scaleFrom = new Vector3[_spawned.Count];
            Vector3[] scaleTo = new Vector3[_spawned.Count];
            for (int i = 0; i < _spawned.Count; i++)
            {
                from[i] = _spawned[i].anchoredPosition;
                scaleFrom[i] = _spawned[i].localScale;
                bool isHover = (i == hoveredIndex);
                scaleTo[i] = isHover ? Vector3.one * hoverScale : Vector3.one;
            }
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, hoverAnimDuration);
                float e = EaseOutCubic(t);
                for (int i = 0; i < _spawned.Count; i++)
                {
                    _spawned[i].anchoredPosition = Vector2.Lerp(from[i], targets[i], e);
                    _spawned[i].localScale = Vector3.Lerp(scaleFrom[i], scaleTo[i], e);
                }
                yield return null;
            }
            _hoverTween = null;
        }
    }
}
