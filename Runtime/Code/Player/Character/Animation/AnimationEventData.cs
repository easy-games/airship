using UnityEngine;

[CreateAssetMenu(menuName = "Airship/Animation Event Data", fileName = "AnimationEventData")]
public class AnimationEventData : ScriptableObject {
    public string key;
    public string stringValue;
    public int intValue;
    public float floatValue;
}
