using UnityEngine;

namespace TR.VFX
{
    
    
    
    
    
    [DisallowMultipleComponent]
    public class ParticleQualityActivator : MonoBehaviour
    {
        [Header("VFX Root or Prefab")]

        [SerializeField] private GameObject vfxRoot;


        [SerializeField] private GameObject vfxPrefab;


        [SerializeField] private Transform parentForPrefab;


        [SerializeField] private Vector3 prefabLocalPosition = Vector3.zero;

        [SerializeField] private Vector3 prefabLocalEuler = Vector3.zero;

        [SerializeField] private Vector3 prefabLocalScale = Vector3.one;

        private GameObject _spawnedInstance;
        private TR.VFX.PooledParticle _pooledInstance;

        [Header("Auto From Card (optional)")]

        [SerializeField] private bool useCardIdleVfxKey = true;

        [SerializeField] private Vector3 cardVfxLocalPosition = Vector3.zero;

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

            
            if (enabled)
            {
                EnsureVfxRootExists();
                if (vfxRoot != null)
                {
                    if (!vfxRoot.activeSelf) vfxRoot.SetActive(true);
                    EnsureBinder(vfxRoot);
                    
                    var binder = vfxRoot.GetComponent<ParticleQualityBinder>();
                    if (binder != null) binder.Refresh();
                }
            }
            else
            {
                
                if (vfxRoot != null)
                {
                    var binder = vfxRoot.GetComponent<ParticleQualityBinder>();
                    if (binder != null) binder.Refresh(); 
                    
                    if (vfxRoot.activeSelf) vfxRoot.SetActive(false);
                }
                
                if (_pooledInstance != null)
                {
                    _pooledInstance.ForceReturn();
                    _pooledInstance = null;
                    vfxRoot = null;
                }
                
                
                
            }
        }

        private void EnsureVfxRootExists()
        {
            if (vfxRoot != null) return;
            var parent = parentForPrefab != null ? parentForPrefab : this.transform;
            if (vfxPrefab != null)
            {
                
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
                
                var tower = GetComponent<TR.Battle.TowerBase>();
                var def = tower != null ? tower.Definition : null;
                string key = def != null ? def.GetIdleVfxKey() : string.Empty;
                if (!string.IsNullOrEmpty(key) && TR.VFX.ParticleQuality.AllowVfx())
                {
                    
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
            
        }
    }
}
