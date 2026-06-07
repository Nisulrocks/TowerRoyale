using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace TR.Infrastructure
{
    
    
    
    public class SceneTransition : MonoBehaviour
    {
        [Header("Target Scene")] public string TargetScene;
        [Header("Durations")] public float FadeOut = 0.35f; public float FadeIn = 0.35f;

        
        public void Go()
        {
            
            _ = GoAsync();
        }

        private async Task GoAsync()
        {
            await SceneFader.Instance.LoadSceneWithFade(TargetScene, FadeOut, FadeIn);
        }

        
        public static void GoToScene(string sceneName, float fadeOut = 0.35f, float fadeIn = 0.35f)
        {
            _ = SceneFader.Instance.LoadSceneWithFade(sceneName, fadeOut, fadeIn);
        }
    }
}
