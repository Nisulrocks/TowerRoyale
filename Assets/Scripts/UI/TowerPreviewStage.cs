using System.Collections;
using UnityEngine;
using TR.Data;
using TR.Battle;

namespace TR.UI
{
    public class TowerPreviewStage : MonoBehaviour
    {
        private static readonly Vector3 StageOrigin = new Vector3(0f, -10000f, 0f);
        private const int TextureSize = 256;

        private const float MinInterval = 0.45f;
        private const float MaxInterval = 1.4f;

        private static TowerPreviewStage _instance;

        private Camera _camera;
        private RenderTexture _texture;
        private GameObject _current;
        private Coroutine _loop;
        private CardDefinition _currentDef;
        private int _currentLevel = -1;

        private static TowerPreviewStage Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("TowerPreviewStage");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<TowerPreviewStage>();
                _instance.Build();
                return _instance;
            }
        }

        private void Build()
        {
            transform.position = StageOrigin;

            _texture = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "TowerPreviewRT",
                antiAliasing = 2
            };
            _texture.Create();

            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -10f);

            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 1.6f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.targetTexture = _texture;
            _camera.enabled = false; 
        }

        public static RenderTexture Acquire(CardDefinition card, int level)
        {
            if (card == null || card.TowerPrefab == null) return null;

            var stage = Instance;
            if (stage._currentDef == card && stage._currentLevel == level && stage._current != null)
            {
                stage._camera.enabled = true;
                return stage._texture;
            }

            stage.Clear();
            stage.Spawn(card, level);
            stage._camera.enabled = true;
            return stage._texture;
        }

        public static void Release()
        {
            if (_instance == null) return;
            _instance.Clear();
            if (_instance._camera != null) _instance._camera.enabled = false;
        }

        private void Clear()
        {
            if (_loop != null) { StopCoroutine(_loop); _loop = null; }
            if (_current != null) Destroy(_current);
            _current = null;
            _currentDef = null;
            _currentLevel = -1;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (_camera != null && child == _camera.transform) continue;
                Destroy(child.gameObject);
            }
        }

        private void Spawn(CardDefinition card, int level)
        {
            _currentDef = card;
            _currentLevel = level;

            _current = Instantiate(card.TowerPrefab, transform);
            _current.transform.localPosition = Vector3.zero;
            _current.name = "Preview_" + card.CardId;

            var towerBase = _current.GetComponent<TowerBase>();
            _muzzleVfxKey = towerBase != null ? towerBase.MuzzleFlashVfxKey : null;
            _muzzleAnchor = towerBase != null ? towerBase.MuzzleFlashAnchor : null;
            _impactVfxOverride = towerBase != null ? towerBase.ProjectileImpactVfxKey : null;
            _nextBeamVfx = 0f;

            DisableGameplayBehaviours(_current);
            FrameCamera(_current);

            _loop = StartCoroutine(AttackLoop(card, Mathf.Max(1, level)));
        }

        private static void DisableGameplayBehaviours(GameObject root)
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string ns = mb.GetType().Namespace;
                if (string.IsNullOrEmpty(ns)) continue;
                if (ns.StartsWith("TR.Battle") || ns.StartsWith("TR.Net"))
                    mb.enabled = false;
            }

            foreach (var src in root.GetComponentsInChildren<AudioSource>(true))
            {
                src.playOnAwake = false;
                src.mute = true;
                src.Stop();
            }

            foreach (var col in root.GetComponentsInChildren<Collider2D>(true))
                col.enabled = false;
        }

        private void FrameCamera(GameObject tower)
        {
            var renderers = tower.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            bool any = false;
            Bounds b = new Bounds(tower.transform.position, Vector3.zero);
            foreach (var r in renderers)
            {
                if (r == null || r.sprite == null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any) return;

            float extent = Mathf.Max(b.extents.x, b.extents.y);
            _camera.orthographicSize = Mathf.Clamp(extent * 2.4f, 0.6f, 6f);
            _camera.transform.position = new Vector3(b.center.x, b.center.y, StageOrigin.z - 10f);
        }

        private IEnumerator AttackLoop(CardDefinition card, int level)
        {
            Vector3 pivot = _current.transform.position;

            Vector3 dir = -_current.transform.up;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.down;
            dir.Normalize();

            Vector3 target = pivot + dir * (_camera.orthographicSize * 0.75f);
            Vector3 muzzle = _muzzleAnchor != null ? _muzzleAnchor.position : pivot;

            if (card is InfernoCardDefinition infernoDef)
            {
                yield return BeamLoop(infernoDef, muzzle, target);
                yield break;
            }

            float fireRate = Mathf.Max(0.01f, card.GetStatsForLevel(level).fireRate);
            float interval = Mathf.Clamp(1f / fireRate, MinInterval, MaxInterval);

            var wait = new WaitForSecondsRealtime(interval);
            yield return new WaitForSecondsRealtime(0.15f);

            while (_current != null)
            {
                FireOnce(card, muzzle, target);
                yield return wait;
            }
        }

        private IEnumerator BeamLoop(InfernoCardDefinition def, Vector3 muzzle, Vector3 target)
        {
            var go = new GameObject("PreviewBeam");
            go.transform.SetParent(transform, false);

            var beam = go.AddComponent<BeamController>();
            var lr = go.GetComponent<LineRenderer>();
            if (lr != null)
            {
                var mat = def.GetBeamMaterial();
                if (mat != null) lr.sharedMaterial = mat;
                else if (lr.sharedMaterial == null)
                    lr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
            }

            beam.Configure(def.GetBeamStartColor(), def.GetBeamEndColor(),
                           def.GetBeamBaseWidth(), def.GetBeamMaxWidth(),
                           def.UseBeamJitter(), def.GetBeamJitterAmplitude());
            beam.SetEndpoints(muzzle, target);

            float t = 0f;
            while (_current != null && beam != null)
            {
                t += Time.unscaledDeltaTime;
                float ramp = Mathf.Clamp01(t / 1.2f);
                beam.SetEndpoints(muzzle, target);
                beam.SetIntensity01(ramp);

                string hitKey = def.GetProjectileImpactVfxKey();
                if (!string.IsNullOrEmpty(hitKey) && t >= _nextBeamVfx)
                {
                    _nextBeamVfx = t + 0.35f;
                    TR.VFX.ParticleManager.SpawnOneShot(hitKey, target);
                }
                yield return null;
            }

            if (go != null) Destroy(go);
        }

        private float _nextBeamVfx;

        private void FireOnce(CardDefinition card, Vector3 muzzle, Vector3 target)
        {
            if (!string.IsNullOrEmpty(_muzzleVfxKey))
                TR.VFX.ParticleManager.SpawnOneShot(_muzzleVfxKey, muzzle);

            if (card.UseLightningZapOnHit())
            {
                var mat = card.GetForceDefaultZapMaterial() ? null : card.GetZapMaterial();
                if (mat != null)
                {
                    LightningZap.Spawn(muzzle, target, card.GetZapDuration(), card.GetZapWidth(),
                        card.GetZapJitter(), card.GetZapSegments(), card.GetZapColor(), mat,
                        card.GetZapGlowEnabled(), card.GetZapGlowBoost());
                }
                else
                {
                    LightningZap.Spawn(muzzle, target, card.GetZapDuration(), card.GetZapWidth(),
                        card.GetZapJitter(), card.GetZapSegments(), card.GetZapColor(),
                        card.GetZapGlowEnabled(), card.GetZapGlowBoost());
                }
                SpawnImpact(card, target);
                return;
            }

            var prefab = card.GetProjectilePrefab();
            if (prefab == null)
            {
                StartCoroutine(Recoil());
                return;
            }

            var proj = Instantiate(prefab, muzzle, Quaternion.identity, transform);
            DisableGameplayBehaviours(proj);
            StartCoroutine(FlyProjectile(card, proj, muzzle, target));
        }

        private IEnumerator FlyProjectile(CardDefinition card, GameObject proj, Vector3 from, Vector3 to)
        {
            float speed = Mathf.Max(0.5f, card.GetProjectileSpeed());
            float dist = Vector3.Distance(from, to);
            float duration = Mathf.Clamp(dist / speed, 0.08f, 1.2f);
            float t = 0f;

            Vector3 dir = (to - from).normalized;
            if (proj != null && dir.sqrMagnitude > 1e-6f)
                proj.transform.up = dir;

            while (t < 1f && proj != null)
            {
                t += Time.unscaledDeltaTime / duration;
                proj.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t));
                yield return null;
            }

            if (proj != null) Destroy(proj);
            SpawnImpact(card, to);
        }

        private void SpawnImpact(CardDefinition card, Vector3 at)
        {
            string key = !string.IsNullOrEmpty(_impactVfxOverride)
                ? _impactVfxOverride
                : card.GetProjectileImpactVfxKey();
            if (string.IsNullOrEmpty(key)) return;
            TR.VFX.ParticleManager.SpawnOneShot(key, at);
        }

        private string _muzzleVfxKey;
        private Transform _muzzleAnchor;
        private string _impactVfxOverride;

        private IEnumerator Recoil()
        {
            if (_current == null) yield break;
            Transform tr = _current.transform;
            Vector3 baseScale = tr.localScale;
            float t = 0f;
            while (t < 1f && _current != null)
            {
                t += Time.unscaledDeltaTime / 0.18f;
                float p = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                tr.localScale = baseScale * (1f + p * 0.08f);
                yield return null;
            }
            if (_current != null) tr.localScale = baseScale;
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
            if (_instance == this) _instance = null;
        }
    }
}
