using UnityEngine.SceneManagement;
using TR.Infrastructure;

namespace TR.Systems
{
    public static class PackOpeningService
    {
        // Centralized way to go to the pack opening scene
        public const string DefaultOpeningSceneName = "PackOpening";

        public static void OpenPackScene(string packId, int openCount = 1, string sceneName = DefaultOpeningSceneName)
        {
            if (string.IsNullOrEmpty(packId)) return;
            if (openCount <= 0) openCount = 1;
            SceneParams.Set("packId", packId);
            SceneParams.Set("openCount", openCount);
            _ = SceneFader.Instance.LoadSceneWithFade(sceneName);
        }
    }
}
