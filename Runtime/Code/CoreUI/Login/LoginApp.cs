using System.Collections.Generic;
using Cdm.Authentication.Browser;
using Cdm.Authentication.Clients;
using Cdm.Authentication.OAuth2;
using Code.Http.Internal;
using Proyecto26;
using UnityEngine;

public class LoginApp : MonoBehaviour {
    public GameObject loginPage;
    public GameObject pickUsernamePage;

    private void OnEnable() {
        Cursor.lockState = CursorLockMode.None;
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

        print("Got access token: " + accessTokenResponse.accessToken);

        if (accessTokenResponse.accessToken != "") {
            var reqBody = new SignInWithIdpRequest() {
                postBody = "access_token=" + accessTokenResponse.accessToken + "&providerId=google.com",
                requestUri = "http://localhost",
                returnSecureToken = true
            };
            RestClient.Post(new RequestHelper() {
                Uri = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp",
                Params = new Dictionary<string, string>() {
                    { "key", AirshipApp.firebaseApiKey },
                },
                ContentType = "application/json",
                BodyString = JsonUtility.ToJson(reqBody),
            }).Then(async (res) => {
                print("Res: " + res.Text);

                var data = JsonUtility.FromJson<LoginResponse>(res.Text);
                AuthManager.SaveAuthAccount(data.refreshToken);
                InternalHttpManager.SetAuthToken(data.idToken);

                var selfRes = await InternalHttpManager.GetAsync(AirshipApp.gameCoordinatorUrl + "/users/self");
                print("Self: " + selfRes.data);

                this.RouteToPage(this.pickUsernamePage);

                // print("Routing to main menu...");
                // SceneManager.LoadScene("MainMenu");
            }).Catch((err) => {
                Debug.LogError(err.Message);
                Debug.LogError(err);
            });
        }
    }
}