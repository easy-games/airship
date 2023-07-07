
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SetupManager : Singleton<SetupManager>
{
    public FishNetSetup fishNetSetup;
    public MiscProjectSetup miscProjectSetup;

    #if UNITY_EDITOR
    [MenuItem("EasyGG/Repair Project", priority = 110)]
    #endif
    public static void FixProject()
    {
#if UNITY_EDITOR
        SetupManager.Instance.fishNetSetup?.Setup();
        SetupManager.Instance.miscProjectSetup?.Setup();
#endif
        Debug.Log("Project repaired!");
    }
}