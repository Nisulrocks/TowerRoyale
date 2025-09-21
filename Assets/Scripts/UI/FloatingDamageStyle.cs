using System;
using System.Collections.Generic;
using UnityEngine;
using TR.Battle;

namespace TR.UI
{
    [CreateAssetMenu(fileName = "FloatingDamageStyle", menuName = "TR/UI/Floating Damage Style")]
    public class FloatingDamageStyle : ScriptableObject
    {
        [Header("General")]
        [Tooltip("Base font size for the damage number")] public int baseFontSize = 36;
        [Tooltip("Time in seconds to merge additional hits into the same number")] [Min(0f)] public float mergeWindow = 0.4f;
        [Tooltip("Total lifetime after the last merge window, before fully faded out")] [Min(0f)] public float lifetime = 1.2f;
        [Tooltip("Vertical offset above target while merging (in pixels for screen-space canvas)")] public float followOffsetY = 120f;
        [Tooltip("Fade speed multiplier (alpha/sec); 1 = fade across lifetime")] [Min(0f)] public float fadeSpeed = 1.0f;

        [Header("Pop Animation")] 
        [Tooltip("Scale multiplier applied instantly when new damage is added (stacks multiplicatively)")] public float popScale = 1.15f;
        [Tooltip("Maximum stacked scale multiplier")] public float popMaxScale = 2.0f;
        [Tooltip("Seconds to ease back to 1.0 scale after pop")] [Min(0f)] public float popReturnTime = 0.2f;

        [Header("Free-Fall (after merge window)")]
        [Tooltip("Initial upward velocity when entering the fade-out phase (pixels/sec)")] public float initialUpVelocity = 80f;
        [Tooltip("Gravity applied during fade-out (pixels/sec^2). Negative falls downward.")] public float gravity = -300f;
        [Tooltip("Horizontal kick applied once at fade start (pixels/sec); random left/right")] public float endKickHorizontal = 80f;

        [Header("Damage-Based Color")] 
        [Tooltip("Damage value mapped to the start of the gradient")] public float damageColorMin = 10f;
        [Tooltip("Damage value mapped to the end of the gradient")] public float damageColorMax = 500f;
        [Tooltip("Gradient to evaluate color from damage magnitude")] public Gradient damageGradient;

        [Header("Display Options")]
        [Tooltip("If true, the floating damage number will never exceed the target's remaining HP.")]
        public bool clampDisplayedDamageToRemainingHealth = true;

        public Color GetColorForDamage(float damage)
        {
            if (damageGradient != null)
            {
                float t = 0.5f;
                if (damageColorMax > damageColorMin)
                {
                    t = Mathf.InverseLerp(damageColorMin, damageColorMax, damage);
                }
                t = Mathf.Clamp01(t);
                return damageGradient.Evaluate(t);
            }
            return Color.white;
        }
    }
}
