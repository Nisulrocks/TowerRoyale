using UnityEngine;

namespace TR.Data
{
    [System.Serializable]
    public class RarityWeight
    {
        public RarityDefinition rarity;
        public int weight = 1; // higher = more likely
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

        public string PackId => packId;
        public string DisplayName => displayName;
        public int CardsPerPack => cardsPerPack;
        public RarityWeight[] RarityWeights => rarityWeights;
        public int Cost => Mathf.Max(0, cost);
        public RarityDefinition GuaranteedRarity => guaranteedRarity;
        public bool GiveSpecificCardOnly => giveSpecificCardOnly;
        public CardDefinition SpecificCard => specificCard;
        public GameObject PackArtPrefab => packArtPrefab;
        public Sprite PackArtSprite => packArtSprite;
        public string SealCrackKey => sealCrackKey;
        public string OpenWhooshKey => openWhooshKey;
    }
}
