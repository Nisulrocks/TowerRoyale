using System.Collections.Generic;
using UnityEngine;
using TR.Data;

namespace TR.Systems
{
    public static class DeckService
    {
        // Default deck size fallback
        private const int DefaultMaxDeckSize = 8;
        public static int MaxDeckSize
        {
            get
            {
                var cfg = GameDB.GetGameplayConfig();
                return cfg != null ? Mathf.Max(1, cfg.MaxDeckSize) : DefaultMaxDeckSize;
            }
        }

        public static IReadOnlyList<string> GetDeck() => PlayerProfile.Data.deck;

        public static bool IsInDeck(string cardId) => PlayerProfile.Data.deck.Contains(cardId);

        public static bool TryAddToDeck(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            if (IsInDeck(cardId)) return false;
            if (PlayerProfile.Data.deck.Count >= MaxDeckSize) return false;
            // Ensure card exists
            if (GameDB.GetCardById(cardId) == null) return false;
            PlayerProfile.Data.deck.Add(cardId);
            PlayerProfile.Save();
            return true;
        }

        public static bool TryRemoveFromDeck(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            var res = PlayerProfile.Data.deck.Remove(cardId);
            if (res) PlayerProfile.Save();
            return res;
        }

        public static void ClearDeck()
        {
            PlayerProfile.Data.deck.Clear();
            PlayerProfile.Save();
        }
    }
}
