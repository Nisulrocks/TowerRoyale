using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Data;

namespace TR.Battle
{
    
    
    public class EnemyHealthBarUI : MonoBehaviour
    {
        [Header("Binding")]
        [SerializeField] private Slider slider;
        [SerializeField] private Image fillImage;

        [Header("Hover Info")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text attackText;

        [Header("Follow Settings")]
        [SerializeField] private bool billboardToCamera = true;
        [SerializeField] private bool hideWhenFull = true;

        private EnemyBase2D _enemy;
        private Vector3 _worldOffset;
        private RectTransform _rt;
        private Canvas _canvas;
        private bool _hovered;

        
        [Header("Fill Colors by Percent")] 
        [SerializeField] private Color colorFull = new Color(0.2f, 1f, 0.2f, 1f);
        [SerializeField] private Color colorMid = new Color(1f, 0.9f, 0.2f, 1f);
        [SerializeField] private Color colorLow = new Color(1f, 0.2f, 0.2f, 1f);

        public void Bind(EnemyBase2D enemy, Vector3 worldOffset)
        {
            _enemy = enemy;
            _worldOffset = worldOffset;
            if (_enemy != null)
            {
                _enemy.OnHealthChanged -= HandleHealthChanged;
                _enemy.OnHealthChanged += HandleHealthChanged;
                HandleHealthChanged(_enemy.CurrentHealth, _enemy.MaxHealth);
                SetInfo(_enemy.Definition);
            }
        }

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            if (slider == null) slider = GetComponentInChildren<Slider>(true);
            if (nameText == null) nameText = transform.Find("NameText")?.GetComponent<TMP_Text>();
            if (attackText == null) attackText = transform.Find("AttackText")?.GetComponent<TMP_Text>();

            if (nameText == null || attackText == null)
            {
                var texts = GetComponentsInChildren<TMP_Text>(true);
                if (nameText == null && texts.Length > 0) nameText = texts[0];
                if (attackText == null && texts.Length > 1) attackText = texts[1];
            }
        }

        private void OnDestroy()
        {
            if (_enemy != null)
            {
                _enemy.OnHealthChanged -= HandleHealthChanged;
            }
        }

        private void LateUpdate()
        {
            if (_enemy == null || _rt == null) return;
            var worldPos = _enemy.transform.position + _worldOffset;
            if (_canvas != null && _canvas.renderMode == RenderMode.WorldSpace)
            {
                _rt.position = worldPos;
                if (billboardToCamera && Camera.main != null)
                {
                    _rt.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                    _rt.right = Camera.main.transform.right; 
                }
            }
            else
            {
                
                if (Camera.main != null)
                {
                    Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(Camera.main, worldPos);
                    _rt.position = screenPos;
                }
            }
        }

        public void SetHover(bool hovered)
        {
            _hovered = hovered;
            if (hovered && !gameObject.activeSelf)
                gameObject.SetActive(true);

            if (nameText != null) nameText.gameObject.SetActive(hovered);
            if (attackText != null) attackText.gameObject.SetActive(hovered);
        }

        public void SetInfo(EnemyDefinition def)
        {
            if (def == null) return;
            if (nameText != null) nameText.text = def.DisplayName;
            if (attackText != null) attackText.text = $"ATK: {def.DamagePerHit}";
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (slider == null) return;
            max = Mathf.Max(1f, max);
            float pct = Mathf.Clamp01(current / max);
            slider.normalizedValue = pct;

            if (hideWhenFull && !_hovered)
            {
                bool hide = pct >= 0.999f;
                if (gameObject.activeSelf != !hide)
                {
                    gameObject.SetActive(!hide);
                }
            }

            if (fillImage != null)
            {
                if (pct <= 0.5f)
                {
                    float t = Mathf.InverseLerp(0f, 0.5f, pct);
                    fillImage.color = Color.Lerp(colorLow, colorMid, t);
                }
                else
                {
                    float t = Mathf.InverseLerp(0.5f, 1f, pct);
                    fillImage.color = Color.Lerp(colorMid, colorFull, t);
                }
            }
        }
    }
}
