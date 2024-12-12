using System;
using System.Collections.Generic;
using Code.Http.Internal;
using Code.Http.Public;
using Code.Platform.Shared;
using ElRaccoone.Tweens;
using Proyecto26;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PickUsernamePage : MonoBehaviour {
    public TMP_InputField usernameField;
    public GameObject continueBtn;
    public TMP_Text responseTxt;
    public Color enabledBtnColor;
    public Color disableBtnColor;
    [NonSerialized] public string usernameTakenText = "Username is unavailable.";
    public float checkUsernameCooldown = 0.1f;
    public float checkUsernameInputDelay = 0.15f;
    public bool slideUpWhileInputting = false;
    private bool inputSlidUp = false;

    public List<TMP_InputField> tabOrdering = new();

    private bool inputDirty;
    private float inputDirtyTime;
    private float lastCheckUsernameTime;
    private bool continueBtnEnabled;

    private void OnEnable() {
        this.CheckUsername();
    }

    private void OnDisable() {

    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Tab)) {
            var found = false;
            for (int i = 0; i < this.tabOrdering.Count; i++) {
                var input = this.tabOrdering[i];
                if (input.isFocused) {
                    var nextInput = i + 1 >= this.tabOrdering.Count ? this.tabOrdering[0] : this.tabOrdering[i + 1];
                    nextInput.Select();
                    found = true;
                    break;
                }
            }

            if (!found) {
                this.tabOrdering[0].Select();
            }
        }

        if (Input.GetKeyDown(KeyCode.Hash)) {
            if (this.tabOrdering[0].isFocused) {
                this.tabOrdering[1].Select();
            }
        }

        if (Input.GetKeyDown(KeyCode.Return) && this.continueBtnEnabled) {
            this.Submit();
        }

        if (this.inputDirty && Time.time - this.lastCheckUsernameTime > this.checkUsernameCooldown && Time.time - this.inputDirtyTime > this.checkUsernameInputDelay) {
            this.inputDirty = false;
            this.lastCheckUsernameTime = Time.time;
            this.CheckUsername();
        }

        if (this.slideUpWhileInputting && this.usernameField.isFocused && !this.inputSlidUp) {
            this.inputSlidUp = true;
            var rect = this.transform as RectTransform;
            NativeTween.OffsetMin(rect, new Vector2(0, 400), 0.15f).SetEaseQuadOut();
        } else if (this.slideUpWhileInputting && !this.usernameField.isFocused && this.inputSlidUp) {
            this.inputSlidUp = false;
            var rect = this.transform as RectTransform; 
            NativeTween.OffsetMin(rect, new Vector2(0, 0), 0.15f).SetEaseQuadOut();
        }
    }

    public void UsernameValueChanged(string val) {
        this.inputDirty = true;
        this.inputDirtyTime = Time.time;
    }

    public void TagValueChanged(string val) {
        this.inputDirty = true;
        this.inputDirtyTime = Time.time;
    }

    private async void CheckUsername() {
        bool avail = false;

        var username = this.usernameField.text;

        if (username == string.Empty) {
            ClearResponse();
            SetContinueButtonState(false);
            return;
        }

        var res = await InternalHttpManager.GetAsync(AirshipPlatformUrl.gameCoordinator +
                                                     "/users/availability?username=" + username);
        avail = res.success;
        print("username check: " + res.data);
        if (!res.success) {
            SetResponse(this.usernameTakenText);
            Debug.LogError(res.error);
        } else {
            var resData = JsonUtility.FromJson<CheckUsernameResponse>(res.data);
            avail = resData.available;
            if (!avail) {
                SetResponse(this.usernameTakenText);
            }
        }

        this.SetContinueButtonState(avail);
        if (avail) {
            this.ClearResponse();
        }
    }

    public async void Submit() {
        if (!this.continueBtnEnabled) {
            return;
        }
        this.ClearResponse();

        var username = this.usernameField.text;

        var res = await InternalHttpManager.PostAsync(AirshipPlatformUrl.gameCoordinator + "/users/self", JsonUtility.ToJson(
            new CreateAccountRequest() {
                username = username,
            }));
        if (res.success) {
            SceneManager.LoadScene("MainMenu");
        } else {
            Debug.LogError(res.error);
            this.SetResponse("Failed to set username. Please try again.");
        }
    }

    public void SetResponse(string text) {
        this.responseTxt.text = text;
        this.responseTxt.gameObject.SetActive(true);
    }

    public void ClearResponse() {
        this.responseTxt.gameObject.SetActive(false);
    }

    public void SetContinueButtonState(bool enabled) {
        var img = this.continueBtn.GetComponent<Image>();
        NativeTween.GraphicColor(img, enabled ? this.enabledBtnColor : this.disableBtnColor, 0.12f);
        this.continueBtnEnabled = enabled;
    }
}