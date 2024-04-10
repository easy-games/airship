using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

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



    [SerializeField] public bool promptIfLuauPluginChanged = true;

    #region TYPESCRIPT COMPILER OPTIONS

    public bool typescriptVerbose = false;
    public bool typescriptWriteOnlyChanged = false;
    
    public bool typescriptUseDevBuild = false;
    
    [FormerlySerializedAs("automaticTypeScriptCompilation")] 
    [SerializeField] public bool typescriptAutostartCompiler = true;
    
    #endregion

    public string TypeScriptLocation =>
        typescriptUseDevBuild ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/npm/node_modules/roblox-ts-dev/utsc-dev.js" : "./node_modules/@easy-games/unity-ts/out/CLI/cli.js";
    
    public IReadOnlyList<string> TypeScriptWatchArgs {
        get {
            List<string> args = new List<string>(new [] { "build", "--watch" });

            if (typescriptUseDevBuild) {
                args.Add("-E");
            }
            
            if (typescriptWriteOnlyChanged) {
                args.Add("--writeOnlyChanged");
            }

            if (typescriptVerbose) {
                args.Add("--verbose");
            }

            return args;
        }
    }
    
    public void Modify()
    {
        Save(true);
    }
}