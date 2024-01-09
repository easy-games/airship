using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Accessory : MonoBehaviour {
    public enum VisibilityMode {
        THIRD_PERSON,
        FIRST_PERSON,
        BOTH
    }
    
    public AccessorySlot accessorySlot;
    public VisibilityMode visibilityMode = VisibilityMode.THIRD_PERSON;
    public bool skinnedToCharacter = false;
    
    private bool _checkedForSkinnedMeshes = false;
    private bool _hasSkinnedMeshes = false;

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

    public void Copy(Accessory other) {
        transform.localPosition = other.transform.localPosition;
        transform.localRotation = other.transform.localRotation;
        transform.localScale = other.transform.localScale;
        accessorySlot = other.accessorySlot;
        visibilityMode = other.visibilityMode;
        skinnedToCharacter = other.skinnedToCharacter;
    }

    public bool HasSkinnedMeshes {
        get {
            if (!_checkedForSkinnedMeshes) {
                _checkedForSkinnedMeshes = true;
                _hasSkinnedMeshes = gameObject.GetComponentInChildren<SkinnedMeshRenderer>() != null;
            }
            
            return _hasSkinnedMeshes;
        }
    }

    public override string ToString() {
        return gameObject.name;
    }

    public int GetSlotNumber() {
        return (int)accessorySlot;
    }
}
