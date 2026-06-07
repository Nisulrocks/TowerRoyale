using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Data;
using TR.Systems;
using System.Collections;

namespace TR.UI
{
    
    public class HoverCardDetailsUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject root; 
        [SerializeField] private RectTransform panel; 
        [SerializeField] private float showX = -40f;  
        [SerializeField] private float hiddenX = -420f; 
        [SerializeField] private float animDuration = 0.18f;

        [Header("Header")] 
        [SerializeField] private Image icon;
        [SerializeField] private Image rarityStripe;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;

        [Header("Stats")]
        [SerializeField] private TMP_Text dpsText;
        [SerializeField] private TMP_Text fireRateText;
        [SerializeField] private TMP_Text rangeText;
        [SerializeField] private TMP_Text splashText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text effectText; 

        public static HoverCardDetailsUI Instance { get; private set; }
        
        
        
        private static bool s_inCollectionContext = false;
        public static void SetCollectionContext(bool on) => s_inCollectionContext = on;

        private Coroutine _anim;
        private bool _visible;
        private float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        private TR.Battle.TowerBase _boundTower;
        
        private void SetLine(TMP_Text field, string content)
        {
            if (field == null) return;
            bool show = !string.IsNullOrEmpty(content);
            field.gameObject.SetActive(show);
            if (show) field.text = content;
        }
        private void RebuildLayout()
        {
            if (panel == null) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        private void Awake()
        {
            Instance = this;
            if (panel != null)
            {
                var p = panel.anchoredPosition;
                p.x = hiddenX;
                panel.anchoredPosition = p;
            }
            if (root == null && panel != null) root = panel.gameObject;
            if (root != null) root.SetActive(false);
        }

        public static void Show(CardDefinition card, int level)
        {
            var inst = Instance ?? FindFirstObjectByType<HoverCardDetailsUI>(FindObjectsInactive.Include);
            if (inst == null)
            {
                Debug.LogWarning("[HoverCardDetailsUI] No instance found. Add HoverCardDetailsUI to the scene and assign references.");
                return;
            }
            
            inst.UnbindTower();
            
            var cp = TR.Systems.PlayerProfile.GetOrCreateCard(card.CardId);
            bool undiscovered = cp != null && cp.ownedCount <= 0;
            if (undiscovered)
            {
                inst.SetData(card, level); 
                inst.SetVisible(true);
                return;
            }
            
            if (card is BuffCardDefinition buff)
            {
                int lvb = Mathf.Max(1, level);
                
                bool showNext = false;
                int nextLv = lvb;
                if (s_inCollectionContext && cp != null)
                {
                    var rarity = card.Rarity;
                    int maxL = rarity != null ? rarity.MaxLevel : lvb;
                    int nlv = Mathf.Min(lvb + 1, maxL);
                    if (nlv > lvb)
                    {
                        int needed = rarity != null ? rarity.GetPointsRequiredForLevel(nlv) : int.MaxValue;
                        int cost = rarity != null ? rarity.GetUpgradeCostForLevel(nlv) : int.MaxValue;
                        showNext = (cp.points >= needed) && (PlayerProfile.GetSoftCurrency() >= cost);
                        if (showNext) nextLv = nlv;
                    }
                }
                
                if (inst.icon) inst.icon.sprite = card.Icon;
                if (inst.rarityStripe && card.Rarity) inst.rarityStripe.color = card.Rarity.Color;
                if (inst.nameText) inst.nameText.text = card.DisplayName;
                if (inst.levelText) inst.levelText.text = showNext ? $"Lv {lvb} -> {nextLv}" : $"Lv {lvb}";
                float auraRange = buff.GetBuffRange(lvb);
                if (showNext)
                {
                    float nextAura = buff.GetBuffRange(nextLv);
                    inst.SetLine(inst.dpsText, $"Aura Range: {auraRange:0.#} -> {nextAura:0.#}");
                }
                else
                {
                    inst.SetLine(inst.dpsText, $"Aura Range: {auraRange:0.#}");
                }

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                if (buff.BuffDps)
                {
                    if (showNext) sb.Append($"DPS +{buff.GetDpsPercent(lvb) * 100f:0.#}% -> +{buff.GetDpsPercent(nextLv) * 100f:0.#}%");
                    else sb.Append($"DPS +{buff.GetDpsPercent(lvb) * 100f:0.#}%");
                }
                if (buff.BuffFireRate)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    if (showNext) sb.Append($"FireRate +{buff.GetFireRatePercent(lvb) * 100f:0.#}% -> +{buff.GetFireRatePercent(nextLv) * 100f:0.#}%");
                    else sb.Append($"FireRate +{buff.GetFireRatePercent(lvb) * 100f:0.#}%");
                }
                if (buff.BuffRange)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    if (showNext) sb.Append($"Range +{buff.GetRangePercent(lvb) * 100f:0.#}% -> +{buff.GetRangePercent(nextLv) * 100f:0.#}%");
                    else sb.Append($"Range +{buff.GetRangePercent(lvb) * 100f:0.#}%");
                }
                if (buff.BuffSplash)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    if (showNext) sb.Append($"Splash +{buff.GetSplashPercent(lvb) * 100f:0.#}% -> +{buff.GetSplashPercent(nextLv) * 100f:0.#}%");
                    else sb.Append($"Splash +{buff.GetSplashPercent(lvb) * 100f:0.#}%");
                }
                if (buff.BuffBurn)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    if (showNext) sb.Append($"Burn Dmg +{buff.GetBurnDpsBuffPercent(lvb) * 100f:0.#}% -> +{buff.GetBurnDpsBuffPercent(nextLv) * 100f:0.#}%  Dur +{buff.GetBurnDurBuffPercent(lvb) * 100f:0.#}% -> +{buff.GetBurnDurBuffPercent(nextLv) * 100f:0.#}%");
                    else sb.Append($"Burn Dmg +{buff.GetBurnDpsBuffPercent(lvb) * 100f:0.#}% Dur +{buff.GetBurnDurBuffPercent(lvb) * 100f:0.#}%");
                }
                if (buff.BuffPoison)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    if (showNext) sb.Append($"Poison Dmg +{buff.GetPoisonDpsBuffPercent(lvb) * 100f:0.#}% -> +{buff.GetPoisonDpsBuffPercent(nextLv) * 100f:0.#}%  Dur +{buff.GetPoisonDurBuffPercent(lvb) * 100f:0.#}% -> +{buff.GetPoisonDurBuffPercent(nextLv) * 100f:0.#}%");
                    else sb.Append($"Poison Dmg +{buff.GetPoisonDpsBuffPercent(lvb) * 100f:0.#}% Dur +{buff.GetPoisonDurBuffPercent(lvb) * 100f:0.#}%");
                }
                if (buff.BuffSlow)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    if (showNext) sb.Append($"Slow % +{buff.GetSlowPercentBuffPercent(lvb) * 100f:0.#}% -> +{buff.GetSlowPercentBuffPercent(nextLv) * 100f:0.#}%  Dur +{buff.GetSlowDurBuffPercent(lvb) * 100f:0.#}% -> +{buff.GetSlowDurBuffPercent(nextLv) * 100f:0.#}%");
                    else sb.Append($"Slow % +{buff.GetSlowPercentBuffPercent(lvb) * 100f:0.#}% Dur +{buff.GetSlowDurBuffPercent(lvb) * 100f:0.#}%");
                }
                if (buff.BuffStun)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    if (showNext) sb.Append($"Stun % +{buff.GetStunChanceBuffPercent(lvb) * 100f:0.#}% -> +{buff.GetStunChanceBuffPercent(nextLv) * 100f:0.#}%  Dur +{buff.GetStunDurBuffPercent(lvb) * 100f:0.#}% -> +{buff.GetStunDurBuffPercent(nextLv) * 100f:0.#}%");
                    else sb.Append($"Stun % +{buff.GetStunChanceBuffPercent(lvb) * 100f:0.#}% Dur +{buff.GetStunDurBuffPercent(lvb) * 100f:0.#}%");
                }
                
                if (buff.BuffEconomyIncome)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    if (showNext) sb.Append($"Econ Income +{buff.GetEconomyIncomePercent(lvb) * 100f:0.#}% -> +{buff.GetEconomyIncomePercent(nextLv) * 100f:0.#}%");
                    else sb.Append($"Econ Income +{buff.GetEconomyIncomePercent(lvb) * 100f:0.#}%");
                }
                inst.SetLine(inst.fireRateText, sb.Length > 0 ? sb.ToString() : null);
                
                string affects = null;
                var allowed = buff.AllowedRarities;
                if (allowed == null || allowed.Count == 0)
                {
                    affects = "Affects: All Rarities";
                }
                else
                {
                    System.Text.StringBuilder rsb = new System.Text.StringBuilder();
                    for (int i = 0; i < allowed.Count; i++)
                    {
                        var r = allowed[i]; if (r == null) continue;
                        string label = !string.IsNullOrEmpty(r.RarityId) ? r.RarityId : r.name;
                        if (rsb.Length > 0) rsb.Append(", ");
                        rsb.Append(label);
                    }
                    affects = rsb.Length > 0 ? ("Affects: " + rsb.ToString()) : "Affects: (None)";
                }
                inst.SetLine(inst.rangeText, affects);
                inst.SetLine(inst.splashText, null);
                { var cstats = card.GetStatsForLevel(lvb); if (showNext) { var nstats = card.GetStatsForLevel(nextLv); inst.SetLine(inst.costText, $"Cost: {cstats.cost} -> {nstats.cost}"); } else inst.SetLine(inst.costText, $"Cost: {cstats.cost}"); }
                inst.SetLine(inst.effectText, "Effect: Buff Aura");
                inst.RebuildLayout();
                inst.SetVisible(true);
                return;
            }
            inst.SetData(card, level);
            inst.SetVisible(true);
        }

        
        public static void Show(TR.Battle.TowerBase tower)
        {
            var inst = Instance ?? FindFirstObjectByType<HoverCardDetailsUI>(FindObjectsInactive.Include);
            if (inst == null) return;
            inst.BindTower(tower);
            inst.SetVisible(true);
        }

        private void BindTower(TR.Battle.TowerBase tower)
        {
            UnbindTower();
            _boundTower = tower;
            if (_boundTower != null)
            {
                _boundTower.OnBuffsChanged += RefreshBoundTowerUI;
            }
            RefreshBoundTowerUI();
        }

        private void UnbindTower()
        {
            if (_boundTower != null)
            {
                _boundTower.OnBuffsChanged -= RefreshBoundTowerUI;
                _boundTower = null;
            }
        }

        private void RefreshBoundTowerUI()
        {
            if (_boundTower == null || _boundTower.Definition == null)
            {
                return;
            }
            var card = _boundTower.Definition;
            int lv = _boundTower.Level;
            if (icon) icon.sprite = card.Icon;
            if (rarityStripe && card.Rarity) rarityStripe.color = card.Rarity.Color;
            if (nameText) nameText.text = card.DisplayName;
            if (levelText) levelText.text = $"Lv {lv}";

            
            if (card is EconomyCardDefinition econ)
            {
                float baseIncome = econ.GetIncomePerSecond(lv);
                
                float income = baseIncome;
                float buffPct = 0f;
                var econRt = _boundTower.GetComponent<TR.Battle.EconomyTower>();
                if (econRt != null)
                {
                    income = econRt.GetEffectiveIncomePerSecond();
                    if (baseIncome > 1e-5f) buffPct = Mathf.Max(0f, (income / baseIncome) - 1f);
                }
                float decay = econ.GetDecayPerSecond(lv);
                float maxHp = econ.GetMaxHealth(lv);
                if (buffPct > 0.0001f)
                    SetLine(dpsText, $"Income/s: {income:0.#}  (+{buffPct * 100f:0.#}%)");
                else
                    SetLine(dpsText, $"Income/s: {income:0.#}");
                SetLine(fireRateText, $"Decay/s: {decay:0.#}");
                SetLine(rangeText, $"Max HP: {maxHp:0.#}");
                SetLine(splashText, null);
                { var cstats = card.GetStatsForLevel(lv); SetLine(costText, $"Cost: {cstats.cost}"); }
                SetLine(effectText, null);
                RebuildLayout();
                return;
            }

            
            if (card is BuffCardDefinition buff)
            {
                
                float auraRange = buff.GetBuffRange(lv);
                SetLine(dpsText, $"Aura Range: {auraRange:0.#}");
                
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                if (buff.BuffDps) sb.Append($"DPS +{buff.GetDpsPercent(lv) * 100f:0.#}%");
                if (buff.BuffFireRate)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"FireRate +{buff.GetFireRatePercent(lv) * 100f:0.#}%");
                }
                if (buff.BuffRange)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Range +{buff.GetRangePercent(lv) * 100f:0.#}%");
                }
                if (buff.BuffSplash)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Splash +{buff.GetSplashPercent(lv) * 100f:0.#}%");
                }
                if (buff.BuffEconomyIncome)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Econ Income +{buff.GetEconomyIncomePercent(lv) * 100f:0.#}%");
                }
                
                if (buff.BuffBurn)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Burn Dmg +{buff.GetBurnDpsBuffPercent(lv) * 100f:0.#}% Dur +{buff.GetBurnDurBuffPercent(lv) * 100f:0.#}%");
                }
                if (buff.BuffPoison)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Poison Dmg +{buff.GetPoisonDpsBuffPercent(lv) * 100f:0.#}% Dur +{buff.GetPoisonDurBuffPercent(lv) * 100f:0.#}%");
                }
                if (buff.BuffSlow)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Slow % +{buff.GetSlowPercentBuffPercent(lv) * 100f:0.#}% Dur +{buff.GetSlowDurBuffPercent(lv) * 100f:0.#}%");
                }
                if (buff.BuffStun)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Stun % +{buff.GetStunChanceBuffPercent(lv) * 100f:0.#}% Dur +{buff.GetStunDurBuffPercent(lv) * 100f:0.#}%");
                }
                SetLine(fireRateText, sb.Length > 0 ? sb.ToString() : null);
                
                string affects = null;
                var allowed = buff.AllowedRarities;
                if (allowed == null || allowed.Count == 0)
                {
                    affects = "Affects: All Rarities";
                }
                else
                {
                    System.Text.StringBuilder rsb = new System.Text.StringBuilder();
                    for (int i = 0; i < allowed.Count; i++)
                    {
                        var r = allowed[i]; if (r == null) continue;
                        string label = !string.IsNullOrEmpty(r.RarityId) ? r.RarityId : r.name;
                        if (rsb.Length > 0) rsb.Append(", ");
                        rsb.Append(label);
                    }
                    affects = rsb.Length > 0 ? ("Affects: " + rsb.ToString()) : "Affects: (None)";
                }
                SetLine(rangeText, affects);
                SetLine(splashText, null);
                { var cstats = card.GetStatsForLevel(lv); SetLine(costText, $"Cost: {cstats.cost}"); }
                SetLine(effectText, "Effect: Buff Aura");
                RebuildLayout();
                return;
            }

            
            if (card is PulseCardDefinition pulseDefBound)
            {
                
                float dmg = pulseDefBound.GetPulseDamage(lv) * (_boundTower != null ? _boundTower.GetDpsMultiplier() : 1f);
                float interval = pulseDefBound.GetPulseInterval(lv) / Mathf.Max(0.01f, (_boundTower != null ? _boundTower.GetFireRateMultiplier() : 1f));
                float radius = _boundTower.GetEffectiveRange();
                SetLine(dpsText, $"Pulse Dmg: {dmg:0.#}");
                SetLine(fireRateText, $"Interval: {interval:0.##}s");
                SetLine(rangeText, $"Pulse Radius: {radius:0.#}");
                SetLine(splashText, null);
                { var cstats = card.GetStatsForLevel(lv); SetLine(costText, $"Cost: {cstats.cost}"); }
                
                goto EffectsSummary;
            }

            
            if (card is InfernoCardDefinition inferno)
            {
                var statsInf = inferno.GetStatsForLevel(lv);
                float effDps = _boundTower.GetEffectiveDps();
                float effRange = _boundTower.GetEffectiveRange();
                int maxTargets = inferno.GetMaxTargets(lv);
                float rampUp = inferno.GetRampUpPerSecond(lv);
                float rampMax = inferno.GetRampMaxMultiplier(lv);
                float penalty = inferno.GetMultiTargetPenalty(lv);

                SetLine(dpsText, $"Base DPS: {effDps:0.#}");
                SetLine(fireRateText, $"Ramp: +{rampUp:0.##}/s to {rampMax:0.#}x");
                SetLine(rangeText, $"Range: {effRange:0.#}");
                SetLine(splashText, $"Max Targets: {maxTargets} (pen {penalty:0.##})");
                SetLine(costText, $"Cost: {statsInf.cost}");
            }
            else
            {
                
                SetLine(dpsText, $"DPS: {_boundTower.GetEffectiveDps():0.#}");
                SetLine(fireRateText, $"Fire Rate: {_boundTower.GetEffectiveFireRate():0.##}/s");
                SetLine(rangeText, $"Range: {_boundTower.GetEffectiveRange():0.#}");
                {
                    float s = _boundTower.GetEffectiveSplashRadius();
                    SetLine(splashText, s > 0 ? $"Splash: {s:0.#}" : null);
                }
                { var cstats = card.GetStatsForLevel(lv); SetLine(costText, $"Cost: {cstats.cost}"); }
            }

        EffectsSummary:
            
            if (effectText)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                float burnDps = card.GetBurnDps(lv) * _boundTower.GetBurnDpsMultiplier();
                float burnDur = card.GetBurnDuration(lv) * _boundTower.GetBurnDurMultiplier();
                float poisonDps = card.GetPoisonDps(lv) * _boundTower.GetPoisonDpsMultiplier();
                float poisonDur = card.GetPoisonDuration(lv) * _boundTower.GetPoisonDurMultiplier();
                if (burnDps > 0f && burnDur > 0f) sb.Append($"Burn {burnDps:0.#}/s {burnDur:0.#}s");
                if (poisonDps > 0f && poisonDur > 0f)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Poison {poisonDps:0.#}/s {poisonDur:0.#}s");
                }
                if (card.HasSlowOnHit())
                {
                    float sp = card.GetSlowPercent(lv) * _boundTower.GetSlowPercentMultiplier();
                    float sd = card.GetSlowDuration(lv) * _boundTower.GetSlowDurMultiplier();
                    if (sp > 0f && sd > 0f)
                    {
                        if (sb.Length > 0) sb.Append("  |  ");
                        sb.Append($"Slow {sp * 100f:0.#}% {sd:0.#}s");
                    }
                }
                
                if (card.HasSlowOnHit() && card.HasFrostbiteOnHit())
                {
                    float fbDps = card.GetFrostbiteDps(lv);
                    float fbDur = card.GetFrostbiteDuration(lv);
                    if (fbDps > 0f && fbDur > 0f)
                    {
                        if (sb.Length > 0) sb.Append("  |  ");
                        sb.Append($"Frostbite {fbDps:0.#}/s {fbDur:0.#}s");
                    }
                }
                
                if (card.HasStunOnHit())
                {
                    float sc = card.GetStunChance(lv) * (_boundTower != null ? _boundTower.GetStunChanceMultiplier() : 1f);
                    float sd = card.GetStunDuration(lv) * (_boundTower != null ? _boundTower.GetStunDurMultiplier() : 1f);
                    if (sc > 0f && sd > 0f)
                    {
                        if (sb.Length > 0) sb.Append("  |  ");
                        sb.Append($"Stun {sc * 100f:0.#}% {sd:0.#}s");
                    }
                }
                
                float cc = card.GetCritChance(lv);
                float cm = card.GetCritMultiplier(lv);
                if (cc > 0f && cm > 1f)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Crit {cc * 100f:0.#}% x{cm:0.##}");
                }
                
                if (card.HasTornadoOnHit())
                {
                    float tr = card.GetTornadoRadius(lv);
                    float ts = card.GetTornadoStrength(lv);
                    float td = card.GetTornadoDuration(lv);
                    int tmax = card.GetTornadoMaxPullTargets();
                    bool e = card.TornadoAllowsTier(ArenaDefinition.EnemyTier.Easy);
                    bool m = card.TornadoAllowsTier(ArenaDefinition.EnemyTier.Medium);
                    bool h = card.TornadoAllowsTier(ArenaDefinition.EnemyTier.Hard);
                    bool b = card.TornadoAllowsTier(ArenaDefinition.EnemyTier.Boss);
                    if (tr > 0f && ts > 0f && td > 0f)
                    {
                        if (sb.Length > 0) sb.Append("  |  ");
                        sb.Append($"Tornado R {tr:0.#} D {td:0.#}s S {ts:0.#} Max {tmax} [");
                        if (e) sb.Append('E');
                        if (m) sb.Append('M');
                        if (h) sb.Append('H');
                        if (b) sb.Append('B');
                        sb.Append(']');
                    }
                }
                
                if (card.HasChainOnHit())
                {
                    int jumps = card.GetChainMaxJumps(lv);
                    float fall = card.GetChainFalloffPerJump(lv);
                    if (jumps > 0 && fall > 0f)
                    {
                        if (sb.Length > 0) sb.Append("  |  ");
                        sb.Append($"Chain Jumps {jumps} Falloff {fall * 100f:0.#}%");
                    }
                }
                SetLine(effectText, sb.Length > 0 ? ("Effect: " + sb.ToString()) : null);
                RebuildLayout();
            }
        }

        public static void Hide()
        {
            var inst = Instance ?? FindFirstObjectByType<HoverCardDetailsUI>(FindObjectsInactive.Include);
            if (inst == null) return;
            inst.UnbindTower();
            inst.SetVisible(false);
        }

        private void SetData(CardDefinition card, int level)
        {
            if (card == null) return;
            int lv = Mathf.Max(1, level);
            if (icon) icon.sprite = card.Icon;
            if (rarityStripe && card.Rarity) rarityStripe.color = card.Rarity.Color;
            if (nameText) nameText.text = card.DisplayName;
            if (levelText) levelText.text = $"Lv {lv}";

            
            var cp = TR.Systems.PlayerProfile.GetOrCreateCard(card.CardId);
            bool undiscovered = cp != null && cp.ownedCount <= 0;
            if (undiscovered)
            {
                SetLine(dpsText, "DPS: ???");
                SetLine(fireRateText, "Fire Rate: ???");
                SetLine(rangeText, "Range: ???");
                SetLine(splashText, "Splash: ???");
                SetLine(costText, string.Empty);
                SetLine(effectText, null);
                RebuildLayout();
                return;
            }
            
            bool showNext = false; int nextLv = lv;
            if (s_inCollectionContext && cp != null)
            {
                var rarity = card.Rarity;
                int maxL = rarity != null ? rarity.MaxLevel : lv;
                int nlv = Mathf.Min(lv + 1, maxL);
                if (nlv > lv)
                {
                    int needed = rarity != null ? rarity.GetPointsRequiredForLevel(nlv) : int.MaxValue;
                    int cost = rarity != null ? rarity.GetUpgradeCostForLevel(nlv) : int.MaxValue;
                    showNext = (cp.points >= needed) && (PlayerProfile.GetSoftCurrency() >= cost);
                    if (showNext) nextLv = nlv;
                }
            }
            
            if (card is PulseCardDefinition pulseDef)
            {
                float dmg = pulseDef.GetPulseDamage(lv);
                float interval = pulseDef.GetPulseInterval(lv);
                float radius = pulseDef.GetPulseRadius(lv);
                if (showNext)
                {
                    float ndmg = pulseDef.GetPulseDamage(nextLv);
                    float nint = pulseDef.GetPulseInterval(nextLv);
                    float nrad = pulseDef.GetPulseRadius(nextLv);
                    SetLine(dpsText, $"Pulse Dmg: {dmg:0.#} -> {ndmg:0.#}");
                    SetLine(fireRateText, $"Interval: {interval:0.##}s -> {nint:0.##}s");
                    SetLine(rangeText, $"Pulse Radius: {radius:0.#} -> {nrad:0.#}");
                }
                else
                {
                    SetLine(dpsText, $"Pulse Dmg: {dmg:0.#}");
                    SetLine(fireRateText, $"Interval: {interval:0.##}s");
                    SetLine(rangeText, $"Pulse Radius: {radius:0.#}");
                }
                SetLine(splashText, null);
                { var cstats = card.GetStatsForLevel(lv); if (showNext) { var nstats = card.GetStatsForLevel(nextLv); SetLine(costText, $"Cost: {cstats.cost} -> {nstats.cost}"); } else SetLine(costText, $"Cost: {cstats.cost}"); }
                
                goto EffectSummaryPreview;
            }

            
            if (card is EconomyCardDefinition econ)
            {
                float income = econ.GetIncomePerSecond(lv);
                float decay = econ.GetDecayPerSecond(lv);
                float maxHp = econ.GetMaxHealth(lv);
                if (showNext)
                {
                    float nincome = econ.GetIncomePerSecond(nextLv);
                    float ndecay = econ.GetDecayPerSecond(nextLv);
                    float nmaxHp = econ.GetMaxHealth(nextLv);
                    SetLine(dpsText, $"Income/s: {income:0.#} -> {nincome:0.#}");
                    SetLine(fireRateText, $"Decay/s: {decay:0.#} -> {ndecay:0.#}");
                    SetLine(rangeText, $"Max HP: {maxHp:0.#} -> {nmaxHp:0.#}");
                }
                else
                {
                    SetLine(dpsText, $"Income/s: {income:0.#}");
                    SetLine(fireRateText, $"Decay/s: {decay:0.#}");
                    SetLine(rangeText, $"Max HP: {maxHp:0.#}");
                }
                SetLine(splashText, null);
                { var cstats = card.GetStatsForLevel(lv); if (showNext) { var nstats = card.GetStatsForLevel(nextLv); SetLine(costText, $"Cost: {cstats.cost} -> {nstats.cost}"); } else SetLine(costText, $"Cost: {cstats.cost}"); }
                SetLine(effectText, null);
                RebuildLayout();
                return;
            }

            
            if (card is InfernoCardDefinition inferno)
            {
                var statsInf = inferno.GetStatsForLevel(lv);
                int maxTargets = inferno.GetMaxTargets(lv);
                float rampUp = inferno.GetRampUpPerSecond(lv);
                float rampMax = inferno.GetRampMaxMultiplier(lv);
                float penalty = inferno.GetMultiTargetPenalty(lv);
                if (showNext)
                {
                    var nstats = inferno.GetStatsForLevel(nextLv);
                    int nmaxTargets = inferno.GetMaxTargets(nextLv);
                    float nrampUp = inferno.GetRampUpPerSecond(nextLv);
                    float nrampMax = inferno.GetRampMaxMultiplier(nextLv);
                    float npenalty = inferno.GetMultiTargetPenalty(nextLv);
                    SetLine(dpsText, $"Base DPS: {statsInf.dps:0.#} -> {nstats.dps:0.#}");
                    SetLine(fireRateText, $"Ramp: +{rampUp:0.##}/s -> +{nrampUp:0.##}/s to {rampMax:0.#}x -> {nrampMax:0.#}x");
                    SetLine(rangeText, $"Range: {statsInf.range:0.#} -> {nstats.range:0.#}");
                    SetLine(splashText, $"Max Targets: {maxTargets} -> {nmaxTargets} (pen {penalty:0.##} -> {npenalty:0.##})");
                    SetLine(costText, $"Cost: {statsInf.cost} -> {nstats.cost}");
                }
                else
                {
                    SetLine(dpsText, $"Base DPS: {statsInf.dps:0.#}");
                    SetLine(fireRateText, $"Ramp: +{rampUp:0.##}/s to {rampMax:0.#}x");
                    SetLine(rangeText, $"Range: {statsInf.range:0.#}");
                    SetLine(splashText, $"Max Targets: {maxTargets} (pen {penalty:0.##})");
                    SetLine(costText, $"Cost: {statsInf.cost}");
                }

                
                if (effectText)
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    float infBurnDps = card.GetBurnDps(lv);
                    float infBurnDur = card.GetBurnDuration(lv);
                    float infPoisonDps = card.GetPoisonDps(lv);
                    float infPoisonDur = card.GetPoisonDuration(lv);
                    if (infBurnDps > 0f && infBurnDur > 0f) sb.Append($"Burn {infBurnDps:0.#}/s {infBurnDur:0.#}s");
                    if (infPoisonDps > 0f && infPoisonDur > 0f)
                    {
                        if (sb.Length > 0) sb.Append("  |  ");
                        sb.Append($"Poison {infPoisonDps:0.#}/s {infPoisonDur:0.#}s");
                    }
                    if (card.HasSlowOnHit())
                    {
                        float sp = card.GetSlowPercent(lv);
                        float sd = card.GetSlowDuration(lv);
                        if (sp > 0f && sd > 0f)
                        {
                            if (sb.Length > 0) sb.Append("  |  ");
                            sb.Append($"Slow {sp * 100f:0.#}% {sd:0.#}s");
                        }
                    }
                    
                    if (card.HasStunOnHit())
                    {
                        float sc = card.GetStunChance(lv);
                        float sd = card.GetStunDuration(lv);
                        if (sc > 0f && sd > 0f)
                        {
                            if (sb.Length > 0) sb.Append("  |  ");
                            sb.Append($"Stun {sc * 100f:0.#}% {sd:0.#}s");
                        }
                    }
                    
                    float cc = card.GetCritChance(lv);
                    float cm = card.GetCritMultiplier(lv);
                    if (cc > 0f && cm > 1f)
                    {
                        if (sb.Length > 0) sb.Append("  |  ");
                        sb.Append($"Crit {cc * 100f:0.#}% x{cm:0.##}");
                    }
                    SetLine(effectText, sb.Length > 0 ? ("Effect: " + sb.ToString()) : null);
                }
                return;
            }

            var stats = card.GetStatsForLevel(lv);
            if (showNext)
            {
                var nstats = card.GetStatsForLevel(nextLv);
                SetLine(dpsText, $"DPS: {stats.dps:0.#} -> {nstats.dps:0.#}");
                SetLine(fireRateText, $"Fire Rate: {stats.fireRate:0.##}/s -> {nstats.fireRate:0.##}/s");
                SetLine(rangeText, $"Range: {stats.range:0.#} -> {nstats.range:0.#}");
                SetLine(splashText, stats.splashRadius > 0 || nstats.splashRadius > 0 ? $"Splash: {stats.splashRadius:0.#} -> {nstats.splashRadius:0.#}" : null);
                SetLine(costText, $"Cost: {stats.cost} -> {nstats.cost}");
            }
            else
            {
                SetLine(dpsText, $"DPS: {stats.dps:0.#}");
                SetLine(fireRateText, $"Fire Rate: {stats.fireRate:0.##}/s");
                SetLine(rangeText, $"Range: {stats.range:0.#}");
                SetLine(splashText, stats.splashRadius > 0 ? $"Splash: {stats.splashRadius:0.#}" : null);
                SetLine(costText, $"Cost: {stats.cost}");
            }

        EffectSummaryPreview:
            
            float burnDps = card.GetBurnDps(lv);
            float burnDur = card.GetBurnDuration(lv);
            float poisonDps = card.GetPoisonDps(lv);
            float poisonDur = card.GetPoisonDuration(lv);
            if (effectText)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                if (burnDps > 0f && burnDur > 0f) sb.Append($"Burn {burnDps:0.#}/s {burnDur:0.#}s");
                if (poisonDps > 0f && poisonDur > 0f)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Poison {poisonDps:0.#}/s {poisonDur:0.#}s");
                }
                if (card.HasSlowOnHit())
                {
                    float sp = card.GetSlowPercent(lv);
                    float sd = card.GetSlowDuration(lv);
                    if (sp > 0f && sd > 0f)
                    {
                        if (sb.Length > 0) sb.Append("  |  ");
                        sb.Append($"Slow {sp * 100f:0.#}% {sd:0.#}s");
                    }
                }
                
                if (card.HasSlowOnHit() && card.HasFrostbiteOnHit())
                {
                    float fbDps = card.GetFrostbiteDps(lv);
                    float fbDur = card.GetFrostbiteDuration(lv);
                    if (fbDps > 0f && fbDur > 0f)
                    {
                        if (sb.Length > 0) sb.Append("  |  ");
                        sb.Append($"Frostbite {fbDps:0.#}/s {fbDur:0.#}s");
                    }
                }
                
                if (card.HasStunOnHit())
                {
                    float sc = card.GetStunChance(lv);
                    float sd = card.GetStunDuration(lv);
                    if (sc > 0f && sd > 0f)
                    {
                        if (sb.Length > 0) sb.Append("  |  ");
                        sb.Append($"Stun {sc * 100f:0.#}% {sd:0.#}s");
                    }
                }
                
                float cc = card.GetCritChance(lv);
                float cm = card.GetCritMultiplier(lv);
                if (cc > 0f && cm > 1f)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Crit {cc * 100f:0.#}% x{cm:0.##}");
                }
                
                if (card.HasTornadoOnHit())
                {
                    float tr = card.GetTornadoRadius(lv);
                    float ts = card.GetTornadoStrength(lv);
                    float td = card.GetTornadoDuration(lv);
                    int tmax = card.GetTornadoMaxPullTargets();
                    bool e = card.TornadoAllowsTier(ArenaDefinition.EnemyTier.Easy);
                    bool m = card.TornadoAllowsTier(ArenaDefinition.EnemyTier.Medium);
                    bool h = card.TornadoAllowsTier(ArenaDefinition.EnemyTier.Hard);
                    bool b = card.TornadoAllowsTier(ArenaDefinition.EnemyTier.Boss);
                    if (tr > 0f && ts > 0f && td > 0f)
                    {
                        if (sb.Length > 0) sb.Append("  |  ");
                        sb.Append($"Tornado R {tr:0.#} D {td:0.#}s S {ts:0.#} Max {tmax} [");
                        if (e) sb.Append('E');
                        if (m) sb.Append('M');
                        if (h) sb.Append('H');
                        if (b) sb.Append('B');
                        sb.Append(']');
                    }
                }
                
                if (card.HasChainOnHit())
                {
                    int jumps = card.GetChainMaxJumps(lv);
                    float fall = card.GetChainFalloffPerJump(lv);
                    if (jumps > 0 && fall > 0f)
                    {
                        if (sb.Length > 0) sb.Append("  |  ");
                        sb.Append($"Chain Jumps {jumps} Falloff {fall * 100f:0.#}%");
                    }
                }
                SetLine(effectText, sb.Length > 0 ? ("Effect: " + sb.ToString()) : null);
                RebuildLayout();
            }
        }

        private void SetVisible(bool show)
        {
            if (panel == null) return;
            if (_visible == show) return;
            _visible = show;
            if (show && root != null && !root.activeSelf) root.SetActive(true);
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(AnimatePanel(show));
        }

        private IEnumerator AnimatePanel(bool show)
        {
            float from = panel.anchoredPosition.x;
            float to = show ? showX : hiddenX;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, animDuration);
                float e = EaseOut(t);
                var p = panel.anchoredPosition;
                p.x = Mathf.Lerp(from, to, e);
                panel.anchoredPosition = p;
                yield return null;
            }
            _anim = null;
            if (!show && root != null) root.SetActive(false);
        }
    }
}
