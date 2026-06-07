using UnityEngine;
using TR.Systems;

namespace TR.Data
{
    [System.Serializable]
    public class RarityWeight
    {
        public RarityDefinition rarity;
        public int weight = 1; 
        [Range(0f,100f)] public float percent = 0f; 
    }

    [CreateAssetMenu(fileName = "PackDefinition", menuName = "TR/Data/Pack Definition")]
    public class PackDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string packId; 
        [SerializeField] private string displayName;

        [Header("Economy")]
        [Min(0)] [SerializeField] private int cost = 100; 

        [Header("Contents")]
        [Min(1)] [SerializeField] private int cardsPerPack = 5;
        [SerializeField] private RarityWeight[] rarityWeights;

        [SerializeField] private RarityDefinition guaranteedRarity;
        [Header("Weights vs Percentages")]

        [SerializeField] private bool usePercentages = false;

        [Header("Specific Card (optional)")]

        [SerializeField] private bool giveSpecificCardOnly = false;

        [SerializeField] private CardDefinition specificCard;

        [Header("Pack Opening Art (optional)")]

        [SerializeField] private GameObject packArtPrefab;

        [SerializeField] private Sprite packArtSprite;

        [Header("Pack Opening SFX (optional)")]
        [Tooltip("SFX key played when the pack seal cracks (start of pop phase)")]
        [SerializeField] private string sealCrackKey;
        [Tooltip("SFX key played as the pack opens/slides (whoosh during slide phase)")]
        [SerializeField] private string openWhooshKey;

        [Header("Unlocking")]

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
