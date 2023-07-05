using FishNet;
using FishNet.Object;
using UnityEngine;

public class TestTickRate : NetworkBehaviour
{
  
    private float _lastTime = -1f;

    private void Update()
    {
        if (!base.IsClient) return;
        if (!base.TimeManager.FrameTicked) return;

        float theTime = Time.time;
        if (_lastTime != -1f)
        {
            float passedTime = (theTime - _lastTime);
            float difference = Mathf.Abs(passedTime - (float)base.TimeManager.TickDelta);
            // Debug.Log("Tick difference: " + difference);
        }

        _lastTime = theTime;
    }

}