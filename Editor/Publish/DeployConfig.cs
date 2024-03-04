using System.Collections.Generic;
using UnityEditor;
using UnityEngine;



public class DeployConfigWindow : EditorWindow
{
    // Add menu named "My Window" to the Window menu
    [MenuItem("Airship/Configuration", priority = 2001)]
    static void Init()
    {
        //Open the Settings menu to Airship/Airship Settings
        SettingsService.OpenProjectSettings("Project/Airship/Airship Settings");
    }
}