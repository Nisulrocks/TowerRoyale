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
    public class PlayerProfileDTO
    {
        public int softCurrency = 0;
        public int trophies = 0; 
        
        public int trophiesFloor = 0;
        public List<CardProgress> cards = new();
        public List<string> deck = new(); 

        
        public string pendingArenaUnlockName = null; 

        
        public int saveVersion = 1;               
        public string integrityHash = "";        
        public int tamperCount = 0;               
        public long lastTamperUnix = 0;           
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

        
        public int pendingCastleXpDelta = 0; 
    }

    public static class PlayerProfile
    {
        private static PlayerProfileDTO _data;
        public static PlayerProfileDTO Data => _data ?? (_data = LoadOrCreate());
        private const string Pepper = "tr_pepper_v1_!@#Ch33rs"; 

        
        
        
        public static bool BanTestModeEnabled = false;
        public static int BanTestStrike1Seconds = 10;
        public static int BanTestStrike2Seconds = 20;
        public static int BanTestStrike3Seconds = 30;

        
        public static void EnableBanTestMode(int strike1Seconds = 10, int strike2Seconds = 20, int strike3Seconds = 30)
        {
            BanTestModeEnabled = true;
            BanTestStrike1Seconds = Mathf.Max(1, strike1Seconds);
            BanTestStrike2Seconds = Mathf.Max(1, strike2Seconds);
            BanTestStrike3Seconds = Mathf.Max(1, strike3Seconds);
        }
        public static void DisableBanTestMode()
        {
            BanTestModeEnabled = false;
        }

        
        public static event Action<int> OnSoftCurrencyChanged; 

        public static PlayerProfileDTO LoadOrCreate()
        {
            try
            {
                string json = SaveSystem.Load();
                if (!string.IsNullOrEmpty(json))
                {
                    var dto = JsonUtility.FromJson<PlayerProfileDTO>(json);
                    if (dto != null)
                    {
                        if (VerifyIntegrity(dto))
                        {
                            return dto;
                        }
                        else
                        {
                            
                            string bak = SaveSystem.LoadBackup();
                            if (!string.IsNullOrEmpty(bak))
                            {
                                var backupDto = JsonUtility.FromJson<PlayerProfileDTO>(bak);
                                if (backupDto != null && VerifyIntegrity(backupDto))
                                {
                                    
                                    var moderated = HandleTamperAndModerate(backupDto);
                                    Save();
                                    return moderated;
                                }
                            }
                            
                            var fresh = new PlayerProfileDTO();
                            var moderatedFresh = HandleTamperAndModerate(fresh);
                            _data = moderatedFresh;
                            Save();
                            return _data;
                        }
                    }
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
                
                Data.saveVersion = Mathf.Max(1, Data.saveVersion);
                Data.integrityHash = ComputeIntegrityHash(Data);
                string json = JsonUtility.ToJson(Data, true);
                SaveSystem.Save(json);
                
                SaveSystem.SaveBackup(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"TR Profile save error: {ex}");
            }
        }

        private static bool VerifyIntegrity(PlayerProfileDTO dto)
        {
            if (dto == null) return false;
            if (string.IsNullOrEmpty(dto.integrityHash)) return false;
            string expected = ComputeIntegrityHash(dto);
            return string.Equals(dto.integrityHash, expected, StringComparison.Ordinal);
        }

        private static string ComputeIntegrityHash(PlayerProfileDTO dto)
        {
            try
            {
                
                string originalHash = dto.integrityHash;
                dto.integrityHash = string.Empty;
                string canonical = JsonUtility.ToJson(dto, false);
                dto.integrityHash = originalHash;

                
                string keyStr = Pepper + SystemInfo.deviceUniqueIdentifier;
                var key = System.Text.Encoding.UTF8.GetBytes(keyStr);
                var data = System.Text.Encoding.UTF8.GetBytes(canonical);

                using (var hmac = new System.Security.Cryptography.HMACSHA256(key))
                {
                    var hash = hmac.ComputeHash(data);
                    return System.BitConverter.ToString(hash).Replace("-", string.Empty);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Integrity hash failed: {ex}");
                return string.Empty;
            }
        }

        private static PlayerProfileDTO HandleTamperAndModerate(PlayerProfileDTO baseDto)
        {
            
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int strikes = Mathf.Max(0, baseDto.tamperCount) + 1;
            baseDto.tamperCount = strikes;
            baseDto.lastTamperUnix = now;
            long seconds;
            if (BanTestModeEnabled)
            {
                seconds = strikes == 1 ? BanTestStrike1Seconds : (strikes == 2 ? BanTestStrike2Seconds : BanTestStrike3Seconds);
            }
            else
            {
                int hours = strikes == 1 ? 6 : (strikes == 2 ? 12 : 24);
                seconds = hours * 3600L;
            }
            baseDto.banUntilUnix = now + seconds;

            
            if (strikes >= 3)
            {
                var fresh = new PlayerProfileDTO();
                fresh.tamperCount = strikes; 
                fresh.lastTamperUnix = now;
                fresh.banUntilUnix = baseDto.banUntilUnix;
                _data = fresh;
            }
            else
            {
                _data = baseDto;
            }
            return _data;
        }

        public static bool IsBanned(out TimeSpan remaining)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long until = System.Math.Max(0L, Data.banUntilUnix);
            long left = System.Math.Max(0L, until - now);
            remaining = TimeSpan.FromSeconds(left);
            return left > 0;
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
            int current = Mathf.Max(0, Data.trophies);
            
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
