using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Data;
using TR.Systems;
using System.Collections;

namespace TR.UI
{
    // A simple panel that slides in to show card details on hover.
    public class HoverCardDetailsUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject root; // root GameObject for the panel window
        [SerializeField] private RectTransform panel; // anchored to right or left side of canvas
        [SerializeField] private float showX = -40f;  // anchoredPosition.x when visible (e.g., slightly inside)
        [SerializeField] private float hiddenX = -420f; // anchoredPosition.x when hidden (e.g., off-screen)
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
        [SerializeField] private TMP_Text effectText; // shows Burn or Poison if present, otherwise '-'

        public static HoverCardDetailsUI Instance { get; private set; }

        private Coroutine _anim;
        private bool _visible;
        private float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        private TR.Battle.TowerBase _boundTower;
        // Small helpers to drive VerticalLayoutGroup-friendly toggling
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
            // Unbind any previous tower; this path is card-only (e.g., collection hover)
            inst.UnbindTower();
            // Buff cards: show aura stats and full buff breakdown (same as placed tower view)
            if (card is BuffCardDefinition buff)
            {
                int lvb = Mathf.Max(1, level);
                // Header
                if (inst.icon) inst.icon.sprite = card.Icon;
                if (inst.rarityStripe && card.Rarity) inst.rarityStripe.color = card.Rarity.Color;
                if (inst.nameText) inst.nameText.text = card.DisplayName;
                if (inst.levelText) inst.levelText.text = $"Lv {lvb}";
                float auraRange = buff.GetBuffRange(lvb);
                inst.SetLine(inst.dpsText, $"Aura Range: {auraRange:0.#}");

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                if (buff.BuffDps) sb.Append($"DPS +{buff.GetDpsPercent(lvb) * 100f:0.#}%");
                if (buff.BuffFireRate)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"FireRate +{buff.GetFireRatePercent(lvb) * 100f:0.#}%");
                }
                if (buff.BuffRange)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Range +{buff.GetRangePercent(lvb) * 100f:0.#}%");
                }
                if (buff.BuffSplash)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Splash +{buff.GetSplashPercent(lvb) * 100f:0.#}%");
                }
                if (buff.BuffBurn)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Burn Dmg +{buff.GetBurnDpsBuffPercent(lvb) * 100f:0.#}% Dur +{buff.GetBurnDurBuffPercent(lvb) * 100f:0.#}%");
                }
                if (buff.BuffPoison)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Poison Dmg +{buff.GetPoisonDpsBuffPercent(lvb) * 100f:0.#}% Dur +{buff.GetPoisonDurBuffPercent(lvb) * 100f:0.#}%");
                }
                if (buff.BuffSlow)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Slow % +{buff.GetSlowPercentBuffPercent(lvb) * 100f:0.#}% Dur +{buff.GetSlowDurBuffPercent(lvb) * 100f:0.#}%");
                }
                if (buff.BuffStun)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Stun % +{buff.GetStunChanceBuffPercent(lvb) * 100f:0.#}% Dur +{buff.GetStunDurBuffPercent(lvb) * 100f:0.#}%");
                }
                // Economy income buff (new)
                if (buff.BuffEconomyIncome)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Econ Income +{buff.GetEconomyIncomePercent(lvb) * 100f:0.#}%");
                }
                inst.SetLine(inst.fireRateText, sb.Length > 0 ? sb.ToString() : null);
                // Rarity filter display
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
                { var cstats = card.GetStatsForLevel(lvb); inst.SetLine(inst.costText, $"Cost: {cstats.cost}"); }
                inst.SetLine(inst.effectText, "Effect: Buff Aura");
                inst.RebuildLayout();
                inst.SetVisible(true);
                return;
            }
            inst.SetData(card, level);
            inst.SetVisible(true);
        }

        // Bind to a specific placed tower so stats reflect live multipliers
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

            // Economy towers keep their special display
            if (card is EconomyCardDefinition econ)
            {
                float baseIncome = econ.GetIncomePerSecond(lv);
                // If we have a runtime EconomyTower, show effective income and buff percent
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

            // Buff towers (placed): show aura stats and buff breakdown like card preview
            if (card is BuffCardDefinition buff)
            {
                // Header already set above
                float auraRange = buff.GetBuffRange(lv);
                SetLine(dpsText, $"Aura Range: {auraRange:0.#}");
                // Build a concise buff summary line
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
                // On-hit effect buffs
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
                // Rarity filter display
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

            // Pulse towers show pulse stats
            if (card is PulseCardDefinition pulseDefBound)
            {
                // Effective values with buffs
                float dmg = pulseDefBound.GetPulseDamage(lv) * (_boundTower != null ? _boundTower.GetDpsMultiplier() : 1f);
                float interval = pulseDefBound.GetPulseInterval(lv) / Mathf.Max(0.01f, (_boundTower != null ? _boundTower.GetFireRateMultiplier() : 1f));
                float radius = _boundTower.GetEffectiveRange();
                SetLine(dpsText, $"Pulse Dmg: {dmg:0.#}");
                SetLine(fireRateText, $"Interval: {interval:0.##}s");
                SetLine(rangeText, $"Pulse Radius: {radius:0.#}");
                SetLine(splashText, null);
                { var cstats = card.GetStatsForLevel(lv); SetLine(costText, $"Cost: {cstats.cost}"); }
                // Effects summary remains as below (burn/poison/slow/stun/crit/etc.)
                goto EffectsSummary;
            }

            // Inferno towers show continuous-beam stats with multipliers
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
                // Regular towers use effective stats
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
            // Effects summary from card (show EFFECTIVE values with buffs)
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
                // Frostbite (not buffed): only shown if Slow is enabled, and gated by threshold slow percent
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
                // Stun
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
                // Crit (from card, not affected by buffs)
                float cc = card.GetCritChance(lv);
                float cm = card.GetCritMultiplier(lv);
                if (cc > 0f && cm > 1f)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Crit {cc * 100f:0.#}% x{cm:0.##}");
                }
                // Tornado (card-driven; not affected by buffs)
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
                // Chain (level-aware)
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

            // Undiscovered handling: if player has not obtained this card yet, hide stats behind ???
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

            // Pulse cards (preview): show pulse stats
            if (card is PulseCardDefinition pulseDef)
            {
                float dmg = pulseDef.GetPulseDamage(lv);
                float interval = pulseDef.GetPulseInterval(lv);
                float radius = pulseDef.GetPulseRadius(lv);
                SetLine(dpsText, $"Pulse Dmg: {dmg:0.#}");
                SetLine(fireRateText, $"Interval: {interval:0.##}s");
                SetLine(rangeText, $"Pulse Radius: {radius:0.#}");
                SetLine(splashText, null);
                { var cstats = card.GetStatsForLevel(lv); SetLine(costText, $"Cost: {cstats.cost}"); }
                // Keep effect summary below (card-level values)
                goto EffectSummaryPreview;
            }

            // Economy cards show different stats; reuse existing text fields to avoid prefab changes
            if (card is EconomyCardDefinition econ)
            {
                float income = econ.GetIncomePerSecond(lv);
                float decay = econ.GetDecayPerSecond(lv);
                float maxHp = econ.GetMaxHealth(lv);
                SetLine(dpsText, $"Income/s: {income:0.#}");
                SetLine(fireRateText, $"Decay/s: {decay:0.#}");
                SetLine(rangeText, $"Max HP: {maxHp:0.#}");
                SetLine(splashText, null);
                { var cstats = card.GetStatsForLevel(lv); SetLine(costText, $"Cost: {cstats.cost}"); }
                SetLine(effectText, null);
                RebuildLayout();
                return;
            }

            // Inferno cards: override to show continuous-beam stats
            if (card is InfernoCardDefinition inferno)
            {
                var statsInf = inferno.GetStatsForLevel(lv);
                int maxTargets = inferno.GetMaxTargets(lv);
                float rampUp = inferno.GetRampUpPerSecond(lv);
                float rampMax = inferno.GetRampMaxMultiplier(lv);
                float penalty = inferno.GetMultiTargetPenalty(lv);

                SetLine(dpsText, $"Base DPS: {statsInf.dps:0.#}");
                SetLine(fireRateText, $"Ramp: +{rampUp:0.##}/s to {rampMax:0.#}x");
                SetLine(rangeText, $"Range: {statsInf.range:0.#}");
                SetLine(splashText, $"Max Targets: {maxTargets} (pen {penalty:0.##})");
                SetLine(costText, $"Cost: {statsInf.cost}");

                // Keep on-hit effect summary if any (Burn/Poison/Slow)
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
                    // Stun
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
                    // Crit
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
            SetLine(dpsText, $"DPS: {stats.dps:0.#}");
            SetLine(fireRateText, $"Fire Rate: {stats.fireRate:0.##}/s");
            SetLine(rangeText, $"Range: {stats.range:0.#}");
            SetLine(splashText, stats.splashRadius > 0 ? $"Splash: {stats.splashRadius:0.#}" : null);
            SetLine(costText, $"Cost: {stats.cost}");

        EffectSummaryPreview:
            // On-hit effect summary (Burn/Poison/Slow, multiple if present)
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
                // Frostbite (not buffed): only shown if Slow is enabled
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
                // Stun
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
                // Crit
                float cc = card.GetCritChance(lv);
                float cm = card.GetCritMultiplier(lv);
                if (cc > 0f && cm > 1f)
                {
                    if (sb.Length > 0) sb.Append("  |  ");
                    sb.Append($"Crit {cc * 100f:0.#}% x{cm:0.##}");
                }
                // Tornado (if enabled)
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
                // Chain stats (level-aware)
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
