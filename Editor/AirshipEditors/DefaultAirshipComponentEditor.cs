

using Luau;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The default editor for airship components
/// </summary>
public class DefaultAirshipComponentEditor : AirshipEditor {
    public override void OnInspectorGUI() {
        // By default we just draw the default inspector here.
        DrawDefaultInspector();
    }
}