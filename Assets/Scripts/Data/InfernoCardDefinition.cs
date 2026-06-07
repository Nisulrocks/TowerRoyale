using UnityEngine;

namespace TR.Data
{
    [CreateAssetMenu(fileName = "InfernoCardDefinition", menuName = "TR/Data/Inferno Card Definition")]
    public class InfernoCardDefinition : CardDefinition
    {
        [Header("Inferno Settings (Formula: base + perLevel*(level-1))")]
        [SerializeField] private int maxTargetsBase = 1;         [SerializeField] private int maxTargetsPerLevel = 0;
        [SerializeField] private float rampUpPerSecBase = 0.5f;  [SerializeField] private float rampUpPerSecPerLevel = 0.0f;
        [SerializeField] private float rampMaxMultiplierBase = 3.0f; [SerializeField] private float rampMaxMultiplierPerLevel = 0.0f;
        [SerializeField] private float multiTargetPenaltyBase = 0.5f; [SerializeField] private float multiTargetPenaltyPerLevel = 0.0f;
        [SerializeField] private float rampDownPerSecBase = 0.0f; [SerializeField] private float rampDownPerSecPerLevel = 0.0f; 

        [Header("Beam Visuals")]
        [SerializeField] private Color beamStartColor = new Color(1f, 0.6f, 0.2f, 1f); 
        [SerializeField] private Color beamEndColor = new Color(1f, 0.2f, 0f, 1f);
        [SerializeField] private float beamBaseWidth = 0.04f;
        [SerializeField] private float beamMaxWidth = 0.10f;
        [SerializeField] private bool beamJitter = true;
        [SerializeField] private float beamJitterAmplitude = 0.05f;
        [Tooltip("Optional: Material used by the LineRenderer for this beam (use URP Unlit ShaderGraph with Emission for bloom)")]
        [SerializeField] private Material beamMaterial;

        public int GetMaxTargets(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Clamp(maxTargetsBase + maxTargetsPerLevel * (lv - 1), 1, 50);
        }

        public float GetRampUpPerSecond(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, rampUpPerSecBase + rampUpPerSecPerLevel * (lv - 1));
        }

        public float GetRampMaxMultiplier(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(1f, rampMaxMultiplierBase + rampMaxMultiplierPerLevel * (lv - 1));
        }

        public float GetMultiTargetPenalty(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, multiTargetPenaltyBase + multiTargetPenaltyPerLevel * (lv - 1));
        }

        public float GetRampDownPerSecond(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, rampDownPerSecBase + rampDownPerSecPerLevel * (lv - 1));
        }

        public Color GetBeamStartColor() => beamStartColor;
        public Color GetBeamEndColor() => beamEndColor;
        public float GetBeamBaseWidth() => Mathf.Max(0f, beamBaseWidth);
        public float GetBeamMaxWidth() => Mathf.Max(GetBeamBaseWidth(), beamMaxWidth);
        public bool UseBeamJitter() => beamJitter;
        public float GetBeamJitterAmplitude() => Mathf.Max(0f, beamJitterAmplitude);
        public Material GetBeamMaterial() => beamMaterial;
    }
}
