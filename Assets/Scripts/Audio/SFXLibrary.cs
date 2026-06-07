using UnityEngine;

namespace TR.Audio
{
    [CreateAssetMenu(fileName = "SFXLibrary", menuName = "TR/Audio/SFX Library")]
    public class SFXLibrary : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
public string key;
public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;
public float pitchMin = 1f;
            public float pitchMax = 1f;
public float cooldown = 0f;
            [Tooltip("Optional: Limit how many of this key can overlap at once (0 = no limit)")] public int maxConcurrent = 0;
        }

        public Entry[] entries;

        public Entry Get(string key)
        {
            if (entries == null || string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e != null && e.key == key) return e;
            }
            return null;
        }

        public AudioClip GetRandomClip(Entry e)
        {
            if (e == null || e.clips == null || e.clips.Length == 0) return null;
            if (e.clips.Length == 1) return e.clips[0];
            return e.clips[Random.Range(0, e.clips.Length)];
        }

        public float GetRandomPitch(Entry e)
        {
            if (e == null) return 1f;
            float a = Mathf.Min(e.pitchMin, e.pitchMax);
            float b = Mathf.Max(e.pitchMin, e.pitchMax);
            return Random.Range(a, b);
        }
    }
}
