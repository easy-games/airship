using System.Collections;
using System.Collections.Generic;
using Player.Entity;
using UnityEngine;

public enum EntityAnimationEventKey {
    DEFAULT = -1,
    FOOTSTEP,
}

public class EntityAnimationEventData {
    public EntityAnimationEventKey key;
}

public class EntityAnimationEvents : MonoBehaviour {
    public EntityAnimator anim;
    
    public delegate void EntityAnimationEvent(EntityAnimationEventData key);
    public event EntityAnimationEvent entityAnimationEvent;
    
    private float minFootstepTime = .2f;
    private float lastFootstepTime = 0;
    
    public void Footstep() {
        if (Time.time - lastFootstepTime > minFootstepTime) {
            lastFootstepTime = Time.time;
            entityAnimationEvent?.Invoke(new EntityAnimationEventData(){key = EntityAnimationEventKey.FOOTSTEP});
        }
    }
}
