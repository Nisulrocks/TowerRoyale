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
public int baseFontSize = 36;
[Min(0f)] public float mergeWindow = 0.4f;
[Min(0f)] public float lifetime = 1.2f;
        [Tooltip("Vertical offset above target while merging (in pixels for screen-space canvas)")] public float followOffsetY = 120f;
        [Tooltip("Fade speed multiplier (alpha/sec); 1 = fade across lifetime")] [Min(0f)] public float fadeSpeed = 1.0f;

        [Header("Pop Animation")] 
        [Tooltip("Scale multiplier applied instantly when new damage is added (stacks multiplicatively)")] public float popScale = 1.15f;
public float popMaxScale = 2.0f;
[Min(0f)] public float popReturnTime = 0.2f;

        [Header("Free-Fall (after merge window)")]
        [Tooltip("Initial upward velocity when entering the fade-out phase (pixels/sec)")] public float initialUpVelocity = 80f;
        [Tooltip("Gravity applied during fade-out (pixels/sec^2). Negative falls downward.")] public float gravity = -300f;
        [Tooltip("Horizontal kick applied once at fade start (pixels/sec); random left/right")] public float endKickHorizontal = 80f;

        [Header("Damage-Based Color")] 
public float damageColorMin = 10f;
public float damageColorMax = 500f;
public Gradient damageGradient;

        [Header("Display Options")]

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
