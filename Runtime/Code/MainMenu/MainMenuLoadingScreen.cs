using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using Cursor = UnityEngine.Cursor;

public class MainMenuLoadingScreen : MonoBehaviour
{
    public Canvas canvas;
    public TMP_Text progressText;

    private void Start()
    {
        if (RunCore.IsServer())
        {
            Close();
            return;
        }

        canvas.enabled = true;
    }

    private void OnEnable() {
        canvas.enabled = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void SetProgress(string text, float percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        progressText.text = text;
    }

    public void Close()
    {
        canvas.enabled = false;
    }
}