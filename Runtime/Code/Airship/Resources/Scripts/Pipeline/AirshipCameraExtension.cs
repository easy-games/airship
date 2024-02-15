using UnityEngine;
using Airship;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class AirshipCameraExtension : MonoBehaviour
{
    [SerializeField]
    public AirshipRenderSettings airshipRenderSettings;
 
    
}

//Create an editor for AirshipCameraExtension
#if UNITY_EDITOR

[CustomEditor(typeof(AirshipCameraExtension))]
public class AirshipCameraExtensionEditor : Editor
{
    public override void OnInspectorGUI()
    {
         
        //Create a picker for the renderSetttings
        var airshipCameraExtension = (AirshipCameraExtension)target;
        airshipCameraExtension.airshipRenderSettings = (AirshipRenderSettings)EditorGUILayout.ObjectField("Airship Render Settings", airshipCameraExtension.airshipRenderSettings, typeof(AirshipRenderSettings), true);

        //add a tooltip for it
        EditorGUILayout.HelpBox("This RenderSettings will be used if this is the first camera in a given scene.", MessageType.Info);
    }
}
#endif