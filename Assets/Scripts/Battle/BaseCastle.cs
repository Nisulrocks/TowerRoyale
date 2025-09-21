using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;
using TR.VFX;
using TR.Audio;

namespace TR.Battle
{
    // Player's main castle/king tower. Health is determined by castle level from PlayerProfile + CastleProgression.
    public class BaseCastle : MonoBehaviour
    {
        [Header("UI (Optional)")]
        [SerializeField] private TMP_Text healthText; // optional HUD for debugging
        [SerializeField] private Slider healthSlider; // optional health bar
        [SerializeField] private Image healthFill;    // optional: the fill Image of the slider
        [Header("Health Bar Colors")]
        [SerializeField] private Color colorHigh = new Color(0.2f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color colorMid  = new Color(0.95f, 0.8f, 0.1f, 1f);
        [SerializeField] private Color colorLow  = new Color(0.9f, 0.2f, 0.2f, 1f);

        [Header("Auto Position Settings")]
        [Tooltip("If true, the health slider is automatically positioned above the castle each frame.")]
        [SerializeField] private bool autoPositionSlider = true;
        [Tooltip("World-space offset above the castle for the health bar.")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.0f, 0f);

        private RectTransform _sliderRect;
        private Canvas _sliderCanvas;

        [Header("Death VFX/SFX (Optional)")]
        [Tooltip("ParticleManager key to spawn when the castle is destroyed")]
        [SerializeField] private string deathVfxKey = "";
        [Tooltip("Optional transform to use as the position for the death VFX; if null, uses castle position")]
        [SerializeField] private Transform deathVfxAnchor;
        [Tooltip("SFXManager key to play when the castle is destroyed")]
        [SerializeField] private string deathSfxKey = "";

        [Header("Runtime")]
        [SerializeField] private int maxHealth;
        [SerializeField] private int currentHealth;

        public System.Action OnCastleDestroyed;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;

        private void Start()
        {
            // Initialize from player profile progression
            maxHealth = PlayerProfile.GetCastleMaxHealth();
            currentHealth = maxHealth;
            // Initialize slider max
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
                // Ensure slider stays in sync even if max health changes (level up, etc.)
                if (!Mathf.Approximately(healthSlider.maxValue, maxHealth))
                {
                    healthSlider.maxValue = Mathf.Max(1, maxHealth);
                }
                healthSlider.value = Mathf.Clamp(currentHealth, 0, maxHealth);

                // Update fill colour based on health percentage
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
                // Fallback: place in world space if no canvas found
                _sliderRect.position = transform.position + worldOffset;
                return;
            }

            if (_sliderCanvas.renderMode == RenderMode.WorldSpace)
            {
                // In world space, set world position directly
                _sliderRect.position = transform.position + worldOffset;
            }
            else
            {
                if (cam == null)
                {
                    // Screen-space overlay without camera
                    Vector3 screen = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
                    // Try to map world position; without camera, approximate by keeping as is
                    _sliderRect.position = screen;
                    return;
                }
                Vector3 world = transform.position + worldOffset;
                Vector3 screenPos = cam.WorldToScreenPoint(world);
                if (_sliderCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    // Position in screen pixels
                    _sliderRect.position = screenPos;
                }
                else if (_sliderCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    // Convert to canvas local position
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
            // Play death VFX
            if (!string.IsNullOrEmpty(deathVfxKey))
            {
                var pos = deathVfxAnchor != null ? deathVfxAnchor.position : transform.position;
                ParticleManager.SpawnOneShot(deathVfxKey, pos);
            }
            // Play death SFX
            if (!string.IsNullOrEmpty(deathSfxKey) && SFXManager.Instance != null)
            {
                SFXManager.Instance.Play(deathSfxKey);
            }
            OnCastleDestroyed?.Invoke();
            // Consumers (e.g., BattleSceneController) can subscribe to OnCastleDestroyed to end the match.
            // Hide health UI elements
            if (healthText != null) healthText.gameObject.SetActive(false);
            if (healthSlider != null) healthSlider.gameObject.SetActive(false);
            // Finally, destroy the castle GameObject
            Destroy(gameObject);
        }
    }
}
