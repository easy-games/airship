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

public class CoreLoadingScreen : MonoBehaviour
{
    private Canvas _canvas;
    public TMP_Text progressText;
    public Button disconnectButton;
    
    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
    }

    private void Start()
    {

        if (RunCore.IsServer())
        {
            Close();
            return;
        }

        _canvas.enabled = true;
        
        SetProgress("Connecting to Server", 5);
        InstanceFinder.SceneManager.OnLoadPercentChange += OnLoadPercentChanged;
        InstanceFinder.SceneManager.OnLoadEnd += OnLoadEnd;

        disconnectButton.onClick.AddListener(DisconnectButton_OnClicked);
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

    private void DisconnectButton_OnClicked()
    {
        SceneManager.LoadScene("MainMenu");
    }

    private void OnLoadPercentChanged(SceneLoadPercentEventArgs e)
    {
        SetProgress("Opening Game", 45 + e.Percent * 5);
    }

    private void OnLoadEnd(SceneLoadEndEventArgs e)
    {
        // SetLabel("Opening Bundle", 100);
        // Close();
    }

    public void SetProgress(string text, float percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        progressText.text = text;
    }

    public void Close()
    {
        _canvas.enabled = false;
    }
}