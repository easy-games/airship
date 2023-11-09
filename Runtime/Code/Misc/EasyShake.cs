using System.Collections;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

[LuauAPI]
public class EasyShake : MonoBehaviour
{
    public float duration = -1;
    public float movementsPerSecond = 2;
    public Vector3 maxRadius = new Vector3(.5f,.5f,.5f);
    public Vector3 minRadius = new Vector3(0,0,0);
    public bool resolveShakeOverTime = false;
    public bool destroyOnEnd = true;

    private float lastMovement = 0;
    private Vector3 originalPosition;
    private bool started = false;
    private float startingDuration = 0;

    private void OnEnable() {
        originalPosition = transform.localPosition;
    }

    // Update is called once per frame
    void LateUpdate() {
        if (duration > 0) {
            if (!started) {
                //START
                startingDuration = duration;
                started = true;
            }
            duration -= Time.deltaTime;
            if (Time.time - lastMovement > (1 / movementsPerSecond)) {
                //TICK
                Vector3 radius = maxRadius;
                if (resolveShakeOverTime) {
                    radius = Vector3.Lerp(maxRadius, minRadius, 1-(duration / startingDuration));
                }
                transform.localPosition = originalPosition + new Vector3(Random.Range(-radius.x, radius.x),
                    Random.Range(-radius.y, radius.y), Random.Range(-radius.z, radius.z));
                lastMovement = Time.time;
            }
            if (duration < 0) {
                //END
                started = false;
                transform.localPosition = originalPosition;
                if (destroyOnEnd) {
                    Destroy(this);
                }
            }
        }
    }
}
