using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SetupManager : Singleton<SetupManager>
{
    private void Awake()
    {
        FixProject();
    }

#if UNITY_EDITOR
    [MenuItem("EasyGG/Repair Project", priority = 110)]
    #endif
    public static void FixProject()
    {
#if UNITY_EDITOR
        FishNetSetup.Setup();
        MiscProjectSetup.Setup();
        PhysicsSetup.Setup();
        Debug.Log("Project repaired!");
#endif
    }
}