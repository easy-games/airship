using System.Collections.Generic;
using UnityEditor;
using UnityEngine;



public class DeployConfigWindow : EditorWindow
{
    // Add menu named "My Window" to the Window menu
    [MenuItem("Airship/Configuration", priority = 2001)]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        DeployConfigWindow window = (DeployConfigWindow) EditorWindow.GetWindow(typeof(DeployConfigWindow), false, "Deploy Configuration", true);
        window.name = "Deploy Configuration";
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Authentication Settings", EditorStyles.boldLabel);

        AuthConfig.instance.githubAccessToken =
            EditorGUILayout.TextField("Github Access Token", AuthConfig.instance.githubAccessToken);
        EditorGUILayout.Space();

        AuthConfig.instance.deployKey = EditorGUILayout.TextField("Airship API Key", AuthConfig.instance.deployKey);
        EditorGUILayout.LabelField("Access your API key from create-staging.airship.gg");
        EditorGUILayout.Space();
        if (GUI.changed) {
            AuthConfig.instance.Modify();
        }
    }
}