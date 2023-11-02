using DavidFDev.DevConsole;
using UnityEngine;

public class ClientBootstrap : MonoBehaviour
{
    private void Start()
    {
        if (RunCore.IsServer()) return;
        
        Application.targetFrameRate = 240;
        DevConsole.EnableConsole();
    }
}