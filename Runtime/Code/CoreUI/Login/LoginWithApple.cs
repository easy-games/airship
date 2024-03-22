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

    private const string AppleUserIdKey = "AppleUserId";

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

        // this.LoginMenu.UpdateLoadingMessage(Time.deltaTime);
    }

    public void SignInWithAppleButtonPressed()
    {
        // this.SetupLoginMenuForAppleSignIn();
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

        // If we have an Apple User Id available, get the credential status for it
        // if (PlayerPrefs.HasKey(AppleUserIdKey)) {
        //     var storedAppleUserId = PlayerPrefs.GetString(AppleUserIdKey);
        //     // this.SetupLoginMenuForCheckingCredentials();
        //     this.CheckCredentialStatusForUserId(storedAppleUserId);
        // }
        // // If we do not have an stored Apple User Id, attempt a quick login
        // else {
        //     // this.SetupLoginMenuForQuickLoginAttempt();
        //     this.AttemptQuickLogin();
        // }
    }

    // private void CheckCredentialStatusForUserId(string appleUserId)
    // {
    //     // If there is an apple ID available, we should check the credential state
    //     this._appleAuthManager.GetCredentialState(
    //         appleUserId,
    //         state =>
    //         {
    //             switch (state)
    //             {
    //                 // If it's authorized, login with that user id
    //                 case CredentialState.Authorized:
    //                     this.SetupGameMenu(appleUserId, null);
    //                     return;
    //
    //                 // If it was revoked, or not found, we need a new sign in with apple attempt
    //                 // Discard previous apple user id
    //                 case CredentialState.Revoked:
    //                 case CredentialState.NotFound:
    //                     this.SetupLoginMenuForSignInWithApple();
    //                     PlayerPrefs.DeleteKey(AppleUserIdKey);
    //                     return;
    //             }
    //         },
    //         error =>
    //         {
    //             var authorizationErrorCode = error.GetAuthorizationErrorCode();
    //             Debug.LogWarning("Error while trying to get credential state " + authorizationErrorCode.ToString() + " " + error.ToString());
    //             this.SetupLoginMenuForSignInWithApple();
    //         });
    // }

    private void AttemptQuickLogin() {
        var quickLoginArgs = new AppleAuthQuickLoginArgs();

        // Quick login should succeed if the credential was authorized before and not revoked
        this._appleAuthManager.QuickLogin(
            quickLoginArgs,
            credential => {
                // If it's an Apple credential, save the user ID, for later logins
                if (credential is IAppleIDCredential appleIdCredential) {
                    PlayerPrefs.SetString(AppleUserIdKey, credential.User);
                    this.loginApp.AuthenticateFirebaseWithApple(appleIdCredential);
                }
                // this.SetupGameMenu(credential.User, credential);
            },
            error => {
                // If Quick Login fails, we should show the normal sign in with apple menu, to allow for a normal Sign In with apple
                var authorizationErrorCode = error.GetAuthorizationErrorCode();
                Debug.LogWarning("Quick Login Failed " + authorizationErrorCode.ToString() + " " + error.ToString());
                // this.SetupLoginMenuForSignInWithApple();
            });
    }

    private void SignInWithApple() {
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
                    Debug.LogError("Failed to parse apple credential.");
                }
            },
            error => {
                var authorizationErrorCode = error.GetAuthorizationErrorCode();
                Debug.LogWarning("Sign in with Apple failed " + authorizationErrorCode.ToString() + " " + error.ToString());
                // this.SetupLoginMenuForSignInWithApple();
            });
    }
}