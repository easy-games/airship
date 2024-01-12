using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EasyDestroy : MonoBehaviour {
    public enum DestroyMode {
        DESTROY,
        DEACTIVATE,
    }
    public float timeUntilDeathInSeconds = 1f;
    public DestroyMode destroyMode = DestroyMode.DESTROY; 

    // Use this for initialization
    void Start () {
        if (timeUntilDeathInSeconds <= 0) {
            Destroy();
            return;
        }

        if (destroyMode == DestroyMode.DEACTIVATE) {
            gameObject.SetActive(true);
        }
        Invoke ("die", timeUntilDeathInSeconds);
    }
    private void Destroy(){
        if (destroyMode == DestroyMode.DESTROY) {
            Destroy (gameObject);
        } else {
            gameObject.SetActive(false);
        }
    }
}
