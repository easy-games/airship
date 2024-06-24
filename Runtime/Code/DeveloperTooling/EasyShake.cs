using UnityEngine;

[LuauAPI]
public class EasyShake : MonoBehaviour
{
    public float duration = -1;
    public float movementsPerSecond = 2;
    public float lerpMod = -1;
    public Vector3 maxRadius = new Vector3(.5f,.5f,.5f);
    public Vector3 minRadius = new Vector3(0,0,0);
    public float positionRadiusMod = 1;
    public float rotationRadiusMod = 0;
    
    public bool minimizeShakeOverTime = false;
    public bool infinite = false;
    public bool destroyOnEnd = false;

    private float lastMovement = 0;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool started = false;
    private float startingDuration = 0;
    private Vector3 targetPosition;
    private Vector3 tickRandomOffset = Vector3.zero;
    private Quaternion targetRotation;

    private void OnEnable() {
        originalPosition = transform.localPosition;
        originalRotation = transform.localRotation;
        targetPosition = originalPosition;
        targetRotation = originalRotation;
    }

    // Update is called once per frame
    void LateUpdate() {
        if (duration > 0 || infinite) {
            if (!started) {
                //START
                startingDuration = duration;
                started = true;
            }

            if (!infinite) {
                duration -= Time.deltaTime;
            }

            if (Time.time - lastMovement > (1 / movementsPerSecond)) {
                //TICK
                Vector3 radius = maxRadius;
                if (minimizeShakeOverTime) {
                    radius = Vector3.Lerp(maxRadius, minRadius, 1-(duration / startingDuration));
                }

                tickRandomOffset.x = Random.Range(-radius.x, radius.x);
                tickRandomOffset.y = Random.Range(-radius.y, radius.y);
                tickRandomOffset.z = Random.Range(-radius.z, radius.z);
                targetPosition = originalPosition + tickRandomOffset * positionRadiusMod;
                targetRotation = originalRotation * Quaternion.Euler(tickRandomOffset * rotationRadiusMod);
                
                lastMovement = Time.time;
            }
            
            //Lerp to target
            if (lerpMod <= 0) {
                transform.localPosition = targetPosition;
                transform.localRotation = targetRotation;
            } else {
                transform.localPosition
                    = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * lerpMod);
                transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, Time.deltaTime * lerpMod);
            }
        } else if(started){
            //END
            started = false;
            transform.localPosition = originalPosition;
            if (destroyOnEnd) {
                Destroy(this);
            }
        }
    }
}
