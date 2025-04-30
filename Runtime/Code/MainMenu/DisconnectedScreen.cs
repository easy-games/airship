using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DisconnectedScreen : MonoBehaviour {
    public TMP_Text reasonText;
    public GameObject continueButton;
    public GameObject logoutButton;
    public GameObject quitButton;

    private void Start() {
        this.reasonText.text = CrossSceneState.kickMessage;
        Cursor.lockState = CursorLockMode.None;
        if (CrossSceneState.kickForceLogout) {
            CrossSceneState.kickForceLogout = false;
            this.continueButton.SetActive(false);
            this.logoutButton.SetActive(true);
            this.quitButton.SetActive(true);
        } else {
            this.continueButton.SetActive(true);
            this.logoutButton.SetActive(false);
            this.quitButton.SetActive(false);
        }
    }

    public void ContinueButton_OnClick() {
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitButton_OnClick() {
        Application.Quit();
    }

    public void LogoutButton_OnClick() {
        AuthManager.ClearSavedAccount();
        SceneManager.LoadScene("Login");
    }
}