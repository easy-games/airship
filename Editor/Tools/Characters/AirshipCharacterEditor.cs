#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

using Object = UnityEngine.Object;

public class AirshipCharacterEditor : MonoBehaviour {
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
}
#endif
