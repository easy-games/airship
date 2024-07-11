using System;
using System.Collections.Generic;
using AppleAuth.Interfaces;
using Cdm.Authentication.Browser;
using Cdm.Authentication.Clients;
using Cdm.Authentication.OAuth2;
using Code.Http.Internal;
using Code.Platform.Shared;
using ElRaccoone.Tweens;
using Proyecto26;
#if STEAMWORKS_NET
using Steamworks;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoginApp : MonoBehaviour {
    [Header("Desktop")]
    [SerializeField] public Canvas desktopCanvas;
    [SerializeField] public GameObject loginPage;
    [SerializeField] public GameObject pickUsernamePage;
    [SerializeField] public Button appleBtn;
    [SerializeField] public GameObject errorMessage;
    [SerializeField] public TMP_Text errorMessageText;
    [SerializeField] public LoginButton steamLoginButton;

    [Header("Mobile")]
    [SerializeField] public Canvas mobileCanvas;
    [SerializeField] public RectTransform mobileBottom;
    [SerializeField] public GameObject mobileLoginPage;
    [SerializeField] public GameObject mobilePickUsernamePage;
    [SerializeField] public GameObject mobileLoadingPage;
    [SerializeField] public GameObject mobileErrorMessage;
    [SerializeField] public TMP_Text mobileErrorMessageText;

    [Header("Configuration")] [SerializeField]
    public bool mockBackend;

    [Header("State")]
    [SerializeField] public bool loading;

    private bool mobileMode = false;
    private int screenWidth;
    private int screenHeight;
    private bool showedNoInternet = false;

    private void OnEnable() {
        Cursor.lockState = CursorLockMode.None;

        var device = DeviceBridge.GetDeviceType();
        if (device == AirshipDeviceType.Phone) {
            Screen.orientation = ScreenOrientation.Portrait;
        } else {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }
#if !UNITY_IOS
        this.appleBtn.gameObject.SetActive(false);
#endif
        Application.targetFrameRate = (int)Math.Ceiling(Screen.currentResolution.refreshRateRatio.value);

        StateManager.Clear();
        SocketManager.Disconnect();

        if (SystemRoot.Instance.isActiveAndEnabled) {}
        CalcLayout();
        RouteToPage(this.mobileMode ? this.mobileLoginPage : this.loginPage, false, true);

        this.steamLoginButton.SetLoading(false);
        this.CloseError();
    }

    private void Update() {
        if (Screen.width != this.screenWidth || Screen.height != this.screenHeight) {
            this.CalcLayout();
        }

        this.mobileLoadingPage.SetActive(this.loading);

        if (Application.internetReachability == NetworkReachability.NotReachable) {
            if (!this.showedNoInternet) {
                this.showedNoInternet = true;
                this.SetError("Your internet connection is offline.");
            }
        } else {
            this.showedNoInternet = false;
        }
    }

    private void CalcLayout() {
        this.screenWidth = Screen.width;
        this.screenHeight = Screen.height;

        var deviceType = DeviceBridge.GetDeviceType();
        SetMobileMode(deviceType == AirshipDeviceType.Phone);
    }

    private void SetMobileMode(bool val) {
        this.mobileMode = val;
        this.desktopCanvas.gameObject.SetActive(!val);
        this.mobileCanvas.gameObject.SetActive(val);
    }

    public void CloseError() {
        this.mobileErrorMessage.SetActive(false);
        this.errorMessage.SetActive(false);
    }

    public void SetError(string msg) {
        this.mobileErrorMessage.SetActive(true);
        this.mobileErrorMessageText.text = msg;

        this.errorMessage.SetActive(true);
        this.errorMessageText.text = msg;
    }

    public void StopLoading() {
        this.loading = false;
    }

    public void RouteToPage(GameObject pageGameObject, bool fullScreen, bool instant = false) {
        if (this.mobileMode) {
            if (fullScreen) {
                NativeTween.OffsetMax(mobileBottom, new Vector2(0, Screen.height * 0.64f), instant ? 0f : 0.12f);
            } else {
                NativeTween.OffsetMax(mobileBottom, new Vector2(0, Screen.height * 0.4f), instant ? 0f : 0.12f);
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
        // if (this.mockBackend && Application.isEditor) {
        //     this.RouteToPage(this.mobileMode ? this.mobilePickUsernamePage : this.pickUsernamePage, true);
        //     return;
        // }

        this.loading = true;

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
                        this.loading = false;
                        this.SetError("Failed to fetch account. Error Code: Air-3. Please try again.");
                        Debug.LogError("Failed to get self: " + selfRes.error);
                        return;
                    }

                    if (selfRes.data.Length == 0) {
                        this.loading = false;
                        this.RouteToPage(this.mobileMode ? this.mobilePickUsernamePage : this.pickUsernamePage, true);
                        return;
                    }
                    this.loading = false;
                    SceneManager.LoadScene("MainMenu");
                } catch (Exception e) {
                    Debug.LogError(e);
                    this.SetError("Failed to login. Error Code: Air-2. Please try again.");
                    this.loading = false;
                    // todo: display error
                }
            }).Catch((err) => {
                Debug.LogError("Failed login.");
                Debug.LogError(err.Message);
                Debug.LogError(err);
                this.SetError("Failed to reach login servers. Error Code: Air-1. Please try again.");
                this.loading = false;
            });
        } else {
            // login cancelled
            this.loading = false;
        }
    }

    public async void AuthenticateFirebaseWithApple(IAppleIDCredential credential) {
        var reqBody = new SignInWithIdpRequest() {
            postBody = "id_token=" + System.Text.Encoding.Default.GetString(credential.IdentityToken) + "&providerId=apple.com",
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
                print("Firebase res: " + res.Text);
                var data = JsonUtility.FromJson<LoginResponse>(res.Text);
                AuthManager.SaveAuthAccount(data.refreshToken);
                InternalHttpManager.SetAuthToken(data.idToken);
                StateManager.SetString("firebase_refreshToken", data.refreshToken);

                var selfRes = await InternalHttpManager.GetAsync(AirshipApp.gameCoordinatorUrl + "/users/self");
                if (!selfRes.success) {
                    Debug.LogError("Failed to get self: " + selfRes.error);
                    this.SetError("Failed to login with Apple. Error Code: Air-4");
                    this.loading = false;
                    return;
                }

                if (selfRes.data.Length == 0) {
                    this.RouteToPage(this.mobileMode ? this.mobilePickUsernamePage : this.pickUsernamePage, true);
                    return;
                }
                SceneManager.LoadScene("MainMenu");
            } catch (Exception e) {
                Debug.LogError(e);
                this.SetError("Failed to login with Apple. Error Code: Air-5");
                this.loading = false;
                // todo: display error
            }

        }).Catch((err) => {
            Debug.LogError("Failed apple auth with firebase: " + err.Message);
            this.SetError("Failed to login with Apple. Error Code: Air-6");
            this.loading = false;
        });
    }

    public async void AuthenticateFirebaseWithSteam() {
        this.steamLoginButton.SetLoading(true);
        this.loading = true;
#if STEAMWORKS_NET
        SteamUser.GetAuthTicketForWebApi("airship");
#endif
        print("waiting for token...");
        var steamToken = await SteamLuauAPI.Instance.GetSteamTokenAsync();
        print("got steam token: " + steamToken);

        if (string.IsNullOrEmpty(steamToken)) {
            this.loading = false;
            this.steamLoginButton.SetLoading(false);
            return;
        }

        RestClient.Get(new RequestHelper() {
            Uri = AirshipUrl.GameCoordinator + "/auth/steam/in-game",
            Headers = new Dictionary<string, string>() {
                { "Authorization", steamToken }
            },
        }).Then((gcRes) => {
            print("gc response: " + gcRes.Text);
            var gcData = JsonUtility.FromJson<SteamInGameLoginResponse>(gcRes.Text);

            // Now we send token to firebase for final auth
            var reqBody = new FirebaseSignInWithCustomToken() {
                token = gcData.firebaseToken,
                returnSecureToken = true
            };

            RestClient.Post(new RequestHelper() {
                Uri = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithCustomToken",
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
                        this.loading = false;
                        this.steamLoginButton.SetLoading(false);
                        this.SetError("Failed to fetch account. Error Code: Air-3. Please try again.");
                        Debug.LogError("Failed to get self: " + selfRes.error);
                        return;
                    }

                    if (selfRes.data.Length == 0) {
                        this.loading = false;
                        this.RouteToPage(this.mobileMode ? this.mobilePickUsernamePage : this.pickUsernamePage, true);
                        this.steamLoginButton.SetLoading(false);
                        return;
                    }

                    this.loading = false;
                    this.steamLoginButton.SetLoading(false);
                    SceneManager.LoadScene("MainMenu");
                } catch (Exception e) {
                    Debug.LogError(e);
                    this.SetError("Failed to login. Error Code: Air-2. Please try again.");
                    this.loading = false;
                    this.steamLoginButton.SetLoading(false);
                    // todo: display error
                }
            }).Catch((err) => {
                Debug.LogError("Failed login.");
                Debug.LogError(err.Message);
                Debug.LogError(err);
                this.SetError("Failed to reach login servers. Error Code: Air-1. Please try again.");
                this.loading = false;
                this.steamLoginButton.SetLoading(false);
            });
        }).Catch((err) => {
            Debug.LogError("Failed in-game steam login: " + err);
            this.SetError("Failed to login with steam.");
            this.loading = false;
            this.steamLoginButton.SetLoading(false);
        });
    }

    public void OpenPrivacyPolicy() {
        Application.OpenURL("https://staging.airship.gg/privacy");
    }

    public void OpenTermsAndConditions() {
        Application.OpenURL("https://staging.airship.gg/tos");
    }
}