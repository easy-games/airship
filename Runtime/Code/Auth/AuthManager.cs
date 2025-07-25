using System;
using System.IO;
using System.Threading.Tasks;
using Cdm.Authentication.Browser;
using Cdm.Authentication.Clients;
using Cdm.Authentication.OAuth2;
using Code.Http.Internal;
#if UNITY_ANDROID
using Google;
#endif
using JetBrains.Annotations;
using Proyecto26;
using RSG;
using UnityEngine;
using UnityEngine.Networking;

[LuauAPI(LuauContext.Protected)]
public class AuthManager {
    public static Action authed;

	private static string GetAccountJSONPath() {
		var stagingExtension = "";
#if AIRSHIP_STAGING
		stagingExtension = "_staging"; 
#endif
#if DEVELOPMENT_BUILD
		return Path.Combine(Application.persistentDataPath, $"account_devbuild{stagingExtension}.json");
#endif
#if UNITY_EDITOR
		return Path.Combine(Application.persistentDataPath, $"account_editor{stagingExtension}.json");
#endif
		return Path.Combine(Application.persistentDataPath, $"account{stagingExtension}.json");
	}

	[CanBeNull]
	public static AuthSave GetSavedAccount() {
		var path = GetAccountJSONPath();
		if (!File.Exists(path)) {
			return null;
		}

		try {
			var authSave = JsonUtility.FromJson<AuthSave>(File.ReadAllText(path));
			return authSave;
		} catch (Exception e) {
			Debug.LogError(e);
		}
		return null;
	}
   
	public static void SaveAuthAccount(string refreshToken) {
		var authSave = new AuthSave {
			refreshToken = refreshToken,
			time = DateTimeOffset.Now.ToUnixTimeSeconds()
		};
		var path = GetAccountJSONPath();
		File.WriteAllText(path, JsonUtility.ToJson(authSave));
	}

	public static async Task<FirebaseTokenResponse> LoginWithRefreshToken(string refreshToken) {
		var body = $"grantType=refresh_token&refresh_token={refreshToken}";
		var req = UnityWebRequest.PostWwwForm("https://securetoken.googleapis.com/v1/token?key=" + AirshipApp.firebaseApiKey + "&" + body, "");
		req.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
		await req.SendWebRequest();
		if (req.result == UnityWebRequest.Result.ProtocolError) {
			Debug.LogError(req.error);
			return null;
		}
		return JsonUtility.FromJson<FirebaseTokenResponse>(req.downloadHandler.text);
	}

	public static void ClearSavedAccount() {
#if UNITY_EDITOR
		InternalHttpManager.editorAuthToken = "";
		InternalHttpManager.editorUserId = "";
#else
		InternalHttpManager.authToken = "";
#endif
		var path = GetAccountJSONPath();
		if (File.Exists(path)) {
			File.Delete(path);
		}
	}
   
	public static async Task<(bool success, string error)> AuthWithGoogle() {
#if AIRSHIP_STAGING
        string clientId = "987279961241-0mjidme48us0fis0vtqk4jqrsmk7ar0n.apps.googleusercontent.com";
        string clientSecret = "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";
#else
		string clientId = "457451560440-fvhufuvt3skas9m046jqin0l10h8uaph.apps.googleusercontent.com";
		string clientSecret = "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";
#endif
        string redirectUri = "http://localhost:8080";

#if UNITY_IOS && !UNITY_EDITOR
#if AIRSHIP_STAGING
        clientId = "987279961241-e2klb9k8ikdkh12ja6m93uulm8mkmme7.apps.googleusercontent.com";
        clientSecret = null;
        redirectUri = "gg.easy.airship:/oauth2";
#else
        clientId = "457451560440-qq4qg87evvnk8k26b2mt5ahphp2iug4t.apps.googleusercontent.com";
        clientSecret = null;
        redirectUri = "gg.easy.airship:/oauth2";
#endif
#endif

        var auth = new GoogleAuth(new AuthorizationCodeFlow.Configuration() {
            clientId = clientId,

            // Why we include this: https://stackoverflow.com/a/73779731
            clientSecret = clientSecret,

            redirectUri = redirectUri,
            scope = "openid email profile",
        });

#if UNITY_ANDROID
        GoogleSignIn.Configuration = new GoogleSignInConfiguration() {
			RequestEmail = true,
			RequestProfile = true,
			RequestAuthCode = true,
			WebClientId = clientId,
#if UNITY_EDITOR || UNITY_STANDALONE
			ClientSecret = clientSecret,
#endif
        };
#endif
        
#if AIRSHIP_ANDROID_DEBUG
        GoogleSignIn.DefaultInstance.EnableDebugLogging(true);
#endif

        var accessToken = "";
        
#if UNITY_ANDROID
		var (user, err) = await AuthWithGoogleAndroid();
		if (err != null) {
			return (false, err);
		}

		var accessTokenRes = await auth.ExchangeCodeForAccessTokenAsync($"http://localhost?code={user.AuthCode}");
		accessToken = accessTokenRes.accessToken;
#else
        var crossPlatformBrowser = new CrossPlatformBrowser();
        var standaloneBrowser = new StandaloneBrowser();
        #if UNITY_EDITOR
			var returnApp = "Unity";
		#else
			var returnApp = "Airship";
		#endif
        standaloneBrowser.closePageResponse =
	        $"<html><head><meta http-equiv=\"refresh\" content=\"0;url=https://create.airship.gg/welcome\"></head><body><b>Success!</b><br>Redirecting to Airship...</body></html>";

        crossPlatformBrowser.platformBrowsers.Add(RuntimePlatform.WindowsEditor, standaloneBrowser);
        crossPlatformBrowser.platformBrowsers.Add(RuntimePlatform.WindowsPlayer, standaloneBrowser);
        crossPlatformBrowser.platformBrowsers.Add(RuntimePlatform.OSXEditor, standaloneBrowser);
        crossPlatformBrowser.platformBrowsers.Add(RuntimePlatform.OSXPlayer, standaloneBrowser);
        crossPlatformBrowser.platformBrowsers.Add(RuntimePlatform.IPhonePlayer, new ASWebAuthenticationSessionBrowser());

        using var authenticationSession = new AuthenticationSession(auth, crossPlatformBrowser);

        // Opens a browser to log user in
        AccessTokenResponse accessTokenResponse = await authenticationSession.AuthenticateAsync();
		accessToken = accessTokenResponse.accessToken;
#endif
        if (accessToken != "") {
            var reqBody = new SignInWithIdpRequest() {
                postBody = "access_token=" + accessToken + "&providerId=google.com",
                requestUri = "http://localhost",
                returnSecureToken = true
            };
            
            using UnityWebRequest req = UnityWebRequest.Post(
                $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={AirshipApp.firebaseApiKey}",
                JsonUtility.ToJson(reqBody),
                "application/json");
            await req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.ProtocolError) {
                return (false, "Authentication request failed");
            }

            try {
                var data = JsonUtility.FromJson<LoginResponse>(req.downloadHandler.text);
                AuthManager.SaveAuthAccount(data.refreshToken);
                InternalHttpManager.SetAuthToken(data.idToken);
                #if UNITY_EDITOR
                InternalHttpManager.editorUserId = data.localId;
                InternalHttpManager.SetEditorAuthToken(data.idToken);
                #endif
                
                StateManager.SetString("firebase_refreshToken", data.refreshToken);
                authed?.Invoke();
                return (true, "");
            } catch (Exception e) {
                Debug.LogError(e);
                return (false, "Failed to login. Error Code: Air-2. Please try again.");
            }
        } else {
            // login cancelled
            Debug.Log("Login cancelled.");
            return (false, ""); // Don't return a display error
        }
	}

#if UNITY_ANDROID
	private static Task<(GoogleSignInUser user, string error)> AuthWithGoogleAndroid() {
		GoogleSignIn.Configuration.UseGameSignIn = false;

		return GoogleSignIn.DefaultInstance.SignIn().ContinueWith(OnAuthGoogleAndroidFinished, TaskScheduler.FromCurrentSynchronizationContext());
	}

	private static (GoogleSignInUser user, string error) OnAuthGoogleAndroidFinished(Task<GoogleSignInUser> task) {
		if (task.IsFaulted) {
			// Attempt to get the SignInException and return the message:
			if (task.Exception != null) {
				using var enumerator = task.Exception.InnerExceptions.GetEnumerator();
				if (enumerator.MoveNext()) {
					var err = (GoogleSignIn.SignInException)enumerator.Current;
					return (null, $"{err!.Status}: {err!.Message}");
				}
			}
			return (null, "Unknown sign in exception");
		}
		
		if (task.IsCanceled) {
			return (null, "Sign in cancelled");
		}

		return (task.Result, null);
	}
#endif
}
