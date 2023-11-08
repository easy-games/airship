using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EasyShake : MonoBehaviour
{
    public float duration = -1;
    public float movementsPerSecond = 2;
    public Vector3 maxRadius = new Vector3(.5f,.5f,.5f);

    private float lastMovement = 0;
    private Vector3 originalPosition;

    private void OnEnable() {
        originalPosition = transform.localPosition;
    }

    // Update is called once per frame
    void Update()
    {
        if (duration > 0) {
            duration -= Time.deltaTime;
            if (Time.deltaTime - lastMovement > (1 / movementsPerSecond)) {
                //TICK
                transform.localPosition = originalPosition + new Vector3(Random.Range(-maxRadius.x, maxRadius.x),
                    Random.Range(-maxRadius.y, maxRadius.y), Random.Range(-maxRadius.z, maxRadius.z));
            }
            if (duration < 0) {
                //END
                transform.localPosition = originalPosition;
                Destroy(this);
            }
        }
    }
}
