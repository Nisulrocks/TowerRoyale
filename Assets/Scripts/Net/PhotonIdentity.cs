using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

namespace TR.Net
{
    
    
    
    public static class PhotonIdentity
    {
        private const string PrefsUserId = "PhotonUserId";

        public static string UserId { get; private set; }

        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Init()
        {
            string id = PlayerPrefs.GetString(PrefsUserId, "");
            if (string.IsNullOrEmpty(id))
            {
                id = System.Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(PrefsUserId, id);
                PlayerPrefs.Save();
            }
            UserId = id;

            
            PhotonNetwork.AuthValues = new AuthenticationValues(id);
            Debug.Log($"[PhotonIdentity] Stable Photon UserId set: {id}");
        }
    }
}
