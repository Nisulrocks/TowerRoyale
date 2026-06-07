using UnityEngine;

namespace TR.VFX
{
    
    
    public static class ParticleQuality
    {
        public static int Current { get; private set; } = 3;
        public static System.Action<int> OnChanged;

        public static void SetQuality(int q)
        {
            int clamped = Mathf.Clamp(q, 0, 3);
            if (clamped == Current) return;
            Current = clamped;
            try { OnChanged?.Invoke(Current); } catch { }
        }

        public static bool AllowVfx() => Current > 0;

        public static float EmissionScale()
        {
            switch (Current)
            {
                case 0: return 0f;   
                case 1: return 0.4f; 
                case 2: return 0.7f; 
                default: return 1f;  
            }
        }
    }
}
