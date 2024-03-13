using System;
using System.Collections.Generic;
using Cdm.Authentication.Browser;
using Cdm.Authentication.Clients;
using Cdm.Authentication.OAuth2;
using Code.Http.Internal;
using ElRaccoone.Tweens;
using MiniJSON;
using Proyecto26;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoginApp : MonoBehaviour {
    [Header("Desktop")]
    [SerializeField] public Canvas desktopCanvas;
    [SerializeField] public GameObject loginPage;
    [SerializeField] public GameObject pickUsernamePage;

    [Header("Mobile")]
    [SerializeField] public Canvas mobileCanvas;
    [SerializeField] public RectTransform mobileBottom;
    [SerializeField] public GameObject mobileLoginPage;
    [SerializeField] public GameObject mobilePickUsernamePage;

    [Header("Configuration")] [SerializeField]
    public bool mockBackend;

    private bool mobileMode = false;
    private int screenWidth;
    private int screenHeight;

    private void OnEnable() {
        Cursor.lockState = CursorLockMode.None;

#if UNITY_IOS || UNITY_ANDROID
        Screen.orientation = ScreenOrientation.Portrait;
#endif

        StateManager.Clear();
        SocketManager.Disconnect();

        if (SystemRoot.Instance.isActiveAndEnabled) {}
        CalcLayout();
        RouteToPage(this.mobileMode ? this.mobileLoginPage : this.loginPage, false, true);
    }

    private void Update() {
        if (Screen.width != this.screenWidth || Screen.height != this.screenHeight) {
            this.CalcLayout();
        }
    }

    private void CalcLayout() {
        this.screenWidth = Screen.width;
        this.screenHeight = Screen.height;

        SetMobileMode(this.screenWidth < this.screenHeight);
    }

    private void SetMobileMode(bool val) {
        this.mobileMode = val;
        this.desktopCanvas.gameObject.SetActive(!val);
        this.mobileCanvas.gameObject.SetActive(val);
    }

    public void RouteToPage(GameObject pageGameObject, bool fullScreen, bool instant = false) {
        if (this.mobileMode) {
            if (fullScreen) {
                this.mobileBottom.TweenSizeDelta(new Vector2(Screen.width, Screen.height * 0.7f),  instant ? 0f : 0.12f);
            } else {
                this.mobileBottom.TweenSizeDelta(new Vector2(Screen.width, Screen.height * 0.4f), instant ? 0f : 0.12f);
            }
            this.mobileLoginPage.SetActive(false);
            this.mobilePickUsernamePage.SetActive(false);
            pageGameObject.SetActive(true);
            return;
        }
        loginPage.SetActive(false);
        pickUsernamePage.SetActive(false);
        pageGameObject.SetActive(true);
    }

    public async void PressContinueWithGoogle() {
#if UNITY_EDITOR
        if (this.mockBackend) {
            this.RouteToPage(this.mobileMode ? this.mobilePickUsernamePage : this.pickUsernamePage, true);
            return;
        }
#endif

        string clientId = "987279961241-0mjidme48us0fis0vtqk4jqrsmk7ar0n.apps.googleusercontent.com";
        string clientSecret = "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";
        string redirectUri = "http://localhost:8080";
#if UNITY_IOS && !UNITY_EDITOR
        clientId = "987279961241-e2klb9k8ikdkh12ja6m93uulm8mkmme7.apps.googleusercontent.com";
        clientSecret = null;
        redirectUri = "gg.easy.airship:/oauth2";
#endif
        print("RedirectURI: " + redirectUri);

        var auth = new GoogleAuth(new AuthorizationCodeFlow.Configuration() {
            clientId = clientId,

            // Why we include this: https://stackoverflow.com/a/73779731
            clientSecret = clientSecret,

            redirectUri = redirectUri,
            scope = "openid email"
        });

        var crossPlatformBrowser = new CrossPlatformBrowser();
        var standaloneBrowser = new StandaloneBrowser();
        standaloneBrowser.closePageResponse =
            "<html><body><b>Success!</b><br>Please close this window and return to Airship.</body></html>";

        crossPlatformBrowser.platformBrowsers.Add(RuntimePlatform.WindowsEditor, standaloneBrowser);
        crossPlatformBrowser.platformBrowsers.Add(RuntimePlatform.WindowsPlayer, standaloneBrowser);
        crossPlatformBrowser.platformBrowsers.Add(RuntimePlatform.OSXEditor, standaloneBrowser);
        crossPlatformBrowser.platformBrowsers.Add(RuntimePlatform.OSXPlayer, standaloneBrowser);
        crossPlatformBrowser.platformBrowsers.Add(RuntimePlatform.IPhonePlayer, new ASWebAuthenticationSessionBrowser());

        using var authenticationSession = new AuthenticationSession(auth, crossPlatformBrowser);

        // Opens a browser to log user in
        AccessTokenResponse accessTokenResponse = await authenticationSession.AuthenticateAsync();

        if (accessTokenResponse.accessToken != "") {
            var reqBody = new SignInWithIdpRequest() {
                postBody = "access_token=" + accessTokenResponse.accessToken + "&providerId=google.com",
                requestUri = "http://localhost",
                returnSecureToken = true
            };

            print("posting...");
            RestClient.Post(new RequestHelper() {
                Uri = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp",
                Params = new Dictionary<string, string>() {
                    { "key", AirshipApp.firebaseApiKey },
                },
                ContentType = "application/json",
                BodyString = JsonUtility.ToJson(reqBody),
            }).Then(async (res) => {
                try {
                    var data = JsonUtility.FromJson<LoginResponse>(res.Text);
                    AuthManager.SaveAuthAccount(data.refreshToken);
                    InternalHttpManager.SetAuthToken(data.idToken);
                    StateManager.SetString("firebase_refreshToken", data.refreshToken);

                    var selfRes = await InternalHttpManager.GetAsync(AirshipApp.gameCoordinatorUrl + "/users/self");
                    if (!selfRes.success) {
                        Debug.LogError("Failed to get self: " + selfRes.error);
                        return;
                    }

                    if (selfRes.data.Length == 0) {
                        this.RouteToPage(this.mobileMode ? this.mobilePickUsernamePage : this.pickUsernamePage, true);
                        return;
                    }
                    SceneManager.LoadScene("MainMenu");
                } catch (Exception e) {
                    Debug.LogError(e);
                    // todo: display error
                }
            }).Catch((err) => {
                Debug.LogError("Failed login.");
                Debug.LogError(err.Message);
                Debug.LogError(err);
            });
        }
    }
}