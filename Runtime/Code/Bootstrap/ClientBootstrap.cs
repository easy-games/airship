using Airship.DevConsole;
using UnityEngine;

public class ClientBootstrap : MonoBehaviour
{
    private void Start()
    {
        if (RunCore.IsClient()) {
            Application.targetFrameRate = 240;
        }
    }
}