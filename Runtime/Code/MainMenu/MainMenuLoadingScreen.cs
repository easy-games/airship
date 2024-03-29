using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using Cursor = UnityEngine.Cursor;

public class MainMenuLoadingScreen : BundleLoadingScreen
{
    public Canvas canvas;
    public TMP_Text progressText;
    public GameObject spinner;
    public GameObject errorWrapper;
    public TMP_Text errorText;
    public MainMenuSceneManager sceneManager;

    private void Start()
    {
        if (!RunCore.IsClient())
        {
            Close();
            return;
        }

        canvas.enabled = true;
        this.errorWrapper.SetActive(false);
        this.spinner.SetActive(true);
        this.progressText.gameObject.SetActive(true);
    }

    private void OnEnable() {
        canvas.enabled = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Retry() {
        this.spinner.SetActive(true);
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