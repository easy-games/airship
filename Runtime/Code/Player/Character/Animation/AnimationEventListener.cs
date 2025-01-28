using System.Collections.Generic;
using UnityEngine;

[LuauAPI]
public class AnimationEventListener : MonoBehaviour {
    
    [Tooltip("If identical events come in within this threshold only 1 will fire. In seconds.")]
    public float minRepeatMessageTime = .075f;
        
    public event System.Action<object> OnAnimEvent;
    public event System.Action<object> OnAnimObjEvent;
    
    private Dictionary<string, float> lastMessageTime = new Dictionary<string, float>();
    
    /// <summary>
    /// Animation events triggering function to be passed into TS
    /// </summary>
    public void TriggerEventObj(Object obj){
        //print("Animation object trigger");
        var data = (AnimationEventData)obj;
        if(data){
            //print("found data object: " + data.key + ", " + data.stringValue + ", " + data.intValue + ", " + data.floatValue);
            if(CanMessage(data.key))
                OnAnimObjEvent?.Invoke(data);
        }
    }

    public void TriggerEvent(string key){
        //print("Animation key trigger: " + key);
        if(CanMessage(key))
            OnAnimEvent?.Invoke(key);
    }

    private bool CanMessage(string key){
        bool canMessage = false;
        
        if(lastMessageTime.TryGetValue(key, out float lastTime)){
            if(Time.time - lastTime > this.minRepeatMessageTime){
                canMessage = true;
            }
        }
        else {
            canMessage = true;
        }

        lastMessageTime[key] = Time.time;
        return canMessage;
    }
}
