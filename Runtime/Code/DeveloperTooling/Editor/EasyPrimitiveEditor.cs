using UnityEditor;
using UnityEngine;

public class EasyPrimitiveEditor : MonoBehaviour {
    private const int priorityGroup = -100;
    //CUBE
    //[CreateAssetMenu(fileName = "AirshipCube", menuName = "3D Objects/Airship Cube", order = 100)]
    [MenuItem("GameObject/3D Object/Airship Cube", false, priorityGroup)]
    static void CreateCube(MenuCommand menuCommand) {
        CreateMesh("AirshipCube", menuCommand.context as GameObject);
    }
    
    //HEART
    [MenuItem("GameObject/3D Object/Airship Heart", false, priorityGroup)]
    static void CreateHeart(MenuCommand menuCommand) {
        CreateMesh("AirshipHeart", menuCommand.context as GameObject);
    }

    private static void CreateMesh(string goName, GameObject parent) {
        // Create a custom game object
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Packages/com.unity.images-library/Example/Images/image.png");
        var models = fbx.GetComponentsInChildren<MeshRenderer>();
        GameObject go = Instantiate(models[0].gameObject);// new GameObject(goName);
        //go.AddComponent<MeshFilter>();
        //go.AddComponent<MeshRenderer>();
        // Ensure it gets reparented if this was a context click (otherwise does nothing)
        GameObjectUtility.SetParentAndAlign(go, parent);
        // Register the creation in the undo system
        Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
        Selection.activeObject = go;
    }
}
