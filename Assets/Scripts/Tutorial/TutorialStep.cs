using UnityEngine;

namespace TR.Tutorial
{
    public enum TargetMode
    {
        ByName,
        ShopPackById,
        OwnedCollectionCards
    }

    public enum StepWaitMode
    {
        None,
        WaitSeconds,
        WaitForTargetClick,
        WaitForTargetDrag
    }

    [System.Serializable]
    public class TutorialStep
    {
        [Header("Targeting")]
        public TargetMode targetMode = TargetMode.ByName;
public string targetObjectName;
public string targetPackId;

        public Vector2 targetScreenOffset = new Vector2(0, 60);

        [Header("Context (optional)")]

        public string requiredSceneName;

        [Tooltip("Optional: If set, this object (Button) will be clicked programmatically when the step starts (after scene matches). Useful to auto-open tabs/panels like Shop.")]
        public string autoClickObjectNameOnStart;


        public int maxArrows = 12;

        [Header("Dialogue")]
        [TextArea]
        public string dialogueText;
        [Range(0.01f, 0.1f)] public float typewriterCharDelay = 0.03f;

        [Header("Progression")]
        public StepWaitMode waitMode = StepWaitMode.None;
public float waitSeconds = 0f;

        [Header("Options")]
        [Tooltip("If true, input outside the target is ignored (MVP: not enforced).")]
        public bool blockOutside = false;
    }
}
