using UnityEngine;

public class EasyTransformAnchor : MonoBehaviour {
    public Transform anchor;
    public bool matchPosition = true;
    public bool matchRotation = true;
    public bool matchScale = false;

    void LateUpdate()
    {
        if(matchPosition)
            this.transform.position = anchor.position;

        if(matchRotation)
            this.transform.rotation = anchor.rotation;

        if(matchScale)  
            this.transform.localScale = anchor.localScale;
    }
}
