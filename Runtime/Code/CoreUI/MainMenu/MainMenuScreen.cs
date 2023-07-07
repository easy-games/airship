using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Button = UnityEngine.UIElements.Button;
using Cursor = UnityEngine.Cursor;

public class MainMenuScreen : MonoBehaviour
{
    public Canvas canvas;
    public TMP_InputField _usernameTextField;
    public TMP_Text _playerVersion;

    public GameObject _joinCodeWrapper;
    public TMP_Text _joinCodeButtonText;
    public TMP_InputField _joinCodeTextField;
    public bool _connectInProgress = false;

    public bool _createServerInProgress = false;
    public TMP_Text _createButtonText;

    public Toggle _localHostToggle;
    public Toggle _useLocalBundles;

    public TMP_Text _errorMessageText;
    public GameObject _errorMessageWrapper;
    
    public PlayerConfig playerConfig;

    private void Awake()
    {
        _usernameTextField.text = "Player";
        _connectInProgress = false;

        _playerVersion.text = "Version " + playerConfig.playerVersion;
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.None;
    }

    public void JoinButton_OnClicked()
    {
        _joinCodeWrapper.SetActive(!_joinCodeWrapper.activeSelf);
    }

    public void QuitButton_OnClicked()
    {
        Application.Quit();
    }

    public void CloseErrorMessageButton_OnClicked()
    {
        _errorMessageWrapper.SetActive(false);
    }

    public void ConnectButton_OnClicked()
    {
        if (_connectInProgress) return;

        if (!ValidateUsername())
        {
            return;
        }

        string joinCode = _joinCodeTextField.text;
        if (joinCode == "")
        {
            return;
        }

        _connectInProgress = true;

        Debug.Log($"Connect button pressed. Finding server with join code \"{joinCode}\"");
        ClearErrorMessage();
        
        if (_localHostToggle.isOn)
        {
            Debug.Log("Connecting to localhost..");
            CrossSceneState.ServerTransferData = new ServerTransferData()
            {
                address = "127.0.0.1",
                port = 7770,
            };
            SceneManager.LoadScene("CoreScene");
            _createServerInProgress = false;
            return;
        }
        
        GameCoordinatorCore.MatchMaking.FindServerFromJoinCode(joinCode).Then((serverTransferData) =>
        {
            CrossSceneState.ServerTransferData = serverTransferData;
            SceneManager.LoadScene("CoreScene");
            _connectInProgress = false;
        }).Catch((err) =>
        {
            Debug.LogError(err);
            _connectInProgress = false;
            SetErrorMessage(err.Message);
        });
    }

    public void CreateServerButton_OnClicked()
    {
        if (_createServerInProgress) return;

        if (!ValidateUsername())
        {
            return;
        }

        _createServerInProgress = true;

        Debug.Log("Create server button pressed. Creating server...");
        ClearErrorMessage();
        
        StartCoroutine(StartLoading());

        if (_localHostToggle.isOn)
        {
            Debug.Log("Connecting to localhost..");
            CrossSceneState.ServerTransferData = new ServerTransferData()
            {
                address = "127.0.0.1",
                port = 7770,
            };
            SceneManager.LoadScene("CoreScene");
            _createServerInProgress = false;
            return;
        }
        
        GameCoordinatorCore.MatchMaking.CreateServer().Then((serverTransferData) =>
        {
            CrossSceneState.ServerTransferData = serverTransferData;
            SceneManager.LoadScene("CoreScene");
            _createServerInProgress = false;
        }).Catch((err) =>
        {
            Debug.LogError(err);
            _createServerInProgress = false;
            SetErrorMessage(err.Message);
        });
    }

    private IEnumerator StartLoading()
    {
        var i = 0;
        var inc = true;
        while (_connectInProgress || _createServerInProgress)
        {
            if (inc)
            {
                i++;
                if (i == 3)
                {
                    inc = false;
                }
            } else
            {
                i--;
                if (i == 1)
                {
                    inc = true;
                }
            }
            
            var text = "";
            for (var x = 0; x < i; x++)
            {
                text += ".";
            }
            _createButtonText.text = text;
            yield return new WaitForSeconds(0.4f);
        }

        _createButtonText.text = "Create Server";
    }

    private bool ValidateUsername()
    {
        string username = _usernameTextField.text;
        if (username == "")
        {
            SetErrorMessage("Pick a username!");
            return false;
        }

        CrossSceneState.Username = username;
        CrossSceneState.UseLocalBundles = _useLocalBundles.isOn;
        Debug.Log("Using local bundles: " + CrossSceneState.UseLocalBundles);
        return true;
    }

    public void SetErrorMessage(string errorMessage)
    {
        if (errorMessage == "")
        {
            ClearErrorMessage();
            return;
        }
        _errorMessageText.text = errorMessage;
        _errorMessageWrapper.SetActive(true);
    }

    public void ClearErrorMessage()
    {
        _errorMessageText.text = "";
        _errorMessageWrapper.SetActive(false);
    }
}