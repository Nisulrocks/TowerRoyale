using UnityEngine;
using System.Collections;
using TMPro;
using TR.Audio;

namespace TR.Battle
{
    
    public class BattleEconomyUI : MonoBehaviour
    {
        [SerializeField] private MatchEconomy economy;
        [SerializeField] private TMP_Text moneyText;
        [SerializeField] private string format = "$ {0}"; 
        [Header("Floating Delta Text")]

        [SerializeField] private TMP_Text deltaTextPrefab;

        [SerializeField] private Vector2 deltaOffset = new Vector2(0f, 0f);
        [Tooltip("How far (in UI units) the text floats up before disappearing.")]
        [SerializeField] private float floatUpDistance = 30f;

        [SerializeField] private float floatDuration = 0.6f;
        private int _lastValue;
        
        private TMP_Text _activePosText;
        private int _activePosAmount;
        private float _activePosTime;
        private Vector2 _activePosStart;
        private TMP_Text _activeNegText;
        private int _activeNegAmount;
        private float _activeNegTime;
        private Vector2 _activeNegStart;

        [Header("Insufficient Funds Pulse")]
        [SerializeField] private Color pulseColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField] private float pulseScale = 1.12f;
        [SerializeField] private float pulseUpTime = 0.08f;
        [SerializeField] private float pulseDownTime = 0.14f;
[SerializeField]
        private string sfxInsufficientKey = "";
[SerializeField]
        private float shakeAmplitude = 8f;
[SerializeField]
        private float shakeDuration = 0.25f;
        private Color _defaultColor;
        private Vector3 _defaultScale;
        private Coroutine _pulseCo;
        private Coroutine _shakeCo;
        private Vector2 _defaultAnchoredPos;

        private void OnEnable()
        {
            if (economy != null)
            {
                economy.OnMoneyChanged -= OnMoneyChanged;
                economy.OnMoneyChanged += OnMoneyChanged;
                _lastValue = economy.Current;
                OnMoneyChanged(economy.Current);
            }
            else
            {
                
                economy = FindFirstObjectByType<MatchEconomy>(FindObjectsInactive.Include);
                if (economy != null)
                {
                    economy.OnMoneyChanged -= OnMoneyChanged;
                    economy.OnMoneyChanged += OnMoneyChanged;
                    _lastValue = economy.Current;
                    OnMoneyChanged(economy.Current);
                }
            }
            
            if (moneyText != null)
            {
                _defaultScale = moneyText.transform.localScale;
                _defaultColor = moneyText.color;
                _defaultAnchoredPos = moneyText.rectTransform.anchoredPosition;
            }
        }

        private void OnDisable()
        {
            if (economy != null)
            {
                economy.OnMoneyChanged -= OnMoneyChanged;
            }
            if (_pulseCo != null)
            {
                StopCoroutine(_pulseCo);
                _pulseCo = null;
            }
            if (_shakeCo != null)
            {
                StopCoroutine(_shakeCo);
                _shakeCo = null;
            }
        }

        private void Update()
        {
            
            if (_activePosText != null)
            {
                _activePosTime += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(_activePosTime / Mathf.Max(0.001f, floatDuration));
                var rect = _activePosText.rectTransform;
                rect.anchoredPosition = Vector2.Lerp(_activePosStart, _activePosStart + Vector2.up * floatUpDistance, k);
                var c = _activePosText.color; c.a = 1f - k; _activePosText.color = c;
                if (_activePosTime >= floatDuration)
                {
                    Destroy(_activePosText.gameObject);
                    _activePosText = null;
                    _activePosAmount = 0;
                    _activePosTime = 0f;
                }
            }
            
            if (_activeNegText != null)
            {
                _activeNegTime += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(_activeNegTime / Mathf.Max(0.001f, floatDuration));
                var rect = _activeNegText.rectTransform;
                rect.anchoredPosition = Vector2.Lerp(_activeNegStart, _activeNegStart + Vector2.up * floatUpDistance, k);
                var c = _activeNegText.color; c.a = 1f - k; _activeNegText.color = c;
                if (_activeNegTime >= floatDuration)
                {
                    Destroy(_activeNegText.gameObject);
                    _activeNegText = null;
                    _activeNegAmount = 0;
                    _activeNegTime = 0f;
                }
            }
        }

        private void OnMoneyChanged(int value)
        {
            if (moneyText != null)
            {
                moneyText.text = string.Format(format, value);
            }
            
            int delta = value - _lastValue;
            if (delta != 0 && deltaTextPrefab != null && moneyText != null)
            {
                if (delta > 0)
                {
                    if (_activePosText == null)
                    {
                        _activePosAmount = delta;
                        _activePosText = InstantiateDeltaText(_activePosAmount);
                        _activePosStart = GetMoneyAnchoredPosition();
                        _activePosTime = 0f;
                    }
                    else
                    {
                        _activePosAmount += delta;
                        _activePosText.text = "+" + _activePosAmount.ToString();
                        _activePosText.color = new Color(0.2f, 1f, 0.2f, 1f);
                        _activePosStart = GetMoneyAnchoredPosition();
                        _activePosText.rectTransform.anchoredPosition = _activePosStart;
                        _activePosTime = 0f;
                    }
                }
                else 
                {
                    if (_activeNegText == null)
                    {
                        _activeNegAmount = delta;
                        _activeNegText = InstantiateDeltaText(_activeNegAmount);
                        _activeNegStart = GetMoneyAnchoredPosition();
                        _activeNegTime = 0f;
                    }
                    else
                    {
                        _activeNegAmount += delta;
                        _activeNegText.text = _activeNegAmount.ToString(); 
                        _activeNegText.color = new Color(1f, 0.4f, 0.2f, 1f);
                        _activeNegStart = GetMoneyAnchoredPosition();
                        _activeNegText.rectTransform.anchoredPosition = _activeNegStart;
                        _activeNegTime = 0f;
                    }
                }
            }
            _lastValue = value;
        }

        
        public void RefreshNow()
        {
            if (economy != null) OnMoneyChanged(economy.Current);
        }

        
        public void PulseInsufficient()
        {
            if (moneyText == null) return;
            
            if (_defaultScale == Vector3.zero) _defaultScale = moneyText.transform.localScale;
            if (_defaultColor == default) _defaultColor = moneyText.color;
            if (_pulseCo != null) StopCoroutine(_pulseCo);
            _pulseCo = StartCoroutine(PulseRoutine());
            
            if (_shakeCo != null) StopCoroutine(_shakeCo);
            _shakeCo = StartCoroutine(ShakeRoutine());
            
            if (!string.IsNullOrEmpty(sfxInsufficientKey) && SFXManager.Instance != null)
            {
                SFXManager.Instance.Play(sfxInsufficientKey);
            }
        }

        private IEnumerator PulseRoutine()
        {
            var tr = moneyText.transform;
            
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, pulseUpTime);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                tr.localScale = Vector3.Lerp(_defaultScale, _defaultScale * pulseScale, e);
                moneyText.color = Color.Lerp(_defaultColor, pulseColor, e);
                yield return null;
            }
            
            t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, pulseDownTime);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                tr.localScale = Vector3.Lerp(_defaultScale * pulseScale, _defaultScale, e);
                moneyText.color = Color.Lerp(pulseColor, _defaultColor, e);
                yield return null;
            }
            tr.localScale = _defaultScale;
            moneyText.color = _defaultColor;
            _pulseCo = null;
        }

        private IEnumerator ShakeRoutine()
        {
            if (moneyText == null) yield break;
            var rect = moneyText.rectTransform;
            Vector2 basePos = _defaultAnchoredPos = rect.anchoredPosition; 
            float t = 0f;
            while (t < shakeDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.001f, shakeDuration));
                
                float amp = shakeAmplitude * (1f - k);
                
                var offset = Random.insideUnitCircle * amp;
                rect.anchoredPosition = basePos + offset;
                yield return null;
            }
            rect.anchoredPosition = basePos;
            _shakeCo = null;
        }

        private TMP_Text InstantiateDeltaText(int delta)
        {
            var moneyRect = moneyText.rectTransform;
            var parent = moneyRect.parent as RectTransform;
            if (parent == null) parent = moneyRect; 
            var inst = Instantiate(deltaTextPrefab, parent);
            var instRect = inst.rectTransform;
            
            instRect.anchorMin = moneyRect.anchorMin;
            instRect.anchorMax = moneyRect.anchorMax;
            instRect.pivot = moneyRect.pivot;
            var start = GetMoneyAnchoredPosition();
            instRect.anchoredPosition = start;
            inst.text = (delta > 0 ? "+" : "") + delta.ToString();
            inst.raycastTarget = false;
            
            inst.color = delta > 0 ? new Color(0.2f, 1f, 0.2f, 1f) : new Color(1f, 0.4f, 0.2f, 1f);
            return inst;
        }

        private Vector2 GetMoneyAnchoredPosition()
        {
            return moneyText.rectTransform.anchoredPosition + deltaOffset;
        }
    }
}
