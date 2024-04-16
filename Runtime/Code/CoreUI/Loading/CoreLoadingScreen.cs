using System;
using System.Collections;
using FishNet;
using FishNet.Managing.Scened;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;
using Cursor = UnityEngine.Cursor;
using SceneManager = UnityEngine.SceneManagement.SceneManager;
using Screen = UnityEngine.Device.Screen;

[LuauAPI]
public class CoreLoadingScreen : BundleLoadingScreen
{
    private Canvas _canvas;
    public TMP_Text progressText;
    public Button disconnectButton;
    public Button continueButton;
    public GameObject spinner;
    
    private void Awake() {
        base.showContinueButton = true;
        _canvas = GetComponent<Canvas>();
    }

    private void Start() {
        if (!RunCore.IsClient()) {
            Close();
            return;
        }

        Screen.orientation = ScreenOrientation.LandscapeLeft;

        var deviceInfo = DeviceBridge.GetDeviceType();
        if (deviceInfo is AirshipDeviceType.Phone or AirshipDeviceType.Tablet) {
            var t = this.disconnectButton.transform as RectTransform;
            t.anchoredPosition = new Vector2(-50, 50);
            t.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        }

        _canvas.enabled = true;
        this.continueButton.gameObject.SetActive(false);
        this.spinner.SetActive(true);
        
        SetProgress("Connecting to Server", 5);
        InstanceFinder.SceneManager.OnLoadPercentChange += OnLoadPercentChanged;
        InstanceFinder.SceneManager.OnLoadEnd += OnLoadEnd;

        disconnectButton.onClick.AddListener(DisconnectButton_OnClicked);
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

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnDisable()
    {
        if (InstanceFinder.SceneManager)
        {
            InstanceFinder.SceneManager.OnLoadPercentChange -= OnLoadPercentChanged;
            InstanceFinder.SceneManager.OnLoadEnd -= OnLoadEnd;   
        }

        if (RunCore.IsClient())
        {
            disconnectButton.onClick.RemoveListener(DisconnectButton_OnClicked);
        }
    }

    private void DisconnectButton_OnClicked() {
        TransferManager.Instance.Disconnect();
    }

    private void OnLoadPercentChanged(SceneLoadPercentEventArgs e) {
        // SetProgress("Opening Game", 45 + e.Percent * 5);
    }

    private void OnLoadEnd(SceneLoadEndEventArgs e)
    {
        // SetLabel("Opening Bundle", 100);
        // Close();
    }

    public override void SetProgress(string text, float percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        progressText.text = text;
    }

    public void Close()
    {
        _canvas.enabled = false;
    }
}