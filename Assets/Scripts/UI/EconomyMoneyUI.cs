using System.Collections;
using UnityEngine;
using TMPro;
using TR.Battle;

namespace TR.UI
{
    
    public class EconomyMoneyUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TMP_Text moneyText;
        [SerializeField] private MatchEconomy economy;

        [Header("Pulse (Insufficient Funds)")]
        [SerializeField] private Color pulseColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private float pulseScale = 1.15f;
        [SerializeField] private float pulseUpTime = 0.08f;
        [SerializeField] private float pulseDownTime = 0.15f;
        private Color _defaultColor;
        private Vector3 _defaultScale;
        private Coroutine _pulseCo;

        private void Awake()
        {
            if (moneyText == null)
            {
                moneyText = GetComponentInChildren<TMP_Text>(true);
            }
            _defaultScale = moneyText != null ? moneyText.transform.localScale : Vector3.one;
            _defaultColor = moneyText != null ? moneyText.color : Color.white;
            if (economy == null)
            {
                economy = FindFirstObjectByType<MatchEconomy>(FindObjectsInactive.Include);
            }
        }

        private void OnEnable()
        {
            if (economy == null)
            {
                economy = FindFirstObjectByType<MatchEconomy>(FindObjectsInactive.Include);
            }
            if (economy != null)
            {
                economy.OnMoneyChanged += HandleMoneyChanged;
                HandleMoneyChanged(economy.Current);
            }
        }

        private void OnDisable()
        {
            if (economy != null)
            {
                economy.OnMoneyChanged -= HandleMoneyChanged;
            }
        }

        private void HandleMoneyChanged(int value)
        {
            if (moneyText == null) return;
            moneyText.text = value.ToString();
        }

        public void PulseInsufficient()
        {
            if (moneyText == null) return;
            if (_pulseCo != null) StopCoroutine(_pulseCo);
            _pulseCo = StartCoroutine(PulseRoutine());
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
    }
}
