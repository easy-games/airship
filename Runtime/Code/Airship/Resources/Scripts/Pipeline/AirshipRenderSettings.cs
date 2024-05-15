using System.IO;
using UnityEngine;
using System;
using Unity.Mathematics;
using System.Globalization;
using System.Xml;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Airship {
    [ExecuteInEditMode]
    public class AirshipRenderSettings : MonoBehaviour {
        public enum GlobalLightingMode {
            RealtimeOnly,
            BakedMixed,
        }

        public Vector3 _negativeSunDirectionNormalized;
        public Vector3 _sunDirectionNormalized;
        public Vector3 _sunDirection = math.normalize(new Vector3(0.5f, -2, 1.3f));
        public Vector3 sunDirection {
            set {
                _sunDirectionNormalized = math.normalize(value);
                _negativeSunDirectionNormalized = math.normalize(-value);
                _sunDirection = value;
            }
            get {
                return _sunDirection;
            }
        }

        [SerializeField]
        public float sunBrightness = 1f;
        [SerializeField]
        public float sunShadow = 0.85f;
        [SerializeField]
        public Color sunColor = new Color(1, 1, 1, 1);
        [SerializeField]
        public float skySaturation = 0.3f;
        [SerializeField]
        public Color globalAmbientLight = new Color(1, 1, 1, 1);
        [SerializeField]
        public float globalAmbientBrightness = 0.25f;
        [SerializeField]
        public float globalAmbientOcclusion = 0.25f;
        [SerializeField]
        public bool fogEnabled = true;
        [SerializeField]
        public float fogStart = 75;
        [SerializeField]
        public float fogEnd = 280;
        [SerializeField]
        public Color fogColor = new Color(0.5f, 0.8f, 1, 1);
        [SerializeField]
        public float shadowRange = 100;
        [SerializeField]
        public bool postProcess = true;

        [SerializeField]
        public bool bakeSun = true;

        [SerializeField]
        public GlobalLightingMode globalLightingMode = GlobalLightingMode.BakedMixed;

        [SerializeField]
        public bool doShadows = true;
        [SerializeField]
        public Cubemap cubeMap;

        [SerializeField]
        public float maxBrightness = 1;

        //For baking sunlight in
        [NonSerialized]
        GameObject temporarySun;
        [NonSerialized]
        public Light temporarySunComponent;

        static bool indirectSun = true;

#if UNITY_EDITOR
        public void GetCubemapFromScene() {

            //See if the current scene has a render settings object
            Material skyBox = RenderSettings.skybox;

            if (skyBox == null) {
                Debug.LogError("Scene has no skybox Material - ambient lighting will look incorrect.");
                return;
            }

            //Grab the cubemap from the material
            if (skyBox.HasProperty("_CubemapTex")) {
                cubeMap = skyBox.GetTexture("_CubemapTex") as Cubemap;
            }
            else {
                Debug.LogError("Skybox Material has no _CubemapTex property - ambient lighting will look incorrect.");
                return;
            }

        }
#endif

        private void OnBakeStarted() {
#if UNITY_EDITOR
            if (!bakeSun) {
                return;
            }

            //Doublecheck to make sure theres no other indirect suns
            

            if (indirectSun) {
                //Add a sun and set it to mixed, make it align with our settings
                temporarySun = new GameObject("SunProxy");
                temporarySun.transform.SetParent(transform);
                temporarySun.transform.localPosition = Vector3.zero;
                
                //the rotation looks down our sun dir
                temporarySun.transform.localRotation = Quaternion.LookRotation(sunDirection, Vector3.up);
                
                Light sunLight = temporarySun.AddComponent<Light>();
                sunLight.type = LightType.Directional;
                sunLight.shadows = LightShadows.Soft;
                sunLight.color = sunColor;
                sunLight.intensity = sunBrightness;
                sunLight.lightmapBakeType = LightmapBakeType.Mixed;
                sunLight.bounceIntensity = 1;
                sunLight.shadows = LightShadows.Soft;
                temporarySunComponent = sunLight;
            }

            Light[] suns = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light sun in suns) {
                if (sun == temporarySun) {
                    continue;
                }
                if (sun.name == "SunProxy") {
                    //Destroy it
                    DestroyImmediate(sun.gameObject);
                }
            }

#endif
        }

        private void OnBakeCompleted() {

            if (temporarySun) {
                GameObject.DestroyImmediate(temporarySun);
            }
        }
        private void Awake() {
            RegisterAirshipRenderSettings();
        }
        private void OnEnable() {
            RegisterAirshipRenderSettings();
#if UNITY_EDITOR
            Lightmapping.bakeStarted += OnBakeStarted;
            Lightmapping.bakeCompleted += OnBakeCompleted;
#endif            
        }
        private void Start() {
            RegisterAirshipRenderSettings();
        }

        private void OnDisable() {
            UnregisterAirshipRenderSettings();
#if UNITY_EDITOR            
            Lightmapping.bakeStarted -= OnBakeStarted;
            Lightmapping.bakeCompleted -= OnBakeCompleted;
#endif            
        }

        private void OnDestroy() {
            UnregisterAirshipRenderSettings();
        }
        private void RegisterAirshipRenderSettings() {
            if (gameObject.scene.isLoaded == false) {
                return;
            }

            var manager = Airship.SingletonClassManager<AirshipRenderSettings>.Instance;
            manager.RegisterItem(this);
        }

        private void UnregisterAirshipRenderSettings() {
            var manager = Airship.SingletonClassManager<AirshipRenderSettings>.Instance;
            manager.UnregisterItem(this);
        }

        public static List<AirshipRenderSettings> GetAllAirshipRenderSettings() {
            var manager = Airship.SingletonClassManager<AirshipRenderSettings>.Instance;
            return manager.GetAllActiveItems();
        }

        public static AirshipRenderSettings GetFirstOne() {
            //Todo: Something slightly more clever?
            var manager = Airship.SingletonClassManager<AirshipRenderSettings>.Instance;
            var list = manager.GetAllActiveItems();

            foreach (var value in list) {
                return value;
            }
            return null;
        }
    }

#if UNITY_EDITOR

    //make an editor for it
    [UnityEditor.CustomEditor(typeof(AirshipRenderSettings))]
    public class RenderSettingsEditor : UnityEditor.Editor {
        AirshipRenderSettings settings;


        private bool isCustomGizmoActive = false;

        protected virtual void OnSceneGUI() {
            AirshipRenderSettings settings = (AirshipRenderSettings)target;

            if (settings != null) {
                // Activate custom gizmo mode and suppress default gizmos
                isCustomGizmoActive = true;
                Tools.current = Tool.None;

                // Get the current sun direction as a rotation
                Quaternion currentRotation = Quaternion.LookRotation(settings.sunDirection);

                // Use the RotationHandle to get a new rotation based on user input
                EditorGUI.BeginChangeCheck();
                Quaternion newRotation = Handles.RotationHandle(currentRotation, settings.transform.position);
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(settings, "Change Sun Direction");

                    // Apply the new rotation back to the sun direction vector
                    settings.sunDirection = newRotation * Vector3.forward;
                }

                Handles.color = Color.yellow;
                Vector3 startPosition = settings.transform.position;
                Vector3 endPosition = startPosition + settings.sunDirection.normalized * 5; // Adjust the multiplier for arrow size
                Handles.ArrowHandleCap(0, startPosition, Quaternion.LookRotation(settings.sunDirection), 5, EventType.Repaint);
                Handles.color = Color.white;
                Handles.DrawWireDisc(startPosition, settings.sunDirection, 1);
                Handles.DrawWireDisc(endPosition, settings.sunDirection, 1);


                Vector3 startVector = Vector3.Cross(settings.sunDirection, Vector3.up).normalized;
                if (startVector == Vector3.zero) // This means sunDirection is parallel to Vector3.up, so choose a different vector
                    startVector = Vector3.Cross(settings.sunDirection, Vector3.right).normalized;

                for (int i = 0; i < 4; i++) {
                    Quaternion rotation = Quaternion.AngleAxis(i * 90, settings.sunDirection);
                    Vector3 rotatedStartVector = rotation * startVector;

                    Vector3 discEdgeStart = startPosition + rotatedStartVector; // Edge of the starting disc
                    Vector3 discEdgeEnd = endPosition + rotatedStartVector; // Edge of the ending disc

                    Handles.DrawLine(discEdgeStart, discEdgeEnd);
                }
            }
        }

        public override void OnInspectorGUI() {

            settings = (AirshipRenderSettings)target;

            EditorGUI.BeginChangeCheck();

            // If the custom gizmo was active but we're now interacting with the inspector, reset.
            if (isCustomGizmoActive) {
                Tools.current = Tool.Move; // Or any other default tool you wish to reset to
                isCustomGizmoActive = false;
            }

            //Draw gizmos for all the render settings
            if (settings != null) {

                EditorGUILayout.BeginHorizontal();
                //GUILayout.Button(new GUIContent("?", ""), EditorStyles.miniButton, GUILayout.Width(20));
                EditorGUILayout.LabelField("Lighting Settings", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(4);
                
                //Enum for bake mode

                var oldSettings = settings.globalLightingMode;

                settings.globalLightingMode = (AirshipRenderSettings.GlobalLightingMode)EditorGUILayout.EnumPopup(new GUIContent("Airship Light Mode", "Realtime Only is for scenes that can't use lightmapping. Baked Mixed is lightmapping. Both modes can use realtime lights, but only baked mode can see baked lights."), settings.globalLightingMode);

                if (settings.globalLightingMode == AirshipRenderSettings.GlobalLightingMode.RealtimeOnly) {
                    Lightmapping.lightingSettings.bakedGI = false;
                    //grey out the asset pickers!
                    GUI.enabled = false;
                }
                else {
                    Lightmapping.lightingSettings.bakedGI = true;
                    GUI.enabled = true;
                }
                //Show an asset picker for Lightsettings
                if (Lightmapping.lightingSettings != null) {
                    Lightmapping.lightingSettings = (LightingSettings)EditorGUILayout.ObjectField("Unity Bake Settings", Lightmapping.lightingSettings, typeof(LightingSettings), false);
                }
                //Show an asset picker for the Lighting Asset
                Lightmapping.lightingDataAsset = (LightingDataAsset)EditorGUILayout.ObjectField(new GUIContent("Lighting Data Asset", "This contains the baked lighting information for this particular scene."), Lightmapping.lightingDataAsset, typeof(LightingDataAsset), false);
                GUI.enabled = true;


                //button to update baking
                if (Lightmapping.isRunning) {
                    GUI.enabled = false;
                }
                if (GUILayout.Button(new GUIContent("Generate Lighting","Kick off a unity lightmap bake"))) {
                    
                    Lightmapping.Bake();
                }
                GUI.enabled = true;
                
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.LabelField("Sun", EditorStyles.boldLabel);
                settings.sunBrightness = EditorGUILayout.Slider("Sun Brightness", settings.sunBrightness, 0, 2);
                settings.sunColor = EditorGUILayout.ColorField("Sun Color", settings.sunColor);
                settings.sunDirection = EditorGUILayout.Vector3Field("Sun Direction", settings.sunDirection);
              

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                if (settings.globalLightingMode == AirshipRenderSettings.GlobalLightingMode.RealtimeOnly) {
                    EditorGUILayout.LabelField("Realtime Imagebased Ambient", EditorStyles.boldLabel);
                    settings.globalAmbientBrightness = EditorGUILayout.Slider(new GUIContent("Realtime Ambient Brightness", "This is used on both environment and entities"), settings.globalAmbientBrightness, 0, 2);
                    settings.globalAmbientLight = EditorGUILayout.ColorField("Realtime Ambient Color", settings.globalAmbientLight);

                    //SKYBOX
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Lighting+Reflection Cubemap"); // Label with the same style as Unity's default
                    settings.cubeMap = (Cubemap)EditorGUILayout.ObjectField(
                        settings.cubeMap,
                        typeof(Cubemap),
                        false,
                        GUILayout.Width(16), // Control the width of the ObjectField
                        GUILayout.Height(16) // Control the height of the ObjectField
                    );
                    EditorGUILayout.EndHorizontal();
                    settings.skySaturation = EditorGUILayout.Slider("Cubemap Saturation", settings.skySaturation, 0, 1);

                    //Add a button to invoke fetching the cubemap
                    if (GUILayout.Button("Extract Cubemap From Scenes Sky")) {
                        settings.GetCubemapFromScene();
                    }

                }
                else{
                    EditorGUILayout.LabelField("Baked Lighting Ambient", EditorStyles.boldLabel);
                    RenderSettings.ambientIntensity = EditorGUILayout.Slider(new GUIContent("Baked Ambient", "This is the Lighting->Environment->Intensity Multiplier. It pulls its color from the skybox."), RenderSettings.ambientIntensity, 0, 2);

                    settings.bakeSun = EditorGUILayout.Toggle(new GUIContent("Bake Sun Indirect Lighting", "The sun usually doesn't contribute to indirect lighting. This will make it do so while still remaining a realtime light."), settings.bakeSun);
                }


                if (settings.globalLightingMode == AirshipRenderSettings.GlobalLightingMode.BakedMixed) {
                    //Reflection cubemap
                    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                    EditorGUILayout.LabelField("Reflections", EditorStyles.boldLabel);
                
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Reflection Cubemap"); // Label with the same style as Unity's default
                    settings.cubeMap = (Cubemap)EditorGUILayout.ObjectField(
                        settings.cubeMap,
                        typeof(Cubemap),
                        false,
                        GUILayout.Width(16), // Control the width of the ObjectField
                        GUILayout.Height(16) // Control the height of the ObjectField
                    );
                    EditorGUILayout.EndHorizontal();

                    //Add a button to invoke fetching the cubemap
                    if (GUILayout.Button("Extract Cubemap From Scenes Sky")) {
                        settings.GetCubemapFromScene();
                    }
                }
                
                //SHADOWS
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.LabelField("Sun Shadow", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);
                settings.doShadows = EditorGUILayout.Toggle("Sun Shadows Enabled", settings.doShadows);
                settings.sunShadow = EditorGUILayout.Slider("Shadow Alpha", settings.sunShadow, 0, 1);
                settings.shadowRange = EditorGUILayout.Slider("Shadow Range", settings.shadowRange, 50, 1000);
                settings.globalAmbientOcclusion = EditorGUILayout.Slider("VoxelWorld Ambient Occlusion", settings.globalAmbientOcclusion, 0, 1);
                
                //FOG
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.LabelField("Fog", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);
                settings.fogEnabled = EditorGUILayout.Toggle("Fog Enabled", settings.fogEnabled);
                settings.fogStart = EditorGUILayout.Slider("Fog Start", settings.fogStart, 0, 10000);
                settings.fogEnd = EditorGUILayout.Slider("Fog End", settings.fogEnd, 0, 10000);
                settings.fogColor = EditorGUILayout.ColorField("Fog Color", settings.fogColor);
                
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.LabelField("Post Processing", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);
                settings.postProcess = EditorGUILayout.Toggle("Post Process Enabled", settings.postProcess);
            }

            if (EditorGUI.EndChangeCheck()) {

                //Dirty the scene to mark it needs saving
                if (!Application.isPlaying) {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
                }

                Undo.RegisterUndo(settings, "Changed Lighting");
            }
        }
    }
#endif

}
