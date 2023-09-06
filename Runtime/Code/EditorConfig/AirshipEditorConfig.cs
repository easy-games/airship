using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

[CreateAssetMenu(fileName = "AirshipEditorConfig", menuName = "Airship/AirshipEditorConfig", order = 0)]
public class AirshipEditorConfig : ScriptableObject
{
    public bool useBundlesInEditor = false;
    public bool buildBundlesOnPlay = false;
    public bool downloadPackages = false;

    public static AirshipEditorConfig Load() {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<AirshipEditorConfig>("Assets/AirshipEditorConfig.asset");
#endif
        return null;
    }
}