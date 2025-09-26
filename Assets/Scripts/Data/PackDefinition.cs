using UnityEngine;
using TR.Systems;

namespace TR.Data
{
    [System.Serializable]
    public class RarityWeight
    {
        public RarityDefinition rarity;
        public int weight = 1; // higher = more likely
        [Range(0f,100f)] public float percent = 0f; // optional percentage if using percentage mode
    }

    [CreateAssetMenu(fileName = "PackDefinition", menuName = "TR/Data/Pack Definition")]
    public class PackDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string packId; // unique key
        [SerializeField] private string displayName;

        [Header("Economy")]
        [Min(0)] [SerializeField] private int cost = 100; // soft currency cost per pack

        [Header("Contents")]
        [Min(1)] [SerializeField] private int cardsPerPack = 5;
        [SerializeField] private RarityWeight[] rarityWeights;
        [Tooltip("Optional: if assigned, this pack guarantees at least one card of this rarity.")]
        [SerializeField] private RarityDefinition guaranteedRarity;
        [Header("Weights vs Percentages")]
        [Tooltip("If ON, 'percent' fields are used and normalized to 100%. If OFF, integer 'weight' fields are used.")]
        [SerializeField] private bool usePercentages = false;

        [Header("Specific Card (optional)")]
        [Tooltip("If true, this pack ignores rarity weights and always grants the specific card below for all slots.")]
        [SerializeField] private bool giveSpecificCardOnly = false;
        [Tooltip("Specific card to grant when 'Give Specific Card Only' is enabled.")]
        [SerializeField] private CardDefinition specificCard;

        [Header("Pack Opening Art (optional)")]
        [Tooltip("Prefab to represent this pack in the Pack Opening scene. If set, it will be instantiated under the pack anchor.")]
        [SerializeField] private GameObject packArtPrefab;
        [Tooltip("Sprite to represent this pack if no prefab is provided.")]
        [SerializeField] private Sprite packArtSprite;

        [Header("Pack Opening SFX (optional)")]
        [Tooltip("SFX key played when the pack seal cracks (start of pop phase)")]
        [SerializeField] private string sealCrackKey;
        [Tooltip("SFX key played as the pack opens/slides (whoosh during slide phase)")]
        [SerializeField] private string openWhooshKey;

        [Header("Unlocking")]
        [Tooltip("Minimum arena required to unlock this pack in the shop. Leave empty for no restriction.")]
        [SerializeField] private ArenaDefinition unlockArena;

        [Header("Shop")]
        [Tooltip("Order for listing this pack in the shop (lower comes first). Tie-breaker is DisplayName.")]
        [SerializeField] private int shopOrder = 0;

        public string PackId => packId;
        public string DisplayName => displayName;
        public int CardsPerPack => cardsPerPack;
        public RarityWeight[] RarityWeights => rarityWeights;
        public bool UsePercentages => usePercentages;
        public int Cost => Mathf.Max(0, cost);
        public RarityDefinition GuaranteedRarity => guaranteedRarity;
        public bool GiveSpecificCardOnly => giveSpecificCardOnly;
        public CardDefinition SpecificCard => specificCard;
        public GameObject PackArtPrefab => packArtPrefab;
        public Sprite PackArtSprite => packArtSprite;
        public string SealCrackKey => sealCrackKey;
        public string OpenWhooshKey => openWhooshKey;
        public ArenaDefinition UnlockArena => unlockArena;
        public int RequiredTrophies => unlockArena != null ? Mathf.Max(0, unlockArena.TrophyRequirement) : 0;
        public int ShopOrder => shopOrder;

        // Unlock checks
        public bool IsUnlockedForTrophies(int trophies)
        {
            if (unlockArena == null) return true;
            return Mathf.Max(0, trophies) >= Mathf.Max(0, unlockArena.TrophyRequirement);
        }

        public bool IsUnlockedForPlayer()
        {
            return IsUnlockedForTrophies(PlayerProfile.GetTrophies());
        }
    }
}
