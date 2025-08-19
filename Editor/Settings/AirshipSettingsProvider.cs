using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Airship.Editor;
using Code.Bootstrap;
using UnityEditor.Build;
using UnityEngine.Windows;

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
    private bool showBetaOptions = false;

    bool showGithubAccessToken = false;
    bool showAirshipApiKey = false;

    private string currentEnvironment;

    void OnEnvironmentSelected(object envNameBoxed) {
        string envName = (string)envNameBoxed;

        PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone, out var defines);
        var list = new List<string>(defines);

        list.Remove("AIRSHIP_STAGING");

        if (envName == "Staging") {
            list.Add("AIRSHIP_STAGING");
        }

        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, list.ToArray());
    }
   
    public override void OnGUI(string searchContext)
    {
        #if AIRSHIP_STAGING
        this.currentEnvironment = "Staging";
        #else
        this.currentEnvironment = "Production";
        #endif

        // Airship Keys foldout
        showAirshipKeys = EditorGUILayout.BeginFoldoutHeaderGroup(showAirshipKeys, "Authentication Settings");
        if (showAirshipKeys) {
            // Airship API Key
            {
                EditorGUILayout.LabelField("Airship API Key",
                    GUILayout.Width(150)); // Ensure the label is always visible
                EditorGUILayout.BeginHorizontal();
                showAirshipApiKey = GUILayout.Toggle(showAirshipApiKey,
                    new GUIContent(showAirshipApiKey
                        ? EditorGUIUtility.IconContent("d_viewToolZoom")
                        : EditorGUIUtility.IconContent("d_viewToolZoom On")), "Button", GUILayout.Width(20));
                if (showAirshipApiKey) {
                    AuthConfig.instance.deployKey =
                        EditorGUILayout.TextField(AuthConfig.instance.deployKey, GUILayout.ExpandWidth(true));
                } else {
                    GUI.enabled = false; // Disable user interaction
                    EditorGUILayout.TextField("(hidden)", GUILayout.ExpandWidth(true));
                    GUI.enabled = true; // R
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(" (Access your API key from create.airship.gg)",
                    GUILayout.ExpandWidth(true));

                EditorGUILayout.Space();
            }

            #if AIRSHIP_INTERNAL
            {
                // Staging API Key
                EditorGUILayout.LabelField("Staging API Key", GUILayout.Width(150)); // Ensure the label is always visible
                EditorGUILayout.BeginHorizontal();
                showAirshipApiKey = GUILayout.Toggle(showAirshipApiKey, new GUIContent(showAirshipApiKey ? EditorGUIUtility.IconContent("d_viewToolZoom") : EditorGUIUtility.IconContent("d_viewToolZoom On")), "Button", GUILayout.Width(20));
                if (showAirshipApiKey)
                {
                    AuthConfig.instance.stagingApiKey = EditorGUILayout.TextField(AuthConfig.instance.stagingApiKey, GUILayout.ExpandWidth(true));
                } else {
                    GUI.enabled = false; // Disable user interaction
                    EditorGUILayout.TextField("(hidden)", GUILayout.ExpandWidth(true));
                    GUI.enabled = true; // R
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(" (Access your Staging API key from create-staging.airship.gg)", GUILayout.ExpandWidth(true));

                EditorGUILayout.Space();
            }
            #endif

            if (GUI.changed) {
                AuthConfig.instance.Modify();
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox("Changing environments will recompile all scripts. This will take a while.", MessageType.Info, true);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Environment");
        if (EditorGUILayout.DropdownButton(new GUIContent(this.currentEnvironment), FocusType.Passive, new []{GUILayout.Width(120)})) {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Production"), this.currentEnvironment == "Production", OnEnvironmentSelected, "Production");
            menu.AddItem(new GUIContent("Staging"), this.currentEnvironment == "Staging", OnEnvironmentSelected, "Staging");
            menu.ShowAsContext();
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        // Automatic Editor Integrations foldout
        showAutomaticEditorIntegrations = EditorGUILayout.BeginFoldoutHeaderGroup(showAutomaticEditorIntegrations, "Editor Settings");
        if (showAutomaticEditorIntegrations) {
            // Booleans with tooltips
            // EditorIntegrationsConfig.instance.autoAddMaterialColor =  EditorGUILayout.Toggle(new GUIContent("Add MaterialColors", "Add a MaterialColor component to GameObjects that use Airship Materials"), EditorIntegrationsConfig.instance.autoAddMaterialColor);

            // EditorIntegrationsConfig.instance.autoConvertMaterials = EditorGUILayout.Toggle(new GUIContent("Convert Materials", "Convert/Create materials for GameObjects when added to the scene, if they don't have materials that have Airship LightPass stages."), EditorIntegrationsConfig.instance.autoConvertMaterials);

            EditorIntegrationsConfig.instance.autoUpdatePackages = EditorGUILayout.Toggle(new GUIContent("Auto Update Packages", "Airship Packages will automatically update whenever a new update is available."), EditorIntegrationsConfig.instance.autoUpdatePackages);
            EditorIntegrationsConfig.instance.enableMainMenu = EditorGUILayout.Toggle(new GUIContent("Enable Main Menu", "When true, the main menu will show when pressing [Escape]."), EditorIntegrationsConfig.instance.enableMainMenu);
            EditorIntegrationsConfig.instance.buildWithoutUpload = EditorGUILayout.Toggle(new GUIContent("Build Without Upload", "When publishing, this will build the asset bundles but won't upload them to Airship. This is useful for testing file sizes with AssetBundle Browser."), EditorIntegrationsConfig.instance.buildWithoutUpload);

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

        EditorGUILayout.Space();
        showBetaOptions = EditorGUILayout.BeginFoldoutHeaderGroup(showBetaOptions, "Betas");
        if (showBetaOptions) {
            EditorGUILayout.HelpBox("You should not touch these settings unless you know what you're doing. Opting into the betas is accepting you are testing these features.", MessageType.Warning);
            
            GUILayout.Label("Reconciler Changes", EditorStyles.boldLabel);
            GUILayout.Label("Changes how AirshipComponent properties are updated in the Editor.", EditorStyles.label);
            
            EditorGUILayout.BeginHorizontal();
            AirshipReconciliationService.ReconcilerVersion = (ReconcilerVersion) EditorGUILayout.EnumPopup(
                new GUIContent("Reconciliation Type", "This is an experimental feature and subject to change: Changes how the properties on your components are reconciled (updated)"), 
                EditorIntegrationsConfig.instance.useProjectReconcileOption ? EditorIntegrationsConfig.instance.projectReconcilerVersion : AirshipLocalArtifactDatabase.instance.reconcilerVersion);
            EditorGUILayout.EndHorizontal();
            
            var result = EditorGUILayout.Popup(new GUIContent("Reconciliation Beta Target", "How to test this feature"), 
                EditorIntegrationsConfig.instance.useProjectReconcileOption ? 1 : 0, new[] { "Local Instance (Only you)", "Project-wide (All users)" });
            EditorIntegrationsConfig.instance.useProjectReconcileOption = result == 1;
            
            if (GUI.changed) {
                AirshipLocalArtifactDatabase.instance.Modify();
                EditorIntegrationsConfig.instance.Modify();
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
    public static SettingsProvider CreateAirshipSettingsProvider() {
        var provider = new AirshipSettingsProvider(Path, SettingsScope.Project);
        provider.keywords = new[] { "Github", "Airship", "MaterialColors", "Convert", "API", "Key", "Token", "Integration" };
        return provider;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnReload() {
        LuauPlugin.LuauSetScriptTimeoutDuration(EditorIntegrationsConfig.instance.luauScriptTimeout);
    }
}