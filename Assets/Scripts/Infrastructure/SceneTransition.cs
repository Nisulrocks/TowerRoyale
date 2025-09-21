using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace TR.Infrastructure
{
    // Simple helpers to trigger fade transitions from UI buttons or code.
    // Option A: Attach this to a Button GameObject in Lobby, set TargetScene, and hook OnClick -> SceneTransition.Go()
    // Option B: Call SceneTransition.GoToScene("Arena01") from your own scripts.
    public class SceneTransition : MonoBehaviour
    {
        [Header("Target Scene")] public string TargetScene;
        [Header("Durations")] public float FadeOut = 0.35f; public float FadeIn = 0.35f;

        // Hook this from a UI Button OnClick
        public void Go()
        {
            // Use async fire-and-forget wrapper
            _ = GoAsync();
        }

        private async Task GoAsync()
        {
            await SceneFader.Instance.LoadSceneWithFade(TargetScene, FadeOut, FadeIn);
        }

        // Static convenience
        public static void GoToScene(string sceneName, float fadeOut = 0.35f, float fadeIn = 0.35f)
        {
            _ = SceneFader.Instance.LoadSceneWithFade(sceneName, fadeOut, fadeIn);
        }
    }
}
