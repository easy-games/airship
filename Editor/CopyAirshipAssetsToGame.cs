using UnityEditor;

[InitializeOnLoad]
public class EditorCopyFilesOnInit
{
    static EditorCopyFilesOnInit()
    {
        //Create Assets/Airship folder if it doesnt exist

        if (!AssetDatabase.IsValidFolder("Assets/Gizmos"))
        {
            AssetDatabase.CreateFolder("Assets", "Gizmos");
        }

        //Copy the Gizmos folder to Assets/Airship/Gizmos
        if (!AssetDatabase.IsValidFolder("Assets/Gizmos/Airship"))
        {
            FileUtil.CopyFileOrDirectory("Packages/gg.easy.airship/Gizmos/Airship", "Assets/Gizmos/Airship");
        }
    }
}