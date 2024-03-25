using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[FilePath("Assets/Editor/EditorIntegrationsConfigData.confg", FilePathAttribute.Location.ProjectFolder)]
public class EditorIntegrationsConfig : ScriptableSingleton<EditorIntegrationsConfig>
{

    [SerializeField]
    public bool autoAddMaterialColor = true;

    [SerializeField] 
    public bool autoConvertMaterials = true;

    [SerializeField] 
    public bool autoUpdatePackages = true;
    
    [SerializeField] 
    public bool manageTypescriptProject = false;

    [SerializeField] public bool automaticTypeScriptCompilation = true;
    
    public void Modify()
    {
        Save(true);
    }
}