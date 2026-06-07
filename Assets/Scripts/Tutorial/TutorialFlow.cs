using System.Collections.Generic;
using UnityEngine;

namespace TR.Tutorial
{
    [CreateAssetMenu(fileName = "TutorialFlow", menuName = "TR/Tutorial/Flow")] 
    public class TutorialFlow : ScriptableObject
    {
        [Header("Eligibility")]

        public bool autoStartForFreshProfiles = true;

        [Header("Steps (ordered)")]
        public List<TutorialStep> steps = new List<TutorialStep>();
    }
}
