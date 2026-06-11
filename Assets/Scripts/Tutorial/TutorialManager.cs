using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TR.Systems;

namespace TR.Tutorial
{
    
    
    public class TutorialManager : MonoBehaviour
    {
        [SerializeField] private TutorialFlow flow;
        [Tooltip("Optional: if flow is not assigned, load from Resources at this path (e.g., 'Tutorial/StarterTutorialFlow')")]
        [SerializeField] private string flowResourcePath;
        [Header("Optional Prefabs")]
        [SerializeField] private TutorialArrowUI arrowPrefab;
        [SerializeField] private TutorialDialogueUI dialoguePrefab;
        [Tooltip("Prefab for the name-input panel used by WaitForNameInput steps. Required for name steps.")]
        [SerializeField] private TutorialNameInputUI nameInputPrefab;

        private TutorialArrowUI _arrow;
        private TutorialDialogueUI _dialogue;
        private TutorialBlockerUI _blocker;
        private TutorialNameInputUI _nameInput;
        private Canvas _overlayCanvas;
        private int _stepIndex = -1;
        private Button _listenedButton;
        private readonly System.Collections.Generic.List<TutorialArrowUI> _extraArrows = new System.Collections.Generic.List<TutorialArrowUI>();

        private static TutorialManager _instance;
        public static TutorialManager Instance => _instance;

        
        private int _resumeIndex = 0;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            if (flow == null && !string.IsNullOrEmpty(flowResourcePath))
            {
                flow = Resources.Load<TutorialFlow>(flowResourcePath);
                if (verboseLogs) Debug.Log(flow != null ? "[Tutorial] Loaded flow from Resources." : "[Tutorial] Flow resource not found.");
            }
        }

        private void Start()
        {
            
            if (PlayerProfile.GetTutorialActive())
            {
                _resumeIndex = Mathf.Max(0, PlayerProfile.GetTutorialStep());
                
                if (flow != null && _resumeIndex >= flow.steps.Count)
                {
                    if (verboseLogs) Debug.Log("[Tutorial] Saved step is out of range; resetting to 0.");
                    ResetTutorialProgress();
                    TryAutoStartIfEligible();
                }
                else
                {
                    if (verboseLogs) Debug.Log($"[Tutorial] Resuming at step {_resumeIndex}");
                    StartTutorial();
                }
                
                StartCoroutine(ResumeSafeguard());
            }
            else
            {
                TryAutoStartIfEligible();
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            
            bool activeFlag = PlayerProfile.GetTutorialActive();
            if (IsRunning() || activeFlag)
            {
                
                EnsureUI();
                if (IsRunning())
                {
                    
                    var step = flow.steps[_stepIndex];
                    
                    if (!string.IsNullOrEmpty(step.requiredSceneName) && s.name != step.requiredSceneName)
                    {
                        if (verboseLogs) Debug.Log($"[Tutorial] Scene loaded: {s.name}, step {_stepIndex} requires {step.requiredSceneName}. Hiding tutorial UI until scene matches.");
                        HideAllUI();
                        if (_overlayCanvas != null) _overlayCanvas.gameObject.SetActive(false);
                    }
                    else
                    {
                        if (verboseLogs) Debug.Log($"[Tutorial] Scene loaded: {s.name}, re-show step {_stepIndex}");
                        if (_overlayCanvas != null && !_overlayCanvas.gameObject.activeSelf) _overlayCanvas.gameObject.SetActive(true);
                        ShowStepUI(step);
                    }
                }
                else if (activeFlag)
                {
                    
                    _resumeIndex = Mathf.Max(0, PlayerProfile.GetTutorialStep());
                    if (flow != null && _resumeIndex >= flow.steps.Count)
                    {
                        if (verboseLogs) Debug.Log("[Tutorial] Saved step out of range on scene load; resetting.");
                        ResetTutorialProgress();
                        TryAutoStartIfEligible();
                    }
                    else
                    {
                        if (verboseLogs) Debug.Log($"[Tutorial] Scene loaded: {s.name}, restarting tutorial at {_resumeIndex}");
                        if (_overlayCanvas != null && !_overlayCanvas.gameObject.activeSelf) _overlayCanvas.gameObject.SetActive(true);
                        StartTutorial();
                    }
                }
            }
        }

        private IEnumerator ResumeSafeguard()
        {
            
            yield return null;
            float t = 0.5f;
            while (t > 0f) { t -= Time.unscaledDeltaTime; yield return null; }
            bool activeFlag = PlayerProfile.GetTutorialActive();
            if (activeFlag && !IsRunning())
            {
                if (verboseLogs) Debug.Log("[Tutorial] ResumeSafeguard re-invoking StartTutorial.");
                StartTutorial();
            }
        }

        private void TryAutoStartIfEligible()
        {
            if (flow == null) return;
            if (!flow.autoStartForFreshProfiles) return;
            var data = PlayerProfile.Data;
            bool noTrophies = data.trophies <= 0;
            bool noCards = true;
            if (data.cards != null)
            {
                for (int i = 0; i < data.cards.Count; i++)
                {
                    if (data.cards[i].ownedCount > 0) { noCards = false; break; }
                }
            }
            if (noTrophies && noCards)
            {
                
                _resumeIndex = 0;
                PlayerProfile.SetTutorialActive(false);
                PlayerProfile.SetTutorialStep(0);
                StartTutorial();
            }
        }

        public void StartTutorial()
        {
            if (flow == null) { Debug.LogWarning("[Tutorial] No flow assigned."); return; }
            StopAllCoroutines();
            EnsureUI();
            _stepIndex = -1;
            
            PlayerProfile.SetTutorialActive(true);
            PlayerProfile.SetTutorialStep(Mathf.Max(0, _resumeIndex));
            StartCoroutine(Run());
        }

        
        public void ResetTutorialProgress()
        {
            _resumeIndex = 0;
            PlayerProfile.SetTutorialActive(false);
            PlayerProfile.SetTutorialStep(0);
            HideAllUI();
        }

        public void StartFromBeginning()
        {
            ResetTutorialProgress();
            StartTutorial();
        }

        public void SetCurrentStepForDebug(int stepIndex)
        {
            _resumeIndex = Mathf.Clamp(stepIndex, 0, flow != null ? Mathf.Max(0, flow.steps.Count - 1) : 0);
            PlayerProfile.SetTutorialActive(true);
            PlayerProfile.SetTutorialStep(_resumeIndex);
            StartTutorial();
        }

        private bool IsRunning()
        {
            return flow != null && _stepIndex >= 0 && _stepIndex < flow.steps.Count;
        }

        private IEnumerator Run()
        {
            int startIdx = Mathf.Clamp(_resumeIndex, 0, Mathf.Max(0, flow.steps.Count - 1));
            for (int i = startIdx; i < flow.steps.Count; i++)
            {
                _stepIndex = i;
                var step = flow.steps[i];
                
                if (!string.IsNullOrEmpty(step.requiredSceneName))
                {
                    
                    while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != step.requiredSceneName)
                    {
                        if (_blocker != null) _blocker.Disable();
                        if (_overlayCanvas != null && _overlayCanvas.gameObject.activeSelf) _overlayCanvas.gameObject.SetActive(false);
                        if (verboseLogs) Debug.Log($"[Tutorial] Waiting for scene '{step.requiredSceneName}', current: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                        yield return null;
                    }
                    
                    yield return null; 
                    float settle = 0.1f; while (settle > 0f) { settle -= Time.unscaledDeltaTime; yield return null; }
                    if (_overlayCanvas != null && !_overlayCanvas.gameObject.activeSelf) _overlayCanvas.gameObject.SetActive(true);
                }

                
                if (!string.IsNullOrEmpty(step.autoClickObjectNameOnStart))
                {
                    var go = GameObject.Find(step.autoClickObjectNameOnStart);
                    var btn = go != null ? go.GetComponent<Button>() : null;
                    if (btn != null)
                    {
                        if (verboseLogs) Debug.Log($"[Tutorial] Auto-clicking '{step.autoClickObjectNameOnStart}' for step {i}.");
                        btn.onClick?.Invoke();
                        
                        yield return null;
                    }
                }

                ShowStepUI(step);

                
                if (step.waitMode == StepWaitMode.WaitSeconds)
                {
                    float t = Mathf.Max(0f, step.waitSeconds);
                    while (t > 0f)
                    {
                        t -= Time.deltaTime;
                        yield return null;
                    }
                }
                else if (step.waitMode == StepWaitMode.WaitForTargetClick)
                {
                    if (step.targetMode == TargetMode.OwnedCollectionCards)
                    {
                        
                        var buttons = ResolveButtonsListOwnedCards();
                        while (buttons == null || buttons.Count == 0)
                        {
                            
                            buttons = ResolveButtonsListOwnedCards();
                            yield return null;
                        }
                        bool clicked = false;
                        void AnyClicked() { clicked = true; }
                        for (int bi = 0; bi < buttons.Count; bi++)
                        {
                            if (buttons[bi] != null) buttons[bi].onClick.AddListener(AnyClicked);
                        }
                        while (!clicked)
                        {
                            yield return null;
                        }
                        for (int bi = 0; bi < buttons.Count; bi++)
                        {
                            if (buttons[bi] != null) buttons[bi].onClick.RemoveListener(AnyClicked);
                        }
                    }
                    else
                    {
                        
                        Button btn = null;
                        
                        while (btn == null)
                        {
                            btn = ResolveButton(step);
                            if (btn == null)
                            {
                                
                                var rtTry = ResolveRect(step);
                                if (_arrow != null)
                                {
                                    if (rtTry != null)
                                    {
                                        _arrow.gameObject.SetActive(true);
                                        _arrow.Follow(rtTry, step.targetScreenOffset);
                                        if (_blocker != null)
                                        {
                                            if (step.blockOutside) _blocker.Enable(rtTry); else _blocker.Disable();
                                        }
                                    }
                                    else
                                    {
                                        _arrow.gameObject.SetActive(false);
                                        if (_blocker != null)
                                        {
                                            if (step.blockOutside) _blocker.Enable(null); else _blocker.Disable();
                                        }
                                    }
                                }
                                yield return null; 
                            }
                        }

                        _buttonClickedFlag = false;
                        _listenedButton = btn;
                        btn.onClick.AddListener(OnListenedButtonClicked);
                        while (true)
                        {
                            
                            if (_listenedButton == null) break; 
                            yield return null;
                            if (_listenedButton == null) break;
                            if (_buttonClickedFlag) break;
                        }
                        
                        if (_listenedButton != null)
                        {
                            _listenedButton.onClick.RemoveListener(OnListenedButtonClicked);
                            _listenedButton = null;
                        }
                        _buttonClickedFlag = false;
                    }
                }
                else if (step.waitMode == StepWaitMode.WaitForTargetDrag)
                {
                    if (step.targetMode == TargetMode.OwnedCollectionCards)
                    {
                        
                        var buttons = ResolveButtonsListOwnedCards();
                        while (buttons == null || buttons.Count == 0)
                        {
                            buttons = ResolveButtonsListOwnedCards();
                            yield return null;
                        }
                        var listeners = new System.Collections.Generic.List<TutorialDragListener>();
                        for (int bi = 0; bi < buttons.Count; bi++)
                        {
                            var btn = buttons[bi];
                            if (btn == null) continue;
                            var l = btn.GetComponent<TutorialDragListener>();
                            if (l == null) l = btn.gameObject.AddComponent<TutorialDragListener>();
                            l.minPixels = 30f;
                            l.requireExitRect = true; 
                            l.ResetFlag();
                            listeners.Add(l);
                        }
                        bool dragged = false;
                        while (!dragged)
                        {
                            for (int i2 = 0; i2 < listeners.Count; i2++)
                            {
                                if (listeners[i2] != null && listeners[i2].Dragged) { dragged = true; break; }
                            }
                            yield return null;
                        }
                    }
                    else
                    {
                        
                        TutorialDragListener listener = null;
                        Button tgtBtn = null;
                        RectTransform rt = null;
                        while (listener == null)
                        {
                            rt = ResolveRect(step);
                            if (rt != null)
                            {
                                tgtBtn = rt.GetComponentInChildren<Button>(true);
                                var host = (tgtBtn != null ? tgtBtn.gameObject : rt.gameObject);
                                listener = host.GetComponent<TutorialDragListener>();
                                if (listener == null) listener = host.AddComponent<TutorialDragListener>();
                                listener.minPixels = 20f;
                                listener.requireExitRect = false;
                                listener.ResetFlag();
                            }
                            else
                            {
                                yield return null;
                            }
                        }
                        while (!listener.Dragged) { yield return null; }
                    }
                }
                else if (step.waitMode == StepWaitMode.WaitForNameInput)
                {
                    EnsureUI();
                    
                    if (_blocker != null) _blocker.Disable();
                    bool done = false;
                    string enteredName = null;
                    if (_nameInput != null)
                    {
                        _nameInput.Show(step.namePromptText, step.namePlaceholderText, n => { enteredName = n; done = true; });
                        
                        if (_dialogue != null) _dialogue.transform.SetAsLastSibling();
                    }
                    else
                    {
                        Debug.LogWarning("[Tutorial] WaitForNameInput step but no name input UI available; skipping.");
                        done = true;
                    }
                    while (!done) yield return null;
                    if (_nameInput != null) _nameInput.Hide();

                    
                    if (!string.IsNullOrEmpty(step.nameGreetingFormat))
                    {
                        string nameForGreeting = !string.IsNullOrEmpty(enteredName) ? enteredName : PlayerProfile.GetPlayerName();
                        string greeting;
                        try { greeting = string.Format(step.nameGreetingFormat, nameForGreeting); }
                        catch { greeting = step.nameGreetingFormat; }
                        if (_dialogue != null) _dialogue.Show(greeting, step.typewriterCharDelay);
                        float g = Mathf.Max(0f, step.nameGreetingSeconds);
                        while (g > 0f) { g -= Time.unscaledDeltaTime; yield return null; }
                    }
                }
                else
                {
                    
                    yield return null;
                }

                
                PlayerProfile.SetTutorialStep(i + 1);
            }
            
            HideAllUI();
            _stepIndex = -1;
            _resumeIndex = 0;
            PlayerProfile.SetTutorialActive(false);
            PlayerProfile.SetTutorialStep(0);
        }

        private bool _buttonClickedFlag = false;
        private void OnListenedButtonClicked()
        {
            _buttonClickedFlag = true;
        }

        private void ShowStepUI(TutorialStep step)
        {
            EnsureUI();
            
            if (!string.IsNullOrEmpty(step.requiredSceneName) &&
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != step.requiredSceneName)
            {
                if (verboseLogs) Debug.Log("[Tutorial] ShowStepUI aborted: scene mismatch.");
                HideAllUI();
                if (_overlayCanvas != null) _overlayCanvas.gameObject.SetActive(false);
                return;
            }
            if (_overlayCanvas != null && !_overlayCanvas.gameObject.activeSelf) _overlayCanvas.gameObject.SetActive(true);
            
            if (_dialogue != null)
            {
                _dialogue.Show(step.dialogueText, step.typewriterCharDelay);
            }
            
            if (_arrow != null)
            {
                
                for (int k = 0; k < _extraArrows.Count; k++)
                {
                    if (_extraArrows[k] != null) _extraArrows[k].gameObject.SetActive(false);
                }
                _extraArrows.Clear();

                if (step.targetMode == TargetMode.OwnedCollectionCards)
                {
                    var targets = ResolveRectsList(step);
                    if (targets != null && targets.Count > 0)
                    {
                        
                        int count = Mathf.Min(step.maxArrows <= 0 ? targets.Count : step.maxArrows, targets.Count);
                        
                        _arrow.gameObject.SetActive(true);
                        _arrow.Follow(targets[0], step.targetScreenOffset);
                        
                        for (int idx = 1; idx < count; idx++)
                        {
                            var inst = (arrowPrefab != null)
                                ? Instantiate(arrowPrefab, _overlayCanvas.transform, false)
                                : new GameObject("TutorialArrowUI_Extra").AddComponent<TutorialArrowUI>();
                            if (inst.transform.parent == null) inst.transform.SetParent(_overlayCanvas.transform, false);
                            inst.gameObject.name = "TutorialArrowUI_Extra";
                            inst.Follow(targets[idx], step.targetScreenOffset);
                            _extraArrows.Add(inst);
                        }
                        
                        if (_blocker != null)
                        {
                            if (step.blockOutside) _blocker.EnableMany(targets); else _blocker.Disable();
                        }
                    }
                    else
                    {
                        _arrow.gameObject.SetActive(false);
                        if (_blocker != null)
                        {
                            if (step.blockOutside) _blocker.Enable(null); else _blocker.Disable();
                        }
                    }
                }
                else
                {
                    var target = ResolveRect(step);
                    if (target != null)
                    {
                        _arrow.gameObject.SetActive(true);
                        _arrow.Follow(target, step.targetScreenOffset);
                        if (_blocker != null)
                        {
                            if (step.blockOutside) _blocker.Enable(target); else _blocker.Disable();
                        }
                    }
                    else
                    {
                        _arrow.gameObject.SetActive(false);
                        if (_blocker != null)
                        {
                            if (step.blockOutside) _blocker.Enable(null); else _blocker.Disable();
                        }
                    }
                }
            }
        }

        private void HideAllUI()
        {
            if (_arrow != null) _arrow.gameObject.SetActive(false);
            for (int i = 0; i < _extraArrows.Count; i++)
            {
                var a = _extraArrows[i];
                if (a != null) a.gameObject.SetActive(false);
            }
            _extraArrows.Clear();
            if (_dialogue != null) _dialogue.Hide();
            if (_blocker != null) _blocker.Disable();
        }

        private RectTransform ResolveRect(TutorialStep step)
        {
            if (step == null) return null;
            switch (step.targetMode)
            {
                case TargetMode.ByName:
                    if (string.IsNullOrEmpty(step.targetObjectName)) return null;
                    var goByName = GameObject.Find(step.targetObjectName);
                    return goByName != null ? goByName.GetComponent<RectTransform>() : null;
                case TargetMode.ShopPackById:
                    if (string.IsNullOrEmpty(step.targetPackId)) return null;
                    var items = Object.FindObjectsByType<TR.UI.ShopPackItemUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var it in items)
                    {
                        if (it != null && it.PackId == step.targetPackId)
                        {
                            
                            var btn = it.OpenButton;
                            if (btn != null) return btn.GetComponent<RectTransform>();
                            return it.GetComponent<RectTransform>();
                        }
                    }
                    return null;
                case TargetMode.OwnedCollectionCards:
                    
                    var list = ResolveRectsList(step);
                    return (list != null && list.Count > 0) ? list[0] : null;
                default:
                    return null;
            }
        }

        private System.Collections.Generic.List<RectTransform> ResolveRectsList(TutorialStep step)
        {
            var result = new System.Collections.Generic.List<RectTransform>();
            if (step == null) return result;
            if (step.targetMode == TargetMode.OwnedCollectionCards)
            {
                
                var cardTiles = Object.FindObjectsByType<TR.UI.CardItemUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (cardTiles != null)
                {
                    for (int i = 0; i < cardTiles.Length; i++)
                    {
                        var tile = cardTiles[i];
                        if (tile == null || string.IsNullOrEmpty(tile.CardId)) continue;
                        var cp = TR.Systems.PlayerProfile.GetOrCreateCard(tile.CardId);
                        if (cp != null && cp.ownedCount > 0)
                        {
                            var rt = tile.GetComponent<RectTransform>();
                            if (rt != null) result.Add(rt);
                        }
                    }
                }
                if (result.Count == 0)
                {
                    var tiles = Object.FindObjectsByType<TR.UI.CollectionItemUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    if (tiles != null)
                    {
                        for (int i = 0; i < tiles.Length; i++)
                        {
                            var tile = tiles[i];
                            if (tile == null || tile.Card == null) continue;
                            var cp = TR.Systems.PlayerProfile.GetOrCreateCard(tile.Card.CardId);
                            if (cp != null && cp.ownedCount > 0)
                            {
                                var rt = tile.GetComponent<RectTransform>();
                                if (rt != null) result.Add(rt);
                            }
                        }
                    }
                }
            }
            return result;
        }

        private System.Collections.Generic.List<Button> ResolveButtonsListOwnedCards()
        {
            var list = new System.Collections.Generic.List<Button>();
            var cardTiles = Object.FindObjectsByType<TR.UI.CardItemUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (cardTiles != null)
            {
                for (int i = 0; i < cardTiles.Length; i++)
                {
                    var tile = cardTiles[i];
                    if (tile == null || string.IsNullOrEmpty(tile.CardId)) continue;
                    var cp = TR.Systems.PlayerProfile.GetOrCreateCard(tile.CardId);
                    if (cp != null && cp.ownedCount > 0)
                    {
                        var btn = tile.GetComponentInChildren<Button>(true);
                        if (btn != null) list.Add(btn);
                    }
                }
            }
            if (list.Count == 0)
            {
                var tiles = Object.FindObjectsByType<TR.UI.CollectionItemUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (tiles != null)
                {
                    for (int i = 0; i < tiles.Length; i++)
                    {
                        var tile = tiles[i];
                        if (tile == null || tile.Card == null) continue;
                        var cp = TR.Systems.PlayerProfile.GetOrCreateCard(tile.Card.CardId);
                        if (cp != null && cp.ownedCount > 0)
                        {
                            var btn = tile.GetComponentInChildren<Button>(true);
                            if (btn != null) list.Add(btn);
                        }
                    }
                }
            }
            return list;
        }

        private void EnsureUI()
        {
            
            if (_overlayCanvas == null)
            {
                var overlayGO = new GameObject("TutorialOverlayCanvas");
                _overlayCanvas = overlayGO.AddComponent<Canvas>();
                _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _overlayCanvas.sortingOrder = 999;
                overlayGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                var scaler = overlayGO.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                DontDestroyOnLoad(overlayGO);
            }
            if (_arrow == null)
            {
                if (arrowPrefab != null)
                {
                    _arrow = Instantiate(arrowPrefab, _overlayCanvas.transform, false);
                    _arrow.gameObject.name = "TutorialArrowUI";
                }
                else
                {
                    var go = new GameObject("TutorialArrowUI");
                    go.transform.SetParent(_overlayCanvas.transform, false);
                    _arrow = go.AddComponent<TutorialArrowUI>();
                }
            }
            if (_dialogue == null)
            {
                if (dialoguePrefab != null)
                {
                    _dialogue = Instantiate(dialoguePrefab, _overlayCanvas.transform, false);
                    _dialogue.gameObject.name = "TutorialDialogueUI";
                }
                else
                {
                    var go = new GameObject("TutorialDialogueUI");
                    go.transform.SetParent(_overlayCanvas.transform, false);
                    _dialogue = go.AddComponent<TutorialDialogueUI>();
                }
            }
            if (_blocker == null)
            {
                var go = new GameObject("TutorialBlockerUI");
                go.transform.SetParent(_overlayCanvas.transform, false);
                _blocker = go.AddComponent<TutorialBlockerUI>();
                _blocker.AttachToCanvas(_overlayCanvas);
                _blocker.Disable();
            }
            
            if (_nameInput == null && nameInputPrefab != null)
            {
                _nameInput = Instantiate(nameInputPrefab, _overlayCanvas.transform, false);
                _nameInput.gameObject.name = "TutorialNameInputUI";
                _nameInput.transform.SetAsLastSibling();
                _nameInput.Hide();
            }
        }

        private Button ResolveButton(TutorialStep step)
        {
            if (step == null) return null;
            switch (step.targetMode)
            {
                case TargetMode.ByName:
                    if (string.IsNullOrEmpty(step.targetObjectName)) return null;
                    var go = GameObject.Find(step.targetObjectName);
                    return go != null ? go.GetComponent<Button>() : null;
                case TargetMode.ShopPackById:
                    if (string.IsNullOrEmpty(step.targetPackId)) return null;
                    var items2 = Object.FindObjectsByType<TR.UI.ShopPackItemUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var it in items2)
                    {
                        if (it != null && it.PackId == step.targetPackId)
                        {
                            var btn = it.OpenButton;
                            if (btn != null) return btn;
                        }
                    }
                    return null;
                default:
                    return null;
            }
        }
    }
}
