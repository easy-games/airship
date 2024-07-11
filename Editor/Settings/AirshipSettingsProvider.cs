using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Airship.Editor;

public class AirshipRootSettingsProvider : SettingsProvider
{
    private const string Path = "Project/Airship";

    public AirshipRootSettingsProvider(string path, SettingsScope scopes = SettingsScope.Project) : base(path, scopes) { }

    public override void OnGUI(string searchContext)
    {
      
    }

    // Register the SettingsProvider
    [SettingsProvider]
    public static SettingsProvider CreateAirshipRootSettingsProvider()
    {
        return new AirshipRootSettingsProvider(Path, SettingsScope.Project);
    }
}


public class AirshipSettingsProvider : SettingsProvider
{
    private const string Path = "Project/Airship/Airship Settings";

    public AirshipSettingsProvider(string path, SettingsScope scopes = SettingsScope.Project) : base(path, scopes) { }

    // Member variables to keep track of foldout states
    private bool showAirshipKeys = true;
    private bool showAutomaticEditorIntegrations = true;
    private bool showLuauOptions = true;
    private bool showNetworkOptions = true;

    bool showGithubAccessToken = false;
    bool showAirshipApiKey = false;
   
    public override void OnGUI(string searchContext)
    {
        // Airship Keys foldout
        showAirshipKeys = EditorGUILayout.BeginFoldoutHeaderGroup(showAirshipKeys, "Authentication Settings");
        if (showAirshipKeys) {
            // Airship API Key
            EditorGUILayout.LabelField("Airship API Key", GUILayout.Width(150)); // Ensure the label is always visible
            EditorGUILayout.BeginHorizontal();
            showAirshipApiKey = GUILayout.Toggle(showAirshipApiKey, new GUIContent(showAirshipApiKey ? EditorGUIUtility.IconContent("d_viewToolZoom") : EditorGUIUtility.IconContent("d_viewToolZoom On")), "Button", GUILayout.Width(20));
            if (showAirshipApiKey)
            {
                AuthConfig.instance.deployKey = EditorGUILayout.TextField(AuthConfig.instance.deployKey, GUILayout.ExpandWidth(true));
            }
            else
            {
                GUI.enabled = false; // Disable user interaction
                EditorGUILayout.TextField("(hidden)", GUILayout.ExpandWidth(true));
                GUI.enabled = true; // R
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(" (Access your API key from create-staging.airship.gg)", GUILayout.ExpandWidth(true));

            EditorGUILayout.Space();

            if (GUI.changed)
            {
                AuthConfig.instance.Modify();
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Create a space
        EditorGUILayout.Space();

        // Automatic Editor Integrations foldout
        showAutomaticEditorIntegrations = EditorGUILayout.BeginFoldoutHeaderGroup(showAutomaticEditorIntegrations, "Editor Settings");
        if (showAutomaticEditorIntegrations) {
            // Booleans with tooltips
            // EditorIntegrationsConfig.instance.autoAddMaterialColor =  EditorGUILayout.Toggle(new GUIContent("Add MaterialColors", "Add a MaterialColor component to GameObjects that use Airship Materials"), EditorIntegrationsConfig.instance.autoAddMaterialColor);

            // EditorIntegrationsConfig.instance.autoConvertMaterials = EditorGUILayout.Toggle(new GUIContent("Convert Materials", "Convert/Create materials for GameObjects when added to the scene, if they don't have materials that have Airship LightPass stages."), EditorIntegrationsConfig.instance.autoConvertMaterials);

            EditorIntegrationsConfig.instance.autoUpdatePackages = EditorGUILayout.Toggle(new GUIContent("Auto Update Packages", "Airship Packages will automatically update whenever a new update is available."), EditorIntegrationsConfig.instance.autoUpdatePackages);
            EditorIntegrationsConfig.instance.enableMainMenu = EditorGUILayout.Toggle(new GUIContent("Enable Main Menu", "When true, the main menu will show when pressing [Escape]."), EditorIntegrationsConfig.instance.enableMainMenu);

            // EditorIntegrationsConfig.instance.manageTypescriptProject = EditorGUILayout.Toggle(new GUIContent("Manage Typescript Projects", "Automatically update Typescript configuration files. (package.json, tsconfig.json)"), EditorIntegrationsConfig.instance.manageTypescriptProject);

            // EditorIntegrationsConfig.instance.typescriptAutostartCompiler = EditorGUILayout.Toggle(
            //     new GUIContent(
            //         "Autostart TypeScript", 
            //         "Automatically run the typescript compiler in Unity"
            //         ), EditorIntegrationsConfig.instance.typescriptAutostartCompiler);
            
            if (GUI.changed) {
                EditorIntegrationsConfig.instance.Modify();
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        
        EditorGUILayout.Space();
        showLuauOptions = EditorGUILayout.BeginFoldoutHeaderGroup(showLuauOptions, "Luau Options");
        if (showLuauOptions) {
            EditorIntegrationsConfig.instance.promptIfLuauPluginChanged = EditorGUILayout.Toggle(
                new GUIContent(
                    "Prompt Luau Plugin Changed", 
                    "Provide a prompt when the Luau plugin changes reminding you to restart Unity."
                ), EditorIntegrationsConfig.instance.promptIfLuauPluginChanged);
            
            var newTimeout = Mathf.Clamp(EditorGUILayout.IntField(
                new GUIContent(
                    "Luau Timeout", 
                    "The amount of seconds a Luau script can run without yielding before being forced to stop."
                ), EditorIntegrationsConfig.instance.luauScriptTimeout, GUILayout.Width(200)), 1, 1000);

            if (newTimeout != EditorIntegrationsConfig.instance.luauScriptTimeout) {
                EditorIntegrationsConfig.instance.luauScriptTimeout = newTimeout;
                LuauPlugin.LuauSetScriptTimeoutDuration(newTimeout);
            }
            
            if (GUI.changed) {
                EditorIntegrationsConfig.instance.Modify();
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        
        EditorGUILayout.Space();
        showNetworkOptions = EditorGUILayout.BeginFoldoutHeaderGroup(showNetworkOptions, "Network Options");
        if (showNetworkOptions) {
            var prev = AirshipEditorNetworkConfig.instance.portOverride;
            AirshipEditorNetworkConfig.instance.portOverride = (ushort) Mathf.Clamp(EditorGUILayout.IntField(
                new GUIContent(
                    "Local Server Port",
                    "Port used for local server that runs when playing in editor. Change if other applications are using default port (including if you have multiple Airship projects running)."
                ), AirshipEditorNetworkConfig.instance.portOverride, GUILayout.Width(200)), 1025, 65535);
            
            if (AirshipEditorNetworkConfig.instance.portOverride != prev) {
                AirshipEditorNetworkConfig.instance.Modify();
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

// #if AIRSHIP_INTERNAL
//         EditorIntegrationsConfig.instance.alwaysDownloadPackages = EditorGUILayout.Toggle(
//             new GUIContent("Always Download Packages", "Ignores cached packages"),
//             EditorIntegrationsConfig.instance.alwaysDownloadPackages);
// #endif
    }


    // Register the SettingsProvider
    [SettingsProvider]
    public static SettingsProvider CreateAirshipSettingsProvider()
    {
        var provider = new AirshipSettingsProvider(Path, SettingsScope.Project);
        provider.keywords = new[] { "Github", "Airship", "MaterialColors", "Convert", "API", "Key", "Token", "Integration" };
        return provider;
    }
}