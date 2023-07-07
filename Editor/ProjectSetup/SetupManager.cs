using UnityEditor;

public class SetupManager : Singleton<SetupManager>
{
    public FishNetSetup fishNetSetup;

    [MenuItem("EasyGG/Fix Project", priority = 110)]
    public static void FixProject()
    {
        SetupManager.Instance.fishNetSetup?.Setup();
    }
}