using UnityEngine;

[LuauAPI]
[ExecuteInEditMode]
public class EasyShake : MonoBehaviour {

    [Header("Lifetime")]
    [Tooltip("How long to shake. Negative number for infinite")]
    public float shakeDuration = 0.1f;
    public bool shakeOnEnable = false;
    public bool destroyComponentOnEnd = false;

    [Space(10)]
    [Header("Movement")]
    [Tooltip("The higher the number the faster the object will transition to the next offset. Negative number for instant.")]
    public float movementLerpMod = 10;
    [Tooltip("How many times the transform moves each second")]
    public float movementsPerSecond = 10;
    [Tooltip("Should the position offsets decrease over time?")]
    public bool minimizeShakeOverTime = true;

    [Header("Position Randomization")]
    public Vector3 maxPositionOffset = new Vector3(.5f,.5f,.5f);

    [Header("Rotation Randomization")]
    public Vector3 maxRotationOffsetAngles = new Vector3(15,15,15);

    private float lastMovement = 0;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool started = false;
    private float shakeStartTime = 0;
    private float remainingDuration = 0;
    private Vector3 targetPosition;
    private Quaternion targetRotation;

    private void OnEnable() {
        this.SetStartingPosRot(this.transform.localPosition, this.transform.localRotation);
        targetPosition = originalPosition;
        targetRotation = originalRotation;
        if(shakeOnEnable){
            Shake(shakeDuration);
        }
    }

    // Update is called once per frame
    void LateUpdate() {
        if (remainingDuration != 0) {
            if (remainingDuration > 0) {
                remainingDuration = Mathf.Max(0, remainingDuration-Time.deltaTime);
            }

            if (Time.time - lastMovement > (1 / movementsPerSecond)) {
                //TICK
                //Position
                var positionOffset = maxPositionOffset;
                if (minimizeShakeOverTime) {
                    positionOffset = Vector3.Lerp(maxPositionOffset, Vector3.zero, (Time.time - shakeStartTime) / shakeDuration);
                }
                targetPosition = originalPosition + GetRandomVector3(positionOffset);

                //Rotation
                var rotationOffset = maxRotationOffsetAngles;
                if (minimizeShakeOverTime) {
                    rotationOffset = Vector3.Lerp(rotationOffset, Vector3.zero, (Time.time - shakeStartTime) / shakeDuration);
                }
                targetRotation = originalRotation * Quaternion.Euler(GetRandomVector3(rotationOffset));
                
                lastMovement = Time.time;
            }
            
            //Lerp to target
            if (movementLerpMod <= 0) {
                transform.localPosition = targetPosition;
                transform.localRotation = targetRotation;
            } else {
                transform.localPosition
                    = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * movementLerpMod);
                transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, Time.deltaTime * movementLerpMod);
            }
        } else if (started){
            StopShake();
        }
    }

    public Vector3 GetRandomVector3(Vector3 maxRange){
        return new Vector3(
            Random.Range(-maxRange.x, maxRange.x),
            Random.Range(-maxRange.y, maxRange.y),
            Random.Range(-maxRange.z, maxRange.z));
    }

    public void Shake(float duration){
        shakeDuration = duration;
        remainingDuration = shakeDuration;
        this.shakeStartTime = Time.time;
        started = true;
    }

    public void ShakeForever(){
        Shake(-1);
    }

    public void StopShake(){
        started = false;
        transform.localPosition = originalPosition;
        transform.localRotation = originalRotation;
        remainingDuration = 0;
        if (destroyComponentOnEnd) {
            Destroy(this);
        }
    }

    public void SetStartingPosRot(Vector3 localPosition, Quaternion localRotation){
        originalPosition = localPosition;
        originalRotation = localRotation;
    }
}
