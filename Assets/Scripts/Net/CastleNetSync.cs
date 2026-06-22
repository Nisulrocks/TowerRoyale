using UnityEngine;
using Photon.Pun;
using TR.Battle;

namespace TR.Net
{
    
    
    [RequireComponent(typeof(PhotonView))]
    public class CastleNetSync : MonoBehaviourPun, IPunObservable
    {
        private BaseCastle _castle;

        private void Awake()
        {
            _castle = GetComponent<BaseCastle>();
            if (_castle == null) _castle = GetComponentInParent<BaseCastle>();
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (_castle == null) return;

            if (stream.IsWriting)
            {
                
                stream.SendNext(_castle.CurrentHealth);
            }
            else
            {
                
                int hp = (int)stream.ReceiveNext();
                _castle.SetNetworkedHealth(hp);
            }
        }
    }
}
