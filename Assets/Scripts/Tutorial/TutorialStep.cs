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
        [Tooltip("Used when targetMode is ByName")] public string targetObjectName;
        [Tooltip("Used when targetMode is ShopPackById")] public string targetPackId;
        [Tooltip("Offset in screen pixels relative to target anchor.")]
        public Vector2 targetScreenOffset = new Vector2(0, 60);

        [Header("Context (optional)")]
        [Tooltip("If set, this step will wait until the active scene name matches before showing the arrow or blocking input.")]
        public string requiredSceneName;

        [Tooltip("Optional: If set, this object (Button) will be clicked programmatically when the step starts (after scene matches). Useful to auto-open tabs/panels like Shop.")]
        public string autoClickObjectNameOnStart;

        [Tooltip("When using OwnedCollectionCards, maximum number of arrows to show.")]
        public int maxArrows = 12;

        [Header("Dialogue")]
        [TextArea]
        public string dialogueText;
        [Range(0.01f, 0.1f)] public float typewriterCharDelay = 0.03f;

        [Header("Progression")]
        public StepWaitMode waitMode = StepWaitMode.None;
        [Tooltip("Used when waitMode is WaitSeconds")] public float waitSeconds = 0f;

        [Header("Options")]
        [Tooltip("If true, input outside the target is ignored (MVP: not enforced).")]
        public bool blockOutside = false;
    }
}
