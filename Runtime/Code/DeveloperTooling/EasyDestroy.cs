using UnityEngine;

public class EasyDestroy : MonoBehaviour {
    public enum DestroyMode {
        DESTROY,
        DEACTIVATE,
    }
    public float timeUntilDeathInSeconds = 1f;
    public DestroyMode destroyMode = DestroyMode.DESTROY; 

    // Use this for initialization
    void OnEnable() {
        if (timeUntilDeathInSeconds <= 0) {
            Destroy();
            return;
        }

        if (destroyMode == DestroyMode.DEACTIVATE) {
            gameObject.SetActive(true);
        }
        Invoke (nameof(Destroy), timeUntilDeathInSeconds);
    }
    
    private void Destroy(){
        if (destroyMode == DestroyMode.DESTROY) {
            Destroy (gameObject);
        } else {
            gameObject.SetActive(false);
        }
    }
}
