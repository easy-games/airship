using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

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

    bool showGithubAccessToken = false;
    bool showAirshipApiKey = false;
   
    public override void OnGUI(string searchContext)
    {
        // Airship Keys foldout
        showAirshipKeys = EditorGUILayout.BeginFoldoutHeaderGroup(showAirshipKeys, "Authentication Settings");
        if (showAirshipKeys)
        {
            // Github Access Token
            EditorGUILayout.LabelField("Github Access Token", GUILayout.Width(150)); // Ensure the label is always visible
            EditorGUILayout.BeginHorizontal();
            showGithubAccessToken = GUILayout.Toggle(showGithubAccessToken, new GUIContent(showGithubAccessToken ? EditorGUIUtility.IconContent("d_viewToolZoom") : EditorGUIUtility.IconContent("d_viewToolZoom On")), "Button", GUILayout.Width(20));
            if (showGithubAccessToken)
            {
                AuthConfig.instance.githubAccessToken = EditorGUILayout.TextField(AuthConfig.instance.githubAccessToken, GUILayout.ExpandWidth(true));
            }
            else
            {
                GUI.enabled = false; // Disable user interaction
                EditorGUILayout.TextField("(hidden)", GUILayout.ExpandWidth(true));
                GUI.enabled = true; // R
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

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
        showAutomaticEditorIntegrations = EditorGUILayout.BeginFoldoutHeaderGroup(showAutomaticEditorIntegrations, "Automatic Editor Integrations");
        if (showAutomaticEditorIntegrations)
        {
            // Booleans with tooltips
            EditorIntegrationsConfig.instance.autoAddMaterialColor =  EditorGUILayout.Toggle(new GUIContent("Add MaterialColors", "Add a MaterialColor component to GameObjects that use Airship Materials"), EditorIntegrationsConfig.instance.autoAddMaterialColor);
            
            EditorIntegrationsConfig.instance.autoConvertMaterials = EditorGUILayout.Toggle(new GUIContent("Convert Materials", "Convert/Create materials for GameObjects when added to the scene, if they don't have materials that have Airship LightPass stages."), EditorIntegrationsConfig.instance.autoConvertMaterials);

            if (GUI.changed)
            {
                EditorIntegrationsConfig.instance.Modify();
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
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