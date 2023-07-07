using UnityEditor;

public class SetupManager : Singleton<SetupManager>
{
    public FishNetSetup fishNetSetup;
    public MiscProjectSetup miscProjectSetup;

    [MenuItem("EasyGG/Repair Project", priority = 110)]
    public static void FixProject()
    {
        SetupManager.Instance.fishNetSetup?.Setup();
        SetupManager.Instance.miscProjectSetup?.Setup();
    }
}