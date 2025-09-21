using UnityEngine;

namespace TR.VFX
{
    // Attach this to gameplay objects (e.g., towers) that have a VFX root (child GameObject)
    // which may be inactive or missing when VFX is OFF. This component ensures that when
    // VFX is turned ON, the VFX root is activated (and optionally instantiated from a prefab),
    // and that it will stop/disable when VFX is turned OFF.
    // It also ensures a ParticleQualityBinder is present so particles follow the toggle thereafter.
    [DisallowMultipleComponent]
    public class ParticleQualityActivator : MonoBehaviour
    {
        [Header("VFX Root or Prefab")]
        [Tooltip("If assigned, this object will be toggled active/inactive based on VFX quality.")]
        [SerializeField] private GameObject vfxRoot;

        [Tooltip("If no VFX root exists at runtime and VFX becomes enabled, this prefab will be instantiated as a child of 'parentForPrefab'.")]
        [SerializeField] private GameObject vfxPrefab;

        [Tooltip("Parent transform for the spawned VFX prefab. If null, this GameObject is used.")]
        [SerializeField] private Transform parentForPrefab;

        [Tooltip("Local position for the spawned VFX prefab.")]
        [SerializeField] private Vector3 prefabLocalPosition = Vector3.zero;
        [Tooltip("Local rotation for the spawned VFX prefab.")]
        [SerializeField] private Vector3 prefabLocalEuler = Vector3.zero;
        [Tooltip("Local scale for the spawned VFX prefab.")]
        [SerializeField] private Vector3 prefabLocalScale = Vector3.one;

        private GameObject _spawnedInstance;
        private TR.VFX.PooledParticle _pooledInstance;

        [Header("Auto From Card (optional)")]
        [Tooltip("If true and no vfxRoot/prefab is set, will use the CardDefinition idle VFX key and ParticleManager to create a pooled particle as the VFX root.")]
        [SerializeField] private bool useCardIdleVfxKey = true;
        [Tooltip("Local position offset for ParticleManager-spawned idle VFX")]
        [SerializeField] private Vector3 cardVfxLocalPosition = Vector3.zero;
        [Tooltip("Local euler rotation for ParticleManager-spawned idle VFX")]
        [SerializeField] private Vector3 cardVfxLocalEuler = Vector3.zero;

        private void Awake()
        {
            if (parentForPrefab == null) parentForPrefab = this.transform;
        }

        private void OnEnable()
        {
            ParticleQuality.OnChanged += HandleQualityChanged;
            HandleQualityChanged(ParticleQuality.Current);
        }

        private void OnDisable()
        {
            ParticleQuality.OnChanged -= HandleQualityChanged;
        }

        private void HandleQualityChanged(int q)
        {
            bool enabled = q > 0;

            // Ensure we have a VFX root if needed and toggled on
            if (enabled)
            {
                EnsureVfxRootExists();
                if (vfxRoot != null)
                {
                    if (!vfxRoot.activeSelf) vfxRoot.SetActive(true);
                    EnsureBinder(vfxRoot);
                    // Apply current state to play systems
                    var binder = vfxRoot.GetComponent<ParticleQualityBinder>();
                    if (binder != null) binder.Refresh();
                }
            }
            else
            {
                // Disable or stop
                if (vfxRoot != null)
                {
                    var binder = vfxRoot.GetComponent<ParticleQualityBinder>();
                    if (binder != null) binder.Refresh(); // will stop/clear & disable emission
                    // Optionally also deactivate the root for perf
                    if (vfxRoot.activeSelf) vfxRoot.SetActive(false);
                }
                // If we spawned a pooled particle as the root, return it to pool
                if (_pooledInstance != null)
                {
                    _pooledInstance.ForceReturn();
                    _pooledInstance = null;
                    vfxRoot = null;
                }
                // If we spawned a dynamic instance, you may choose to destroy it on OFF
                // Comment out if you prefer to keep instance around and only stop particles
                // if (_spawnedInstance != null) { Destroy(_spawnedInstance); _spawnedInstance = null; }
            }
        }

        private void EnsureVfxRootExists()
        {
            if (vfxRoot != null) return;
            var parent = parentForPrefab != null ? parentForPrefab : this.transform;
            if (vfxPrefab != null)
            {
                // Spawn prefab as child
                _spawnedInstance = Instantiate(vfxPrefab, parent);
                var rt = _spawnedInstance.transform as RectTransform;
                if (rt != null)
                {
                    rt.anchoredPosition3D = prefabLocalPosition;
                    rt.localRotation = Quaternion.Euler(prefabLocalEuler);
                    rt.localScale = prefabLocalScale;
                }
                else
                {
                    _spawnedInstance.transform.localPosition = prefabLocalPosition;
                    _spawnedInstance.transform.localRotation = Quaternion.Euler(prefabLocalEuler);
                    _spawnedInstance.transform.localScale = prefabLocalScale;
                }
                vfxRoot = _spawnedInstance;
                return;
            }

            if (useCardIdleVfxKey)
            {
                // Try to get idle VFX key from the card and spawn via ParticleManager as child
                var tower = GetComponent<TR.Battle.TowerBase>();
                var def = tower != null ? tower.Definition : null;
                string key = def != null ? def.GetIdleVfxKey() : string.Empty;
                if (!string.IsNullOrEmpty(key) && TR.VFX.ParticleQuality.AllowVfx())
                {
                    // Spawn under manager but parented to our parent with world position
                    Vector3 worldPos = parent.TransformPoint(cardVfxLocalPosition);
                    var ps = TR.VFX.ParticleManager.Spawn(key, worldPos, Quaternion.Euler(cardVfxLocalEuler), null, true);
                    if (ps != null)
                    {
                        var tr = ps.transform;
                        tr.SetParent(parent, worldPositionStays: true);
                        _pooledInstance = ps.GetComponent<TR.VFX.PooledParticle>();
                        vfxRoot = ps.gameObject;
                    }
                }
            }
        }

        private void EnsureBinder(GameObject go)
        {
            if (go == null) return;
            var binder = go.GetComponent<ParticleQualityBinder>();
            if (binder == null) binder = go.AddComponent<ParticleQualityBinder>();
            // Defaults (includeChildren=true, autoPlayWhenEnabled=true) are good for most cases
        }
    }
}
