using System.Collections.Generic;
using UnityEngine;

namespace TR.Tutorial
{
    [CreateAssetMenu(fileName = "TutorialFlow", menuName = "TR/Tutorial/Flow")] 
    public class TutorialFlow : ScriptableObject
    {
        [Header("Eligibility")]
        [Tooltip("If true, this tutorial auto starts when the player has 0 trophies and owns no cards.")]
        public bool autoStartForFreshProfiles = true;

        [Header("Steps (ordered)")]
        public List<TutorialStep> steps = new List<TutorialStep>();
    }
}
