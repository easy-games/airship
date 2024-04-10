using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Cursor = UnityEngine.Cursor;

public class MainMenuLoadingScreen : BundleLoadingScreen
{
    public Canvas canvas;
    public TMP_Text progressText;
    public Button continueButton;
    public GameObject spinner;
    public GameObject errorWrapper;
    public TMP_Text errorText;
    public MainMenuSceneManager sceneManager;

    private void Start() {
        base.showContinueButton = true;
        if (!RunCore.IsClient()) {
            Close();
            return;
        }

        canvas.enabled = true;
        this.errorWrapper.SetActive(false);
        this.spinner.SetActive(true);
        this.progressText.gameObject.SetActive(true);
        this.continueButton.gameObject.SetActive(false);
    }

    public override void SetTotalDownloadSize(long sizeBytes) {
        var sizeMb = sizeBytes / 1_000_000;

#if UNITY_IOS || UNITY_ANDROID
        this.SetProgress($"A {sizeMb}MB update is required.\nWould you like to continue?", 0);
        this.spinner.SetActive(false);
        this.continueButton.gameObject.SetActive(true);
        return;
#endif

        // auto accept on PC
        BundleDownloader.Instance.downloadAccepted = true;
    }

    public void ClickContinueButton() {
        BundleDownloader.Instance.downloadAccepted = true;
        this.continueButton.gameObject.SetActive(false);
        this.spinner.SetActive(true);
    }

    private void OnEnable() {
        canvas.enabled = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Retry() {
        this.spinner.SetActive(true);
        this.continueButton.gameObject.SetActive(false);
        this.progressText.gameObject.SetActive(true);

        this.errorWrapper.SetActive(false);
        this.sceneManager.Retry();
    }

    public void SetError(string msg) {
        this.spinner.SetActive(false);
        this.progressText.gameObject.SetActive(false);

        this.errorText.text = msg;
        this.errorWrapper.SetActive(true);
    }

    public override void SetProgress(string text, float percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        progressText.text = text;
    }

    public void Close()
    {
        canvas.enabled = false;
    }
}