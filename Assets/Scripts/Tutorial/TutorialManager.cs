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
        [SerializeField] private string flowResourcePath;
        [Header("Optional Prefabs")]
        [SerializeField] private TutorialArrowUI arrowPrefab;
        [SerializeField] private TutorialDialogueUI dialoguePrefab;
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
                    bool resumedFirstStep = (i == startIdx) && startIdx > 0;
                    float mismatchTimer = 0f;
                    int rewindTo = -1;

                    while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != step.requiredSceneName)
                    {
                        if (_blocker != null) _blocker.Disable();
                        if (_overlayCanvas != null && _overlayCanvas.gameObject.activeSelf) _overlayCanvas.gameObject.SetActive(false);
                        if (verboseLogs) Debug.Log($"[Tutorial] Waiting for scene '{step.requiredSceneName}', current: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

                        if (resumedFirstStep)
                        {
                            mismatchTimer += Time.unscaledDeltaTime;
                            if (mismatchTimer >= resumeRecoverDelay)
                            {
                                int back = FindStepForCurrentScene(i);
                                if (back >= 0 && back != i) { rewindTo = back; break; }
                                mismatchTimer = 0f; 
                            }
                        }
                        yield return null;
                    }

                    if (rewindTo >= 0)
                    {
                        if (verboseLogs)
                            Debug.Log($"[Tutorial] Resumed on step {i} for scene '{step.requiredSceneName}' but we are in " +
                                      $"'{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'. Rewinding to step {rewindTo}.");
                        PlayerProfile.SetTutorialStep(rewindTo);
                        i = rewindTo - 1; 
                        continue;
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
                        float noTargetTimer = 0f;
                        
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
                                        EnsureTargetVisible(rtTry);
                                        _arrow.gameObject.SetActive(true);
                                        _arrow.Follow(rtTry, step.targetScreenOffset);
                                        ApplyBlocker(step, null, rtTry, restartSpotlight: false);
                                    }
                                    else
                                    {
                                        _arrow.gameObject.SetActive(false);
                                        ApplyBlocker(step, null, null, restartSpotlight: false);
                                    }
                                }

                                if (step.skipIfNoTarget)
                                {
                                    noTargetTimer += Time.unscaledDeltaTime;
                                    if (noTargetTimer >= Mathf.Max(0.01f, step.noTargetSkipDelay))
                                    {
                                        if (verboseLogs) Debug.Log($"[Tutorial] No target found for step {_stepIndex}; auto-advancing.");
                                        break;
                                    }
                                }

                                yield return null; 
                            }
                        }

                        if (btn != null)
                        {
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
                        RectTransform ghostSource = buttons.Count > 0 && buttons[0] != null
                            ? buttons[0].transform as RectTransform
                            : null;
                        if (step.showGhostDrag) BeginGhostDrag(step, ghostSource);

                        bool dragged = false;
                        while (!dragged)
                        {
                            for (int i2 = 0; i2 < listeners.Count; i2++)
                            {
                                if (listeners[i2] != null && listeners[i2].Dragged) { dragged = true; break; }
                            }
                            if (step.showGhostDrag) UpdateGhostDrag(ghostSource);
                            yield return null;
                        }
                        StopGhostDrag();
                        yield return WaitForUnassistedDrag(step);
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
                        if (step.showGhostDrag) BeginGhostDrag(step, rt);
                        while (!listener.Dragged)
                        {
                            if (step.showGhostDrag) UpdateGhostDrag(rt);
                            yield return null;
                        }
                        StopGhostDrag();
                        yield return WaitForUnassistedDrag(step);
                    }
                }
                else if (step.waitMode == StepWaitMode.WaitForNameInput)
                {
                    EnsureUI();

                    bool done = false;
                    string enteredName = null;
                    if (_nameInput != null)
                    {
                        _nameInput.Show(step.namePromptText, step.namePlaceholderText, n => { enteredName = n; done = true; });
                        _nameInput.transform.SetAsLastSibling();

                        if (_blocker != null)
                        {
                            _blocker.Enable(_nameInput.InputPanel);
                            _blocker.transform.SetAsLastSibling();
                        }

                        if (_dialogue != null) _dialogue.transform.SetAsLastSibling();
                        if (_blocker != null) _blocker.transform.SetAsLastSibling();
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
                        if (_blocker != null) _blocker.Enable(null);
                        if (_blocker != null) _blocker.transform.SetAsLastSibling();

                        string nameForGreeting = !string.IsNullOrEmpty(enteredName) ? enteredName : PlayerProfile.GetPlayerName();
                        string greeting;
                        try { greeting = string.Format(step.nameGreetingFormat, nameForGreeting); }
                        catch { greeting = step.nameGreetingFormat; }
                        Sprite greetingGuide = step.nameGreetingGuideSprite != null ? step.nameGreetingGuideSprite : step.guideSprite;
                        if (_dialogue != null) _dialogue.Show(greeting, step.typewriterCharDelay, greetingGuide);
                        float g = Mathf.Max(0f, step.nameGreetingSeconds);
                        while (g > 0f) { g -= Time.unscaledDeltaTime; yield return null; }
                    }

                    if (_blocker != null) _blocker.Disable();
                }
                else if (step.waitMode == StepWaitMode.WaitForMatchVictory)
                {
                    _matchVictory = false;
                    _matchDefeat = false;
                    TR.Battle.BattleSceneController.OnMatchVictory += OnTutorialMatchVictory;
                    TR.Battle.BattleSceneController.OnMatchDefeat += OnTutorialMatchDefeat;

                    string battleScene = step.requiredSceneName;
                    bool lost = false;

                    while (!_matchVictory)
                    {
                        if (_matchDefeat) { lost = true; break; }

                        if (!string.IsNullOrEmpty(battleScene) &&
                            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != battleScene)
                        {
                            lost = true;
                            break;
                        }
                        yield return null;
                    }

                    TR.Battle.BattleSceneController.OnMatchVictory -= OnTutorialMatchVictory;
                    TR.Battle.BattleSceneController.OnMatchDefeat -= OnTutorialMatchDefeat;

                    if (lost)
                    {
                        int retry = ResolveRetryStepIndex(step, i);
                        if (verboseLogs) Debug.Log($"[Tutorial] Match not won on step {i}; rewinding to step {retry}.");

                        string retryScene = flow.steps[retry] != null ? flow.steps[retry].requiredSceneName : null;
                        if (!string.IsNullOrEmpty(retryScene))
                        {
                            while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != retryScene)
                            {
                                if (_blocker != null) _blocker.Disable();
                                if (_overlayCanvas != null && _overlayCanvas.gameObject.activeSelf)
                                    _overlayCanvas.gameObject.SetActive(false);
                                yield return null;
                            }
                            yield return null;
                            if (_overlayCanvas != null && !_overlayCanvas.gameObject.activeSelf)
                                _overlayCanvas.gameObject.SetActive(true);
                        }

                        if (!string.IsNullOrEmpty(step.defeatDialogueText))
                        {
                            EnsureUI();
                            if (_arrow != null) _arrow.gameObject.SetActive(false);
                            Sprite guide = step.defeatGuideSprite != null ? step.defeatGuideSprite : step.guideSprite;
                            if (_dialogue != null) _dialogue.Show(step.defeatDialogueText, step.typewriterCharDelay, guide);

                            float d = Mathf.Max(0f, step.defeatMessageSeconds);
                            while (d > 0f) { d -= Time.unscaledDeltaTime; yield return null; }
                        }

                        PlayerProfile.SetTutorialStep(retry);
                        i = retry - 1; 
                        continue;
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


        private readonly System.Collections.Generic.HashSet<TutorialDragListener> _repeatArmed = new System.Collections.Generic.HashSet<TutorialDragListener>();

        private IEnumerator WaitForUnassistedDrag(TutorialStep step)
        {
            if (step == null || !step.showGhostDrag || !step.requireUnassistedRepeat) yield break;

            StopGhostDrag();
            if (_arrow != null) _arrow.gameObject.SetActive(false);
            for (int i = 0; i < _extraArrows.Count; i++)
                if (_extraArrows[i] != null) _extraArrows[i].gameObject.SetActive(false);

            if (_dialogue != null && !string.IsNullOrEmpty(step.repeatDialogueText))
            {
                Sprite guide = step.repeatGuideSprite != null ? step.repeatGuideSprite : step.guideSprite;
                _dialogue.Show(step.repeatDialogueText, step.typewriterCharDelay, step.dialogueAnchor, guide);
            }

            _repeatArmed.Clear();
            if (verboseLogs) Debug.Log("[Tutorial] Waiting for an unassisted repeat of the drag.");

            while (!PollUnassistedDrag(step)) yield return null;

            _repeatArmed.Clear();
        }

        private bool PollUnassistedDrag(TutorialStep step)
        {
            var hosts = new System.Collections.Generic.List<GameObject>();

            if (step.targetMode == TargetMode.OwnedCollectionCards)
            {
                var buttons = ResolveButtonsListOwnedCards();
                if (buttons != null)
                {
                    for (int i = 0; i < buttons.Count; i++)
                        if (buttons[i] != null) hosts.Add(buttons[i].gameObject);
                }
            }
            else
            {
                var rt = ResolveRect(step);
                if (rt != null)
                {
                    var btn = rt.GetComponentInChildren<Button>(true);
                    hosts.Add(btn != null ? btn.gameObject : rt.gameObject);
                }
            }

            bool many = step.targetMode == TargetMode.OwnedCollectionCards;

            for (int i = 0; i < hosts.Count; i++)
            {
                var host = hosts[i];
                if (host == null) continue;

                var listener = host.GetComponent<TutorialDragListener>();
                if (listener == null) listener = host.AddComponent<TutorialDragListener>();

                if (_repeatArmed.Add(listener))
                {
                    listener.minPixels = many ? 30f : 20f;
                    listener.requireExitRect = many;
                    listener.ResetFlag();
                    continue;
                }

                if (listener.Dragged) return true;
            }

            return false;
        }

        private TutorialGhostDragUI _ghostDrag;

        private void BeginGhostDrag(TutorialStep step, RectTransform source)
        {
            if (source == null) return;
            EnsureUI();
            if (_overlayCanvas == null) return;

            if (_ghostDrag == null)
                _ghostDrag = TutorialGhostDragUI.Create(_overlayCanvas.transform);

            if (!TryGetGhostEndpoints(source, out Vector2 from, out Vector2 to)) return;

            Sprite sprite = step.ghostDragSprite;
            if (sprite == null) sprite = ResolveTowerSprite(source);

            _ghostDrag.Play(from, to, sprite);

            if (_arrow != null && _ghostDrag.Rect != null)
            {
                _arrow.gameObject.SetActive(true);
                _arrow.Follow(_ghostDrag.Rect, step.targetScreenOffset);
            }
        }

        private Sprite ResolveTowerSprite(RectTransform source)
        {
            var def = ResolveCardFromSource(source);
            var prefab = def != null ? def.TowerPrefab : null;
            if (prefab == null) return null;

            Sprite best = null;
            float bestArea = -1f;
            foreach (var sr in prefab.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null || sr.sprite == null) continue;
                var b = sr.sprite.bounds.size;
                float area = b.x * b.y;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = sr.sprite;
                }
            }
            return best;
        }

        private TR.Data.CardDefinition ResolveCardFromSource(RectTransform source)
        {
            if (source == null) return null;

            var drag = source.GetComponentInParent<TR.Battle.CardDragPlacement>();
            if (drag == null) drag = source.GetComponentInChildren<TR.Battle.CardDragPlacement>(true);
            if (drag != null && drag.Card != null) return drag.Card;

            var cardItem = source.GetComponentInParent<TR.UI.CardItemUI>();
            if (cardItem == null) cardItem = source.GetComponentInChildren<TR.UI.CardItemUI>(true);
            if (cardItem != null && !string.IsNullOrEmpty(cardItem.CardId))
                return GameDB.GetCardById(cardItem.CardId);

            return null;
        }

        private void UpdateGhostDrag(RectTransform source)
        {
            if (_ghostDrag == null || source == null) return;
            if (TryGetGhostEndpoints(source, out Vector2 from, out Vector2 to))
                _ghostDrag.UpdateEndpoints(from, to);
        }

        private void StopGhostDrag()
        {
            if (_ghostDrag != null) _ghostDrag.StopAndHide();
        }

        private bool TryGetGhostEndpoints(RectTransform source, out Vector2 from, out Vector2 to)
        {
            from = RectTransformUtility.WorldToScreenPoint(null, source.position);
            to = from;

            var placement = FindFirstObjectByType<TR.Battle.TowerPlacementController>(FindObjectsInactive.Include);
            var cam = Camera.main;
            if (placement == null || cam == null) return false;
            if (!placement.TryGetSuggestedPlacementPoint(out Vector3 worldPos)) return false;

            to = cam.WorldToScreenPoint(worldPos);
            return true;
        }

        private bool _matchVictory;
        private bool _matchDefeat;
        private void OnTutorialMatchVictory() { _matchVictory = true; }
        private void OnTutorialMatchDefeat() { _matchDefeat = true; }

        [SerializeField] private float resumeRecoverDelay = 2.5f;

        private int FindStepForCurrentScene(int before)
        {
            string current = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            for (int i = before - 1; i >= 0; i--)
            {
                var s = flow.steps[i];
                if (s == null || string.IsNullOrEmpty(s.requiredSceneName)) continue;
                if (string.Equals(s.requiredSceneName, current, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private int ResolveRetryStepIndex(TutorialStep step, int currentIndex)
        {
            if (step.defeatRewindToStep >= 0 && step.defeatRewindToStep < flow.steps.Count)
                return step.defeatRewindToStep;

            for (int i = currentIndex - 1; i >= 0; i--)
            {
                var s = flow.steps[i];
                if (s == null) continue;
                if (string.IsNullOrEmpty(s.requiredSceneName)) continue;
                if (!string.Equals(s.requiredSceneName, step.requiredSceneName, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return 0;
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
                _dialogue.Show(step.dialogueText, step.typewriterCharDelay, step.dialogueAnchor, step.guideSprite);
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

                        EnsureTargetVisible(targets[0]);

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
                        
                        ApplyBlocker(step, targets, null);
                    }
                    else
                    {
                        _arrow.gameObject.SetActive(false);
                        ApplyBlocker(step, null, null);
                    }
                }
                else
                {
                    var target = ResolveRect(step);
                    if (target != null)
                    {
                        EnsureTargetVisible(target);
                        _arrow.gameObject.SetActive(true);
                        _arrow.Follow(target, step.targetScreenOffset);
                        ApplyBlocker(step, null, target);
                    }
                    else
                    {
                        _arrow.gameObject.SetActive(false);
                        ApplyBlocker(step, null, null);
                    }
                }
            }
        }

        private static bool ShouldSpotlight(TutorialStep step, bool hasTarget)
        {
            if (step == null || !hasTarget) return false;
            switch (step.spotlight)
            {
                case SpotlightMode.Always: return true;
                case SpotlightMode.Never: return false;
                default: return step.waitMode == StepWaitMode.WaitForTargetClick;
            }
        }

        private void ApplyBlocker(TutorialStep step, System.Collections.Generic.List<RectTransform> many, RectTransform single, bool restartSpotlight = true)
        {
            if (_blocker == null) return;

            bool hasTarget = single != null || (many != null && many.Count > 0);
            bool spot = ShouldSpotlight(step, hasTarget);

            if (!step.blockOutside && !spot)
            {
                _blocker.Disable();
                return;
            }

            if (many != null) _blocker.EnableMany(many);
            else _blocker.Enable(single);

            _blocker.BlockInput = step.blockOutside || spot;
            _blocker.SetSpotlight(spot, restartSpotlight);

            RaiseTutorialUIAboveBlocker();
        }


        [Header("Target Focus")]
        [SerializeField] private bool scrollTargetIntoView = true;
        [SerializeField] private float scrollFocusSeconds = 0.35f;
        [SerializeField] private float scrollVisibleMargin = 8f;

        private Coroutine _scrollFocus;

        private void EnsureTargetVisible(RectTransform target)
        {
            if (!scrollTargetIntoView || target == null) return;
            if (_scrollFocus != null) return; 

            var scroll = target.GetComponentInParent<ScrollRect>(true);
            if (scroll == null || scroll.content == null) return;
            if (!scroll.horizontal && !scroll.vertical) return;
            if (IsFullyVisible(scroll, target)) return;

            _scrollFocus = StartCoroutine(ScrollTargetIntoView(scroll, target));
        }

        private bool IsFullyVisible(ScrollRect scroll, RectTransform target)
        {
            var viewport = scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
            if (viewport == null) return true;

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);

            Rect view = viewport.rect;
            for (int i = 0; i < 4; i++)
            {
                Vector2 lp = viewport.InverseTransformPoint(corners[i]);
                if (lp.x < view.xMin + scrollVisibleMargin || lp.x > view.xMax - scrollVisibleMargin) return false;
                if (lp.y < view.yMin + scrollVisibleMargin || lp.y > view.yMax - scrollVisibleMargin) return false;
            }
            return true;
        }

        private IEnumerator ScrollTargetIntoView(ScrollRect scroll, RectTransform target)
        {
            var viewport = scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
            var content = scroll.content;

            Canvas.ForceUpdateCanvases();
            scroll.velocity = Vector2.zero;

            Vector2 from = content.anchoredPosition;

            Vector2 targetInView = viewport.InverseTransformPoint(target.TransformPoint(target.rect.center));
            Vector2 delta = viewport.rect.center - targetInView;
            if (!scroll.horizontal) delta.x = 0f;
            if (!scroll.vertical) delta.y = 0f;

            content.anchoredPosition = from + delta;
            Canvas.ForceUpdateCanvases();
            if (scroll.horizontal) scroll.horizontalNormalizedPosition = Mathf.Clamp01(scroll.horizontalNormalizedPosition);
            if (scroll.vertical) scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition);
            Vector2 to = content.anchoredPosition;

            content.anchoredPosition = from;

            float t = 0f;
            float dur = Mathf.Max(0.01f, scrollFocusSeconds);
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / dur;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                content.anchoredPosition = Vector2.Lerp(from, to, e);
                scroll.velocity = Vector2.zero; 
                yield return null;
            }

            content.anchoredPosition = to;
            _scrollFocus = null;
        }

        private void RaiseTutorialUIAboveBlocker()
        {
            if (_dialogue != null && _blocker != null
                && _dialogue.transform.parent == _blocker.transform.parent
                && _dialogue.transform.GetSiblingIndex() > _blocker.transform.GetSiblingIndex())
                return;

            if (_arrow != null) _arrow.transform.SetAsLastSibling();
            for (int i = 0; i < _extraArrows.Count; i++)
                if (_extraArrows[i] != null) _extraArrows[i].transform.SetAsLastSibling();
            if (_dialogue != null) _dialogue.transform.SetAsLastSibling();
            if (_nameInput != null) _nameInput.transform.SetAsLastSibling();
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

            if (_scrollFocus != null) { StopCoroutine(_scrollFocus); _scrollFocus = null; }
            _repeatArmed.Clear();
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
                case TargetMode.UpgradeReadyCollectionCard:
                    var collectionTiles = Object.FindObjectsByType<TR.UI.CollectionItemUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var it in collectionTiles)
                    {
                        if (it != null && it.Card != null && it.CanUpgradeNow())
                        {
                            var upgradeBtn = it.UpgradeButton;
                            if (upgradeBtn != null) return upgradeBtn.GetComponent<RectTransform>();
                            return it.GetComponent<RectTransform>();
                        }
                    }
                    return null;
                case TargetMode.TrophyRoadClaimable:
                    var nodes = Object.FindObjectsByType<TR.UI.TrophyRoad.TrophyRoadNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var node in nodes)
                    {
                        if (node != null && node.IsClaimable)
                        {
                            var claimBtn = node.ClaimButton;
                            if (claimBtn != null) return claimBtn.GetComponent<RectTransform>();
                            return node.GetComponent<RectTransform>();
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
                case TargetMode.UpgradeReadyCollectionCard:
                    var collectionItems = Object.FindObjectsByType<TR.UI.CollectionItemUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var it in collectionItems)
                    {
                        if (it != null && it.Card != null && it.CanUpgradeNow())
                        {
                            var upgradeBtn = it.UpgradeButton;
                            if (upgradeBtn != null) return upgradeBtn;
                        }
                    }
                    return null;
                case TargetMode.TrophyRoadClaimable:
                    var roadNodes = Object.FindObjectsByType<TR.UI.TrophyRoad.TrophyRoadNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var node in roadNodes)
                    {
                        if (node != null && node.IsClaimable)
                        {
                            var claimBtn = node.ClaimButton;
                            if (claimBtn != null) return claimBtn;
                        }
                    }
                    return null;
                default:
                    return null;
            }
        }
    }
}
