using UnityEngine;

[ExecuteInEditMode]
public class EasyLook : MonoBehaviour{
    public EngineRunMode refreshMode;
    public Transform lookTarget;
    public bool scaleToTarget = false;

    private void LateUpdate() {
        if(EasyTooling.IsValidRunMode(refreshMode)){
            this.transform.LookAt(lookTarget);
            if(scaleToTarget){
                this.transform.localScale = new Vector3(transform.localScale.x, transform.localScale.y, Vector3.Distance(lookTarget.position, this.transform.position));
            }
        }
    }
}
