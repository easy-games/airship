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
        public bool doShadows = true;
        [SerializeField]
        public Cubemap cubeMap;

        [SerializeField]
        public float maxBrightness = 1;

        [NonSerialized]
        GameObject temporarySun;

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
            if (!bakeSun) {
                return;
            }

            if (indirectSun) {//Add a sun and set it to mixed, make it align with our settings
                temporarySun = new GameObject("Sun");
                temporarySun.transform.SetParent(transform);
                temporarySun.transform.localPosition = Vector3.zero;
                //Create the rotation to look down our sun dir
                temporarySun.transform.localRotation = Quaternion.LookRotation(sunDirection, Vector3.up);
                Light sunLight = temporarySun.AddComponent<Light>();
                sunLight.type = LightType.Directional;
                sunLight.shadows = LightShadows.Soft;
                sunLight.color = sunColor;
                sunLight.intensity = sunBrightness;
                sunLight.lightmapBakeType = LightmapBakeType.Mixed;
                sunLight.bounceIntensity = 1;
                sunLight.shadows = LightShadows.Soft;
            }

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

            Lightmapping.bakeStarted += OnBakeStarted;
            Lightmapping.bakeCompleted += OnBakeCompleted;
        }
        private void Start() {
            RegisterAirshipRenderSettings();
        }

        private void OnDisable() {
            UnregisterAirshipRenderSettings();
            Lightmapping.bakeStarted -= OnBakeStarted;
            Lightmapping.bakeCompleted -= OnBakeCompleted;
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

                EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                //Add a divider
                GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });

                settings.maxBrightness = EditorGUILayout.Slider("MaxBrightness (HDR)", settings.maxBrightness, 0, 10);

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                settings.sunBrightness = EditorGUILayout.Slider("Sun Brightness", settings.sunBrightness, 0, 2);
                settings.globalAmbientBrightness = EditorGUILayout.Slider("Global Ambient Brightness", settings.globalAmbientBrightness, 0, 2);

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                settings.sunDirection = EditorGUILayout.Vector3Field("Sun Direction", settings.sunDirection);

                //SHADOWS
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.LabelField("Shadow", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);
                settings.doShadows = EditorGUILayout.Toggle("Shadows Enabled", settings.doShadows);
                settings.sunShadow = EditorGUILayout.Slider("Shadow Alpha", settings.sunShadow, 0, 1);
                settings.shadowRange = EditorGUILayout.Slider("ShadowRange", settings.shadowRange, 50, 1000);
                settings.globalAmbientOcclusion = EditorGUILayout.Slider("VoxelWorld Ambient Occlusion", settings.globalAmbientOcclusion, 0, 1);

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.LabelField("Lightmapping", EditorStyles.boldLabel);
                settings.bakeSun = EditorGUILayout.Toggle("Bake Sun Indirect Lighting", settings.bakeSun);

                //FOG
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.LabelField("Fog", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);
                settings.fogEnabled = EditorGUILayout.Toggle("Fog Enabled", settings.fogEnabled);
                settings.fogStart = EditorGUILayout.Slider("Fog Start", settings.fogStart, 0, 10000);
                settings.fogEnd = EditorGUILayout.Slider("Fog End", settings.fogEnd, 0, 10000);
                settings.fogColor = EditorGUILayout.ColorField("Fog Color", settings.fogColor);

                //SKYBOX
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.LabelField("Sky", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);
                settings.cubeMap = (Cubemap)EditorGUILayout.ObjectField("Cubemap", settings.cubeMap, typeof(Cubemap), false);

                settings.skySaturation = EditorGUILayout.Slider("Sky Cubemap Saturation", settings.skySaturation, 0, 1);

                //Add a button to invoke fetching the cubemap
                if (GUILayout.Button("Get Cubemap From Scenes Sky")) {
                    settings.GetCubemapFromScene();
                }

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                settings.sunColor = EditorGUILayout.ColorField("Sun Color", settings.sunColor);
                settings.globalAmbientLight = EditorGUILayout.ColorField("Ambient Color", settings.globalAmbientLight);

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                settings.fogEnabled = EditorGUILayout.Toggle("Fog Enabled", settings.fogEnabled);
                settings.fogStart = EditorGUILayout.Slider("Fog Start", settings.fogStart, 0, 10000);
                settings.fogEnd = EditorGUILayout.Slider("Fog End", settings.fogEnd, 0, 10000);
                settings.fogColor = EditorGUILayout.ColorField("Fog Color", settings.fogColor);

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                settings.globalAmbientOcclusion = EditorGUILayout.Slider("VoxelWorld Ambient Occlusion", settings.globalAmbientOcclusion, 0, 1);

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                settings.doShadows = EditorGUILayout.Toggle("Shadows Enabled", settings.doShadows);
                settings.shadowRange = EditorGUILayout.Slider("ShadowRange", settings.shadowRange, 50, 1000);
                settings.sunShadow = EditorGUILayout.Slider("Sun Shadow Alpha", settings.sunShadow, 0, 1);

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
