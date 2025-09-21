using UnityEngine;

namespace TR.Systems
{
    public static class WeightedRandom
    {
        // Returns index into weights array, proportional to weight values
        public static int PickIndex(int[] weights, System.Random rng = null)
        {
            if (weights == null || weights.Length == 0) return -1;
            rng ??= new System.Random();

            int total = 0;
            for (int i = 0; i < weights.Length; i++) total += Mathf.Max(0, weights[i]);
            if (total <= 0) return -1;

            int roll = rng.Next(0, total);
            int cum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                cum += Mathf.Max(0, weights[i]);
                if (roll < cum) return i;
            }
            return weights.Length - 1;
        }
    }
}
