using UnityEngine;

namespace TR.Data
{
    [CreateAssetMenu(fileName = "GameplayConfig", menuName = "TR/Config/Gameplay Config")]
    public class GameplayConfig : ScriptableObject
    {
        [Header("Deck")]
        [Min(1)] [SerializeField] private int maxDeckSize = 8;
        [Min(1)] 
        [SerializeField] private int maxDeckPresets = 3;

        public int MaxDeckSize => Mathf.Max(1, maxDeckSize);
        public int MaxDeckPresets => Mathf.Max(1, maxDeckPresets);

        [System.Serializable]
        public class CardPointsOfferSlot
        {
             public string rarityId;
            [Min(0)] public int pointsMin = 5;
            [Min(0)] public int pointsMax = 15;
            [Min(0)] public int costPerPointMin = 10;
            [Min(0)] public int costPerPointMax = 20;
        }

        [Header("Shop: Card Points Offers")]

        public CardPointsOfferSlot[] cardPointsOfferSlots = System.Array.Empty<CardPointsOfferSlot>();
         [Range(0,23)] public int offersRefreshHourUTC = 0;
    }
}
