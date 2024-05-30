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
    
    [SerializeField] 
    public bool manageTypescriptProject = false;
    
    [SerializeField] 
    public bool safeguardBundleModification = true;

    [SerializeField] public bool promptIfLuauPluginChanged = true;

    #region TYPESCRIPT COMPILER OPTIONS

    public bool typescriptVerbose = false;
    public bool typescriptWriteOnlyChanged = false;
    
    [Obsolete]
    public bool typescriptUseDevBuild = false;

    public bool typescriptPreventPlayOnError = true;

    public string typescriptProjectConfig => "Assets/tsconfig.json";
    public string typescriptPackagesLocation => "Assets";
    
    [FormerlySerializedAs("automaticTypeScriptCompilation")] 
    [SerializeField] public bool typescriptAutostartCompiler = true;

    /// <summary>
    /// The version of the compiler to use
    /// </summary>
    public TypescriptCompilerVersion compilerVersion = TypescriptCompilerVersion.UsePackageJson;
    
    public TypescriptEditor typescriptEditor;
    public string typescriptEditorCustomPath = "";

    public static bool UseBundledCompiler => instance.compilerVersion == TypescriptCompilerVersion.UseBuiltIn;
    public static string TypeScriptLocation {
        get {
            var option = instance.compilerVersion;
            
            switch (option) {
                case TypescriptCompilerVersion.UseLocalDevelopmentBuild:
#if UNITY_EDITOR_OSX
                return "/usr/local/lib/node_modules/roblox-ts-dev/utsc-dev.js";
#else
                    return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                           "/npm/node_modules/roblox-ts-dev/utsc-dev.js";
#endif
                case TypescriptCompilerVersion.UseBuiltIn:
                    return Path.GetFullPath("Packages/gg.easy.airship/Editor/TypescriptCompiler~/utsc.js");
                case TypescriptCompilerVersion.UsePackageJson:
                default:
                    return PosixPath.Join(Path.GetRelativePath(Application.dataPath, TypescriptProjectsService.Project.Package.Directory), "node_modules/@easy-games/unity-ts/out/CLI/cli.js");
            }
        }   
    }
    
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