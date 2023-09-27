using System;
using System.Collections;
using System.Collections.Generic;
using Player.Entity;
using UnityEngine;

public enum EntityAnimationEventKey {
    DEFAULT = -1,
    FOOTSTEP,
    JUMP,
    LAND,
    SLIDE_START,
    SLIDE_END
}

public class EntityAnimationEventData {
    public int key;
}

public class EntityAnimationEvents : MonoBehaviour {
    public CoreEntityAnimator anim;
    public event Action<object> entityAnimationEvent;
    
    private float minFootstepTime = .075f;
    private float lastFootstepTime = 0;
    
    public void Footstep() {
        if (Time.time - lastFootstepTime > minFootstepTime) {
            lastFootstepTime = Time.time;
            //Trigger footstep event
            entityAnimationEvent?.Invoke((int)EntityAnimationEventKey.FOOTSTEP);
        }
    }

    public void TriggerBasicEvent(EntityAnimationEventKey key) {
        //Trigger slide event
        entityAnimationEvent?.Invoke((int)key);
    }
}
