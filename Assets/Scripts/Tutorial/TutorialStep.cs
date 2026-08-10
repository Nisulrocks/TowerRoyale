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
        // Holds until the player actually wins the match. Losing or leaving rewinds the tutorial
        // so they are guided back in, instead of advancing into steps that assume a victory.
        WaitForMatchVictory
    }

    public enum SpotlightMode
    {
        // On for steps that ask the player to click something, off for everything else.
        Auto,
        Always,
        Never
    }

    public enum DialogueAnchor
    {
        Left,
        Right,
        // In-battle positions: the bottom is the deck bar and the top corners hold currency, so
        // Left/Right always cover something. These sit clear of both.
        Top,
        Center,
        Bottom,
        // Appended so existing assets keep their saved values.
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

        [Tooltip("Optional: If set, this object (Button) will be clicked programmatically when the step starts (after scene matches). Useful to auto-open tabs/panels like Shop.")]
        public string autoClickObjectNameOnStart;


        public int maxArrows = 12;

        [Header("Dialogue")]
        [TextArea]
        public string dialogueText;
        [Range(0.01f, 0.1f)] public float typewriterCharDelay = 0.03f;
        public DialogueAnchor dialogueAnchor = DialogueAnchor.Left;

        [Header("Guide Sprite")]
        [Tooltip("Optional guide/emotion sprite shown with this dialogue step.")]
        public Sprite guideSprite;

        [Header("Progression")]
        public StepWaitMode waitMode = StepWaitMode.None;
public float waitSeconds = 0f;

        [Header("Options")]
        [Tooltip("If true, input outside the target is ignored (MVP: not enforced).")]
        public bool blockOutside = false;

        [Tooltip("Darkens everything except the target, the darkness sweeping inward until only the target is lit. Auto turns it on for click steps only. A spotlight always blocks input outside the target, since dimming something the player can still press is confusing.")]
        public SpotlightMode spotlight = SpotlightMode.Auto;

        [Header("Skip if target missing")]
        [Tooltip("If true and the target cannot be found, the step will auto-advance after No Target Skip Delay. Useful for optional upgrade/reward steps.")]
        public bool skipIfNoTarget = false;
        [Tooltip("Seconds to wait before auto-advancing when the target is missing. Only used when Skip If No Target is true.")]
        public float noTargetSkipDelay = 3f;

        [Header("Ghost Drag (waitMode = WaitForTargetDrag)")]
        [Tooltip("Loops a phantom card from the target card to a free placement area, showing the player the drag they need to make.")]
        public bool showGhostDrag = false;
        [Tooltip("Optional sprite for the ghost. Leave empty to use the dragged card's own icon.")]
        public Sprite ghostDragSprite;

        [Tooltip("After the guided drag, ask for one more with no ghost and no arrow, so the gesture is learned rather than copied. Only used when Show Ghost Drag is on.")]
        public bool requireUnassistedRepeat = false;
        [TextArea]
        [Tooltip("Dialogue for the unassisted repeat.")]
        public string repeatDialogueText = "Now you try — place one yourself.";
        [Tooltip("Optional guide sprite for the repeat prompt. Falls back to the step's guide sprite.")]
        public Sprite repeatGuideSprite;

        [Header("Match Outcome (waitMode = WaitForMatchVictory)")]
        [TextArea]
        [Tooltip("Shown back in the lobby after a loss, before the tutorial sends the player to retry.")]
        public string defeatDialogueText = "Not this time, Commander. Regroup and try that arena again.";
        [Tooltip("Optional guide sprite for the defeat message. Falls back to the step's guide sprite.")]
        public Sprite defeatGuideSprite;
        [Tooltip("How long the defeat message is shown before the tutorial rewinds.")]
        public float defeatMessageSeconds = 3f;
        [Tooltip("Step index to rewind to after a loss. Leave at -1 to rewind automatically to the last step that runs in the lobby, i.e. the one that sends the player into the arena.")]
        public int defeatRewindToStep = -1;

        [Header("Name Input (waitMode = WaitForNameInput)")]
        [Tooltip("Prompt label shown above the name input field.")]
        public string namePromptText = "Enter your name";
        [Tooltip("Placeholder text inside the input field.")]
        public string namePlaceholderText = "Your name...";
        [Tooltip("Greeting shown in the dialogue box after the name is confirmed. Use {0} for the name. Leave empty to skip.")]
        public string nameGreetingFormat = "Hello, {0}!";
        [Tooltip("Optional guide/emotion sprite shown during the name greeting. Falls back to the step's main guideSprite if not set.")]
        public Sprite nameGreetingGuideSprite;
        [Tooltip("How long (seconds) to show the greeting before moving to the next step.")]
        public float nameGreetingSeconds = 1.5f;
    }
}
