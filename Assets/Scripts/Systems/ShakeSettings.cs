using UnityEngine;

namespace TR.Systems
{
    public static class ShakeSettings
    {
        private const string PREF = "tr_screen_shake";

        public static bool ScreenShakeEnabled { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            ScreenShakeEnabled = PlayerPrefs.GetInt(PREF, 1) != 0;
        }

        public static void SetScreenShakeEnabled(bool enabled)
        {
            ScreenShakeEnabled = enabled;
            PlayerPrefs.SetInt(PREF, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
