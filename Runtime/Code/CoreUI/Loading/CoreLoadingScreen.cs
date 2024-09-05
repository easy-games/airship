using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Code.CoreUI.Components;
using ElRaccoone.Tweens;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Cursor = UnityEngine.Cursor;
using SceneManager = UnityEngine.SceneManagement.SceneManager;
using Screen = UnityEngine.Device.Screen;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

[LuauAPI]
public class CoreLoadingScreen : BundleLoadingScreen
{
    private Canvas _canvas;
    public TMP_Text progressText;
    public Button disconnectButton;
    public Button continueButton;
    public GameObject spinner;
    public RawImage gameImage;
    public Color editorGameImageColor;

    [NonSerialized] private float startTime = 0f;
    [NonSerialized] private bool showedVoiceChatCard = false;
    public RectTransform voiceChatCard;
    public InternalToggle voiceChatToggle;

    public bool updatedByGame = false;

    public static Dictionary<string, Texture2D> gameImageCache = new Dictionary<string, Texture2D>();

    private void Awake() {
        base.showContinueButton = true;
        _canvas = GetComponent<Canvas>();
    }

    private void Start() {
        if (!RunCore.IsClient()) {
            Close();
            return;
        }

        this.gameImage.color = new Color(1, 1, 1, 0);
#if AIRSHIP_PLAYER
        this.UpdateGameImage();
#else
        // if (Application.isEditor) {
        //     this.gameImage.enabled = false;
        // }
#endif

        this.startTime = 0f;
        this.voiceChatCard.gameObject.SetActive(false);

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

        disconnectButton.onClick.AddListener(DisconnectButton_OnClicked);
        this.voiceChatToggle.onValueChanged += VoiceChatToggle_OnValueChanged;

        if (Application.isMobilePlatform) {
            this.disconnectButton.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        }
    }

    private async void UpdateGameImage() {
        var gameId = CrossSceneState.ServerTransferData.gameId;
        if (string.IsNullOrEmpty(gameId)) {
            Debug.Log("GameID was null. Skipping background image download.");
            return;
        }

        if (gameImageCache.TryGetValue(gameId, out var tex)) {
            this.gameImage.texture = tex;
            this.gameImage.color = new Color(1, 1, 1, 1);
            return;
        }

        var www = UnityWebRequestTexture.GetTexture(
            "https://cdn.airship.gg/images/4a56b023-cf41-4fd2-93f1-2326eb35ba28");
        await www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success) {
            Debug.LogError("Failed to download loading screen image: " + www.error);
            return;
        }
        var texture = DownloadHandlerTexture.GetContent(www);
        this.gameImage.texture = texture;
        gameImageCache[gameId] = texture;
        NativeTween.GraphicAlpha(this.gameImage, 1, 0.7f);
    }

    private async void VoiceChatToggle_OnValueChanged(bool val) {
        if (val) {
            Bridge.RequestMicrophonePermissionAsync();
            if (!Bridge.HasMicrophonePermission()) {
                this.voiceChatToggle.SetValue(false);
            }
        }
    }

    private void Update() {
        this.startTime += Time.deltaTime;
        if (!this.showedVoiceChatCard && this.startTime > 1f) {
            #if !AIRSHIP_PLAYER
            return;
            #endif
            this.showedVoiceChatCard = true;
            this.ShowVoiceChatCard();
        }
    }

    private void ShowVoiceChatCard() {
        if (Bridge.HasMicrophonePermission()) {
            return;
        }

        this.voiceChatCard.gameObject.SetActive(true);
        this.voiceChatCard.anchoredPosition = new Vector2(this.voiceChatCard.anchoredPosition.x, -50);
        var canvasGroup = this.voiceChatCard.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        NativeTween.CanvasGroupAlpha(canvasGroup, 1f, 1f).SetEaseQuadOut();;
        NativeTween.AnchoredPositionY(voiceChatCard, -37, 1f).SetEaseQuadOut();
    }

    public override void SetTotalDownloadSize(long sizeBytes) {
        var sizeMb = sizeBytes / 1_000_000;

#if UNITY_IOS || UNITY_ANDROID
        if (sizeMb > 1) {
            this.SetProgress($"A {sizeMb}MB update is required.\nWould you like to continue?", 0);
            this.spinner.SetActive(false);
            this.continueButton.gameObject.SetActive(true);
            return;
        }
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
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnDisable() {
        if (RunCore.IsClient()) {
            disconnectButton.onClick.RemoveListener(DisconnectButton_OnClicked);
        }
    }

    private void OnDestroy() {
        if (this.voiceChatToggle) {
            this.voiceChatToggle.onValueChanged -= VoiceChatToggle_OnValueChanged;
        }
    }

    private void DisconnectButton_OnClicked() {
        TransferManager.Instance.Disconnect();
    }

    public override void SetProgress(string text, float percent) {
        percent = Math.Clamp(percent, 0, 100);
        progressText.text = text;
    }

    public void Close() {
        _canvas.enabled = false;
    }
}