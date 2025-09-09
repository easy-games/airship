using UnityEngine;
using UnityEngine.Animations;

[LuauAPI]
[ExecuteInEditMode]
public class EasyLookAt : MonoBehaviour {
    public EngineRunMode refreshMode = EngineRunMode.PLAY;
    public Transform lookTarget;
    public bool scaleToTarget = false;
    public bool invertAxis = false;
    public EasyAxis forwardAxis = EasyAxis.Z;
    public EasyAxis lockAxis = EasyAxis.None;


    private Quaternion lookRotation = Quaternion.identity;

    private void LateUpdate() {
        if (EasyTooling.IsValidRunMode(refreshMode)) {
            //transform.LookAt(lookTarget);
            var relativePos = lookTarget.position - transform.position;
            switch (lockAxis) {
                case EasyAxis.X:
                    relativePos.x = 0;
                    break;
                case EasyAxis.Y:
                    relativePos.y = 0;
                    break;
                case EasyAxis.Z:
                    relativePos.z = 0;
                    break;
            }

            lookRotation = Quaternion.LookRotation(relativePos);
            switch (forwardAxis) {
                case EasyAxis.X:
                    lookRotation *= Quaternion.Euler(new Vector3(0, invertAxis ? 90 : -90, 0));
                    break;
                case EasyAxis.Y:
                    lookRotation *= Quaternion.Euler(new Vector3(invertAxis ? -90 : 90, 0, 0));
                    break;
                case EasyAxis.Z:
                    lookRotation *= Quaternion.Euler(new Vector3(0, invertAxis ? 180 : 0, 0));
                    break;
            }

            transform.rotation = lookRotation;


            if (scaleToTarget) {
                transform.localScale = new Vector3(transform.localScale.x, transform.localScale.y,
                    Vector3.Distance(lookTarget.position, transform.position));
            }
        }
    }
}