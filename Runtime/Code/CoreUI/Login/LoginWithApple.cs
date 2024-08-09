using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Extensions;
using AppleAuth.Interfaces;
using AppleAuth.Native;
using UnityEngine;
using UnityEngine.UI;

public class LoginWithApple : MonoBehaviour {
    [SerializeField] public LoginApp loginApp;
    [SerializeField] public Button loginWithAppleButton;

    #if AIRSHIP_STAGING
    private const string AppleUserIdKey = "AirshipAppleUserId_Staging";
    #else
    private const string AppleUserIdKey = "AirshipAppleUserId";
    #endif

    private IAppleAuthManager _appleAuthManager;

    // public LoginMenuHandler LoginMenu;
    // public GameMenuHandler GameMenu;

    private void Start() {
        // If the current platform is supported
        if (AppleAuthManager.IsCurrentPlatformSupported) {
            // Creates a default JSON deserializer, to transform JSON Native responses to C# instances
            var deserializer = new PayloadDeserializer();
            // Creates an Apple Authentication manager with the deserializer
            this._appleAuthManager = new AppleAuthManager(deserializer);
        }

        this.InitializeLoginMenu();
    }

    private void Update() {
        // Updates the AppleAuthManager instance to execute
        // pending callbacks inside Unity's execution loop
        if (this._appleAuthManager != null) {
            this._appleAuthManager.Update();
        }
    }

    public void SignInWithAppleButtonPressed() {
        this.SignInWithApple();
    }

    private void InitializeLoginMenu() {
        // Check if the current platform supports Sign In With Apple
        if (this._appleAuthManager == null) {
            this.loginWithAppleButton.gameObject.SetActive(false);
            return;
        }

        // If at any point we receive a credentials revoked notification, we delete the stored User ID, and go back to login
        this._appleAuthManager.SetCredentialsRevokedCallback(result => {
            Debug.Log("Received revoked callback " + result);
            // this.SetupLoginMenuForSignInWithApple();
            // PlayerPrefs.DeleteKey(AppleUserIdKey);
        });
    }

    private void SignInWithApple() {
        this.loginApp.loading = true;
        var loginArgs = new AppleAuthLoginArgs(LoginOptions.IncludeEmail | LoginOptions.IncludeFullName);

        this._appleAuthManager.LoginWithAppleId(
            loginArgs,
            credential => {
                if (credential is IAppleIDCredential appleIdCredential) {
                    // If a sign in with apple succeeds, we should have obtained the credential with the user id, name, and email, save it
                    print("Apple credential. id_token=" + System.Text.Encoding.Default.GetString(appleIdCredential.IdentityToken));
                    PlayerPrefs.SetString(AppleUserIdKey, credential.User);
                    this.loginApp.AuthenticateFirebaseWithApple(appleIdCredential);
                } else {
                    this.loginApp.SetError("Failed to read login credentials. Please try again.");
                    this.loginApp.loading = false;
                }
            },
            error => {
                var authorizationErrorCode = error.GetAuthorizationErrorCode();
                Debug.LogWarning("Sign in with Apple failed " + authorizationErrorCode.ToString() + " " + error.ToString());
                if (authorizationErrorCode == AuthorizationErrorCode.Canceled) {
                    this.loginApp.loading = false;
                } else {
                    this.loginApp.SetError("Failed to login. Error Code: " + authorizationErrorCode);
                    this.loginApp.loading = false;
                }
            });
    }
}