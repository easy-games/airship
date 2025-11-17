using UnityEditor;
using UnityEngine;

[FilePath("Assets/Editor/VoxelWorldEditorConfigData.confg", FilePathAttribute.Location.ProjectFolder)]
public class VoxelWorldEditorConfig : ScriptableSingleton<VoxelWorldEditorConfig> {
    [SerializeField] public bool renderVoxelWorldInServerView = false;
    
    public void Modify() {
        Save(true);
    }
}