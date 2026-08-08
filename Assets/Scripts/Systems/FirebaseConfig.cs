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
        [Tooltip("OAuth 2.0 Web Client ID from Google Cloud Console")]
        public string webClientId = "";
        [Tooltip("OAuth 2.0 Web Client Secret from Google Cloud Console")]
        public string clientSecret = "";
        [Tooltip("Local redirect URI port for OAuth callback")]
        public int redirectPort = 5050;
        [Tooltip("OAuth scopes (space-separated)")]
        public string scopes = "openid email profile";
    }
}
