using System.Collections.Generic;
using UnityEditor;
using UnityEngine;



public class DeployConfigWindow : EditorWindow
{
    // Add menu named "My Window" to the Window menu
    [MenuItem("Airship/⚙️ Configuration", priority = 311)]
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

        AuthConfig.instance.deployKey = EditorGUILayout.TextField("Staging Deploy Key", AuthConfig.instance.deployKey);
        EditorGUILayout.LabelField("Use \"gcloud auth print-identity-token --project easygg-bw-staging\" to retrieve this token.");
        EditorGUILayout.Space();
        if (GUI.changed) {
            AuthConfig.instance.Modify();
        }
    }
}