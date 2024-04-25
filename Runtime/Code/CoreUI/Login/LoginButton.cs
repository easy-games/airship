using System;
using UnityEngine;using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LoginButton : MonoBehaviour {
    public GameObject loadingIndicator;

    public void SetLoading(bool loading) {
        for (int i = 0; i < this.transform.childCount; i++) {
            var child = this.transform.GetChild(i);
            if (child.gameObject == this.loadingIndicator) {
                child.gameObject.SetActive(loading);
            } else {
                child.gameObject.SetActive(!loading);
            }
        }
    }
}