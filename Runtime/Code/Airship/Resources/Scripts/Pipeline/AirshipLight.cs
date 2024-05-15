using System.Reflection;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
//Custom editor for limiting the properties on lights
//To what airship can actually render


[CustomEditor(typeof(Light))]
public class CustomLightEditor : Editor {
    public override void OnInspectorGUI() {

        Light light = (Light)target;

        EditorGUI.BeginChangeCheck();

        //Show a limited dropdown for light.type that has only the values that airship can render, which are point, direction, area (baked), spot
        EditorGUILayout.BeginHorizontal();
        //label
        EditorGUILayout.LabelField("Light Type");
        //The order here matters :)
        light.type = (LightType)EditorGUILayout.Popup((int)light.type, new string[] { "Spot", "Directional", "Point", "Area (baked)"});
        EditorGUILayout.EndHorizontal();
        
        //toggle
        if (light.type == LightType.Rectangle) {
            light.lightmapBakeType = LightmapBakeType.Baked;
            var op = light.bakingOutput;
            op.lightmapBakeType = LightmapBakeType.Baked;
            op.mixedLightingMode = MixedLightingMode.Shadowmask;
            op.isBaked = true;
            light.bakingOutput = op;

            //show a readonly bool for isbaked
            //EditorGUI.BeginDisabledGroup(true);
            //EditorGUILayout.Toggle("Is Baked", true);
            //EditorGUI.EndDisabledGroup();
  
            //Add information
            EditorGUILayout.HelpBox("Currently Airship SRP only supports dynamic pointlights, spotlights and directional lights. All other light types are baked.", MessageType.Info);
        }

        //popup the enum
        var output = light.bakingOutput;

        //Show Realtime, Mixed, Baked as strings in a dropdown
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Lightmap Bake Type");

        int selected = 0;
        if (output.lightmapBakeType == LightmapBakeType.Realtime) {
            selected = 0;
        }
        if (output.lightmapBakeType == LightmapBakeType.Mixed) {
            selected = 1;
        }
        if (output.lightmapBakeType == LightmapBakeType.Baked) {
            selected = 2;
        }
        
        selected = EditorGUILayout.Popup(selected, new string[] { "Realtime", "Mixed (Realtime + Baked Indirect)", "Baked" });
        EditorGUILayout.EndHorizontal();
        
        if (selected == 2) { //baked
          
            light.lightmapBakeType = LightmapBakeType.Baked;
            output.lightmapBakeType = LightmapBakeType.Baked;
            output.mixedLightingMode = MixedLightingMode.Shadowmask;
            output.isBaked = true;
            light.bakingOutput = output;
        }
        if (selected == 0) { //realtime
        
            light.lightmapBakeType = LightmapBakeType.Realtime;
            output.lightmapBakeType = LightmapBakeType.Realtime;
            output.mixedLightingMode = MixedLightingMode.Shadowmask;
            output.isBaked = false;
            light.bakingOutput = output;
        }
        if (selected == 1) { //mixed
           
            light.lightmapBakeType = LightmapBakeType.Mixed;
            output.lightmapBakeType = LightmapBakeType.Mixed;
            output.mixedLightingMode = MixedLightingMode.Shadowmask;
            output.isBaked = true;
            light.bakingOutput = output;
        }

        //Color
        light.color = EditorGUILayout.ColorField("Color", light.color);

        //Intensity
        light.intensity = EditorGUILayout.FloatField("Intensity", light.intensity);

        //Indirect intensity if we're baked
        if (selected == 1 || selected == 2) {
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

        //If its an area light
        if (light.type == LightType.Rectangle) {
            //Add a label
            EditorGUILayout.LabelField("Area Light Settings", EditorStyles.boldLabel);
            light.areaSize = EditorGUILayout.Vector2Field("Area Size", light.areaSize);
        }
                
        //Shadows
        if (selected == 2) { //baked

            //light.lightShadowCasterMode = (LightShadowCasterMode)EditorGUILayout.EnumPopup("Shadow Casting Mode", light.lightShadowCasterMode);
            //light.shadowStrength = EditorGUILayout.Slider("Shadow Strength", light.shadowStrength, 0, 1);

            light.shadows = (LightShadows)EditorGUILayout.EnumPopup("Shadow Type", light.shadows);

        }

        if (EditorGUI.EndChangeCheck()) {

            //Dirty the scene to mark it needs saving
            if (!Application.isPlaying) {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(light.gameObject.scene);
            }

            Undo.RegisterUndo(light, "Changed Light Property");
        }
        //Draw Default
        //DrawDefaultInspector();
    }

    private Editor defaultEditor;

    void OnEnable() {
        // Create the default editor
        CreateDefaultEditor();
    }

    void OnDisable() {
        // Clean up the default editor
        if (defaultEditor != null) {
            DestroyImmediate(defaultEditor);
        }
    }
 
    protected virtual void OnSceneGUI() {
        // Invoke the OnSceneGUI of the default editor
        MethodInfo onSceneGUIMethod = defaultEditor.GetType().GetMethod("OnSceneGUI", BindingFlags.NonPublic | BindingFlags.Instance);
        if (onSceneGUIMethod != null) {
            onSceneGUIMethod.Invoke(defaultEditor, null);
        }
    }

    private void CreateDefaultEditor() {
        // Find the internal editor type
        Assembly unityEditorAssembly = typeof(Editor).Assembly;
        System.Type lightEditorType = unityEditorAssembly.GetType("UnityEditor.LightEditor");

        if (lightEditorType != null) {
            defaultEditor = Editor.CreateEditor(targets, lightEditorType);
        }
    }
}
#endif