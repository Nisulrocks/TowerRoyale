using System;
using System.Collections.Generic;
using UnityEngine;

namespace TR.Systems
{
    [Serializable]
    public class CardProgress
    {
        public string cardId;
        public int level = 1;     // level >= 1 when owned
        public int points = 0;    // card points toward next level
        public int ownedCount = 1; // number of copies seen (for stats/info)
    }

    [Serializable]
    public class PlayerProfileDTO
    {
        public int softCurrency = 0;
        public int trophies = 0; // Arena progression trophies
        // Trophy floor: minimum trophies based on the highest arena reached
        public int trophiesFloor = 0;
        public List<CardProgress> cards = new();
        public List<string> deck = new(); // list of cardIds

        // Castle progression
        public int castleLevel = 1; // starts at 1
        public int castleXP = 0;    // XP toward next level

        // Simple pack inventory (packId -> count) using parallel lists for JsonUtility friendliness
        public List<string> packIds = new();
        public List<int> packCounts = new();

        // Meta flags / timers
        public bool starterClaimed = false;   // whether the persistent starter free pack has been claimed
        public long lastDailyPackUnix = 0;    // UTC seconds when last daily pack was granted

        // Tutorial persistence
        public bool tutorialActive = false;
        public int tutorialStep = 0;

        // Trophy Road claimed milestones (indices). Evergreen single road.
        public List<int> trophyRoadClaimed = new();

        // Shop: daily card points offers persistence
        public List<TR.Systems.ShopService.CardPointsOffer> cardPointOffers = new();
        public int cardPointOffersDayKey = 0; // yyyymmdd key of current offers

        public int GetPackCount(string packId)
        {
            int idx = packIds.IndexOf(packId);
            return idx >= 0 ? packCounts[idx] : 0;
        }

        public void AddPacks(string packId, int count)
        {
            int idx = packIds.IndexOf(packId);
            if (idx < 0)
            {
                packIds.Add(packId);
                packCounts.Add(Mathf.Max(0, count));
            }
            else packCounts[idx] = Mathf.Max(0, packCounts[idx] + count);
        }
        public bool ConsumePack(string packId)
        {
            int idx = packIds.IndexOf(packId);
            if (idx < 0 || packCounts[idx] <= 0) return false;
            packCounts[idx] -= 1;
            return true;
        }
    }

    public static class PlayerProfile
    {
        private static PlayerProfileDTO _data;
        public static PlayerProfileDTO Data => _data ?? (_data = LoadOrCreate());

        // Events
        public static event Action<int> OnSoftCurrencyChanged; // new balance

        public static PlayerProfileDTO LoadOrCreate()
        {
            try
            {
                string json = SaveSystem.Load();
                if (!string.IsNullOrEmpty(json))
                {
                    var dto = JsonUtility.FromJson<PlayerProfileDTO>(json);
                    if (dto != null) return dto;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"TR Profile load error: {ex}");
            }
            return new PlayerProfileDTO();
        }

        public static void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(Data, true);
                SaveSystem.Save(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"TR Profile save error: {ex}");
            }
        }

        // Daily pack helpers (correct location)
        public static long GetLastDailyPackUnix() => Data.lastDailyPackUnix;
        public static void SetLastDailyPackNow()
        {
            Data.lastDailyPackUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Save();
        }

        // Tutorial helpers
        public static bool GetTutorialActive() => Data.tutorialActive;
        public static void SetTutorialActive(bool active)
        {
            Data.tutorialActive = active;
            Save();
        }
        public static int GetTutorialStep() => Mathf.Max(0, Data.tutorialStep);
        public static void SetTutorialStep(int step)
        {
            Data.tutorialStep = Mathf.Max(0, step);
            Save();
        }

        // Danger: resets ALL local player progress (currency, cards, decks, XP, etc.)
        public static void WipeAllData()
        {
            _data = new PlayerProfileDTO();
            Save();
            OnSoftCurrencyChanged?.Invoke(_data.softCurrency);
        }

        // Convenience helpers for trophies and currency
        public static int GetTrophies() => Data.trophies;
        public static int GetTrophyFloor() => Mathf.Max(0, Data.trophiesFloor);
        // Raise the floor to at least 'value' (never decreases)
        public static void SetTrophyFloorAtLeast(int value)
        {
            int v = Mathf.Max(0, value);
            if (v > Data.trophiesFloor)
            {
                Data.trophiesFloor = v;
                // If current trophies somehow below new floor, lift them up to the floor
                if (Data.trophies < Data.trophiesFloor)
                    Data.trophies = Data.trophiesFloor;
                Save();
            }
        }
        public static void AddTrophies(int amount)
        {
            int add = Mathf.Max(0, amount);
            int current = Mathf.Max(0, Data.trophies);
            // Clamp to Trophy Road max if available
            var road = TR.Systems.GameDB.GetTrophyRoad();
            if (road != null)
            {
                int capped = Mathf.Min(current + add, Mathf.Max(0, road.MaxTrophies));
                Data.trophies = capped;
            }
            else
            {
                Data.trophies = current + add;
            }
            Save();
        }
        public static void RemoveTrophies(int amount)
        {
            int sub = Mathf.Max(0, amount);
            int current = Mathf.Max(0, Data.trophies);
            int floor = Mathf.Max(0, Data.trophiesFloor);
            Data.trophies = Mathf.Max(floor, current - sub);
            Save();
        }
        public static int GetSoftCurrency() => Data.softCurrency;
        public static void AddSoftCurrency(int amount)
        {
            Data.softCurrency = Mathf.Max(0, Data.softCurrency + Mathf.Max(0, amount));
            Save();
            OnSoftCurrencyChanged?.Invoke(Data.softCurrency);
        }

        // ===== Trophy Road claimed helpers (evergreen single road) =====
        public static bool IsTrophyMilestoneClaimed(int index)
        {
            return Data.trophyRoadClaimed != null && Data.trophyRoadClaimed.Contains(index);
        }
        public static void MarkTrophyMilestoneClaimed(int index)
        {
            if (Data.trophyRoadClaimed == null) Data.trophyRoadClaimed = new List<int>();
            if (!Data.trophyRoadClaimed.Contains(index)) Data.trophyRoadClaimed.Add(index);
            Save();
        }

        public static bool TrySpendSoftCurrency(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (Data.softCurrency < amount) return false;
            Data.softCurrency -= amount;
            Save();
            OnSoftCurrencyChanged?.Invoke(Data.softCurrency);
            return true;
        }

        // ===== Castle helpers =====
        public static int GetCastleLevel() => Mathf.Max(1, Data.castleLevel);
        public static int GetCastleXP() => Mathf.Max(0, Data.castleXP);

        public static void AddCastleXP(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (amount <= 0) return;
            var cfg = TR.Systems.GameDB.GetCastleProgression();
            Data.castleXP += amount;
            if (cfg != null)
            {
                int maxLevel = Mathf.Max(1, cfg.MaxLevel);
                // Keep leveling while we have enough XP and below max level
                while (Data.castleLevel < maxLevel)
                {
                    int needed = cfg.GetXPForLevel(Data.castleLevel);
                    if (needed <= 0) break;
                    if (Data.castleXP >= needed)
                    {
                        Data.castleXP -= needed;
                        Data.castleLevel++;
                    }
                    else break;
                }
                // If at max level, cap XP to the last threshold
                if (Data.castleLevel >= maxLevel)
                {
                    Data.castleXP = Mathf.Min(Data.castleXP, cfg.GetXPForLevel(maxLevel));
                }
            }
            Save();
        }

        public static int GetCastleMaxHealth()
        {
            var cfg = TR.Systems.GameDB.GetCastleProgression();
            int level = GetCastleLevel();
            return cfg != null ? Mathf.Max(1, cfg.GetHealthForLevel(level)) : 100;
        }

        public static CardProgress GetOrCreateCard(string cardId)
        {
            var list = Data.cards;
            var cp = list.Find(c => c.cardId == cardId);
            if (cp == null)
            {
                cp = new CardProgress { cardId = cardId, level = 1, points = 0, ownedCount = 0 };
                list.Add(cp);
            }
            return cp;
        }

        // Grant N copies of a card to the player's collection
        public static void AddCardCopies(string cardId, int count)
        {
            if (string.IsNullOrEmpty(cardId)) return;
            count = Mathf.Max(0, count);
            if (count <= 0) return;
            var cp = GetOrCreateCard(cardId);
            cp.ownedCount = Mathf.Max(0, cp.ownedCount + count);
            Save();
        }
    }
}
