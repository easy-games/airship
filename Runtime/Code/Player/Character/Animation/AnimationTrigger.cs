using UnityEngine;

[CreateAssetMenu(menuName = "Airship/Animation Trigger", fileName = "New Animation Trigger")]
public class AnimationTrigger : ScriptableObject {
    public string key;
    public string stringValue;
    public int intValue;
    public float floatValue;
}
