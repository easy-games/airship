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

    [HideInInspector]
    public string serverInstanceId;
    
    public AccessorySlot accessorySlot;
    public VisibilityMode visibilityMode = VisibilityMode.THIRD_PERSON;
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

    public override string ToString() {
        return gameObject.name;
    }

    public int GetSlotNumber() {
        return (int)accessorySlot;
    }
}
