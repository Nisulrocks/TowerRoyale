using System.Collections.Generic;
using UnityEngine;
using TR.Data;

namespace TR.Systems
{
    public static class DeckService
    {
        private const int DefaultMaxDeckSize = 8;
        private const int DefaultMaxDeckPresets = 3;

        public static int MaxDeckSize
        {
            get
            {
                var cfg = GameDB.GetGameplayConfig();
                return cfg != null ? Mathf.Max(1, cfg.MaxDeckSize) : DefaultMaxDeckSize;
            }
        }

        public static int MaxDeckPresets
        {
            get
            {
                var cfg = GameDB.GetGameplayConfig();
                return cfg != null ? Mathf.Max(1, cfg.MaxDeckPresets) : DefaultMaxDeckPresets;
            }
        }

        public static void EnsureDecksInitialized()
        {
            var data = PlayerProfile.Data;
            if (data.decks == null) data.decks = new List<DeckPreset>();
            int target = MaxDeckPresets;
            while (data.decks.Count < target)
                data.decks.Add(new DeckPreset());
            if (data.selectedDeckIndex < 0 || data.selectedDeckIndex >= data.decks.Count)
                data.selectedDeckIndex = 0;
        }

        public static int SelectedDeckIndex
        {
            get
            {
                EnsureDecksInitialized();
                return PlayerProfile.Data.selectedDeckIndex;
            }
        }

        public static int DeckCount
        {
            get
            {
                EnsureDecksInitialized();
                return PlayerProfile.Data.decks.Count;
            }
        }

        public static IReadOnlyList<string> GetDeck()
        {
            EnsureDecksInitialized();
            var data = PlayerProfile.Data;
            int idx = data.selectedDeckIndex;
            if (idx >= 0 && idx < data.decks.Count)
                return data.decks[idx].cards;
            return data.decks[0].cards;
        }

        public static IReadOnlyList<string> GetDeckPreset(int index)
        {
            EnsureDecksInitialized();
            var data = PlayerProfile.Data;
            if (index >= 0 && index < data.decks.Count)
                return data.decks[index].cards;
            return data.decks[0].cards;
        }

        public static bool IsInDeck(string cardId)
        {
            var deck = GetDeck();
            return deck != null && ((List<string>)deck).Contains(cardId);
        }

        public static void SelectDeck(int index)
        {
            EnsureDecksInitialized();
            var data = PlayerProfile.Data;
            if (index >= 0 && index < data.decks.Count)
            {
                data.selectedDeckIndex = index;
                PlayerProfile.Save();
            }
        }

        public static bool TryAddToDeck(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            if (IsInDeck(cardId)) return false;
            EnsureDecksInitialized();
            var data = PlayerProfile.Data;
            var deck = data.decks[data.selectedDeckIndex].cards;
            if (deck.Count >= MaxDeckSize) return false;
            if (GameDB.GetCardById(cardId) == null) return false;
            deck.Add(cardId);
            PlayerProfile.Save();
            return true;
        }

        public static bool TryRemoveFromDeck(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            EnsureDecksInitialized();
            var data = PlayerProfile.Data;
            var deck = data.decks[data.selectedDeckIndex].cards;
            var res = deck.Remove(cardId);
            if (res) PlayerProfile.Save();
            return res;
        }

        public static void ClearDeck()
        {
            EnsureDecksInitialized();
            var data = PlayerProfile.Data;
            data.decks[data.selectedDeckIndex].cards.Clear();
            PlayerProfile.Save();
        }
    }
}
