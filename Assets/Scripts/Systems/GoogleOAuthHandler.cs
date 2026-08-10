using System;
using System.Collections;
using System.Net;
using System.Text;
using System.Web;
using UnityEngine;
using UnityEngine.Networking;

namespace TR.Systems
{
    public static class GoogleOAuthHandler
    {
        public static float TimeoutSeconds = 60f;

        public static float SecondsRemaining { get; private set; }
        public static bool IsWaitingForBrowser { get; private set; }

        private const string AuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string TokenUrl = "https://oauth2.googleapis.com/token";

        public static IEnumerator StartOAuthFlow(
            string clientId,
            string clientSecret,
            string redirectUri,
            string scopes,
            Action<string> onSuccess,
            Action<string> onError)
        {
            string state = Guid.NewGuid().ToString("N");
            string authRequestUrl = $"{AuthUrl}?" +
                $"client_id={HttpUtility.UrlEncode(clientId)}" +
                $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
                $"&response_type=code" +
                $"&scope={HttpUtility.UrlEncode(scopes)}" +
                $"&state={state}" +
                $"&prompt=consent";

            HttpListener listener = null;
            try
            {
                listener = new HttpListener();
                string prefix = redirectUri.EndsWith("/") ? redirectUri : redirectUri + "/";
                listener.Prefixes.Add(prefix);
                listener.Start();
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Failed to start local OAuth listener: {ex.Message}");
                yield break;
            }

            Debug.Log("[GoogleOAuth] Opening browser for Google sign-in...");
            Application.OpenURL(authRequestUrl);

            string authCode = null;
            string receivedState = null;
            string oauthError = null;
            bool listenerDone = false;

            var listenerTask = listener.GetContextAsync();
            float oauthTimeout = TimeoutSeconds;
            IsWaitingForBrowser = true;
            while (!listenerTask.IsCompleted && !listenerDone && oauthTimeout > 0f)
            {
                oauthTimeout -= Time.unscaledDeltaTime;
                SecondsRemaining = Mathf.Max(0f, oauthTimeout);
                yield return null;
            }
            IsWaitingForBrowser = false;
            SecondsRemaining = 0f;

            if (!listenerTask.IsCompleted && !listenerDone)
            {
                try { listener?.Stop(); } catch { }
                onError?.Invoke("Sign-in was not completed. You can try again or continue as a guest.");
                yield break;
            }

            try
            {
                if (listenerTask.IsCompleted)
                {
                    var context = listenerTask.Result;
                    var request = context.Request;
                    var queryParams = HttpUtility.ParseQueryString(request.Url.Query);

                    receivedState = queryParams["state"];
                    authCode = queryParams["code"];
                    oauthError = queryParams["error"];

                    string responseHtml = BuildResponseHtml(string.IsNullOrEmpty(oauthError));

                    byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
                    context.Response.ContentLength64 = buffer.Length;
                    context.Response.ContentType = "text/html";
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    context.Response.OutputStream.Close();
                }
            }
            catch (Exception ex)
            {
                onError?.Invoke($"OAuth listener error: {ex.Message}");
                try { listener?.Stop(); } catch { }
                yield break;
            }
            finally
            {
                try { listener?.Stop(); } catch { }
            }

            if (!string.IsNullOrEmpty(oauthError))
            {
                onError?.Invoke($"Google OAuth error: {oauthError}");
                yield break;
            }

            if (receivedState != state)
            {
                onError?.Invoke("OAuth state mismatch — possible CSRF attack.");
                yield break;
            }

            if (string.IsNullOrEmpty(authCode))
            {
                onError?.Invoke("No authorization code received from Google.");
                yield break;
            }

            yield return ExchangeCodeForIdToken(
                clientId,
                clientSecret,
                redirectUri,
                authCode,
                onSuccess,
                onError);
        }

        private static IEnumerator ExchangeCodeForIdToken(
            string clientId,
            string clientSecret,
            string redirectUri,
            string authCode,
            Action<string> onSuccess,
            Action<string> onError)
        {
            if (string.IsNullOrEmpty(clientSecret))
                Debug.LogWarning("[GoogleOAuth] client_secret is empty — Google requires it for web server token exchange.");

            var form = new WWWForm();
            form.AddField("code", authCode);
            form.AddField("client_id", clientId);
            form.AddField("client_secret", clientSecret);
            form.AddField("redirect_uri", redirectUri);
            form.AddField("grant_type", "authorization_code");

            using (var www = UnityWebRequest.Post(TokenUrl, form))
            {
                www.SendWebRequest();
                while (!www.isDone) yield return null;

                if (www.result != UnityWebRequest.Result.Success)
                {
                    string responseBody = www.downloadHandler != null ? www.downloadHandler.text : "(no body)";
                    Debug.LogError($"[GoogleOAuth] Token exchange failed: {www.error}\nResponse: {responseBody}");
                    onError?.Invoke($"Token exchange failed: {www.error} - {responseBody}");
                    yield break;
                }

                try
                {
                    var response = JsonUtility.FromJson<TokenResponse>(www.downloadHandler.text);
                    if (response != null && !string.IsNullOrEmpty(response.id_token))
                    {
                        onSuccess?.Invoke(response.id_token);
                    }
                    else
                    {
                        onError?.Invoke("Token exchange response missing id_token.");
                    }
                }
                catch (Exception ex)
                {
                    onError?.Invoke($"Failed to parse token response: {ex.Message}");
                }
            }
        }

        private static string BuildResponseHtml(bool success)
{
    string title = success ? "Sign-in Successful" : "Sign-in Failed";
    string heading = success ? "You're Signed In" : "Sign-in Failed";
    string message = success
        ? "You can now close this tab and return to the game."
        : "Something went wrong during sign-in. Please try again from the game.";
    string statusColor = success ? "#d4af37" : "#e05a5a";
    string statusIconPath = success
        ? "M20 6L9 17l-5-5"
        : "M18 6L6 18M6 6l12 12";

    return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>Tower Royale — {title}</title>
<style>
  * {{ margin: 0; padding: 0; box-sizing: border-box; }}

  @@font-face {{
    /* Falls back gracefully if unavailable */
  }}

  body {{
    font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
    background:
      radial-gradient(circle at 20% 20%, rgba(212,175,55,0.06), transparent 40%),
      radial-gradient(circle at 80% 80%, rgba(212,175,55,0.05), transparent 40%),
      linear-gradient(160deg, #0a0912 0%, #14121f 45%, #1c1a2b 100%);
    color: #fff;
    min-height: 100vh;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 24px;
  }}

  .card {{
    position: relative;
    background: rgba(255,255,255,0.04);
    border: 1px solid rgba(212,175,55,0.25);
    border-radius: 16px;
    padding: 56px 44px 40px;
    text-align: center;
    max-width: 440px;
    width: 100%;
    backdrop-filter: blur(14px);
    box-shadow:
      0 20px 60px rgba(0,0,0,0.5),
      inset 0 1px 0 rgba(255,255,255,0.06);
    animation: fadeIn 0.6s cubic-bezier(0.22, 1, 0.36, 1);
  }}

  .card::before {{
    content: '';
    position: absolute;
    top: 0; left: 0; right: 0;
    height: 1px;
    background: linear-gradient(90deg, transparent, rgba(212,175,55,0.6), transparent);
  }}

  @@keyframes fadeIn {{
    from {{ opacity: 0; transform: translateY(16px); }}
    to {{ opacity: 1; transform: translateY(0); }}
  }}

  .wordmark {{
    font-size: 13px;
    font-weight: 700;
    letter-spacing: 4px;
    text-transform: uppercase;
    color: #d4af37;
    margin-bottom: 32px;
  }}

  .wordmark span {{
    color: #6b6880;
  }}

  .status-icon {{
    width: 64px;
    height: 64px;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    margin: 0 auto 24px;
    background: rgba(212,175,55,0.08);
    border: 1px solid {statusColor}55;
    animation: pop 0.5s cubic-bezier(0.22, 1, 0.36, 1) 0.15s both;
  }}

  @@keyframes pop {{
    0% {{ transform: scale(0.5); opacity: 0; }}
    100% {{ transform: scale(1); opacity: 1; }}
  }}

  .status-icon svg {{
    width: 28px;
    height: 28px;
    stroke: {statusColor};
    stroke-width: 2.5;
    fill: none;
    stroke-linecap: round;
    stroke-linejoin: round;
  }}

  h1 {{
    font-size: 24px;
    font-weight: 600;
    letter-spacing: 0.2px;
    margin-bottom: 12px;
    color: #f5f3ee;
  }}

  p {{
    font-size: 15px;
    color: #9a97ac;
    line-height: 1.65;
    margin-bottom: 4px;
  }}

  .divider {{
    width: 40px;
    height: 1px;
    background: rgba(212,175,55,0.3);
    margin: 28px auto 20px;
  }}

  .footer {{
    font-size: 11px;
    color: #4d4b60;
    letter-spacing: 1px;
  }}

  .footer strong {{
    color: #6b6880;
    font-weight: 600;
  }}
</style>
</head>
<body>
  <div class=""card"">
    <div class=""wordmark"">TOWER <span>ROYALE</span></div>

    <div class=""status-icon"">
      <svg viewBox=""0 0 24 24"" xmlns=""http://www.w3.org/2000/svg"">
        <path d=""{statusIconPath}""></path>
      </svg>
    </div>

    <h1>{heading}</h1>
    <p>{message}</p>

    <div class=""divider""></div>
    <div class=""footer"">Developed by <strong>Nisulrocks Studios</strong></div>
  </div>
</body>
</html>";
        }

        [Serializable]
        private class TokenResponse
        {
            public string access_token;
            public string id_token;
            public string refresh_token;
            public string token_type;
            public int expires_in;
        }
    }
}
