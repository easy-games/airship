using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
[CustomEditor(typeof(LuauBinding))]
public class LuauBindingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LuauBinding binding = (LuauBinding)target;

        EditorGUILayout.LabelField("Luau Binding", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
     
        var style = new GUIStyle(EditorStyles.textField);
        style.alignment = TextAnchor.MiddleRight;
        binding.m_fileFullPath = StripAssetsFolder(EditorGUILayout.TextField("Lua File", binding.m_fileFullPath, style, GUILayout.ExpandWidth(true)));
        
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string path = EditorUtility.OpenFilePanelWithFilters("Select .lua file", "Assets", new string[] { "Lua scripts", "lua" });
            if (!string.IsNullOrEmpty(path))
            {
                string relativePath = FileUtil.GetProjectRelativePath(path);
                binding.m_fileFullPath = StripAssetsFolder(relativePath);
                
            }
        }

        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.Toggle("Error", binding.m_error, EditorStyles.radioButton);
        EditorGUILayout.Toggle("Yielded", binding.m_yielded, EditorStyles.radioButton);
        EditorGUILayout.EndHorizontal();
    }

    private string StripAssetsFolder(string filePath)
    {
        int resourcesIndex = filePath.IndexOf("/Resources/");
        if (resourcesIndex >= 0)
        {
            filePath = filePath.Substring(resourcesIndex + "/Resources/".Length);
        }
        return filePath;
    }
}
#endif