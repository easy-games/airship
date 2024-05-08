using System;
using System.Collections.Generic;
using Airship.Editor;
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
    
    [SerializeField] 
    public bool safeguardBundleModification = true;



    [SerializeField] public bool promptIfLuauPluginChanged = true;

    #region TYPESCRIPT COMPILER OPTIONS

    public bool typescriptVerbose = false;
    public bool typescriptWriteOnlyChanged = false;
    
    public bool typescriptUseDevBuild = false;

    public bool typescriptPreventPlayOnError = true;
    
    [FormerlySerializedAs("automaticTypeScriptCompilation")] 
    [SerializeField] public bool typescriptAutostartCompiler = true;
    
    public TypescriptEditor typescriptEditor;
    public string typescriptEditorCustomPath = "";
    
    public static string TypeScriptLocation =>
        instance.typescriptUseDevBuild ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/npm/node_modules/roblox-ts-dev/utsc-dev.js" : "./node_modules/@easy-games/unity-ts/out/CLI/cli.js";
    
    public IReadOnlyList<string> TypeScriptWatchArgs {
        get {
            List<string> args = new List<string>(new [] { "build", "--watch" });
            
            if (typescriptWriteOnlyChanged) {
                args.Add("--writeOnlyChanged");
            }
            
            args.Add("--package .");
            args.Add("-p ..");

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