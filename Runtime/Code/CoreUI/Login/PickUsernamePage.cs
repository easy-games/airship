using System;
using System.Collections.Generic;
using Code.Http.Internal;
using Code.Http.Public;
using ElRaccoone.Tweens;
using Proyecto26;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PickUsernamePage : MonoBehaviour {
    public TMP_InputField usernameField;
    public TMP_InputField tagField;
    public GameObject continueBtn;
    public TMP_Text responseTxt;
    public Color enabledBtnColor;
    public Color disableBtnColor;
    public string usernameTakenText = "Username & tag is unavailable.";
    public float checkUsernameCooldown = 0.1f;

    public List<TMP_InputField> tabOrdering = new();

    private bool inputDirty;
    private float lastCheckUsernameTime;
    private bool continueBtnEnabled = false;

    private void OnEnable() {
        this.CheckUsername();
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

        if (this.inputDirty && Time.time - this.lastCheckUsernameTime > this.checkUsernameCooldown) {
            this.inputDirty = false;
            this.lastCheckUsernameTime = Time.time;
            this.CheckUsername();
        }
    }

    public void UsernameValueChanged(string val) {
        this.inputDirty = true;
    }

    public void TagValueChanged(string val) {
        this.inputDirty = true;
    }

    private async void CheckUsername() {
        bool avail = false;

        var username = this.usernameField.text;
        var tag = this.tagField.text;

        if (username == string.Empty || tag == string.Empty) {
            ClearResponse();
            SetContinueButtonState(false);
            return;
        }

        var res = await InternalHttpManager.GetAsync(AirshipApp.gameCoordinatorUrl +
                                                     "/users/availability?discriminatedUsername=" + username + "%23" + tag);
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
        var tag = this.tagField.text;

        var res = await InternalHttpManager.PatchAsync(AirshipApp.gameCoordinatorUrl + "/users", JsonUtility.ToJson(
            new ChangeUsernameRequest() {
                username = username,
                discriminator = tag,
            }));
        if (res.success) {
            SceneManager.LoadScene("MainMenu");
        } else {
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
        img.TweenGraphicColor(enabled ? this.enabledBtnColor : this.disableBtnColor, 0.12f);
        this.continueBtnEnabled = enabled;
    }
}