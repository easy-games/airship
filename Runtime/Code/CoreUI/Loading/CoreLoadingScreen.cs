using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Code.Bootstrap;
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
    public RectTransform bottomCard;
    public GameObject errorWrapper;
    public TMP_Text errorText;

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public void OnReload() {
        gameImageCache.Clear();
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
        if (Application.isEditor) {
            this.gameImage.enabled = false;
        }
#endif

#if UNITY_IOS || UNITY_ANDROID

#endif

        this.startTime = 0f;
        this.voiceChatCard.gameObject.SetActive(false);

        Screen.orientation = ScreenOrientation.LandscapeLeft;

        var deviceInfo = DeviceBridge.GetDeviceType();
        if (deviceInfo is AirshipDeviceType.Phone or AirshipDeviceType.Tablet) {
            this.bottomCard.localScale = Vector3.one * 1.1f;
            this.bottomCard.anchoredPosition = new Vector2(0, 185);
        }

        _canvas.enabled = true;
        this.continueButton.gameObject.SetActive(false);
        this.spinner.SetActive(true);
        
        SetProgress("Connecting to Server", 5);
        
        this.voiceChatToggle.onValueChanged += VoiceChatToggle_OnValueChanged;

        if (Application.isMobilePlatform) {
            this.disconnectButton.transform.localScale = new Vector3(1.3f, 1.2f, 1.2f);
        }
    }

    private async void UpdateGameImage() {
        var imageUrl = CrossSceneState.ServerTransferData.loadingImageUrl;
        if (string.IsNullOrEmpty(imageUrl)) {
            // fallback
            // imageUrl = "https://cdn.airship.gg/images/4a56b023-cf41-4fd2-93f1-2326eb35ba28";
            // Debug.Log("[Loading Screen] Image url was empty. Skipping background image download.");
            return;
        }

        if (gameImageCache.TryGetValue(imageUrl, out var tex)) {
            this.gameImage.texture = tex;
            NativeTween.GraphicAlpha(this.gameImage, 1, 0.7f);
            return;
        }

        var www = UnityWebRequestTexture.GetTexture(imageUrl);
        await www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success) {
            Debug.LogError("Failed to download loading screen image: " + www.error);
            return;
        }
        var texture = DownloadHandlerTexture.GetContent(www);
        this.gameImage.texture = texture;
        gameImageCache[imageUrl] = texture;
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
        // if (!this.showedVoiceChatCard && this.startTime > 1f) {
        //     #if !AIRSHIP_PLAYER
        //     return;
        //     #endif
        //     this.showedVoiceChatCard = true;
        //     this.ShowVoiceChatCard();
        // }
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
    
    public override void SetError(string msg) {
        this.spinner.SetActive(false);
        this.progressText.gameObject.SetActive(false);
        this.bottomCard.gameObject.SetActive(false);
        this.errorText.text = msg;
        this.errorWrapper.gameObject.SetActive(true);
    }
    
    public void RetryBtn_OnClick() {
        this.spinner.SetActive(true);
        this.continueButton.gameObject.SetActive(false);
        this.progressText.gameObject.SetActive(true);
        this.errorWrapper.SetActive(false);
        this.bottomCard.gameObject.SetActive(true);
        
        FindAnyObjectByType<ClientBundleLoader>().RetryDownload();
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

    private void OnDestroy() {
        if (this.voiceChatToggle) {
            this.voiceChatToggle.onValueChanged -= VoiceChatToggle_OnValueChanged;
        }
    }

    public void DisconnectBtn_OnClick() {
        if (TransferManager.Instance.IsExpectingDisconnect()) {
            return;
        }
        
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