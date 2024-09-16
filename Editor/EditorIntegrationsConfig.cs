using System;
using System.Collections.Generic;
using System.IO;
using Airship.Editor;
using Editor.Util;
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

    [SerializeField] public bool enableMainMenu = false;

    [SerializeField] public bool buildWithoutUpload = false;
    
    [SerializeField] 
    public bool manageTypescriptProject = false;
    
    [SerializeField] 
    public bool safeguardBundleModification = true;

    [SerializeField] public bool selfCompileAllShaders = false;

    #region LUAU OPTIONS
    [SerializeField] public bool promptIfLuauPluginChanged = true;
    
    [SerializeField] public int luauScriptTimeout = 10;
    #endregion

    #region TYPESCRIPT COMPILER OPTIONS

    public bool typescriptVerbose = false;
    public bool typescriptIncremental = false;
    public bool typescriptWriteOnlyChanged = false;
    public bool typescriptRestoreConsoleErrors = true;
    
    public bool typescriptPreventPlayOnError = true;

    public string typescriptProjectConfig => "Assets/tsconfig.json";
    public string typescriptPackagesLocation => "Assets";
    
    [FormerlySerializedAs("automaticTypeScriptCompilation")] 
    [SerializeField] public bool typescriptAutostartCompiler = true;

    /// <summary>
    /// The version of the compiler to use
    /// </summary>
    public TypescriptCompilerVersion compilerVersion = TypescriptCompilerVersion.UseEditorVersion;
    
    public TypescriptEditor typescriptEditor;
    public string typescriptEditorCustomPath = "";

    public IReadOnlyList<string> TypeScriptBuildArgs {
        get {
            List<string> args = new List<string>(new [] { "build" });

            if (typescriptWriteOnlyChanged) {
                args.Add("--writeOnlyChanged");
            }

            args.Add("--json");
            return args;
        }
    }
    
    public IReadOnlyList<string> TypeScriptWatchArgs {
        get {
            List<string> args = new List<string>(new [] { "build", "--watch" });
            
            if (typescriptWriteOnlyChanged) {
                args.Add("--writeOnlyChanged");
            }

            if (typescriptVerbose) {
                args.Add("--verbose");
            }

            return args;
        }
    }
    
    #endregion


    // [SerializeField] public bool alwaysDownloadPackages = false;
    
    public void Modify()
    {
        Save(true);
    }
}