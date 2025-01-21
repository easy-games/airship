using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DisconnectedScreen : MonoBehaviour {
    public TMP_Text reasonText;

    private void Start() {
        this.reasonText.text = CrossSceneState.kickMessage;
        Cursor.lockState = CursorLockMode.None;
    }

    public void ContinueButton_OnClick() {
        SceneManager.LoadScene("MainMenu");
    }
}