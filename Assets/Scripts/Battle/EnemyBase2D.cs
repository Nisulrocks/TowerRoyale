using UnityEngine;
using UnityEngine.EventSystems;
using Photon.Pun;
using TR.Data;
using TR.UI;
using TR.VFX;
using TR.Audio;
using TR.Net;

namespace TR.Battle
{
    
    public class EnemyBase2D : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public enum SpriteFacing { Down, Right, Up, Left }

        private static readonly System.Collections.Generic.HashSet<EnemyBase2D> s_all = new();
        public static System.Collections.Generic.IReadOnlyCollection<EnemyBase2D> All => s_all;
        [Header("Runtime")]
        [SerializeField] private EnemyDefinition definition;
        [SerializeField] private float currentHealth;
        [SerializeField] private float moveSpeed;
        [SerializeField] private int waypointIndex;
        [SerializeField] private Path2D path;
        [SerializeField] private float reachThreshold = 0.02f;
        
        [SerializeField] private float _runtimeMaxHealth; 
        [SerializeField] private float _bossDamageMult = 1f;
        
        private Animator _animator;
        private string _runBoolParam;
        private string _attackBoolParam;
        private string _dieTriggerParam;
        private string _speedFloatParam;
        
        [Header("Facing")]
        [SerializeField] private bool defaultFacingRight = true;
        [SerializeField] private bool rotateToMovement = true;
        [SerializeField] private SpriteFacing defaultSpriteFacing = SpriteFacing.Down;
        [SerializeField] private float rotateSpeedDegPerSec = 720f;

        private Transform _visualRoot;
        private Vector3 _visualBaseScale = Vector3.one;
        private Quaternion _visualBaseRotation = Quaternion.identity;
        private Vector3 _visualBasePosition = Vector3.zero;
        private Vector2 _desiredLookDirection;
        private Vector3 _lastVisualPosition;

        [Header("Visual Bobbing")]
        [SerializeField] private bool bobWhileMoving = true;
        [SerializeField] private float bobFrequency = 6f;
        [SerializeField] private float bobAmplitude = 0.05f;
        private float _bobTimer;

        [Header("Auto-Config")]
        [SerializeField] private bool autoFindPath = true; 

        [Header("UI (Optional)")]
        [SerializeField] private GameObject healthBarUIPrefab; 
        [SerializeField] private Canvas healthBarCanvas;        
        [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 0.6f, 0f);
        private GameObject _healthBarInstance;

        [Header("Hover Outline")]
        [SerializeField] private bool useHoverOutline = true;
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private Color outlineColor = Color.white;
        [SerializeField] private float outlineThickness = 2f;
        private Material _outlineMaterialInstance;
        private Material _cachedOriginalMaterial;
        private SpriteRenderer _cachedVisualRenderer;
        private bool _hovered;

        [Header("Boss UI (Screen Space)")]
        [SerializeField] private BossHealthUI bossHealthUIPrefab; 
        [SerializeField] private GameObject bossHealthUIPrefabGO; 
        private BossHealthUI _bossUIInstance;
        private ArenaDefinition _arena;
        private Transform _castleTr; 

        [Header("VFX")]

        [SerializeField] private string deathVfxKey = "";

        [SerializeField] private Transform deathVfxAnchor;
         [SerializeField] private Transform statusVfxAnchor;
[SerializeField] private string burnTickVfxKey = "";
         [SerializeField] private float burnTickVfxInterval = 0.3f;
[SerializeField] private string poisonTickVfxKey = "";
         [SerializeField] private float poisonTickVfxInterval = 0.3f;
[SerializeField] private string slowTickVfxKey = "";
         [SerializeField] private float slowTickVfxInterval = 0.35f;
         [SerializeField] private float regenVfxInterval = 0.5f;
        [SerializeField] private string hitVfxKey = "";
[SerializeField]
        private Transform hitVfxAnchor;
        [SerializeField] private float hitVfxInterval = 0.12f;
        private float _nextHitVfxTime = 0f;
        [SerializeField] private float statusHitVfxInterval = 0.45f;
        private float _nextStatusHitVfxTime = 0f;
        [Header("SFX")]
        [SerializeField] private string deathSfxKey = "enemy_death";

        
        private float _lastDamageTime;
        private float _totalRegenThisLife;

        
        private float _pulseNukeNextTime;      
        private float _pulseNukeCheckTimer;    
        
        private float _stunPulseNextTime;
        private float _stunPulseCheckTimer;

        private int _waveNumber;

        public EnemyDefinition Definition => definition;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => _runtimeMaxHealth > 0f ? _runtimeMaxHealth : (definition != null ? definition.MaxHealth : Mathf.Max(currentHealth, 1f));
        public int WaveNumber => _waveNumber;
        public float HealthMultiplier { get; private set; } = 1f;
        public float DamageMultiplier { get; private set; } = 1f;
        public float SpeedMultiplier { get; private set; } = 1f;
        public System.Action<float, float> OnHealthChanged; 

        private TR.Net.EnemyNetSync _netSync;

        public void SetWaveNumber(int wave) => _waveNumber = wave;
        public int WaypointIndex => waypointIndex;
        public void SetWaypointIndex(int index) => waypointIndex = Mathf.Max(0, index);

        public void RecalculateWaypointFromPosition()
        {
            if (path == null || path.Waypoints == null || path.Waypoints.Length == 0) return;
            var wps = path.Waypoints;
            int count = wps.Length;
            int best = 0;
            bool found = false;
            for (int i = 0; i < count - 1; i++)
            {
                if (wps[i] == null || wps[i + 1] == null) continue;
                Vector3 a = wps[i].position;
                Vector3 b = wps[i + 1].position;
                Vector3 ab = b - a;
                Vector3 ap = transform.position - a;
                float lenSq = ab.sqrMagnitude;
                if (lenSq < 1e-10f) continue;
                float t = Vector3.Dot(ap, ab) / lenSq;
                if (t < -0.001f)
                {
                    best = i;
                    found = true;
                    break;
                }
                if (t <= 1f)
                {
                    best = i + 1;
                    found = true;
                    break;
                }
                best = i + 2;
            }
            if (!found) best = count;
            waypointIndex = Mathf.Clamp(best, 0, count);
        }

        
        
        private int _pendingDamagerActor = 0;
        private int _lastDamagerActor = 0;

        
        
        public void SetIncomingDamager(int actorNumber)
        {
            _pendingDamagerActor = actorNumber;
        }

        private void Awake()
        {
            
            _netSync = GetComponent<TR.Net.EnemyNetSync>();
            
            if (path == null && autoFindPath)
            {
                path = FindFirstObjectByType<Path2D>(FindObjectsInactive.Include);
            }
            
            if (definition != null && currentHealth <= 0f)
            {
                ApplyDefinition(definition);
                OnHealthChanged?.Invoke(currentHealth, MaxHealth);
                SnapVisualToInitialFacing();
            }
        }

        private void TryTickPulseNuke(float dt)
        {
            if (definition == null || !definition.UsePulseNukeAbility) return;
            if (currentHealth <= 0f) return;
            
            if (Time.time < _pulseNukeNextTime) return;
            
            _pulseNukeCheckTimer += Mathf.Max(0f, dt);
            float interval = definition.PulseNukeRandomCheckInterval;
            if (_pulseNukeCheckTimer < interval) return;
            _pulseNukeCheckTimer = 0f;
            
            if (Random.value <= definition.PulseNukeTriggerChance)
            {
                DoPulseNuke();
                
                float min = definition.PulseNukeCooldownMin;
                float max = definition.PulseNukeCooldownMax;
                if (max < min) max = min;
                _pulseNukeNextTime = Time.time + Random.Range(min, max);
            }
        }

        private static readonly System.Collections.Generic.List<TowerBase> s_towerSnapshot = new System.Collections.Generic.List<TowerBase>(64);

        public const int AbilityPulseNuke = 0;
        public const int AbilityStunPulse = 1;

        public void PlayAbilityPulseFeedback(int kind)
        {
            if (definition == null) return;
            Vector3 origin = transform.position;

            if (kind == AbilityPulseNuke)
            {
                string vfxKey = definition.PulseNukeVfxKey;
                if (!string.IsNullOrEmpty(vfxKey))
                {
                    TR.VFX.ParticleManager.SpawnOneShot(vfxKey, origin);
                }
                else
                {
                    TR.VFX.PulseRipple.Spawn(origin,
                                             Mathf.Max(0f, definition.PulseNukeRadius),
                                             definition.PulseNukeRippleColor,
                                             definition.PulseNukeRippleDuration,
                                             definition.PulseNukeRippleLineWidth,
                                             definition.PulseNukeRippleSegments);
                }

                string sfx = definition.PulseNukeSfxKey;
                if (!string.IsNullOrEmpty(sfx) && TR.Audio.SFXManager.Instance != null)
                {
                    TR.Audio.SFXManager.Instance.Play(sfx);
                }
            }
            else if (kind == AbilityStunPulse)
            {
                string vfxKey = definition.StunPulseVfxKey;
                if (!string.IsNullOrEmpty(vfxKey))
                {
                    TR.VFX.ParticleManager.SpawnOneShot(vfxKey, origin);
                }
                else
                {
                    TR.VFX.PulseRipple.Spawn(origin,
                                             Mathf.Max(0f, definition.StunPulseRadius),
                                             definition.StunPulseRippleColor,
                                             definition.StunPulseRippleDuration,
                                             definition.StunPulseRippleLineWidth,
                                             definition.StunPulseRippleSegments);
                }

                string sfx = definition.StunPulseSfxKey;
                if (!string.IsNullOrEmpty(sfx) && TR.Audio.SFXManager.Instance != null)
                {
                    TR.Audio.SFXManager.Instance.Play(sfx);
                }
            }
        }

        private void PlayAndBroadcastAbilityPulse(int kind)
        {
            PlayAbilityPulseFeedback(kind);
            if (DuoRuntime.IsDuo && DuoRuntime.IsSimulationAuthority)
            {
                _netSync?.BroadcastAbilityPulse(kind);
            }
        }

        private void DoPulseNuke()
        {
            float radius = Mathf.Max(0f, definition.PulseNukeRadius);
            Vector3 origin = transform.position;

            PlayAndBroadcastAbilityPulse(AbilityPulseNuke);

            s_towerSnapshot.Clear();
            foreach (var t in TowerBase.All) s_towerSnapshot.Add((TowerBase)t);
            for (int i = 0; i < s_towerSnapshot.Count; i++)
            {
                var tb = s_towerSnapshot[i];
                if (tb == null || !tb.gameObject.activeInHierarchy) continue;
                float d = Vector2.Distance((Vector2)origin, (Vector2)tb.transform.position);
                if (d <= radius)
                {
                    tb.PlayDestroyFeedback();
                    Destroy(tb.gameObject);
                }
            }

            #if UNITY_EDITOR
            DebugDrawCircle(origin, radius, new Color(1f, 0.6f, 0.9f, 0.6f), 0.25f);
            #endif
        }

        private void TryTickStunPulse(float dt)
        {
            if (definition == null || !definition.UseStunPulseAbility) return;
            if (currentHealth <= 0f) return;
            if (Time.time < _stunPulseNextTime) return;
            _stunPulseCheckTimer += Mathf.Max(0f, dt);
            float interval = definition.StunPulseRandomCheckInterval;
            if (_stunPulseCheckTimer < interval) return;
            _stunPulseCheckTimer = 0f;
            if (Random.value <= definition.StunPulseTriggerChance)
            {
                DoStunPulse();
                float smin = definition.StunPulseCooldownMin;
                float smax = definition.StunPulseCooldownMax;
                if (smax < smin) smax = smin;
                _stunPulseNextTime = Time.time + Random.Range(smin, smax);
            }
        }

        private static readonly System.Collections.Generic.List<TowerBase> s_towerSnapshot2 = new System.Collections.Generic.List<TowerBase>(64);
        private void DoStunPulse()
        {
            float radius = Mathf.Max(0f, definition.StunPulseRadius);
            Vector3 origin = transform.position;

            PlayAndBroadcastAbilityPulse(AbilityStunPulse);

            float dur = Mathf.Max(0f, definition.StunPulseDuration);
            s_towerSnapshot2.Clear();
            foreach (var t in TowerBase.All) s_towerSnapshot2.Add((TowerBase)t);
            for (int i = 0; i < s_towerSnapshot2.Count; i++)
            {
                var tb = s_towerSnapshot2[i];
                if (tb == null || !tb.gameObject.activeInHierarchy) continue;
                float d = Vector2.Distance((Vector2)origin, (Vector2)tb.transform.position);
                if (d <= radius)
                {
                    tb.ApplyTowerStun(dur);
                }
            }
            #if UNITY_EDITOR
            DebugDrawCircle(origin, radius, new Color(0.7f, 0.85f, 1f, 0.6f), 0.25f);
            #endif
        }

        private float GetEffectiveMoveSpeed()
        {
            float baseSpeed = moveSpeed; 
            float mult = 1f - Mathf.Clamp01(_slowPercent);
            return Mathf.Max(0f, baseSpeed * mult);
        }

        
        private void SetAnimRunning(bool value)
        {
            if (_animator == null || string.IsNullOrEmpty(_runBoolParam)) return;
            _animator.SetBool(_runBoolParam, value);
        }
        private void SetAnimAttacking(bool value)
        {
            if (_animator == null || string.IsNullOrEmpty(_attackBoolParam)) return;
            _animator.SetBool(_attackBoolParam, value);
        }
        private void SetAnimSpeed(float value)
        {
            if (_animator == null || string.IsNullOrEmpty(_speedFloatParam)) return;
            _animator.SetFloat(_speedFloatParam, value);
        }

        private void UpdateFacing(float moveX)
        {
            if (rotateToMovement || _visualRoot == null) return;
            if (Mathf.Abs(moveX) < 1e-4f) return;
            bool wantRight = moveX >= 0f;
            bool faceRight = defaultFacingRight ? wantRight : !wantRight;
            Vector3 s = _visualBaseScale;
            float sx = Mathf.Abs(s.x) * (faceRight ? 1f : -1f);
            if (!Mathf.Approximately(_visualRoot.localScale.x, sx))
            {
                _visualRoot.localScale = new Vector3(sx, s.y, s.z);
            }
        }

        
        private void DebugDrawCircle(Vector3 center, float radius, Color color, float duration)
        {
            const int seg = 20;
            Vector3 prev = center + Vector3.right * radius;
            for (int i = 1; i <= seg; i++)
            {
                float ang = i * Mathf.PI * 2f / seg;
                Vector3 next = center + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * radius;
                Debug.DrawLine(prev, next, color, duration);
                prev = next;
            }
        }

        private void OnEnable()
        {
            s_all.Add(this);
            
            TrySpawnHealthBarUI();
        }

        private void OnDisable()
        {
            s_all.Remove(this);


            CastleSiegeRing.Release(this);
            _siegeSlotClaimed = false;
            _siegeSlot = default;

            if (_healthBarInstance != null)
            {
                Destroy(_healthBarInstance);
                _healthBarInstance = null;
            }
            
            if (IsBoss())
            {
                if (_bossUIInstance != null)
                {
                    _bossUIInstance.UnbindIfTarget(this);
                    Destroy(_bossUIInstance.gameObject);
                    _bossUIInstance = null;
                }
            }

            SetHovered(false);
        }

        private void OnMouseEnter() => SetHovered(true);
        private void OnMouseExit() => SetHovered(false);

        public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);
        public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

        public void SetHovered(bool hovered)
        {
            if (_hovered == hovered) return;
            _hovered = hovered;

            if (useHoverOutline || IsBoss()) SetOutline(hovered);

            var ui = _healthBarInstance != null ? _healthBarInstance.GetComponent<EnemyHealthBarUI>() : null;
            ui?.SetHover(hovered);
        }

        public void SetOutline(bool active)
        {
            if (_visualRoot == null) return;
            if (_cachedVisualRenderer == null)
                _cachedVisualRenderer = _visualRoot.GetComponentInChildren<SpriteRenderer>(true);
            if (_cachedVisualRenderer == null) return;

            if (_outlineMaterialInstance == null)
            {
                Material source = outlineMaterial;
                if (source == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite Outline");
                    if (shader != null) source = new Material(shader);
                }
                if (source == null) return;

                _outlineMaterialInstance = new Material(source);
            }

            if (active)
            {
                if (_cachedOriginalMaterial == null)
                    _cachedOriginalMaterial = _cachedVisualRenderer.sharedMaterial;

                _outlineMaterialInstance.SetColor("_OutlineColor", outlineColor);
                _outlineMaterialInstance.SetFloat("_OutlineThickness", outlineThickness);
                _outlineMaterialInstance.SetFloat("_OutlineEnabled", 1f);
                _cachedVisualRenderer.sharedMaterial = _outlineMaterialInstance;
            }
            else
            {
                if (_cachedOriginalMaterial != null)
                {
                    _cachedVisualRenderer.sharedMaterial = _cachedOriginalMaterial;
                }
                else
                {
                    _outlineMaterialInstance.SetFloat("_OutlineEnabled", 0f);
                }
            }
        }

        public void Initialize(EnemyDefinition def, Path2D followPath)
        {
            definition = def;
            path = followPath != null ? followPath : (autoFindPath ? FindFirstObjectByType<Path2D>(FindObjectsInactive.Include) : null);
            ApplyDefinition(definition);
            SnapVisualToInitialFacing();
        }

        public void SetDefinition(EnemyDefinition def)
        {
            definition = def;
            ApplyDefinition(definition);
            SnapVisualToInitialFacing();
        }

        public void SetArena(ArenaDefinition arena)
        {
            _arena = arena;
            
            if (IsBoss())
            {
                
                if (_healthBarInstance != null)
                {
                    Destroy(_healthBarInstance);
                    _healthBarInstance = null;
                }
                
                if (_bossUIInstance == null && (bossHealthUIPrefab != null || bossHealthUIPrefabGO != null))
                {
                    Transform parent = null;
                    
                    bool prefabHasCanvas = false;
                    if (bossHealthUIPrefab != null)
                    {
                        prefabHasCanvas = bossHealthUIPrefab.GetComponentInParent<Canvas>() != null;
                    }
                    else if (bossHealthUIPrefabGO != null)
                    {
                        prefabHasCanvas = bossHealthUIPrefabGO.GetComponentInChildren<Canvas>(true) != null;
                    }
                    if (!prefabHasCanvas)
                    {
                        var overlay = FindOverlayCanvas();
                        parent = overlay != null ? overlay.transform : null;
                    }

                    if (bossHealthUIPrefab != null)
                    {
                        _bossUIInstance = Instantiate(bossHealthUIPrefab, parent);
                    }
                    else
                    {
                        var go = Instantiate(bossHealthUIPrefabGO, parent);
                        _bossUIInstance = go.GetComponentInChildren<BossHealthUI>(true);
                        if (_bossUIInstance == null)
                        {
                            Debug.LogError("[EnemyBase2D] Boss UI prefab (GameObject) is missing BossHealthUI component in children.");
                        }
                    }
                }
                if (_bossUIInstance != null)
                {
                    string name = definition != null ? (string.IsNullOrEmpty(definition.DisplayName) ? definition.name : definition.DisplayName) : "Boss";
                    _bossUIInstance.Bind(this, name);
                }
            }
        }

        private void ApplyDefinition(EnemyDefinition def)
        {
            HealthMultiplier = 1f;
            DamageMultiplier = 1f;
            SpeedMultiplier = 1f;

            _runtimeMaxHealth = def != null ? Mathf.Max(1f, def.MaxHealth) : 10f;
            currentHealth = _runtimeMaxHealth;
            moveSpeed = Mathf.Max(0f, def != null ? def.MovementSpeed : 1.5f);
            waypointIndex = 0;
            _bossDamageMult = 1f;

            EnsureVisualRoot();
            
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null) _animator = GetComponent<Animator>();
            _runBoolParam = def != null ? def.RunBoolParam : null;
            _attackBoolParam = def != null ? def.AttackBoolParam : null;
            _dieTriggerParam = def != null ? def.DieTriggerParam : null;
            _speedFloatParam = def != null ? def.SpeedFloatParam : null;
            if (_animator == null && def != null && def.AnimatorController != null)
            {
                if (_visualRoot == null) EnsureVisualRoot();
                var host = _visualRoot != null ? _visualRoot.gameObject : this.gameObject;
                _animator = host.GetComponent<Animator>();
                if (_animator == null)
                {
                    _animator = host.AddComponent<Animator>();
                }
            }
            if (_animator != null && def != null && def.AnimatorController != null)
            {
                _animator.runtimeAnimatorController = def.AnimatorController;
                _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
            
            if (_visualRoot == null) EnsureVisualRoot();
            if (_visualRoot == null)
            {
                var sr = GetComponentInChildren<SpriteRenderer>(true);
                _visualRoot = sr != null ? sr.transform : this.transform;
            }
            _visualBaseScale = _visualRoot != null ? _visualRoot.localScale : Vector3.one;
            _visualBaseRotation = _visualRoot != null ? _visualRoot.localRotation : Quaternion.identity;
            _visualBasePosition = _visualRoot != null ? _visualRoot.localPosition : Vector3.zero;
            _lastVisualPosition = transform.position;

            var healthBarUI = _healthBarInstance != null ? _healthBarInstance.GetComponent<EnemyHealthBarUI>() : null;
            healthBarUI?.SetInfo(def);

            SetAnimRunning(false);
            SetAnimAttacking(false);
            SetAnimSpeed(0f);
            
            _lastDamageTime = Time.time;
            _totalRegenThisLife = 0f;
            
            if (def != null && def.UsePulseNukeAbility)
            {
                float min = def.PulseNukeCooldownMin;
                float max = def.PulseNukeCooldownMax;
                if (max < min) max = min;
                _pulseNukeNextTime = Time.time + Random.Range(min, max);
            }
            else
            {
                _pulseNukeNextTime = float.PositiveInfinity;
            }
            _pulseNukeCheckTimer = 0f;
            
            if (def != null && def.UseStunPulseAbility)
            {
                float smin = def.StunPulseCooldownMin;
                float smax = def.StunPulseCooldownMax;
                if (smax < smin) smax = smin;
                _stunPulseNextTime = Time.time + Random.Range(smin, smax);
            }
            else
            {
                _stunPulseNextTime = float.PositiveInfinity;
            }
            _stunPulseCheckTimer = 0f;
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        [Header("Attack")]
        [SerializeField] private float attackInterval = 1.0f; 
        private float _attackTimer;

        
        private float _burnDps;      
        private float _burnTime;     
        private float _poisonDps;    
        private float _poisonTime;   
        
        private float _frostbiteDps;   
        private float _frostbiteTime;  
        private float _burnVfxTimer; 
        private float _poisonVfxTimer; 
        private float _frostbiteVfxTimer; 
        private float _slowVfxTimer;   
        private float _regenVfxTimer;   
        
        private float _slowPercent;   
        private float _slowTime;      
        
        private float _stunTime;      
[SerializeField] private string stunTickVfxKey = "";
         [SerializeField] private float stunTickVfxInterval = 0.35f;
        private float _stunVfxTimer;

        private void Update()
        {


            if (!DuoRuntime.IsSimulationAuthority) return;

            TickStatusEffects();

            TryTickPulseNuke(Time.deltaTime);
            TryTickStunPulse(Time.deltaTime);

            if (_castleTr == null)
            {
                var castle = FindFirstObjectByType<BaseCastle>(FindObjectsInactive.Include);
                if (castle != null) _castleTr = castle.transform;
            }

            float baseDirX = 0f;
            Vector2 fallbackDir = Vector2.zero;
            if (_castleTr != null)
            {
                Vector3 toBase = _castleTr.position - transform.position;
                baseDirX = Mathf.Sign(toBase.x);
                if (Mathf.Abs(toBase.x) < 1e-3f) baseDirX = 0f;
                if (toBase.sqrMagnitude > 1e-6f)
                    fallbackDir = new Vector2(toBase.x, toBase.y).normalized;
            }

            if (_stunTime > 0f)
            {
                SetAnimRunning(false);
                SetAnimAttacking(false);
                SetAnimSpeed(0f);
                _desiredLookDirection = fallbackDir;
                if (!rotateToMovement && baseDirX != 0f) UpdateFacing(baseDirX);
                return;
            }
            if (path == null || path.Waypoints == null || path.Waypoints.Length == 0)
            {

                transform.position += Vector3.right * GetEffectiveMoveSpeed() * Time.deltaTime;
                SetAnimRunning(true);
                SetAnimAttacking(false);
                SetAnimSpeed(GetEffectiveMoveSpeed());
                _desiredLookDirection = Vector2.right;
                if (!rotateToMovement) UpdateFacing(baseDirX != 0f ? baseDirX : 1f);
                return;
            }

            var wps = path.Waypoints;
            if (waypointIndex >= wps.Length)
            {



                if (_castleTr != null && TickSiegeApproach(_castleTr.position, baseDirX))
                    return;

                SetAnimRunning(false);
                SetAnimAttacking(true);
                SetAnimSpeed(0f);
                _desiredLookDirection = fallbackDir;
                if (!rotateToMovement && baseDirX != 0f) UpdateFacing(baseDirX);
                TickAttackBase();
                return;
            }

            var target = wps[waypointIndex];
            if (target == null)
            {
                waypointIndex++;
                _desiredLookDirection = fallbackDir;
                return;
            }

            Vector3 pos = transform.position;
            Vector3 to = (target.position - pos);
            float dist = to.magnitude;
            if (dist <= reachThreshold)
            {
                waypointIndex++;
                SetAnimRunning(false);
                SetAnimAttacking(false);
                SetAnimSpeed(0f);
                _desiredLookDirection = fallbackDir;
                if (!rotateToMovement && baseDirX != 0f) UpdateFacing(baseDirX);
            }
            else
            {
                Vector3 dir = to / (dist > 1e-5f ? dist : 1f);
                transform.position = pos + dir * GetEffectiveMoveSpeed() * Time.deltaTime;
                SetAnimRunning(true);
                SetAnimAttacking(false);
                SetAnimSpeed(GetEffectiveMoveSpeed());

                _desiredLookDirection = new Vector2(dir.x, dir.y).normalized;

                if (!rotateToMovement)
                {
                    float desiredX;
                    const float eps = 1e-4f;
                    if (Mathf.Abs(dir.x) <= eps)
                    {

                        desiredX = (baseDirX != 0f) ? baseDirX : 0f;
                    }
                    else if (baseDirX == 0f)
                    {

                        desiredX = dir.x;
                    }
                    else
                    {

                        bool movingTowardBase = Mathf.Sign(dir.x) == Mathf.Sign(baseDirX);
                        desiredX = movingTowardBase ? baseDirX : -baseDirX;
                    }
                    if (desiredX != 0f) UpdateFacing(desiredX);
                }
            }
        }

        private void LateUpdate()
        {
            if (_visualRoot == null) return;

            Vector3 delta = transform.position - _lastVisualPosition;
            _lastVisualPosition = transform.position;

            if (rotateToMovement) UpdateVisualRotation(delta);
            UpdateBobbing(delta.sqrMagnitude > 1e-8f);
        }

        private void UpdateVisualRotation(Vector3 delta)
        {
            Vector2 lookDir = _desiredLookDirection;
            if (delta.sqrMagnitude > 1e-8f)
                lookDir = new Vector2(delta.x, delta.y).normalized;

            if (lookDir.sqrMagnitude < 1e-8f) return;

            float moveAngle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;
            float targetZ = moveAngle - GetDefaultFacingAngle();
            Quaternion desired = Quaternion.Euler(0f, 0f, targetZ) * _visualBaseRotation;

            if (rotateSpeedDegPerSec > 0f)
                _visualRoot.localRotation = Quaternion.RotateTowards(_visualRoot.localRotation, desired, rotateSpeedDegPerSec * Time.deltaTime);
            else
                _visualRoot.localRotation = desired;
        }

        private void UpdateBobbing(bool isMoving)
        {
            if (!bobWhileMoving || _visualRoot == null || _visualRoot == transform) return;

            float pulse = 0f;
            if (isMoving)
            {
                _bobTimer += bobFrequency * Time.deltaTime * 2f * Mathf.PI;
                pulse = Mathf.Sin(_bobTimer) * bobAmplitude;
            }
            else
            {
                _bobTimer = 0f;
            }

            float signX = Mathf.Sign(_visualRoot.localScale.x);
            Vector3 scale = _visualBaseScale * (1f + pulse);
            scale.x = Mathf.Abs(scale.x) * signX;
            _visualRoot.localScale = scale;
        }

        private float GetDefaultFacingAngle()
        {
            switch (defaultSpriteFacing)
            {
                case SpriteFacing.Right: return 0f;
                case SpriteFacing.Up: return 90f;
                case SpriteFacing.Left: return 180f;
                case SpriteFacing.Down:
                default: return -90f;
            }
        }

        private void EnsureVisualRoot()
        {
            if (_visualRoot != null && _visualRoot != transform) return;

            Transform visualChild = transform.Find("Visual");
            if (visualChild != null && visualChild.GetComponent<SpriteRenderer>() != null)
            {
                _visualRoot = visualChild;
                return;
            }

            Animator rootAnim = GetComponent<Animator>();
            SpriteRenderer rootSr = GetComponent<SpriteRenderer>();
            if (rootAnim != null && rootAnim.runtimeAnimatorController != null && rootSr != null)
            {
                _visualRoot = rootSr.transform;
                return;
            }

            if (rootSr != null && rootSr.enabled)
            {
                GameObject visual = new GameObject("Visual");
                visual.transform.SetParent(transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                SpriteRenderer copy = visual.AddComponent<SpriteRenderer>();
                CopySpriteRenderer(rootSr, copy);
                rootSr.enabled = false;
                _visualRoot = visual.transform;
            }
            else
            {
                var sr = GetComponentInChildren<SpriteRenderer>(true);
                _visualRoot = sr != null ? sr.transform : this.transform;
            }
        }

        private static void CopySpriteRenderer(SpriteRenderer src, SpriteRenderer dst)
        {
            if (src == null || dst == null) return;
            dst.sprite = src.sprite;
            dst.color = src.color;
            dst.flipX = src.flipX;
            dst.flipY = src.flipY;
            dst.drawMode = src.drawMode;
            dst.size = src.size;
            dst.tileMode = src.tileMode;
            dst.maskInteraction = src.maskInteraction;
            dst.spriteSortPoint = src.spriteSortPoint;
            dst.sortingLayerID = src.sortingLayerID;
            dst.sortingOrder = src.sortingOrder;
            dst.material = src.material;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            src.GetPropertyBlock(block);
            dst.SetPropertyBlock(block);
        }

        public void SnapVisualToInitialFacing()
        {
            if (_visualRoot == null || !rotateToMovement) return;

            if (_castleTr == null)
            {
                var castle = FindFirstObjectByType<BaseCastle>(FindObjectsInactive.Include);
                if (castle != null) _castleTr = castle.transform;
            }

            Vector2 lookDir = Vector2.zero;
            if (path != null && path.Waypoints != null)
            {
                int count = path.Waypoints.Length;
                for (int i = waypointIndex; i < count; i++)
                {
                    var target = path.Waypoints[i];
                    if (target == null) continue;
                    Vector3 to = target.position - transform.position;
                    if (to.sqrMagnitude > 1e-6f)
                    {
                        lookDir = new Vector2(to.x, to.y).normalized;
                        break;
                    }
                }
            }

            if (lookDir.sqrMagnitude < 1e-6f && _castleTr != null)
            {
                Vector3 toBase = _castleTr.position - transform.position;
                if (toBase.sqrMagnitude > 1e-6f)
                    lookDir = new Vector2(toBase.x, toBase.y).normalized;
            }

            if (lookDir.sqrMagnitude < 1e-6f)
                lookDir = Vector2.right;

            _desiredLookDirection = lookDir;
            _lastVisualPosition = transform.position;

            float moveAngle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;
            float targetZ = moveAngle - GetDefaultFacingAngle();
            _visualRoot.localRotation = Quaternion.Euler(0f, 0f, targetZ) * _visualBaseRotation;
        }


        private void TickStatusEffects()
        {
            float dt = Time.deltaTime;
            if (_burnVfxTimer > 0f) _burnVfxTimer -= dt;
            if (_poisonVfxTimer > 0f) _poisonVfxTimer -= dt;
            if (_frostbiteVfxTimer > 0f) _frostbiteVfxTimer -= dt;
            if (_slowVfxTimer > 0f) _slowVfxTimer -= dt;
            if (_regenVfxTimer > 0f) _regenVfxTimer -= dt;
            if (_stunVfxTimer > 0f) _stunVfxTimer -= dt;
            
            if (_stunTime > 0f)
            {
                _stunTime -= dt;
                if (_stunTime < 0f) _stunTime = 0f;
                else
                {
                    if (!string.IsNullOrEmpty(stunTickVfxKey) && _stunVfxTimer <= 0f)
                    {
                        SpawnStatusVfx(stunTickVfxKey);
                        _stunVfxTimer = Mathf.Max(0.05f, stunTickVfxInterval);
                    }
                }
            }
            
            if (_slowTime > 0f)
            {
                _slowTime -= dt;
                if (_slowTime <= 0f)
                {
                    _slowTime = 0f;
                    _slowPercent = 0f;
                }
                else
                {
                    
                    if (!string.IsNullOrEmpty(slowTickVfxKey) && _slowVfxTimer <= 0f)
                    {
                        SpawnStatusVfx(slowTickVfxKey);
                        _slowVfxTimer = Mathf.Max(0.05f, slowTickVfxInterval);
                    }
                }
            }
            
            if (_burnTime > 0f && _burnDps > 0f)
            {
                float burnTick = _burnDps * dt;
                _burnTime -= dt;
                if (_burnTime <= 0f) { _burnTime = 0f; _burnDps = 0f; }
                if (burnTick > 0f) TakeDamageFromStatus(burnTick, DamageType.Inferno);
                
                if (!string.IsNullOrEmpty(burnTickVfxKey) && _burnVfxTimer <= 0f)
                {
                    SpawnStatusVfx(burnTickVfxKey);
                    _burnVfxTimer = Mathf.Max(0.05f, burnTickVfxInterval);
                }
            }
            
            if (_poisonTime > 0f && _poisonDps > 0f)
            {
                float poisonTick = _poisonDps * dt;
                _poisonTime -= dt;
                if (_poisonTime <= 0f) { _poisonTime = 0f; _poisonDps = 0f; }
                if (poisonTick > 0f) TakeDamageFromStatus(poisonTick, DamageType.Elemental);
                
                if (!string.IsNullOrEmpty(poisonTickVfxKey) && _poisonVfxTimer <= 0f)
                {
                    SpawnStatusVfx(poisonTickVfxKey);
                    _poisonVfxTimer = Mathf.Max(0.05f, poisonTickVfxInterval);
                }
            }
            
            if (_frostbiteTime > 0f && _frostbiteDps > 0f)
            {
                float frostTick = _frostbiteDps * dt;
                _frostbiteTime -= dt;
                if (_frostbiteTime <= 0f) { _frostbiteTime = 0f; _frostbiteDps = 0f; }
                if (frostTick > 0f) TakeDamageFromStatus(frostTick, DamageType.Elemental);
                
                if (!string.IsNullOrEmpty(slowTickVfxKey) && _frostbiteVfxTimer <= 0f)
                {
                    SpawnStatusVfx(slowTickVfxKey);
                    _frostbiteVfxTimer = Mathf.Max(0.05f, slowTickVfxInterval);
                }
            }

            
            TryTickRegeneration(dt);
        }

        private void TryTickRegeneration(float dt)
        {
            if (definition == null || !definition.UseRegenAbility) return;
            if (currentHealth <= 0f) return;
            
            float suppress = definition.RegenSuppressAfterDamageSeconds;
            if (Time.time - _lastDamageTime < suppress) return;

            
            float lifeCap = Mathf.Clamp01(definition.RegenTotalPercentCap) * MaxHealth;
            if (_totalRegenThisLife >= lifeCap - 1e-4f) return;

            
            bool anyDot = (_burnTime > 0f && _burnDps > 0f) || (_poisonTime > 0f && _poisonDps > 0f) || (_frostbiteTime > 0f && _frostbiteDps > 0f);
            float regenMult = 1f;
            if (anyDot)
            {
                if (!definition.RegenWhileDoT) return; 
                regenMult *= Mathf.Clamp01(definition.RegenDoTPenaltyMultiplier);
            }

            
            float baseRate = Mathf.Max(0f, definition.RegenPerSecondBase) * MaxHealth;
            float missingFrac = Mathf.Clamp01((MaxHealth - currentHealth) / Mathf.Max(1f, MaxHealth));
            float missingBonus = Mathf.Max(0f, definition.RegenMissingHealthFactor) * missingFrac * MaxHealth;
            float perSec = (baseRate + missingBonus) * regenMult;
            float perSecCap = Mathf.Max(0f, definition.RegenPerSecondCap) * MaxHealth;
            if (perSecCap > 0f) perSec = Mathf.Min(perSec, perSecCap);
            if (perSec <= 1e-5f) return;

            float delta = perSec * dt;
            
            float remainingCap = Mathf.Max(0f, lifeCap - _totalRegenThisLife);
            if (delta > remainingCap) delta = remainingCap;
            if (delta <= 0f) return;

            float newHp = Mathf.Min(MaxHealth, currentHealth + delta);
            float actual = newHp - currentHealth;
            if (actual <= 0f) return;
            currentHealth = newHp;
            _totalRegenThisLife += actual;
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
            
            string key = definition.RegenVfxKey;
            if (!string.IsNullOrEmpty(key) && _regenVfxTimer <= 0f)
            {
                SpawnStatusVfx(key);
                _regenVfxTimer = Mathf.Max(0.05f, regenVfxInterval);
            }
        }

        
        
        
        private void SpawnStatusVfx(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            PlayStatusVfxLocal(key);
            
            if (DuoRuntime.IsDuo && DuoRuntime.IsSimulationAuthority)
            {
                _netSync?.BroadcastStatusVfx(key);
            }
        }

        
        
        public void PlayStatusVfxLocal(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            var pos = statusVfxAnchor != null ? statusVfxAnchor.position : transform.position;
            TR.VFX.ParticleManager.SpawnOneShot(key, pos);
        }

        public void ApplyBurn(float dps, float duration)
        {
            if (dps <= 0f || duration <= 0f) return;
            if (DuoRuntime.IsRemoteClient) { _netSync?.ForwardBurn(dps, duration); return; }
            _burnDps += Mathf.Max(0f, dps);
            _burnTime = Mathf.Max(_burnTime, duration);
        }

        public void ApplyPoison(float dps, float duration)
        {
            if (dps <= 0f || duration <= 0f) return;
            if (DuoRuntime.IsRemoteClient) { _netSync?.ForwardPoison(dps, duration); return; }
            _poisonDps += Mathf.Max(0f, dps);
            _poisonTime = Mathf.Max(_poisonTime, duration);
        }

        public void ApplySlow(float percent, float duration)
        {
            percent = Mathf.Clamp(percent, 0f, 0.95f);
            if (percent <= 0f || duration <= 0f) return;
            if (DuoRuntime.IsRemoteClient) { _netSync?.ForwardSlow(percent, duration); return; }
            _slowPercent = Mathf.Max(_slowPercent, percent);
            
            _slowTime += duration;
            
            if (!string.IsNullOrEmpty(slowTickVfxKey))
            {
                SpawnStatusVfx(slowTickVfxKey);
                _slowVfxTimer = Mathf.Max(0.05f, slowTickVfxInterval);
            }
        }

        public void TakeDamage(float amount)
        {
            amount = Mathf.Max(0f, amount);
            
            TakeDamage(amount, DamageType.Normal);
        }

        public void TakeDamage(float amount, DamageType type)
        {
            
            
            if (DuoRuntime.IsRemoteClient)
            {
                _netSync?.ForwardDamage(Mathf.Max(0f, amount));
                return;
            }
            if (!DuoRuntime.IsSimulationAuthority) return;
            amount = Mathf.Max(0f, amount);
            
            
            int damager = _pendingDamagerActor;
            _pendingDamagerActor = 0;
            
            float effective = Mathf.Min(amount, Mathf.Max(0f, currentHealth));
            currentHealth -= effective;
            OnHealthChanged?.Invoke(Mathf.Max(0f, currentHealth), MaxHealth);
            
            if (effective > 0f)
            {
                _lastDamageTime = Time.time;
                
                _lastDamagerActor = damager != 0
                    ? damager
                    : (Photon.Pun.PhotonNetwork.InRoom ? Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber : 0);
            }
            
            float display = TR.UI.DamageNumbers.ClampDisplayed ? effective : amount;
            if (display > 0)
            {
                DamageNumbers.ShowFloat(transform, display, type);
            }
            
            if (effective > 0f && !string.IsNullOrEmpty(hitVfxKey))
            {
                if (Time.time >= _nextHitVfxTime)
                {
                    var pos = hitVfxAnchor != null ? hitVfxAnchor.position : transform.position;
                    ParticleManager.SpawnOneShot(hitVfxKey, pos);
                    _nextHitVfxTime = Time.time + Mathf.Max(0.01f, hitVfxInterval);
                }
            }
            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        
        public void TakeDamageFromStatus(float amount, DamageType type)
        {
            
            if (!DuoRuntime.IsSimulationAuthority) return;
            amount = Mathf.Max(0f, amount);
            float effective = Mathf.Min(amount, Mathf.Max(0f, currentHealth));
            currentHealth -= effective;
            OnHealthChanged?.Invoke(Mathf.Max(0f, currentHealth), MaxHealth);
            if (effective > 0f) _lastDamageTime = Time.time;
            float display = TR.UI.DamageNumbers.ClampDisplayed ? effective : amount;
            if (display > 0)
            {
                DamageNumbers.ShowFloat(transform, display, type);
            }
            if (effective > 0f && !string.IsNullOrEmpty(hitVfxKey))
            {
                if (Time.time >= _nextStatusHitVfxTime)
                {
                    var pos = hitVfxAnchor != null ? hitVfxAnchor.position : transform.position;
                    ParticleManager.SpawnOneShot(hitVfxKey, pos);
                    _nextStatusHitVfxTime = Time.time + Mathf.Max(0.05f, statusHitVfxInterval);
                }
            }
            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        
        public void SetNetworkedHealth(float value)
        {
            value = Mathf.Clamp(value, 0f, MaxHealth);
            if (Mathf.Approximately(value, currentHealth)) return;
            
            float delta = currentHealth - value;
            currentHealth = value;
            OnHealthChanged?.Invoke(Mathf.Max(0f, currentHealth), MaxHealth);
            
            
            if (delta > 0f) ShowRemoteDamageFeedback(delta);
            
            if (currentHealth <= 0f)
            {
                PlayRemoteDeathFeedback();
            }
        }

        private bool _remoteDeathPlayed = false;
        private bool _deathProcessed = false;

        
        
        public void PlayRemoteDeathFeedback()
        {
            if (_remoteDeathPlayed) return;
            _remoteDeathPlayed = true;
            if (!string.IsNullOrEmpty(deathVfxKey))
            {
                var pos = deathVfxAnchor != null ? deathVfxAnchor.position : transform.position;
                ParticleManager.SpawnOneShot(deathVfxKey, pos);
            }
            if (!string.IsNullOrEmpty(deathSfxKey) && SFXManager.Instance != null)
            {
                SFXManager.Instance.Play(deathSfxKey);
            }
        }

        
        
        
        private void ShowRemoteDamageFeedback(float amount)
        {
            if (amount <= 0f) return;
            _lastDamageTime = Time.time;
            
            TR.UI.DamageNumbers.ShowFloat(transform, amount, DamageType.Normal);
            
            if (!string.IsNullOrEmpty(hitVfxKey) && Time.time >= _nextHitVfxTime)
            {
                var pos = hitVfxAnchor != null ? hitVfxAnchor.position : transform.position;
                ParticleManager.SpawnOneShot(hitVfxKey, pos);
                _nextHitVfxTime = Time.time + Mathf.Max(0.01f, hitVfxInterval);
            }
        }

        private void Die()
        {
            if (_deathProcessed) return;
            _deathProcessed = true;

            TryAwardKillMoney();
            
            if (!string.IsNullOrEmpty(deathVfxKey))
            {
                var pos = deathVfxAnchor != null ? deathVfxAnchor.position : transform.position;
                ParticleManager.SpawnOneShot(deathVfxKey, pos);
            }
            
            if (!string.IsNullOrEmpty(deathSfxKey) && SFXManager.Instance != null)
            {
                SFXManager.Instance.Play(deathSfxKey);
            }
            
            if (_animator != null && !string.IsNullOrEmpty(_dieTriggerParam))
            {
                _animator.SetTrigger(_dieTriggerParam);
            }
            
            if (DuoRuntime.IsDuo)
            {
                var pv = GetComponent<PhotonView>();
                if (pv != null && PhotonNetwork.IsMasterClient)
                {
                    
                    if (_netSync != null) _netSync.BroadcastDeath();
                    PhotonNetwork.Destroy(gameObject);
                    return;
                }
            }
            Destroy(gameObject);
        }

        private void TryAwardKillMoney()
        {
            if (_arena == null || definition == null) return;
            var tier = _arena.GetTier(definition);
            int min = 0, max = 0;
            switch (tier)
            {
                case ArenaDefinition.EnemyTier.Easy:
                    min = _arena.EasyKillMin; max = _arena.EasyKillMax; break;
                case ArenaDefinition.EnemyTier.Medium:
                    min = _arena.MediumKillMin; max = _arena.MediumKillMax; break;
                case ArenaDefinition.EnemyTier.Hard:
                    min = _arena.HardKillMin; max = _arena.HardKillMax; break;
                case ArenaDefinition.EnemyTier.Boss:
                    min = _arena.BossKillMin; max = _arena.BossKillMax; break;
                default:
                    return;
            }
            if (max < min) { var t = min; min = max; max = t; }
            int amount = Random.Range(min, max + 1);
            if (amount <= 0) return;

            BattleSceneController.Instance?.RecordWaveKill(_waveNumber, amount);

            if (DuoRuntime.IsDuo)
            {
                var coord = TR.Net.DuoBattleCoordinator.Instance;
                if (coord != null)
                {
                    coord.AwardKillMoney(_lastDamagerActor, amount);
                    return;
                }
            }

            
            var econ = FindFirstObjectByType<MatchEconomy>(FindObjectsInactive.Include);
            if (econ != null)
            {
                econ.Earn(amount);
            }
        }

        
        public ArenaDefinition.EnemyTier GetTier()
        {
            if (_arena != null && definition != null) return _arena.GetTier(definition);
            return ArenaDefinition.EnemyTier.Medium;
        }

        [Header("Siege Ring")]
        [SerializeField] private bool useSiegeRing = true;
        [SerializeField] private float siegeArriveThreshold = 0.06f;
        private CastleSiegeRing.Slot _siegeSlot;
        private bool _siegeSlotClaimed;










        private bool TickSiegeApproach(Vector3 castle, float baseDirX)
        {
            if (!useSiegeRing) return false;

            if (!_siegeSlotClaimed)
            {
                _siegeSlot = CastleSiegeRing.Claim(this, castle);
                _siegeSlotClaimed = true;
            }
            if (!_siegeSlot.valid) return false;

            Vector3 pos = transform.position;
            Vector3 slotPos = CastleSiegeRing.SlotPosition(castle, _siegeSlot);

            if ((slotPos - pos).sqrMagnitude <= siegeArriveThreshold * siegeArriveThreshold)
            {
                transform.position = slotPos;
                return false;
            }

            float speed = GetEffectiveMoveSpeed();
            float step = speed * Time.deltaTime;

            Vector2 offset = new Vector2(pos.x - castle.x, pos.y - castle.y);
            float curRadius = offset.magnitude;
            float targetRadius = CastleSiegeRing.RadiusOfRing(_siegeSlot.ring);
            float targetAngle = CastleSiegeRing.AngleOfSlot(_siegeSlot.ring, _siegeSlot.index);
            float curAngle = curRadius > 1e-4f ? Mathf.Atan2(offset.y, offset.x) : targetAngle;




            float newRadius = Mathf.MoveTowards(curRadius, targetRadius, step);




            float angularStep = step / Mathf.Max(0.2f, curRadius);
            float delta = Mathf.DeltaAngle(curAngle * Mathf.Rad2Deg, targetAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float newAngle = curAngle + Mathf.Clamp(delta, -angularStep, angularStep);

            Vector3 next = castle + new Vector3(Mathf.Cos(newAngle) * newRadius, Mathf.Sin(newAngle) * newRadius, 0f);
            Vector3 moveDir = next - pos;
            transform.position = next;

            SetAnimRunning(true);
            SetAnimAttacking(false);
            SetAnimSpeed(speed);

            if (moveDir.sqrMagnitude > 1e-8f)
            {
                _desiredLookDirection = new Vector2(moveDir.x, moveDir.y).normalized;
                if (!rotateToMovement)
                {
                    if (Mathf.Abs(moveDir.x) > 1e-4f) UpdateFacing(Mathf.Sign(moveDir.x));
                    else if (baseDirX != 0f) UpdateFacing(baseDirX);
                }
            }

            return true;
        }

        private void TickAttackBase()
        {
            if (_stunTime > 0f) return; 
            _attackTimer -= Time.deltaTime;
            if (_attackTimer > 0f) return;
            _attackTimer = Mathf.Max(0.05f, attackInterval);
            float baseDmg = definition != null ? definition.DamagePerHit : 1f;
            float dmg = Mathf.Max(0f, baseDmg * Mathf.Max(0.01f, _bossDamageMult));
            var baseObj = FindFirstObjectByType<BaseCastle>(FindObjectsInactive.Include);
            if (baseObj != null)
            {
                baseObj.TakeDamage(Mathf.CeilToInt(dmg));
            }
        }

        
        public void ApplyBossScaling(float healthMult, float damageMult, float speedMult)
        {
            HealthMultiplier = Mathf.Max(0.01f, healthMult);
            DamageMultiplier = Mathf.Max(0.01f, damageMult);
            SpeedMultiplier = Mathf.Max(0.01f, speedMult);

            float h = HealthMultiplier;
            float d = DamageMultiplier;
            float s = SpeedMultiplier;
            
            float baseMax = definition != null ? Mathf.Max(1f, definition.MaxHealth) : Mathf.Max(1f, _runtimeMaxHealth);
            _runtimeMaxHealth = Mathf.Max(1f, baseMax * h);
            currentHealth = _runtimeMaxHealth;
            
            float baseSpeed = definition != null ? Mathf.Max(0f, definition.MovementSpeed) : Mathf.Max(0f, moveSpeed);
            moveSpeed = Mathf.Max(0f, baseSpeed * s);
            
            _bossDamageMult = d;
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        private void TrySpawnHealthBarUI()
        {
            if (healthBarUIPrefab == null) return;
            var parent = healthBarCanvas != null ? healthBarCanvas.transform : null;
            _healthBarInstance = Instantiate(healthBarUIPrefab, parent);
            var ui = _healthBarInstance.GetComponent<EnemyHealthBarUI>();
            if (ui != null)
            {
                ui.Bind(this, healthBarOffset);
            }
            else
            {
                Debug.LogWarning("[EnemyBase2D] HealthBar UI prefab missing EnemyHealthBarUI component.");
            }
        }

        private bool IsBoss()
        {
            return _arena != null && definition != null && _arena.GetTier(definition) == ArenaDefinition.EnemyTier.Boss;
        }

        private Canvas FindOverlayCanvas()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
            }
            return null;
        }

        
        public bool IsStunned => _stunTime > 0f;
        public float GetCurrentSlowPercent() => Mathf.Clamp01(_slowPercent);
        public void ExtendSlow(float extraSeconds)
        {
            extraSeconds = Mathf.Max(0f, extraSeconds);
            if (extraSeconds <= 0f) return;
            _slowTime += extraSeconds;
        }
        public void ApplyFrostbite(float dps, float duration)
        {
            dps = Mathf.Max(0f, dps);
            duration = Mathf.Max(0f, duration);
            if (DuoRuntime.IsRemoteClient) { if (dps > 0f && duration > 0f) _netSync?.ForwardFrostbite(dps, duration); return; }
            if (dps <= 0f || duration <= 0f) return;
            _frostbiteDps += dps;
            _frostbiteTime = Mathf.Max(_frostbiteTime, duration);
        }
        public void ApplyStun(float duration)
        {
            duration = Mathf.Max(0f, duration);
            if (duration <= 0f) return;
            if (DuoRuntime.IsRemoteClient) { _netSync?.ForwardStun(duration); return; }
            
            _stunTime = Mathf.Max(_stunTime, duration);
            
            if (!string.IsNullOrEmpty(stunTickVfxKey))
            {
                var pos = statusVfxAnchor != null ? statusVfxAnchor.position : transform.position;
                TR.VFX.ParticleManager.SpawnOneShot(stunTickVfxKey, pos);
                _stunVfxTimer = Mathf.Max(0.05f, stunTickVfxInterval);
            }
        }
    }
}
