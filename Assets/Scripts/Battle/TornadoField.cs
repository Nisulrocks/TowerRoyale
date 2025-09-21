using UnityEngine;
using System.Linq;
using TR.VFX;
using TR.Audio;

namespace TR.Battle
{
    // A temporary field that pulls enemies toward a center within a radius for a duration.
    public class TornadoField : MonoBehaviour
    {
        [SerializeField] private float radius = 1.5f;
        [SerializeField] private float strength = 2.0f; // units per second pull toward center
        [SerializeField] private float duration = 1.0f; // seconds
        [SerializeField] private AnimationCurve falloff = AnimationCurve.Linear(0, 1, 1, 0); // 1 at center, 0 at edge
        [Header("Swirl/Separation")]
        [Tooltip("Within this inner radius, enemies stop moving inward and orbit instead (prevents stacking at exact center)")]
        [SerializeField] private float innerOrbitRadius = 0.35f;
        [Tooltip("Tangential swirl speed (units/sec) used inside innerOrbitRadius and partially outside for visual spin")]
        [SerializeField] private float tangentialStrength = 1.5f;
        [Tooltip("Soft separation radius among enemies (tries to keep at least this distance between them)")]
        [SerializeField] private float separationRadius = 0.25f;
        [Tooltip("How strong the separation pushes enemies apart (units/sec)")]
        [SerializeField] private float separationStrength = 1.0f;

        private float _time;
        private Vector3 _center;
        // Track a deterministic swirl direction per enemy (clockwise or counter-clockwise)
        private readonly System.Collections.Generic.Dictionary<EnemyBase2D, float> _swirlDir = new System.Collections.Generic.Dictionary<EnemyBase2D, float>();
        // Filters
        private int _maxPullTargets = 9999;
        private bool _allowEasy = true, _allowMedium = true, _allowHard = true, _allowBoss = true;
        [Header("VFX (Optional)")]
        [SerializeField] private string vfxKey = "";
        [SerializeField] private Transform vfxAnchor;
        [SerializeField] private bool autoScaleVfxToRadius = true;
        [SerializeField] private float vfxScaleMultiplier = 1.0f;
        private ParticleSystem _vfx;
        [Header("SFX (Optional)")]
        [SerializeField] private string sfxKey = "";
        [SerializeField] private float sfxFadeIn = 0.15f;
        [SerializeField] private float sfxFadeOut = 0.2f;
        private int _sfxHandle = -1;
        [Header("Debug")]
        [SerializeField] private bool showGizmo = true;
        [Header("Behavior")]
        [SerializeField] private bool allowCenterStack = false; // if true, allow full inward pull at center
        [SerializeField] private float falloffPower = 1.0f;     // shapes pull strength vs distance

        private static TornadoField CreateInactive(Vector3 center, Transform parent)
        {
            var go = new GameObject("TornadoField");
            if (parent != null) go.transform.SetParent(parent, true);
            go.SetActive(false);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            var tf = go.AddComponent<TornadoField>();
            tf._center = go.transform.position;
            return tf;
        }

        public static TornadoField Spawn(Vector3 center, float radius, float strength, float duration, Transform parent = null)
        {
            var tf = CreateInactive(center, parent);
            tf.radius = Mathf.Max(0f, radius);
            tf.strength = Mathf.Max(0f, strength);
            tf.duration = Mathf.Max(0f, duration);
            tf.gameObject.SetActive(true);
            return tf;
        }

        public static TornadoField Spawn(Vector3 center, float radius, float strength, float duration,
                                         int maxPullTargets,
                                         bool allowEasy, bool allowMedium, bool allowHard, bool allowBoss,
                                         Transform parent = null)
        {
            var tf = CreateInactive(center, parent);
            tf.radius = Mathf.Max(0f, radius);
            tf.strength = Mathf.Max(0f, strength);
            tf.duration = Mathf.Max(0f, duration);
            tf._maxPullTargets = Mathf.Max(0, maxPullTargets);
            tf._allowEasy = allowEasy; tf._allowMedium = allowMedium; tf._allowHard = allowHard; tf._allowBoss = allowBoss;
            tf.gameObject.SetActive(true);
            return tf;
        }

        public static TornadoField Spawn(Vector3 center, float radius, float strength, float duration,
                                         int maxPullTargets,
                                         bool allowEasy, bool allowMedium, bool allowHard, bool allowBoss,
                                         string vfxKey, float vfxScaleMultiplier,
                                         bool allowCenterStack, float falloffPower,
                                         Transform parent = null)
        {
            var tf = CreateInactive(center, parent);
            tf.radius = Mathf.Max(0f, radius);
            tf.strength = Mathf.Max(0f, strength);
            tf.duration = Mathf.Max(0f, duration);
            tf._maxPullTargets = Mathf.Max(0, maxPullTargets);
            tf._allowEasy = allowEasy; tf._allowMedium = allowMedium; tf._allowHard = allowHard; tf._allowBoss = allowBoss;
            tf.vfxKey = vfxKey ?? string.Empty;
            tf.vfxScaleMultiplier = Mathf.Max(0f, vfxScaleMultiplier);
            tf.allowCenterStack = allowCenterStack;
            tf.falloffPower = Mathf.Clamp(falloffPower, 0.1f, 5f);
            tf.gameObject.SetActive(true);
            return tf;
        }

        private void OnEnable()
        {
            TrySpawnVfx();
            ApplyVfxScaleFromRadius();
            TryStartSfx();
        }

        private void OnDisable()
        {
            TryReleaseVfx();
            TryStopSfx();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _time += dt;
            if (_time >= duration || radius <= 0f || strength <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // Keep VFX playing
            if (_vfx != null && !_vfx.isPlaying)
            {
                _vfx.Play(true);
            }

            // Snapshot enemies to avoid collection modification during iteration
            var list = EnemyBase2D.All != null ? EnemyBase2D.All.ToArray() : System.Array.Empty<EnemyBase2D>();
            int pulledThisFrame = 0;
            for (int i = 0; i < list.Length; i++)
            {
                var e = list[i];
                if (e == null || !e.gameObject.activeInHierarchy || e.CurrentHealth <= 0f) continue;
                var pos = e.transform.position;
                float d = Vector2.Distance((Vector2)_center, (Vector2)pos);
                if (d > radius || d <= 0.0001f) continue;

                // Filter by allowed tiers if available
                var tier = e.GetTier();
                bool allowed = (tier == TR.Data.ArenaDefinition.EnemyTier.Easy && _allowEasy)
                            || (tier == TR.Data.ArenaDefinition.EnemyTier.Medium && _allowMedium)
                            || (tier == TR.Data.ArenaDefinition.EnemyTier.Hard && _allowHard)
                            || (tier == TR.Data.ArenaDefinition.EnemyTier.Boss && _allowBoss);
                if (!allowed) continue;
                if (pulledThisFrame >= _maxPullTargets) continue;

                Vector3 dir = (_center - pos);
                float norm = Mathf.Clamp01(d / Mathf.Max(0.0001f, radius));
                float mul = falloff != null ? Mathf.Clamp01(falloff.Evaluate(1f - norm)) : (1f - norm);
                // Shape the pull with power to allow stronger near-center suction when < 1
                mul = Mathf.Pow(mul, Mathf.Clamp(falloffPower, 0.1f, 5f));

                // Base inward pull (we'll reduce/zero this inside the inner orbit radius)
                Vector3 inward = dir.normalized * (strength * mul * dt);

                // Tangential swirl (perpendicular to inward)
                if (!_swirlDir.TryGetValue(e, out float sgn))
                {
                    // Assign a stable swirl direction based on instanceID (pseudo-random)
                    sgn = (e.GetInstanceID() & 1) == 0 ? 1f : -1f;
                    _swirlDir[e] = sgn;
                }
                Vector3 tangent = new Vector3(-dir.y, dir.x, 0f).normalized * (tangentialStrength * mul * dt * sgn);

                // Inside inner orbit radius: prevent further inward movement; use pure tangential for swirl
                if (!allowCenterStack)
                {
                    if (d < innerOrbitRadius)
                    {
                        inward = Vector3.zero; // orbit only when close to core
                    }
                }

                // Soft separation among nearby enemies to avoid overlap
                if (separationRadius > 0.0001f && separationStrength > 0f)
                {
                    Vector3 sep = Vector3.zero;
                    for (int j = 0; j < list.Length; j++)
                    {
                        if (i == j) continue;
                        var o = list[j];
                        if (o == null || !o.gameObject.activeInHierarchy || o.CurrentHealth <= 0f) continue;
                        var opos = o.transform.position;
                        float dd = Vector2.Distance((Vector2)pos, (Vector2)opos);
                        if (dd <= 0.0001f || dd > separationRadius) continue;
                        Vector3 away = (pos - opos);
                        sep += away.normalized * (1f - (dd / separationRadius));
                    }
                    if (sep.sqrMagnitude > 0f)
                    {
                        float sepStrength = separationStrength;
                        if (allowCenterStack) sepStrength *= 0.35f; // reduce separation when stacking allowed
                        sep = sep.normalized * (sepStrength * dt);
                        // Apply separation more aggressively when very close to core
                        if (d < innerOrbitRadius) sep *= 1.25f;
                        tangent += sep; // blend separation with swirl
                    }
                }

                // Total step: inward + tangential/separation, but do not overshoot past center
                Vector3 step = inward + tangent;
                if (step.sqrMagnitude > dir.sqrMagnitude)
                {
                    // Clamp to not cross the center in a single frame
                    step = dir;
                }
                e.transform.position = pos + step;
                pulledThisFrame++;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showGizmo) return;
            Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.15f);
            Gizmos.DrawWireSphere(_center == Vector3.zero ? transform.position : _center, radius);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(_center == Vector3.zero ? transform.position : _center, radius);
        }
#endif

        private void TrySpawnVfx()
        {
            if (string.IsNullOrEmpty(vfxKey))
            {
                Debug.LogWarning("[TornadoField] vfxKey is empty; no tornado VFX will be spawned.");
                return;
            }
            if (_vfx != null) return;
            var anchor = vfxAnchor != null ? vfxAnchor : transform;
            var pos = anchor != null ? anchor.position : transform.position;
            _vfx = ParticleManager.Spawn(vfxKey, pos, Quaternion.identity, anchor, true);
            if (_vfx != null)
            {
                var main = _vfx.main;
                main.loop = true;
                _vfx.gameObject.SetActive(true);
                _vfx.Play(true);
            }
            else
            {
                Debug.LogWarning($"[TornadoField] ParticleManager.Spawn returned null for key '{vfxKey}'. Falling back to one-shot (won't loop).");
                ParticleManager.SpawnOneShot(vfxKey, pos);
            }
        }

        private void TryReleaseVfx()
        {
            if (_vfx == null) return;
            _vfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _vfx.gameObject.SetActive(false);
            _vfx = null;
        }

        private void TryStartSfx()
        {
            if (string.IsNullOrEmpty(sfxKey) || SFXManager.Instance == null) return;
            if (_sfxHandle <= 0)
            {
                _sfxHandle = SFXManager.Instance.PlayLoop(sfxKey, Mathf.Max(0f, sfxFadeIn));
            }
        }

        private void TryStopSfx()
        {
            if (_sfxHandle > 0 && SFXManager.Instance != null)
            {
                SFXManager.Instance.StopLoop(_sfxHandle, Mathf.Max(0f, sfxFadeOut));
                _sfxHandle = -1;
            }
        }

        public void SetSfxKey(string key)
        {
            sfxKey = key ?? string.Empty;
            // If already active, restart with the new key
            TryStopSfx();
            TryStartSfx();
        }

        private void ApplyVfxScaleFromRadius()
        {
            if (!autoScaleVfxToRadius) return;
            if (_vfx == null) return;
            float s = Mathf.Max(0f, radius * Mathf.Max(0f, vfxScaleMultiplier));
            if (s <= 0f) return;
            var ps = _vfx;
            if (ps != null)
            {
                var sh = ps.shape;
                if (sh.enabled)
                {
                    sh.radius = s;
                    return;
                }
            }
            _vfx.transform.localScale = new Vector3(s, s, s);
        }
    }
}
