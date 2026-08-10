using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TR.Systems;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace TR.Net
{
    
    
    
    public class DuoNetStats : MonoBehaviourPunCallbacks
    {
        public static DuoNetStats Instance { get; private set; }

        
        public const string PROP_PING = "png";

        [Header("Publish")]
        [SerializeField] private float publishInterval = 1f;

        [Header("Lag thresholds (ms)")]
        [SerializeField] private int weakThresholdMs = 250;
        [SerializeField] private int recoverThresholdMs = 160;

        
        public int LocalPing { get; private set; }
        
        public int PartnerPing { get; private set; }
        public bool HasPartner { get; private set; }

        
        
        public System.Action<bool> OnLocalWeakChanged;
        
        public System.Action<bool> OnPartnerWeakChanged;

        public bool LocalWeak { get; private set; }
        public bool PartnerWeak { get; private set; }

        private float _publishTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        
        public static DuoNetStats EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("DuoNetStats");
            return go.AddComponent<DuoNetStats>();
        }

        public override void OnDisable()
        {
            base.OnDisable();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!MatchContext.IsDuo || !PhotonNetwork.InRoom)
            {
                LocalPing = 0; PartnerPing = 0; HasPartner = false;
                return;
            }

            
            LocalPing = PhotonNetwork.GetPing();
            EvaluateLocalWeak();

            
            _publishTimer -= Time.unscaledDeltaTime;
            if (_publishTimer <= 0f)
            {
                _publishTimer = Mathf.Max(0.25f, publishInterval);
                PublishLocalPing();
            }

            
            ReadPartnerPing();
        }

        private void PublishLocalPing()
        {
            var props = new Hashtable { { PROP_PING, LocalPing } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        private void ReadPartnerPing()
        {
            Player partner = null;
            foreach (var p in PhotonNetwork.PlayerListOthers) { partner = p; break; }
            if (partner == null)
            {
                HasPartner = false;
                if (PartnerWeak) { PartnerWeak = false; OnPartnerWeakChanged?.Invoke(false); }
                return;
            }

            HasPartner = true;
            if (partner.CustomProperties != null && partner.CustomProperties.TryGetValue(PROP_PING, out var v))
            {
                PartnerPing = System.Convert.ToInt32(v);
            }
            EvaluatePartnerWeak();
        }

        private void EvaluateLocalWeak()
        {
            if (!LocalWeak && LocalPing >= weakThresholdMs)
            {
                LocalWeak = true;
                OnLocalWeakChanged?.Invoke(true);
            }
            else if (LocalWeak && LocalPing <= recoverThresholdMs)
            {
                LocalWeak = false;
                OnLocalWeakChanged?.Invoke(false);
            }
        }

        private void EvaluatePartnerWeak()
        {
            if (!PartnerWeak && PartnerPing >= weakThresholdMs)
            {
                PartnerWeak = true;
                OnPartnerWeakChanged?.Invoke(true);
            }
            else if (PartnerWeak && PartnerPing <= recoverThresholdMs)
            {
                PartnerWeak = false;
                OnPartnerWeakChanged?.Invoke(false);
            }
        }

        
        public static Color QualityColor(int ping)
        {
            if (ping <= 0) return new Color(0.6f, 0.6f, 0.6f);       
            if (ping < 100) return new Color(0.35f, 0.9f, 0.4f);     
            if (ping < 200) return new Color(1f, 0.85f, 0.3f);       
            return new Color(1f, 0.4f, 0.4f);                        
        }
    }
}
