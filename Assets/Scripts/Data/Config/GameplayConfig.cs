using UnityEngine;

namespace TR.Data
{
    [CreateAssetMenu(fileName = "GameplayConfig", menuName = "TR/Config/Gameplay Config")]
    public class GameplayConfig : ScriptableObject
    {
        [Header("Deck")]
        [Min(1)] [SerializeField] private int maxDeckSize = 8;

        public int MaxDeckSize => Mathf.Max(1, maxDeckSize);

        [System.Serializable]
        public class CardPointsOfferSlot
        {
            [Tooltip("Rarity Id this slot should target (e.g., 'common','rare','epic','legendary')")] public string rarityId;
            [Min(0)] public int pointsMin = 5;
            [Min(0)] public int pointsMax = 15;
            [Min(0)] public int costPerPointMin = 10;
            [Min(0)] public int costPerPointMax = 20;
        }

        [Header("Shop: Card Points Offers")]

        public CardPointsOfferSlot[] cardPointsOfferSlots = System.Array.Empty<CardPointsOfferSlot>();
        [Tooltip("UTC hour (0-23) at which daily offers refresh")] [Range(0,23)] public int offersRefreshHourUTC = 0;
    }
}
