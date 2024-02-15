using System.IO;
using UnityEditor;
using UnityEngine;

public class EasyPrimitiveEditor : MonoBehaviour {
    private const int priorityGroup = -100;

    private enum PrimitiveIndex {
        NONE = -1,
        Capsule,
        Cone,
        Cube,
        CubeBeveled,
        Cylinder,
        Heart,
        Pyramid,
        Quad,
        Sphere,
        SphereHalf,
        SphereQuarter,
        Torus,
        Trapezoid,
        TriangularPrism
    }
    
    //Quad
    [MenuItem("GameObject/3D Object/Airship Quad", false, priorityGroup)]
    static void CreateQuad(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Quad);
    }
    
    //CUBE
    [MenuItem("GameObject/3D Object/Airship Cube", false, priorityGroup)]
    static void CreateCube(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Cube);
    }
    
    //CubeBeveled
    [MenuItem("GameObject/3D Object/Airship CubeBeveled", false, priorityGroup)]
    static void CreateCubeBeveled(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.CubeBeveled);
    }
    
    //TRAPEZOID
    [MenuItem("GameObject/3D Object/Airship Trapezoid", false, priorityGroup)]
    static void CreateTrapezoid(MenuCommand menuCommand) {
        var go = CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Trapezoid);
        var script = go.AddComponent<EasyPrimitive_Trapezoid>();
        script.meshFilter = go.GetComponent<MeshFilter>();
    }
    
    //Sphere
    [MenuItem("GameObject/3D Object/Airship Sphere", false, priorityGroup)]
    static void CreateSphere(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Sphere);
    }
    
    //SphereHalf
    [MenuItem("GameObject/3D Object/Airship Sphere Half", false, priorityGroup)]
    static void CreateSphereHalf(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.SphereHalf);
    }
    
    //SphereQuarter
    [MenuItem("GameObject/3D Object/Airship Sphere Quarter", false, priorityGroup)]
    static void CreateSphereQuarter(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.SphereQuarter);
    }
    
    //Cylinder
    [MenuItem("GameObject/3D Object/Airship Cylinder", false, priorityGroup)]
    static void CreateCylinder(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Cylinder);
    }
    
    //Torus
    [MenuItem("GameObject/3D Object/Airship Torus", false, priorityGroup)]
    static void CreateTorus(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Torus);
    }
    
    //CONE
    [MenuItem("GameObject/3D Object/Airship Cone", false, priorityGroup)]
    static void CreateCone(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Cone);
    }
    
    
    //HEART
    [MenuItem("GameObject/3D Object/Airship Heart", false, priorityGroup)]
    static void CreateHeart(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Heart);
    }
    
    //Capsule
    [MenuItem("GameObject/3D Object/Airship Capsule", false, priorityGroup)]
    static void CreateCapsule(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Capsule);
    }
    
    //TriangularPrism
    [MenuItem("GameObject/3D Object/Airship TriangularPrism", false, priorityGroup)]
    static void CreateTriangularPrism(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.TriangularPrism);
    }
    
    //Pyramid
    [MenuItem("GameObject/3D Object/Airship Pyramid", false, priorityGroup)]
    static void CreatePyramid(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Pyramid);
    }

    private static GameObject CreateMesh(GameObject parent, PrimitiveIndex type) {
        // Create a custom game object
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Packages/gg.easy.airship/Runtime/Resources/Meshes/AirshipPrimitiveShapes.fbx");
        var models = fbx.GetComponentsInChildren<MeshRenderer>();
        var model = models[(int)type].gameObject;
        GameObject go = Instantiate(model);// new GameObject(goName);
        go.name = "Airship"+type;
        var ren = go.GetComponent<MeshRenderer>();
        ren.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bundles/@Easy/CoreMaterials/Shared/Resources/MaterialLibrary/Organic/Clay.mat");
        go.AddComponent<MaterialColor>();
        
        // Ensure it gets reparented if this was a context click (otherwise does nothing)
        if(parent == null && Selection.gameObjects.Length > 0){
            parent = Selection.gameObjects[0];
        }
        GameObjectUtility.SetParentAndAlign(go, parent);
        // Register the creation in the undo system
        Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
        Selection.activeObject = go;
        return go;
    }
    
    //Default Sprite
    [MenuItem("GameObject/2D Object/Airship Sprite", false, priorityGroup)]
    static void CreateSprite(MenuCommand menuCommand) {
        GameObject go = new GameObject("Sprite2D");// new GameObject(goName);
        SpriteRenderer spriteRen = go.AddComponent<SpriteRenderer>();
        spriteRen.material
            = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Bundles/@Easy/Core/Shared/Resources/VFX/CommonResources/Materials/AirshipSprite.mat");
        Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
    }
    
}

[CustomEditor(typeof(EasyPrimitive_Trapezoid))]
public class EasyPrimitive_TrapezoidEditor : UnityEditor.Editor {

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EasyPrimitive_Trapezoid self = (EasyPrimitive_Trapezoid)target;
        DrawDefaultInspector();
        if (GUILayout.Button("Rebuild")) {
            self.Rebuild();
        }
        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(EasyGridAlign))]
public class EasyGridAlignEditor : UnityEditor.Editor {

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EasyGridAlign self = (EasyGridAlign)target;
        DrawDefaultInspector();
        if (GUILayout.Button("Rebuild")) {
            self.Rebuild();
        }
        serializedObject.ApplyModifiedProperties();
    }
}

