using UnityEngine;
using Photon.Pun;
using TR.Battle;
using TR.Data;
using TR.Systems;

namespace TR.Net
{
    
    
    [RequireComponent(typeof(PhotonView))]
    public class EnemyNetSync : MonoBehaviourPun, IPunObservable, IPunInstantiateMagicCallback
    {
        [Tooltip("How quickly remote clients interpolate toward the networked position.")]
        [SerializeField] private float interpolateSpeed = 12f;

        private EnemyBase2D _enemy;
        private Vector3 _netPosition;
        private float _netHealth = -1f;
        private bool _hasNetState;

        
        private float _pendingDamage;
        private float _flushTimer;
        private const float DamageFlushInterval = 0.08f; 

        public bool IsSimulated => !MatchContext.IsDuo || PhotonNetwork.IsMasterClient;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase2D>();
            _netPosition = transform.position;
        }

        
        public void OnPhotonInstantiate(PhotonMessageInfo info)
        {
            object[] data = info.photonView != null ? info.photonView.InstantiationData : null;
            if (data == null || data.Length < 4)
            {
                Debug.LogWarning("[EnemyNetSync] Missing instantiation data; cannot configure enemy on this client.");
                return;
            }

            string enemyId = data[0] as string;
            float hMul = data.Length > 1 ? System.Convert.ToSingle(data[1]) : 1f;
            float dMul = data.Length > 2 ? System.Convert.ToSingle(data[2]) : 1f;
            float sMul = data.Length > 3 ? System.Convert.ToSingle(data[3]) : 1f;

            var def = GameDB.GetEnemyById(enemyId);
            if (def == null)
            {
                Debug.LogError($"[EnemyNetSync] Unknown enemyId '{enemyId}' on instantiate.");
                return;
            }

            
            if (_enemy == null) _enemy = GetComponent<EnemyBase2D>();
            var arena = BattleSceneController.CurrentArena;
            var path = Object.FindFirstObjectByType<Path2D>(FindObjectsInactive.Include);
            _enemy.Initialize(def, path);
            if (arena != null) _enemy.SetArena(arena);

            
            
            if (!(Mathf.Approximately(hMul, 1f) && Mathf.Approximately(dMul, 1f) && Mathf.Approximately(sMul, 1f)))
            {
                _enemy.ApplyBossScaling(hMul, dMul, sMul);
            }

            _netPosition = transform.position;
            _hasNetState = true;
        }

        private void Update()
        {
            if (IsSimulated) return;
            
            if (_hasNetState)
            {
                transform.position = Vector3.Lerp(transform.position, _netPosition, Mathf.Clamp01(interpolateSpeed * Time.deltaTime));
            }
            
            _flushTimer -= Time.deltaTime;
            if (_pendingDamage > 0f && _flushTimer <= 0f)
            {
                
                if (photonView != null && _enemy != null && _enemy.CurrentHealth > 0f)
                {
                    photonView.RPC(nameof(RpcApplyDamage), RpcTarget.MasterClient, _pendingDamage);
                }
                _pendingDamage = 0f;
                _flushTimer = DamageFlushInterval;
            }
        }

        private void OnDisable()
        {
            
            _pendingDamage = 0f;
        }

        
        
        
        public void ForwardDamage(float amount)
        {
            if (amount <= 0f) return;
            _pendingDamage += amount;
        }

        public void ForwardBurn(float dps, float duration) => photonView.RPC(nameof(RpcApplyBurn), RpcTarget.MasterClient, dps, duration);
        public void ForwardPoison(float dps, float duration) => photonView.RPC(nameof(RpcApplyPoison), RpcTarget.MasterClient, dps, duration);
        public void ForwardSlow(float percent, float duration) => photonView.RPC(nameof(RpcApplySlow), RpcTarget.MasterClient, percent, duration);
        public void ForwardFrostbite(float dps, float duration) => photonView.RPC(nameof(RpcApplyFrostbite), RpcTarget.MasterClient, dps, duration);
        public void ForwardStun(float duration) => photonView.RPC(nameof(RpcApplyStun), RpcTarget.MasterClient, duration);

        [PunRPC]
        private void RpcApplyDamage(float amount, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient || _enemy == null) return;
            
            if (info.Sender != null) _enemy.SetIncomingDamager(info.Sender.ActorNumber);
            _enemy.TakeDamage(amount, DamageType.Normal);
        }

        [PunRPC]
        private void RpcApplyBurn(float dps, float duration)
        {
            if (!PhotonNetwork.IsMasterClient || _enemy == null) return;
            _enemy.ApplyBurn(dps, duration);
        }

        [PunRPC]
        private void RpcApplyPoison(float dps, float duration)
        {
            if (!PhotonNetwork.IsMasterClient || _enemy == null) return;
            _enemy.ApplyPoison(dps, duration);
        }

        [PunRPC]
        private void RpcApplySlow(float percent, float duration)
        {
            if (!PhotonNetwork.IsMasterClient || _enemy == null) return;
            _enemy.ApplySlow(percent, duration);
        }

        [PunRPC]
        private void RpcApplyFrostbite(float dps, float duration)
        {
            if (!PhotonNetwork.IsMasterClient || _enemy == null) return;
            _enemy.ApplyFrostbite(dps, duration);
        }

        [PunRPC]
        private void RpcApplyStun(float duration)
        {
            if (!PhotonNetwork.IsMasterClient || _enemy == null) return;
            _enemy.ApplyStun(duration);
        }

        
        
        public void BroadcastDeath()
        {
            if (!PhotonNetwork.IsMasterClient || photonView == null) return;
            photonView.RPC(nameof(RpcPlayDeath), RpcTarget.Others);
        }

        [PunRPC]
        private void RpcPlayDeath()
        {
            
            if (_enemy != null) _enemy.PlayRemoteDeathFeedback();
        }

        
        
        public void BroadcastStatusVfx(string key)
        {
            if (!PhotonNetwork.IsMasterClient || photonView == null || string.IsNullOrEmpty(key)) return;
            photonView.RPC(nameof(RpcStatusVfx), RpcTarget.Others, key);
        }

        [PunRPC]
        private void RpcStatusVfx(string key)
        {
            if (_enemy != null) _enemy.PlayStatusVfxLocal(key);
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                
                stream.SendNext(transform.position);
                stream.SendNext(_enemy != null ? _enemy.CurrentHealth : 0f);
            }
            else
            {
                
                _netPosition = (Vector3)stream.ReceiveNext();
                _netHealth = (float)stream.ReceiveNext();
                _hasNetState = true;
                if (_enemy != null && _netHealth >= 0f)
                {
                    _enemy.SetNetworkedHealth(_netHealth);
                }
            }
        }
    }
}
