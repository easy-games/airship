using System;
using System.Collections.Generic;
using Cdm.Authentication.Browser;
using Cdm.Authentication.Clients;
using Cdm.Authentication.OAuth2;
using Code.Http.Internal;
using MiniJSON;
using Proyecto26;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoginApp : MonoBehaviour {
    public GameObject loginPage;
    public GameObject pickUsernamePage;

    private void OnEnable() {
        Cursor.lockState = CursorLockMode.None;

        StateManager.Clear();
        SocketManager.Disconnect();

        if (SystemRoot.Instance.isActiveAndEnabled) {}
        RouteToPage(this.loginPage);
    }

    public void RouteToPage(GameObject pageGameObject) {
        loginPage.SetActive(false);
        pickUsernamePage.SetActive(false);
        pageGameObject.SetActive(true);
    }

    public async void PressContinueWithGoogle() {
        var auth = new GoogleAuth(new AuthorizationCodeFlow.Configuration() {
            clientId = "987279961241-0mjidme48us0fis0vtqk4jqrsmk7ar0n.apps.googleusercontent.com",

            // Why we include this: https://stackoverflow.com/a/73779731
            clientSecret = "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",

            redirectUri = "http://localhost:8080",
            // redirectUri = "airship://loginRedirect",
            scope = "openid email"
        });
        using var authenticationSession = new AuthenticationSession(auth, new StandaloneBrowser() {
            closePageResponse = "<html><body><b>Success!</b><br>Please close this window and return to Airship.</body></html>"
        });

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
                        this.RouteToPage(this.pickUsernamePage);
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