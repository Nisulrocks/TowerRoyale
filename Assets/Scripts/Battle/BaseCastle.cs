using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;
using TR.VFX;
using TR.Audio;

namespace TR.Battle
{
    
    public class BaseCastle : MonoBehaviour
    {
        [Header("UI (Optional)")]
        [SerializeField] private TMP_Text healthText; 
        [SerializeField] private Slider healthSlider; 
        [SerializeField] private Image healthFill;    
        [Header("Health Bar Colors")]
        [SerializeField] private Color colorHigh = new Color(0.2f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color colorMid  = new Color(0.95f, 0.8f, 0.1f, 1f);
        [SerializeField] private Color colorLow  = new Color(0.9f, 0.2f, 0.2f, 1f);

        [Header("Auto Position Settings")]

        [SerializeField] private bool autoPositionSlider = true;

        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.0f, 0f);

        private RectTransform _sliderRect;
        private Canvas _sliderCanvas;

        [Header("Death VFX/SFX (Optional)")]

        [SerializeField] private string deathVfxKey = "";

        [SerializeField] private Transform deathVfxAnchor;

        [SerializeField] private string deathSfxKey = "";

        [Header("Runtime")]
        [SerializeField] private int maxHealth;
        [SerializeField] private int currentHealth;

        public System.Action OnCastleDestroyed;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;

        private void Start()
        {
            
            maxHealth = PlayerProfile.GetCastleMaxHealth();
            currentHealth = maxHealth;
            
            if (healthSlider != null)
            {
                healthSlider.minValue = 0f;
                healthSlider.maxValue = Mathf.Max(1, maxHealth);
                healthSlider.value = currentHealth;
                _sliderRect = healthSlider.GetComponent<RectTransform>();
                _sliderCanvas = healthSlider.GetComponentInParent<Canvas>();
            }
            RefreshUI();
        }

        public void TakeDamage(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (amount <= 0) return;
            currentHealth = Mathf.Max(0, currentHealth - amount);
            RefreshUI();
            if (currentHealth <= 0)
            {
                HandleDestroyed();
            }
        }

        public void Heal(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (amount <= 0) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (healthText != null)
            {
                healthText.text = $"Castle HP: {currentHealth}/{maxHealth}";
            }
            if (healthSlider != null)
            {
                
                if (!Mathf.Approximately(healthSlider.maxValue, maxHealth))
                {
                    healthSlider.maxValue = Mathf.Max(1, maxHealth);
                }
                healthSlider.value = Mathf.Clamp(currentHealth, 0, maxHealth);

                
                if (healthFill != null)
                {
                    float pct = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
                    Color c = pct > 0.6f ? colorHigh : (pct > 0.3f ? colorMid : colorLow);
                    healthFill.color = c;
                }
            }
        }

        private void LateUpdate()
        {
            if (!autoPositionSlider || _sliderRect == null) return;
            var cam = Camera.main;
            if (_sliderCanvas == null)
            {
                _sliderCanvas = _sliderRect.GetComponentInParent<Canvas>();
            }
            if (_sliderCanvas == null)
            {
                
                _sliderRect.position = transform.position + worldOffset;
                return;
            }

            if (_sliderCanvas.renderMode == RenderMode.WorldSpace)
            {
                
                _sliderRect.position = transform.position + worldOffset;
            }
            else
            {
                if (cam == null)
                {
                    
                    Vector3 screen = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
                    
                    _sliderRect.position = screen;
                    return;
                }
                Vector3 world = transform.position + worldOffset;
                Vector3 screenPos = cam.WorldToScreenPoint(world);
                if (_sliderCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    
                    _sliderRect.position = screenPos;
                }
                else if (_sliderCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    
                    RectTransform canvasRect = _sliderCanvas.transform as RectTransform;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, _sliderCanvas.worldCamera, out var local))
                    {
                        _sliderRect.anchoredPosition = local;
                    }
                }
            }
        }

        private void HandleDestroyed()
        {
            Debug.Log("[BaseCastle] Castle destroyed!");
            
            if (!string.IsNullOrEmpty(deathVfxKey))
            {
                var pos = deathVfxAnchor != null ? deathVfxAnchor.position : transform.position;
                ParticleManager.SpawnOneShot(deathVfxKey, pos);
            }
            
            if (!string.IsNullOrEmpty(deathSfxKey) && SFXManager.Instance != null)
            {
                SFXManager.Instance.Play(deathSfxKey);
            }
            OnCastleDestroyed?.Invoke();
            
            
            if (healthText != null) healthText.gameObject.SetActive(false);
            if (healthSlider != null) healthSlider.gameObject.SetActive(false);
            
            Destroy(gameObject);
        }
    }
}
