using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Player.Accessories {
    [LuauAPI]
    [Icon("Packages/gg.easy.airship/Editor/shirt-outline-icon.png")]
    public class AccessoryComponent : MonoBehaviour {
        public enum VisibilityMode {
            ThirdPerson,
            FirstPerson,
            Both
        }

        //must match the bodyMask image that artist UV the body regions to
        public enum BodyMask {
            //Row 0
            NONE = 0,
            HAIR = 1 << 0,
            FACE = 1 << 1,
            R_ARM_UPPER = 1 << 2,
            L_ARM_UPPER = 1 << 3,
            EARS = 1 << 4,
            UNUSED1 = 1 << 5,
            UNUSED2 = 1 << 6,
            //Row 1
            L_ARM_LOWER = 1 << 7,
            L_HAND = 1 << 8,
            R_HAND = 1 << 9,
            R_ARM_LOWER = 1 << 10,
            R_ARM_JOINTS = 1 << 11,
            L_ARM_JOINTS = 1 << 12,
            UNUSED5 = 1 << 13,
            UNUSED6 = 1 << 14,
            //Row 2
            L_LEG_UPPER = 1 << 15,
            HIPS = 1 << 16,
            TORSO = 1 << 17,
            R_LEG_UPPER = 1 << 18,
            R_LEG_JOINTS = 1 << 19,
            L_LEG_JOINTS = 1 << 20,
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

        void Start() {
            var renderers = this.gameObject.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers) {
                if (!r.sharedMaterial.shader.isSupported) {
                    r.sharedMaterial.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            // foreach (var r in renderers) {
            //     foreach (var mat in r.sharedMaterials) {
            //         if (!mat.shader.isSupported) {
            //             mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            //         }
            //     }
            // }
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
            new(BodyMask.EARS, "Ears"),
            new(BodyMask.L_ARM_UPPER, "Left Arm Upper"),
            new(BodyMask.L_ARM_JOINTS, "Left Arm Joints"),
            new(BodyMask.L_ARM_LOWER, "Left Arm Lower"),
            new(BodyMask.L_HAND, "Left Hand"),
            new(BodyMask.R_ARM_UPPER, "Right Arm Upper"),
            new(BodyMask.R_ARM_JOINTS, "Right Arm Joints"),
            new(BodyMask.R_ARM_LOWER, "Right Arm Lower"),
            new(BodyMask.R_HAND, "Right Hand"),
            new(BodyMask.L_LEG_UPPER, "Left Leg Upper"),
            new(BodyMask.L_LEG_JOINTS, "Left Leg Joints"),
            new(BodyMask.L_LEG_LOWER, "Left Leg Lower"),
            new(BodyMask.L_FOOT, "Left Foot"),
            new(BodyMask.R_LEG_UPPER, "Right Leg Upper"),
            new(BodyMask.R_LEG_JOINTS, "Right Leg Joints"),
            new(BodyMask.R_LEG_LOWER, "Right Leg Lower"),
            new(BodyMask.R_FOOT, "Right Foot"),
        };

        public static string GetBodyMaskName(int bit) {

            if (bit == 0) {
                return "NONE";
            }
            foreach (var data in BodyMaskInspectorDatas) {
                if (data.bodyMask == (BodyMask)(1<<bit)) {
                    return data.name;
                }
            }
            return "UNUSED";
        }


        public AccessorySlot accessorySlot = AccessorySlot.RightHand;
        public VisibilityMode visibilityMode = VisibilityMode.Both;
        public bool skinnedToCharacter = false;

        [SerializeField]
        public List<Mesh> meshLods = new();

        [Tooltip("True if the mesh should be combined with the character for mesh deformation. This is usually true for clothing, but false for static held items like swords.")]
        [Obsolete]
        public bool canMeshCombine = false;

        //Array of (bones?) that get hidden on body mesh when this accessory is worn
        [HideInInspector]
        public int bodyMask = 0;

        [Header("Legacy IDs")]
        [HideFromTS]
        public string serverClassId;
        [HideFromTS]
        public string serverClassIdStaging;
        private string serverInstanceId;

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

        public string GetServerClassId() {
#if AIRSHIP_STAGING
            return this.serverClassIdStaging;
#else
            return this.serverClassId;
#endif
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
}
 