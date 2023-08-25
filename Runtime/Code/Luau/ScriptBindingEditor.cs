using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
[CustomEditor(typeof(ScriptBinding))]
public class ScriptBindingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        ScriptBinding binding = (ScriptBinding)target;

        EditorGUILayout.LabelField("Script Binding", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
     
        var style = new GUIStyle(EditorStyles.textField);
        style.alignment = TextAnchor.MiddleRight;
        binding.m_fileFullPath = StripAssetsFolder(EditorGUILayout.TextField("Script File", binding.m_fileFullPath, style, GUILayout.ExpandWidth(true)));
        
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string path = EditorUtility.OpenFilePanelWithFilters("Select script file", "Assets", new string[] { "Airship scripts", "lua" });
            if (!string.IsNullOrEmpty(path))
            {
                string relativePath = FileUtil.GetProjectRelativePath(path);
                binding.m_fileFullPath = StripAssetsFolder(relativePath);
                
            }
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Label("Example: shared/resources/ts/main");

        EditorGUILayout.Space(20);

        EditorGUILayout.Toggle("Error", binding.m_error, EditorStyles.radioButton);
        EditorGUILayout.Toggle("Yielded", binding.m_yielded, EditorStyles.radioButton);
    }

    private string StripAssetsFolder(string filePath)
    {
        int resourcesIndex = string.IsNullOrEmpty(filePath) ? -1 : filePath.IndexOf("/Resources/");
        if (resourcesIndex >= 0)
        {
            filePath = filePath.Substring(resourcesIndex + "/Resources/".Length);
        }
        return filePath;
    }
}
#endif