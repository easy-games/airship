#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[FilePath("Assets/Editor/NetworkConfig.confg", FilePathAttribute.Location.ProjectFolder)]
public class AirshipEditorNetworkConfig : ScriptableSingleton<AirshipEditorNetworkConfig> {
    [SerializeField] public ushort portOverride = 7770;

    public void Modify() {
        Save(true);
    }
}
#endif