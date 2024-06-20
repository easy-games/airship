#if UNITY_EDITOR
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AccessoryOutfitLoader))]
[CanEditMultipleObjects]
public class AccessoryOutfitLoaderEditor : Editor {
    private SerializedProperty outfit;

    private void OnEnable() {
        this.outfit = this.serializedObject.FindProperty("outfit");
    }

    public override void OnInspectorGUI() {
        this.serializedObject.Update();
        EditorGUILayout.PropertyField(this.outfit);
        this.serializedObject.ApplyModifiedProperties();

        var outfitLoader = (AccessoryOutfitLoader)target;
        if (GUILayout.Button("Equip Outfit") && outfitLoader.outfit != null) {
            Debug.Log("Equipping outfit " + outfitLoader.outfit.name);
            var accessoryBuilder = target.GetComponent<AccessoryBuilder>();
            accessoryBuilder.AddAccessories(outfitLoader.outfit.accessories, AccessoryAddMode.ReplaceAll, true);
        }

        if (GUILayout.Button("Clear Outfit")) {
            Debug.Log("Clearing outfit.");
            var accessoryBuilder = target.GetComponent<AccessoryBuilder>();
            accessoryBuilder.RemoveClothingAccessories();
        }
    }
}
#endif