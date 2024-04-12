using UnityEngine;
using UnityEngine.Serialization;

public class AccessoryComponent : MonoBehaviour {
    public enum VisibilityMode {
        THIRD_PERSON,
        FIRST_PERSON,
        BOTH
    }

    [FormerlySerializedAs("accessoryId")]
    public string serverClassId;

    private string serverInstanceId;
    
    public AccessorySlot accessorySlot;
    public VisibilityMode visibilityMode = VisibilityMode.BOTH;
    public bool skinnedToCharacter = false;

    public Vector3 localPosition {
        get {
            return transform.localPosition;
        }
        set {
            transform.localPosition = value;
        }
    }

    public Quaternion localRotation {
        get {
            return transform.localRotation;
        }
        set {
            transform.localRotation = value;
        }
    }

    public Vector3 localScale {
        get {
            return transform.localScale;
        }
        set {
            transform.localScale = value;
        }
    }

    public void Copy(AccessoryComponent other) {
        transform.localPosition = other.transform.localPosition;
        transform.localRotation = other.transform.localRotation;
        transform.localScale = other.transform.localScale;
        accessorySlot = other.accessorySlot;
        visibilityMode = other.visibilityMode;
        skinnedToCharacter = other.skinnedToCharacter;
    }

    public int GetSlotNumber() {
        return (int)accessorySlot;
    }

    public void SetInstanceId(string id){
        serverInstanceId = id;
        gameObject.GetComponent<AccessoryRandomizer>()?.Apply(id);
    }

    public string GetServerInstanceId(){
        return serverInstanceId;
    }
}
