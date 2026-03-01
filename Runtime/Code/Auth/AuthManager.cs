using System;
using System.IO;
using System.Threading.Tasks;
using Cdm.Authentication.Browser;
using Cdm.Authentication.Clients;
using Cdm.Authentication.OAuth2;
using Code.Bootstrap;
using Code.Http.Internal;
#if UNITY_ANDROID
using Google;
#endif
using JetBrains.Annotations;
using Proyecto26;
using RSG;
using Sentry;
using UnityEngine;
using UnityEngine.Networking;

[LuauAPI(LuauContext.Protected)]
public class AuthManager {
    public static Action authed;

    public static string uid;
    public static string username;

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

	/**
	 * Additional information about the user that is sent from TS.
	 *
	 * Base info (such as device info) is done in AirshipEntryPoint.cs when app first starts.
	 */
	public static void SetUserInfo(string uid, string username) {
		AuthManager.uid = uid;
		AuthManager.username = username;
#if AIRSHIP_PLAYER
		SentrySdk.ConfigureScope(scope => {
			scope.User.Id = uid;
			scope.User.Username = username;
		});
#endif
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
        string clientId = "987279961241-h6oaa4rf9gsb8mf6ibs54mhtmsol9ptv.apps.googleusercontent.com";
        string clientSecret = "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";
#else
		string clientId = "457451560440-36788k4o8jmnce49c9fv8otkqv1kqhu9.apps.googleusercontent.com";
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
		
#if UNITY_ANDROID && !UNITY_EDITOR
#if AIRSHIP_STAGING
		clientId = "987279961241-0mjidme48us0fis0vtqk4jqrsmk7ar0n.apps.googleusercontent.com";
		clientSecret = null;
#else
        clientId = "457451560440-fvhufuvt3skas9m046jqin0l10h8uaph.apps.googleusercontent.com";
		clientSecret = null;
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
		GoogleSignIn.Configuration ??= new GoogleSignInConfiguration() {
			RequestEmail = true,
			RequestProfile = true,
			RequestIdToken = true,
			WebClientId = clientId,
		};
#endif
        
#if AIRSHIP_ANDROID_DEBUG
        GoogleSignIn.DefaultInstance.EnableDebugLogging(true);
#endif

        var accessToken = "";
        
#if UNITY_ANDROID && !UNITY_EDITOR
		var (user, err) = await AuthWithGoogleAndroid();
		if (err != null) {
			return (false, err);
		}

		// ID token can also be used for sign-in with firebase. AuthWithGoogleAndroid will return this
		// instead so we don't have to exchange an auth code for an access token.
		accessToken = user.IdToken;
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

#if UNITY_EDITOR_LINUX
		crossPlatformBrowser.platformBrowsers.Add(RuntimePlatform.LinuxEditor, standaloneBrowser);
#endif
        
        using var authenticationSession = new AuthenticationSession(auth, crossPlatformBrowser);

        // Opens a browser to log user in
        try {
	        AccessTokenResponse accessTokenResponse = await authenticationSession.AuthenticateAsync();
	        accessToken = accessTokenResponse.accessToken;
        } catch (Exception e) {
			Debug.LogError(e);
			return (false, e.Message);
        }
#endif
        if (accessToken != "") {
            var reqBody = new SignInWithIdpRequest() {
	            #if UNITY_ANDROID && !UNITY_EDITOR
	            // Android sign-in returns an ID token instead of an access token.
	            postBody = "id_token=" + accessToken + "&providerId=google.com",
	            #else
	            postBody = "access_token=" + accessToken + "&providerId=google.com",
	            #endif
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
