using UnityEngine;

public class ClientBootstrap : MonoBehaviour
{
    private void Start()
    {
        if (RunCore.IsServer()) return;
        
        Application.targetFrameRate = 240;
    }
}