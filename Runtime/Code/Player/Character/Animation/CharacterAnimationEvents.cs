using System.Collections.Generic;
using Code.Player.Character;
using UnityEngine;

public class CharacterAnimationEvents : MonoBehaviour {
    public CharacterAnimationHelper anim;
    
    public float minRepeatMessageTime = .075f;
    private Dictionary<string, float> lastMessageTime = new Dictionary<string, float>();
    
    /// <summary>
    /// Animation events triggering function to be passed into TS
    /// </summary>
    public void TriggerEventObj(Object obj){
        if(!anim){
            return;
        }
        //print("Animation object trigger");
        var data = (AnimationTrigger)obj;
        if(data){
            //print("found data object: " + data.key + ", " + data.stringValue + ", " + data.intValue + ", " + data.floatValue);
            if(CanMessage(data.key))
                anim.TriggerEvent(data);
        }
    }

    public void TriggerEvent(string key){
        if(!anim){
            return;
        }
        //print("Animation key trigger: " + key);
        if(CanMessage(key))
            anim.TriggerEvent(key);
    }

    private bool CanMessage(string key){
        bool canMessage = false;
        if(lastMessageTime.TryGetValue(key, out float lastTime)){
            if(Time.time - lastTime > this.minRepeatMessageTime){
                canMessage = true;
            }
        }
        lastMessageTime[key] = Time.time;
        return canMessage;
    }
}
