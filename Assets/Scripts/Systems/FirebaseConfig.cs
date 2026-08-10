using UnityEngine;

namespace TR.Systems
{
    [CreateAssetMenu(fileName = "FirebaseConfig", menuName = "Tower Royale/Firebase Config")]
    public class FirebaseConfig : ScriptableObject
    {
        [Header("Firebase Web App Config")]
        public string apiKey = "";
        public string authDomain = "";
        public string projectId = "";
        public string storageBucket = "";
        public string messagingSenderId = "";
        public string appId = "";

        [Header("Google OAuth")]
        public string webClientId = "";
        public string clientSecret = "";
        public int redirectPort = 5050;
        public string scopes = "openid email profile";
    }
}
