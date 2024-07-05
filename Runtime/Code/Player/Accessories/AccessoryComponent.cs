using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

[LuauAPI]
public class AccessoryComponent : MonoBehaviour {
    public enum VisibilityMode {
        THIRD_PERSON,
        FIRST_PERSON,
        BOTH
    }

    //must match the bodyMask image that artist UV the body regions to
    public enum BodyMask {
        //Row 0
        NONE = 0,
        HAIR = 1 << 0, 
        FACE = 1 << 1,
        R_ARM_UPPER = 1 << 2,
        L_ARM_UPPER = 1 << 3,
        UNUSED0 = 1 << 4,
        UNUSED1 = 1 << 5,
        UNUSED2 = 1 << 6,
        //Row 1 
        L_ARM_LOWER = 1 << 7,
        L_HAND = 1 << 8,
        R_HAND = 1 << 9,
        R_ARM_LOWER = 1 << 10,
        UNUSED3 = 1 << 11,
        UNUSED4 = 1 << 12,
        UNUSED5 = 1 << 13,
        UNUSED6 = 1 << 14,
        //Row 2
        L_LEG_UPPER = 1 << 15,
        HIPS = 1 << 16,
        TORSO = 1 << 17,
        R_LEG_UPPER = 1 << 18,
        UNUSED7 = 1 << 19,
        UNUSED8 = 1 << 20,
        UNUSED9 = 1 << 21,
        UNUSED10 = 1 << 22,
        //Row 3
        L_LEG_LOWER = 1 << 23,
        L_FOOT = 1 << 24,
        R_FOOT = 1 << 25,
        R_LEG_LOWER = 1 << 26,
        UNUSED11 = 1 << 27,
        UNUSED12 = 1 << 28,
        UNUSED13 = 1 << 29,
        UNUSED14 = 1 << 30,
    }

    public struct BodyMaskInspectorData {
        public BodyMaskInspectorData(BodyMask mask, string name) {
            this.name = name;
            this.bodyMask = mask;
        }

        public string name;
        public BodyMask bodyMask;
    }

    [HideInInspector]
    public static BodyMaskInspectorData[] BodyMaskInspectorDatas = new BodyMaskInspectorData[] {
        new(BodyMask.HIPS, "Hips"),
        new(BodyMask.TORSO, "Torso"),
        new(BodyMask.FACE, "Face"),
        new(BodyMask.HAIR, "Hair"),
        new(BodyMask.L_ARM_UPPER, "Left Arm Upper"),
        new(BodyMask.L_ARM_LOWER, "Left Arm Lower"),
        new(BodyMask.L_HAND, "Left Hand"),
        new(BodyMask.R_ARM_UPPER, "Right Arm Upper"),
        new(BodyMask.R_ARM_LOWER, "Right Arm Lower"),
        new(BodyMask.R_HAND, "Right Hand"),
        new(BodyMask.L_LEG_UPPER, "Left Leg Upper"),
        new(BodyMask.L_LEG_LOWER, "Left Leg Lower"),
        new(BodyMask.L_FOOT, "Left Foot"),
        new(BodyMask.R_LEG_UPPER, "Right Leg Upper"),
        new(BodyMask.R_LEG_LOWER, "Right Leg Lower"),
        new(BodyMask.R_FOOT, "Right Foot"),
    };  

    [FormerlySerializedAs("accessoryId")]
    public string serverClassId;

    private string serverInstanceId;
    
    public AccessorySlot accessorySlot;
    public VisibilityMode visibilityMode = VisibilityMode.BOTH;
    public bool skinnedToCharacter = false;

    //Array of (bones?) that get hidden on body mesh when this accessory is worn
    [HideInInspector]
    public int bodyMask = 0;

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

    public bool HasFlag(BodyMask flag) {
        return (bodyMask & (uint)flag) != 0;
    }
}
 