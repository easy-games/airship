using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Airship {

    [ExecuteInEditMode]
    public class MeshCombinerBone : MonoBehaviour {
        [SerializeField]
        public string boneName;
        [SerializeField]
        public Vector3 rotationOffset = Vector3.zero; // Rotation offset
        [SerializeField]
        public Vector3 scale = Vector3.one;           // Scale
        [SerializeField]
        public Vector3 positionOffset = Vector3.zero; // Position offset


        public Matrix4x4 GetMeshTransform() {
            return Matrix4x4.TRS(positionOffset, Quaternion.Euler(rotationOffset), scale);
        }

        // Start is called before the first frame update
        void Start() {
           
            //If we dont have a bone name, use the game object name of the parent of this transform (assuming one)
            if (boneName == null) {
                if (transform.parent != null) {
                    boneName = transform.parent.name;
                    Debug.Log("Bone name set " + boneName);
                }
            }
        }
    }
}
#if UNITY_EDITOR

[CustomEditor(typeof(Airship.MeshCombinerBone))]
public class MeshCombinerBoneEditor : UnityEditor.Editor {
    private string[] boneNames = new string[0];
    private int selectedIndex = 0;

    void OnEnable() {
        
        UpdateBoneNames();
        var source = (Airship.MeshCombinerBone)target;
        if (source.boneName == null) {
            if (source.transform.parent != null) {
                source.boneName = source.transform.parent.name;
                 
            }
        }

        // Set the initial dropdown index to match the current boneName, if possible
        for (int i = 0; i < boneNames.Length; i++) {
            if (boneNames[i] == ((Airship.MeshCombinerBone)target).boneName) {
                selectedIndex = i;
                break;
            }
        }
    }

    public override void OnInspectorGUI() {
        Airship.MeshCombinerBone myTarget = (Airship.MeshCombinerBone)target;

        // Add a description text
        EditorGUILayout.HelpBox("MeshCombiner will take any static meshes under this and add them to the skinned mesh, creating boneweights as needed.", MessageType.Info);

        // Manual string entry
        myTarget.boneName = EditorGUILayout.TextField("Bone Name (manual entry)", myTarget.boneName);

        // Dropdown for bone names
        if (boneNames.Length > 0) {
            selectedIndex = EditorGUILayout.Popup("Available Bones", selectedIndex, boneNames);
            myTarget.boneName = boneNames[selectedIndex];
        }

        // Fields for rotation, scale, and offset
        myTarget.rotationOffset = EditorGUILayout.Vector3Field("Rotation Offset (Euler)", myTarget.rotationOffset);
        myTarget.scale = EditorGUILayout.Vector3Field("Scale", myTarget.scale);
        myTarget.positionOffset = EditorGUILayout.Vector3Field("Position Offset", myTarget.positionOffset);

        if (GUI.changed) {
            //Find the parent MeshCombiner
            Airship.MeshCombiner meshCombiner = myTarget != null ? myTarget.GetComponentInParent<Airship.MeshCombiner>() : null;
            if (meshCombiner) {
                meshCombiner.CombineMesh();
            }
            
            EditorUtility.SetDirty(myTarget);
            EditorSceneManager.MarkSceneDirty(myTarget.gameObject.scene);
         }
    }

    private void UpdateBoneNames() {
        // Assuming bones are GameObjects (e.g., all GameObjects in the scene)
        var bones = FindObjectsOfType<Transform>();
        boneNames = new string[bones.Length];
        for (int i = 0; i < bones.Length; i++) {
            boneNames[i] = bones[i].name;
        }
    }
}
#endif
