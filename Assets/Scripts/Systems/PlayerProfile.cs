using System;
using System.Collections.Generic;
using UnityEngine;

namespace TR.Systems
{
    [Serializable]
    public class CardProgress
    {
        public string cardId;
        public int level = 1;     
        public int points = 0;    
        public int ownedCount = 1; 
    }

    [Serializable]
    public class DeckPreset
    {
        public List<string> cards = new();
    }

    [Serializable]
    public class PlayerProfileDTO
    {
        public string playerName = ""; 
        public int softCurrency = 0;
        public int trophies = 0; 
        
        public int trophiesFloor = 0;
        public List<CardProgress> cards = new();
        public List<string> deck = new();
        public List<DeckPreset> decks = new();
        public int selectedDeckIndex = 0; 

        
        public string pendingArenaUnlockName = null; 

        
        public int pendingCastleLevelFrom = 0;
        public int pendingCastleLevelTo = 0;
        public int pendingCastleHealthFrom = 0;
        public int pendingCastleHealthTo = 0;

        
        public long banUntilUnix = 0;             

        
        public int castleLevel = 1; 
        public int castleXP = 0;    

        
        public List<string> packIds = new();
        public List<int> packCounts = new();

        
        public bool starterClaimed = false;   
        public long lastDailyPackUnix = 0;    

        
        public bool tutorialActive = false;
        public int tutorialStep = 0;

        
        public List<int> trophyRoadClaimed = new();

        
        public List<TR.Systems.ShopService.CardPointsOffer> cardPointOffers = new();
        public int cardPointOffersDayKey = 0;

        public List<string> unlockedPackIds = new();

        public List<MatchRecord> matchLog = new();

        public int lifetimeMatches = 0;
        public int lifetimeWins = 0;
        public int lifetimeLosses = 0;
        public int lifetimeAbandons = 0;
        public int bestTrophies = 0;
        public int currentWinStreak = 0;
        public int longestWinStreak = 0;

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

        public int ConsumePacks(string packId, int count)
        {
            int idx = packIds.IndexOf(packId);
            if (idx < 0 || count <= 0) return 0;
            int consume = Mathf.Min(count, packCounts[idx]);
            packCounts[idx] -= consume;
            return consume;
        }

        
        public int pendingCastleXpDelta = 0; 
    }

    public static class PlayerProfile
    {
        private static PlayerProfileDTO _data;
        public static PlayerProfileDTO Data => _data ?? (_data = LoadOrCreate());
        public static event Action<int> OnSoftCurrencyChanged;
        public static event Action<int> OnTrophiesChanged;
        public static event Action<int, int> OnCastleLevelUp;
        public static event Action OnCloudProfileLoaded;

        private static bool _pendingTamperBan;

        private static bool _cloudSyncReady;

        public static bool IsCloudLinked => FirebaseService.IsSignedIn;

        public static void BeginCloudSync()
        {
            _cloudSyncReady = false;
        }

        public static bool IsEmptyProfile(PlayerProfileDTO d)
        {
            if (d == null) return true;
            if (!string.IsNullOrEmpty(d.playerName)) return false;
            if (d.trophies > 0 || d.softCurrency > 0) return false;
            if (d.castleLevel > 1 || d.castleXP > 0) return false;
            if (d.lifetimeMatches > 0) return false;
            if (d.matchLog != null && d.matchLog.Count > 0) return false;
            if (d.cards != null)
            {
                for (int i = 0; i < d.cards.Count; i++)
                    if (d.cards[i] != null && d.cards[i].ownedCount > 0) return false;
            }
            return true;
        }

        public static void AdoptLocalProfileAsCloud(bool serverConfirmedNew = false)
        {
            _data = LoadOrCreate();

            if (!serverConfirmedNew && IsEmptyProfile(_data))
            {
                Debug.LogWarning("[PlayerProfile] Cloud profile unconfirmed AND the local profile is empty. " +
                                 "Not uploading — refusing to overwrite the account with an empty profile.");
                _cloudSyncReady = false;
                OnCloudProfileLoaded?.Invoke();
                return;
            }

            Debug.Log($"[PlayerProfile] No cloud profile for this account; uploading local data " +
                      $"(trophies={_data.trophies}, serverConfirmedNew={serverConfirmedNew}).");
            _cloudSyncReady = true;
            Save();
            OnCloudProfileLoaded?.Invoke();
        }

        public static void LoadFromCloud(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                AdoptLocalProfileAsCloud(serverConfirmedNew: true);
                return;
            }

            SaveSystem.Load();
            if (SaveSystem.WasTampered || _pendingTamperBan)
                _pendingTamperBan = true;

            PlayerProfileDTO loaded = null;
            try
            {
                loaded = JsonUtility.FromJson<PlayerProfileDTO>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"TR Profile cloud load error: {ex}");
            }

            if (loaded == null)
            {
                Debug.LogError("[PlayerProfile] Cloud profile could not be parsed; keeping local data.");
                AdoptLocalProfileAsCloud();
                return;
            }

            MigrateDecks(loaded);
            _data = loaded;
            _cloudSyncReady = true; 
            Save();

            if (_pendingTamperBan)
            {
                _pendingTamperBan = false;
                Debug.LogWarning("[PlayerProfile] Applying 24h ban for tampered local data.");
                Data.banUntilUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 86400L;
                Save();
            }

            OnCloudProfileLoaded?.Invoke();
        }

        public static PlayerProfileDTO LoadOrCreate()
        {
            try
            {
                string json = SaveSystem.Load();
                if (SaveSystem.WasTampered)
                {
                    Debug.LogWarning("[PlayerProfile] Tampered local data detected - flagging for ban.");
                    _pendingTamperBan = true;
                    var fresh = new PlayerProfileDTO();
                    fresh.banUntilUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 86400L;
                    _data = fresh;
                    Save();
                    return fresh;
                }
                if (!string.IsNullOrEmpty(json))
                {
                    var dto = JsonUtility.FromJson<PlayerProfileDTO>(json);
                    if (dto != null)
                    {
                        MigrateDecks(dto);
                        return dto;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"TR Profile load error: {ex}");
            }
            return new PlayerProfileDTO();
        }

        private static void MigrateDecks(PlayerProfileDTO dto)
        {
            if (dto == null) return;
            if (dto.decks == null) dto.decks = new List<DeckPreset>();
            if (dto.decks.Count == 0 && dto.deck != null && dto.deck.Count > 0)
            {
                dto.decks.Add(new DeckPreset { cards = new List<string>(dto.deck) });
                dto.deck.Clear();
            }
            if (dto.selectedDeckIndex < 0 || (dto.decks.Count > 0 && dto.selectedDeckIndex >= dto.decks.Count))
                dto.selectedDeckIndex = 0;
        }

        public static void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(Data, true);
                SaveSystem.Save(json);

                if (!IsCloudLinked) return;

                if (!_cloudSyncReady)
                {
                    Debug.LogWarning("[PlayerProfile] Local save only — cloud profile not loaded yet for this account.");
                    return;
                }

                if (CloudProfileService.Instance == null)
                {
                    Debug.LogWarning("[PlayerProfile] Local save only — CloudProfileService is missing.");
                    return;
                }

                CloudProfileService.Instance.SaveProfile(
                    FirebaseService.UserId,
                    json,
                    Data.playerName ?? "",
                    Data.trophies);
            }
            catch (Exception ex)
            {
                Debug.LogError($"TR Profile save error: {ex}");
            }
        }

        public static bool IsBanned(out TimeSpan remaining)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long until = System.Math.Max(0L, Data.banUntilUnix);
            long left = System.Math.Max(0L, until - now);
            remaining = TimeSpan.FromSeconds(left);
            return left > 0;
        }

        public static void Ban(int minutes = 60)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Data.banUntilUnix = now + Mathf.Max(1, minutes) * 60L;
            Save();
        }

        public static void Unban()
        {
            Data.banUntilUnix = 0;
            Save();
        }

        
        public static long GetLastDailyPackUnix() => Data.lastDailyPackUnix;
        public static void SetLastDailyPackNow()
        {
            Data.lastDailyPackUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Save();
        }

        
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

        
        public static void WipeAllData()
        {
            _cloudSyncReady = false;
            _data = new PlayerProfileDTO();
            Save();
            OnSoftCurrencyChanged?.Invoke(_data.softCurrency);
        }

        
        public static int GetTrophies() => Data.trophies;
        public static int GetTrophyFloor() => Mathf.Max(0, Data.trophiesFloor);
        
        public static void SetTrophyFloorAtLeast(int value)
        {
            int v = Mathf.Max(0, value);
            if (v > Data.trophiesFloor)
            {
                Data.trophiesFloor = v;
                
                if (Data.trophies < Data.trophiesFloor)
                    Data.trophies = Data.trophiesFloor;
                Save();
            }
        }
        public static void AddTrophies(int amount)
        {
            int add = Mathf.Max(0, amount);
            if (add <= 0) return;

            GameDB.EnsureLoaded();
            var beforeArena = ArenaService.GetCurrentArena();
            int current = Mathf.Max(0, Data.trophies);
            
            var road = GameDB.GetTrophyRoad();
            if (road != null)
            {
                int capped = Mathf.Min(current + add, Mathf.Max(0, road.MaxTrophies));
                Data.trophies = capped;
            }
            else
            {
                Data.trophies = current + add;
            }

            var afterArena = ArenaService.GetCurrentArena();
            if (afterArena != null && (beforeArena == null || afterArena.TrophyRequirement > beforeArena.TrophyRequirement))
            {
                SetTrophyFloorAtLeast(Mathf.Max(0, afterArena.TrophyRequirement));
                SetPendingArenaUnlock(afterArena.DisplayName);
            }

            Save();
            OnTrophiesChanged?.Invoke(Data.trophies);
        }

        
        public static bool TryConsumePendingCastleXp(out int delta)
        {
            delta = Mathf.Max(0, Data.pendingCastleXpDelta);
            if (delta > 0)
            {
                Data.pendingCastleXpDelta = 0;
                Save();
                return true;
            }
            return false;
        }
        public static void RemoveTrophies(int amount)
        {
            int sub = Mathf.Max(0, amount);
            int current = Mathf.Max(0, Data.trophies);
            int floor = Mathf.Max(0, Data.trophiesFloor);
            Data.trophies = Mathf.Max(floor, current - sub);
            Save();
            OnTrophiesChanged?.Invoke(Data.trophies);
        }

        public static void SetTrophies(int target, bool bypassFloor = false)
        {
            GameDB.EnsureLoaded();
            var beforeArena = ArenaService.GetCurrentArena();
            int floor = bypassFloor ? 0 : Mathf.Max(0, Data.trophiesFloor);
            int maxTrophies = int.MaxValue;
            var road = GameDB.GetTrophyRoad();
            if (road != null) maxTrophies = Mathf.Max(0, road.MaxTrophies);

            Data.trophies = Mathf.Clamp(target, floor, maxTrophies);

            var afterArena = ArenaService.GetCurrentArena();
            if (afterArena != null && (beforeArena == null || afterArena.TrophyRequirement > beforeArena.TrophyRequirement))
            {
                SetTrophyFloorAtLeast(Mathf.Max(0, afterArena.TrophyRequirement));
                SetPendingArenaUnlock(afterArena.DisplayName);
            }

            Save();
            OnTrophiesChanged?.Invoke(Data.trophies);
        }
        public static bool IsPackUnlocked(string packId)
        {
            return !string.IsNullOrEmpty(packId) && Data.unlockedPackIds != null && Data.unlockedPackIds.Contains(packId);
        }

        public static void UnlockPack(string packId)
        {
            if (string.IsNullOrEmpty(packId)) return;
            if (Data.unlockedPackIds == null) Data.unlockedPackIds = new List<string>();
            if (!Data.unlockedPackIds.Contains(packId))
            {
                Data.unlockedPackIds.Add(packId);
                Save();
            }
        }

        public static int GetSoftCurrency() => Data.softCurrency;
        public static void AddSoftCurrency(int amount)
        {
            Data.softCurrency = Mathf.Max(0, Data.softCurrency + Mathf.Max(0, amount));
            Save();
            OnSoftCurrencyChanged?.Invoke(Data.softCurrency);
        }

        
        public static void SetPendingArenaUnlock(string arenaDisplayName)
        {
            if (string.IsNullOrEmpty(arenaDisplayName)) return;
            Data.pendingArenaUnlockName = arenaDisplayName;
            Save();
        }

        
        public static bool TryConsumePendingArenaUnlock(out string arenaDisplayName)
        {
            arenaDisplayName = Data.pendingArenaUnlockName;
            if (string.IsNullOrEmpty(arenaDisplayName)) return false;
            Data.pendingArenaUnlockName = null;
            Save();
            return true;
        }

        public static void SetPendingCastleLevelUp(int fromLevel, int toLevel, int fromHealth, int toHealth)
        {
            if (toLevel <= fromLevel) return;
            Data.pendingCastleLevelFrom = fromLevel;
            Data.pendingCastleLevelTo = toLevel;
            Data.pendingCastleHealthFrom = fromHealth;
            Data.pendingCastleHealthTo = toHealth;
            Save();
        }

        public static bool TryConsumePendingCastleLevelUp(out int fromLevel, out int toLevel, out int fromHealth, out int toHealth)
        {
            fromLevel = Data.pendingCastleLevelFrom;
            toLevel = Data.pendingCastleLevelTo;
            fromHealth = Data.pendingCastleHealthFrom;
            toHealth = Data.pendingCastleHealthTo;
            if (toLevel <= fromLevel) return false;
            Data.pendingCastleLevelFrom = 0;
            Data.pendingCastleLevelTo = 0;
            Data.pendingCastleHealthFrom = 0;
            Data.pendingCastleHealthTo = 0;
            Save();
            return true;
        }

        
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

        
        public static int GetCastleLevel() => Mathf.Max(1, Data.castleLevel);
        public static int GetCastleXP() => Mathf.Max(0, Data.castleXP);

        public static void AddCastleXP(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (amount <= 0) return;
            var cfg = TR.Systems.GameDB.GetCastleProgression();
            Data.castleXP += amount;
            
            Data.pendingCastleXpDelta = Mathf.Max(0, Data.pendingCastleXpDelta + amount);

            int oldLevel = Data.castleLevel;
            int oldHealth = cfg != null ? cfg.GetHealthForLevel(oldLevel) : 0;

            if (cfg != null)
            {
                int maxLevel = Mathf.Max(1, cfg.MaxLevel);
                
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
                
                if (Data.castleLevel >= maxLevel)
                {
                    Data.castleXP = Mathf.Min(Data.castleXP, cfg.GetXPForLevel(maxLevel));
                }
            }

            int newLevel = Data.castleLevel;
            if (newLevel > oldLevel)
            {
                int newHealth = cfg != null ? cfg.GetHealthForLevel(newLevel) : 0;
                SetPendingCastleLevelUp(oldLevel, newLevel, oldHealth, newHealth);
                OnCastleLevelUp?.Invoke(oldLevel, newLevel);
            }

            Save();
        }

        public static int GetCastleMaxHealth()
        {
            var cfg = TR.Systems.GameDB.GetCastleProgression();
            int level = GetCastleLevel();
            return cfg != null ? Mathf.Max(1, cfg.GetHealthForLevel(level)) : 100;
        }

        
        public const int PlayerNameMinLength = 2;
        public const int PlayerNameMaxLength = 16;

        public static string GetPlayerName() => Data.playerName ?? string.Empty;

        public static bool HasPlayerName() => !string.IsNullOrWhiteSpace(Data.playerName);

        
        public static void SetPlayerName(string name)
        {
            Data.playerName = SanitizePlayerName(name);
            Save();
        }

        
        public static bool IsValidPlayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string trimmed = name.Trim();
            return trimmed.Length >= PlayerNameMinLength && trimmed.Length <= PlayerNameMaxLength;
        }

        public static string SanitizePlayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            string trimmed = name.Trim();
            if (trimmed.Length > PlayerNameMaxLength) trimmed = trimmed.Substring(0, PlayerNameMaxLength);
            return trimmed;
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
