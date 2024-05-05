using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Light))]
public class CustomLightEditor : Editor {
    public override void OnInspectorGUI() {

        Light light = (Light)target;

        //Dropdown for light type
        light.type = (LightType)EditorGUILayout.EnumPopup("Light Type", light.type);

        //toggle
        if (light.type == LightType.Tube || light.type == LightType.Rectangle || light.type == LightType.Pyramid || light.type == LightType.Disc) {
            light.lightmapBakeType = LightmapBakeType.Baked;
            var output = light.bakingOutput;
            output.lightmapBakeType = LightmapBakeType.Baked;
            output.mixedLightingMode = MixedLightingMode.Shadowmask;
            output.isBaked = true;
            light.bakingOutput = output;

            //show a readonly bool for isbaked
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Toggle("Is Baked", true);
            EditorGUI.EndDisabledGroup();

            //Add information
            EditorGUILayout.HelpBox("Currently Airship SRP only supports dynamic pointlights, spotlights and directional lights. All other light types are baked.", MessageType.Info);
        }
        else {
            //Show a toggle
            bool baked = EditorGUILayout.Toggle("Is Baked", light.lightmapBakeType == LightmapBakeType.Baked || light.lightmapBakeType == LightmapBakeType.Mixed);

            if (baked == false) {
                light.lightmapBakeType = LightmapBakeType.Realtime;
               
                var output = light.bakingOutput;
                output.lightmapBakeType = LightmapBakeType.Realtime;
                output.mixedLightingMode = MixedLightingMode.Shadowmask;
                output.isBaked = false;
                light.bakingOutput = output;
            }
            else {
                light.lightmapBakeType = LightmapBakeType.Baked;
                var output = light.bakingOutput;
                output.lightmapBakeType = LightmapBakeType.Baked;
                output.mixedLightingMode = MixedLightingMode.Shadowmask;
                output.isBaked = true;
                light.bakingOutput = output;
            }
        }
                

        //Color
        light.color = EditorGUILayout.ColorField("Color", light.color);

        //Intensity
        light.intensity = EditorGUILayout.FloatField("Intensity", light.intensity);

        //Indirect intensity if we're baked
        if (light.lightmapBakeType == LightmapBakeType.Baked) {
            light.bounceIntensity = EditorGUILayout.FloatField("Indirect Intensity", light.bounceIntensity);
        }

        //Range
        light.range = EditorGUILayout.FloatField("Range", light.range);

        //If its a spotlight
        
        if (light.type == LightType.Spot) {
            //Add a label
            EditorGUILayout.LabelField("Spotlight Settings", EditorStyles.boldLabel);
            light.spotAngle = EditorGUILayout.Slider("Spot Angle", light.spotAngle, 1, 179);
            light.innerSpotAngle = EditorGUILayout.Slider("Inner Spot Angle", light.innerSpotAngle, 1, 179);
        }

        //add some blank area
        EditorGUILayout.Space();
        

        //Draw Default
        DrawDefaultInspector();
    }
}