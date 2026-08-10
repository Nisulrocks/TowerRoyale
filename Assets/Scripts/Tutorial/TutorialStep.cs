using UnityEngine;

namespace TR.Tutorial
{
    public enum TargetMode
    {
        ByName,
        ShopPackById,
        OwnedCollectionCards,
        UpgradeReadyCollectionCard,
        TrophyRoadClaimable
    }

    public enum StepWaitMode
    {
        None,
        WaitSeconds,
        WaitForTargetClick,
        WaitForTargetDrag,
        WaitForNameInput,
        WaitForMatchVictory
    }

    public enum SpotlightMode
    {
        Auto,
        Always,
        Never
    }

    public enum DialogueAnchor
    {
        Left,
        Right,
        Top,
        Center,
        Bottom,
        MiddleLeft,
        MiddleRight
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

        public string autoClickObjectNameOnStart;


        public int maxArrows = 12;

        [Header("Dialogue")]
        [TextArea]
        public string dialogueText;
        [Range(0.01f, 0.1f)] public float typewriterCharDelay = 0.03f;
        public DialogueAnchor dialogueAnchor = DialogueAnchor.Left;

        [Header("Guide Sprite")]
        public Sprite guideSprite;

        [Header("Progression")]
        public StepWaitMode waitMode = StepWaitMode.None;
public float waitSeconds = 0f;

        [Header("Options")]
        public bool blockOutside = false;

        public SpotlightMode spotlight = SpotlightMode.Auto;

        [Header("Skip if target missing")]
        public bool skipIfNoTarget = false;
        public float noTargetSkipDelay = 3f;

        [Header("Ghost Drag (waitMode = WaitForTargetDrag)")]
        public bool showGhostDrag = false;
        public Sprite ghostDragSprite;

        public bool requireUnassistedRepeat = false;
        [TextArea]
        public string repeatDialogueText = "Now you try — place one yourself.";
        public Sprite repeatGuideSprite;

        [Header("Match Outcome (waitMode = WaitForMatchVictory)")]
        [TextArea]
        public string defeatDialogueText = "Not this time, Commander. Regroup and try that arena again.";
        public Sprite defeatGuideSprite;
        public float defeatMessageSeconds = 3f;
        public int defeatRewindToStep = -1;

        [Header("Name Input (waitMode = WaitForNameInput)")]
        public string namePromptText = "Enter your name";
        public string namePlaceholderText = "Your name...";
        public string nameGreetingFormat = "Hello, {0}!";
        public Sprite nameGreetingGuideSprite;
        public float nameGreetingSeconds = 1.5f;
    }
}
