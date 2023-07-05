using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EntityFootTracker : MonoBehaviour {
    [Header("References")]
    public EntityAnimationEvents events;
    [Header("Variables")]
    public float minDistance = .01f;

    private bool isDown = false;

    private void LateUpdate() {
        var shouldBeDown = events.transform.InverseTransformPoint(transform.position).y < minDistance;
        if (shouldBeDown && !isDown) {
            events.Footstep();
        }
        isDown = shouldBeDown;
    }
}
