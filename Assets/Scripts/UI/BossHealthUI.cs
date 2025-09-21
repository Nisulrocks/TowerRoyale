using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Battle;

namespace TR.UI
{
    // Screen-space UI for boss health. Place under a screen overlay Canvas.
    // Starts inactive; becomes active when a boss binds.
    public class BossHealthUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image fillImage;          // slider fill image for color by HP
        [Header("Abilities UI")]
        [SerializeField] private TMP_Text abilitiesText;   // optional: shows boss abilities summary
        [Header("Fill Colors (Low/Mid/High)")]
        [SerializeField] private Color lowColor = new Color(0.9f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color midColor = new Color(1f, 0.9f, 0.2f, 1f);
        [SerializeField] private Color highColor = new Color(0.2f, 0.9f, 0.2f, 1f);
        [Header("Stacking (Multiple Bosses)")]
        [Tooltip("Vertical spacing (in pixels) between stacked boss health bars")] [SerializeField]
        private float stackSpacing = 1200f;

        private EnemyBase2D _boss;
        private float _maxHP;
        private RectTransform _rect;
        private Vector2 _initialAnchoredPos;

        // Static registry to stack multiple boss bars
        private static readonly System.Collections.Generic.List<BossHealthUI> s_active = new();
        private static bool s_topInitialized = false;
        private static float s_topX;
        private static float s_topY;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect != null)
            {
                _initialAnchoredPos = _rect.anchoredPosition;
            }
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.value = 0f;
                // Try auto-wire fill image if not assigned
                if (fillImage == null && slider.fillRect != null)
                {
                    fillImage = slider.fillRect.GetComponent<Image>();
                }
            }
        }

        public void Bind(EnemyBase2D boss, string displayName)
        {
            if (boss == null)
                return;

            // Unbind any previous boss
            Unbind();

            _boss = boss;
            _boss.OnHealthChanged += HandleBossHealthChanged;

            if (nameText) nameText.text = string.IsNullOrEmpty(displayName) ? "Boss" : displayName;
            // Abilities summary
            ApplyAbilitiesSummary(_boss);

            // Ensure panel is active before we apply UI so the first frame renders correctly
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            // Initialize from boss properties so we don't show 1/1
            _maxHP = Mathf.Max(1f, _boss.MaxHealth);
            float current = Mathf.Clamp(_boss.CurrentHealth, 0f, _maxHP);
            ApplyUI(current, _maxHP);
            Canvas.ForceUpdateCanvases();

            // Register and reflow stack
            if (!s_active.Contains(this))
            {
                s_active.Add(this);
                if (!s_topInitialized)
                {
                    // Use this instance's initial anchored position as the top anchor
                    s_topInitialized = true;
                    s_topX = _rect != null ? _rect.anchoredPosition.x : 0f;
                    s_topY = _rect != null ? _rect.anchoredPosition.y : 0f;
                }
            }
            ReflowStack();
        }

        public void Unbind()
        {
            if (_boss != null)
            {
                _boss.OnHealthChanged -= HandleBossHealthChanged;
                _boss = null;
            }
            if (gameObject.activeSelf) gameObject.SetActive(false);
            if (s_active.Contains(this))
            {
                s_active.Remove(this);
                ReflowStack();
                if (s_active.Count == 0)
                {
                    s_topInitialized = false; // reset for next wave
                }
            }
        }

        public void UnbindIfTarget(EnemyBase2D boss)
        {
            if (_boss == boss)
            {
                Unbind();
            }
        }

        private void HandleBossHealthChanged(float current, float max)
        {
            _maxHP = Mathf.Max(1f, max);
            float clamped = Mathf.Clamp(current, 0f, _maxHP);
            ApplyUI(clamped, _maxHP);
        }

        private void ApplyUI(float current, float max)
        {
            float norm = max > 1e-5f ? (current / max) : 0f;
            if (slider) slider.value = norm;
            if (hpText) hpText.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
            if (fillImage != null)
            {
                // 3-color mapping: low (red) -> mid (yellow) -> high (green)
                if (norm > 0.5f)
                {
                    float t = (norm - 0.5f) / 0.5f;
                    fillImage.color = Color.Lerp(midColor, highColor, t);
                }
                else
                {
                    float t = norm / 0.5f;
                    fillImage.color = Color.Lerp(lowColor, midColor, t);
                }
            }
        }

        private void ApplyAbilitiesSummary(EnemyBase2D boss)
        {
            if (abilitiesText == null)
                return;
            var def = boss != null ? boss.Definition : null;
            if (def == null)
            {
                abilitiesText.text = "-";
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            string bossName = nameText != null && !string.IsNullOrEmpty(nameText.text) ? nameText.text : "Boss";
            sb.AppendLine($"{bossName} Abilities:");

            // Regen ability
            if (def.UseRegenAbility)
            {
                float basePct = def.RegenPerSecondBase * 100f;
                float missPct = def.RegenMissingHealthFactor * 100f;
                float capPct = def.RegenPerSecondCap * 100f;
                float lifeCapPct = def.RegenTotalPercentCap * 100f;
                float sup = def.RegenSuppressAfterDamageSeconds;
                sb.AppendLine($"- Regeneration: heals {basePct:0.#}%/s (+{missPct:0.#}% when low). Max {capPct:0.#}%/s. Starts after {sup:0.#}s. Total {lifeCapPct:0.#}% HP.");
            }

            // Pulse nuke ability
            if (def.UsePulseNukeAbility)
            {
                float r = def.PulseNukeRadius;
                float cdMin = def.PulseNukeCooldownMin;
                float cdMax = def.PulseNukeCooldownMax;
                sb.AppendLine($"- Pulse Nuke: destroys towers in {r:0.##}u. Cooldown {cdMin:0.#}-{cdMax:0.#}s (random trigger).");
            }

            // Stun pulse ability
            if (def.UseStunPulseAbility)
            {
                float r = def.StunPulseRadius;
                float d = def.StunPulseDuration;
                float cdMin = def.StunPulseCooldownMin;
                float cdMax = def.StunPulseCooldownMax;
                sb.AppendLine($"- Stun Pulse: stuns towers in {r:0.##}u for {d:0.#}s. Cooldown {cdMin:0.#}-{cdMax:0.#}s (random).");
            }

            // If only header exists (no abilities), show Ordinary
            var text = sb.ToString().TrimEnd();
            int newlineIdx = text.IndexOf('\n');
            bool hasLines = newlineIdx >= 0 && newlineIdx < text.Length - 1;
            if (!hasLines)
            {
                abilitiesText.text = $"{bossName} Abilities:\n- None";
            }
            else
            {
                abilitiesText.text = text;
            }
        }

        // Reposition all active boss bars so they stack vertically from the top anchor
        private static void ReflowStack()
        {
            for (int i = 0; i < s_active.Count; i++)
            {
                var ui = s_active[i];
                if (ui == null || ui._rect == null) continue;
                float x = s_topInitialized ? s_topX : ui._rect.anchoredPosition.x;
                float yTop = s_topInitialized ? s_topY : ui._rect.anchoredPosition.y;
                float y = yTop - ui.stackSpacing * i;
                ui._rect.anchoredPosition = new Vector2(x, y);
            }
        }
    }
}
