using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class EasyPrimitiveEditor : MonoBehaviour {
    private const int priorityGroup = -1000;

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

    [MenuItem("Assets/Create/Airship/Viewmodel Variant", false, 1)]
    static void CreateViewmodelVariant() {
        GameObject source =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/AirshipPackages/@Easy/Core/Prefabs/Character/CharacterViewmodel.prefab");
        GameObject objSource = (GameObject)PrefabUtility.InstantiatePrefab(source);
        Object prefab = PrefabUtility.SaveAsPrefabAsset(objSource, $"{CurrentProjectFolderPath}/Viewmodel.prefab");
        Object.DestroyImmediate(objSource);
        Selection.activeObject = prefab;
    }

    [MenuItem("Assets/Create/Airship/Character Variant", false, 1)]
    static void CreateCharacterVariant() {
        GameObject source =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/AirshipPackages/@Easy/Core/Prefabs/Character/AirshipCharacter.prefab");
        GameObject objSource = (GameObject)PrefabUtility.InstantiatePrefab(source);
        Object prefab = PrefabUtility.SaveAsPrefabAsset(objSource, $"{CurrentProjectFolderPath}/Character.prefab");
        Object.DestroyImmediate(objSource);
        Selection.activeObject = prefab;
    }

    public static string CurrentProjectFolderPath
    {
        get
        {
            Type projectWindowUtilType = typeof(ProjectWindowUtil);
            MethodInfo getActiveFolderPath = projectWindowUtilType.GetMethod("GetActiveFolderPath", BindingFlags.Static | BindingFlags.NonPublic);
            object obj = getActiveFolderPath.Invoke(null, new object[0]);
            return obj.ToString();
        }
    }
    
    // This is defaulted to off
#if ENABLE_AIRSHIP_3D_OBJECTS
    //Quad
    [MenuItem("GameObject/Airship/3D Object/Quad", false, priorityGroup+1)]
    static void CreateQuad(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Quad);
    }
    
    //CUBE
    [MenuItem("GameObject/Airship/3D Object/Cube", false, priorityGroup+2)]
    static void CreateCube(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Cube);
    }
    
    //CubeBeveled
    [MenuItem("GameObject/Airship/3D Object/CubeBeveled", false, priorityGroup+3)]
    static void CreateCubeBeveled(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.CubeBeveled);
    }
    
    //TRAPEZOID
    [MenuItem("GameObject/Airship/3D Object/Trapezoid", false, priorityGroup+4)]
    static void CreateTrapezoid(MenuCommand menuCommand) {
        var go = CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Trapezoid);
        var script = go.AddComponent<EasyPrimitive_Trapezoid>();
        script.meshFilter = go.GetComponent<MeshFilter>();
    }
    
    //Sphere
    [MenuItem("GameObject/Airship/3D Object/Sphere", false, priorityGroup+5)]
    static void CreateSphere(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Sphere);
    }
    
    //SphereHalf
    [MenuItem("GameObject/Airship/3D Object/Sphere Half", false, priorityGroup+6)]
    static void CreateSphereHalf(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.SphereHalf);
    }
    
    //SphereQuarter
    [MenuItem("GameObject/Airship/3D Object/Sphere Quarter", false, priorityGroup+7)]
    static void CreateSphereQuarter(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.SphereQuarter);
    }
    
    //Cylinder
    [MenuItem("GameObject/Airship/3D Object/Cylinder", false, priorityGroup+8)]
    static void CreateCylinder(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Cylinder);
    }
    
    //Torus
    [MenuItem("GameObject/Airship/3D Object/Torus", false, priorityGroup+9)]
    static void CreateTorus(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Torus);
    }
    
    //CONE
    [MenuItem("GameObject/Airship/3D Object/Cone", false, priorityGroup+10)]
    static void CreateCone(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Cone);
    }
    
    
    //HEART
    [MenuItem("GameObject/Airship/3D Object/Heart", false, priorityGroup+11)]
    static void CreateHeart(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Heart);
    }
    
    //Capsule
    [MenuItem("GameObject/Airship/3D Object/Capsule", false, priorityGroup+12)]
    static void CreateCapsule(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Capsule);
    }
    
    //TriangularPrism
    [MenuItem("GameObject/Airship/3D Object/TriangularPrism", false, priorityGroup+13)]
    static void CreateTriangularPrism(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.TriangularPrism);
    }
    
    //Pyramid
    [MenuItem("GameObject/Airship/3D Object/Pyramid", false, priorityGroup+14)]
    static void CreatePyramid(MenuCommand menuCommand) {
        CreateMesh(menuCommand.context as GameObject, PrimitiveIndex.Pyramid);
    }
#endif

    private static GameObject CreateMesh(GameObject parent, PrimitiveIndex type) {
        // Create a custom game object
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Packages/gg.easy.airship/Runtime/Resources/Meshes/AirshipPrimitiveShapes.fbx");
        var models = fbx.GetComponentsInChildren<MeshRenderer>();
        var model = models[(int)type].gameObject;
        GameObject go = Instantiate(model);// new GameObject(goName);
        go.name = "Airship"+type;
        var ren = go.GetComponent<MeshRenderer>();
        ren.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/AirshipPackages/@Easy/CoreMaterials//MaterialLibrary/Organic/Clay.mat");
        go.AddComponent<MaterialColorURP>();
        
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

