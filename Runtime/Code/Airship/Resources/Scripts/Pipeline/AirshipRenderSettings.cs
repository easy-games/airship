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

        public float sunBrightness = 1f;
        public float sunShadow = 0.85f;
        public Color sunColor = new Color(1, 1, 1, 1);

        public float skySaturation = 0.3f;

        public Color globalAmbientLight = new Color(1, 1, 1, 1);
        public float globalAmbientBrightness = 0.25f;
        public float globalAmbientOcclusion = 0.25f;

        public bool fogEnabled = true;
        public float fogStart = 75;
        public float fogEnd = 280;
        public Color fogColor = new Color(0.5f, 0.8f, 1, 1);

        public float shadowRange = 100;

        public bool postProcess = true;
        public bool convertColorTosRGB = true;
        public bool doShadows = true;

        public Cubemap cubeMap;
        public TextAsset cubemapCoefs;

        [NonSerialized]
        float3[] _cubeMapSHData;

        public float3[] cubeMapSHData {
            get {
                if (_cubeMapSHData == null) {
                    _cubeMapSHData = new float3[9];
                    LoadCubemapSHData();
                }
                return _cubeMapSHData;
            }
            set {
                _cubeMapSHData = value;
            }
        }


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

            //Get the asset path
            string path = AssetDatabase.GetAssetPath(cubeMap);
            if (path != null) {
                //Find a paired text file
                string[] split = path.Split('.');
                string textPath = split[0];// + ".txt";

                //Strip off everything before "/Resources"
                int index = textPath.IndexOf("/Resources");
                if (index != -1) {
                    textPath = textPath.Substring(index + 11);
                }

                cubemapCoefs = Resources.Load<TextAsset>(textPath);
            }

            LoadCubemapSHData();
        }
#endif


        public void LoadCubemapSHData() {


            TextAsset text = cubemapCoefs;
            if (text) {
                //The data is 9 coefficients stored like so
                /*
                < SphericalHarmonicCoefficients >
                < Coefficient index = "0" value = "(1.44, 1.91, 2.37, 3.47)" />
                etc
                */

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(text.text);

                XmlNodeList nodes = doc.GetElementsByTagName("Coefficient");
                for (int i = 0; i < nodes.Count; i++) {
                    string[] values = nodes[i].Attributes["value"].Value.Split(',');
                    float r = float.Parse(values[0].Substring(1), CultureInfo.InvariantCulture);
                    float g = float.Parse(values[1], CultureInfo.InvariantCulture);
                    float b = float.Parse(values[2], CultureInfo.InvariantCulture);
                    this.cubeMapSHData[i] = new float3(r, g, b);
                }
                //Debug.Log("Cubemap loaded from " + this.cubeMapPath);
                //loadedCubemapPath = this.cubeMapPath;
            }
            else {
                Debug.LogError("Failed to load cubemap coefs - ambient lighting will look incorrect.");
            }
        }

        private void Awake() {
            RegisterAirshipRenderSettings();
        }
        private void OnEnable() {
            RegisterAirshipRenderSettings();
        }
        private void Start() {
            RegisterAirshipRenderSettings();
        }

        private void OnDisable() {
            UnregisterAirshipRenderSettings();
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

            // If the custom gizmo was active but we're now interacting with the inspector, reset.
            if (isCustomGizmoActive) {
                Tools.current = Tool.Move; // Or any other default tool you wish to reset to
                isCustomGizmoActive = false;
            }

            //Draw gizmos for all the render settings
            if (settings != null) {

                EditorGUILayout.LabelField("Lighting Settings", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                //Add a divider
                GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
                
                settings.sunBrightness = EditorGUILayout.Slider("Sun Brightness", settings.sunBrightness, 0, 2);
                settings.sunShadow = EditorGUILayout.Slider("Sun Shadow Alpha", settings.sunShadow, 0, 1);
                settings.globalAmbientBrightness = EditorGUILayout.Slider("Global Ambient Brightness", settings.globalAmbientBrightness, 0, 2);

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                settings.sunDirection = EditorGUILayout.Vector3Field("Sun Direction", settings.sunDirection);

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                                
                settings.cubeMap = (Cubemap)EditorGUILayout.ObjectField("Cubemap", settings.cubeMap, typeof(Cubemap), false);
                
                settings.cubemapCoefs = (TextAsset)EditorGUILayout.ObjectField("Cubemap Coefficients", settings.cubemapCoefs, typeof(TextAsset), false);
                settings.skySaturation = EditorGUILayout.Slider("Sky Cubemap Saturation", settings.skySaturation, 0, 1);
                
                if (settings.cubeMap == null || settings.cubemapCoefs == null)
                {
                    //Add a button to invoke fetching the cubemap
                    if (GUILayout.Button("Get Cubemap From Scene")) {
                        settings.GetCubemapFromScene();
                    }
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
                settings.shadowRange = EditorGUILayout.Slider("ShadowRange", settings.shadowRange, 50, 1000);

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                settings.doShadows = EditorGUILayout.Toggle("Shadows Enabled", settings.doShadows);

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                settings.postProcess = EditorGUILayout.Toggle("Post Process Enabled", settings.postProcess);
                settings.convertColorTosRGB = EditorGUILayout.Toggle("Output to sRGB Color", settings.convertColorTosRGB);

            }

            if (GUI.changed) {

                //Dirty the scene to mark it needs saving
                if (!Application.isPlaying) {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
                }
            }

            /*
            //add a text field to get just the string asset path to a cubemap for the skybox, for world.cubeMapPath
            world.cubeMapPath = EditorGUILayout.TextField("Cube Map Path", world.cubeMapPath);
            //Add a button to pick the cubeMap file, and store its path in world.cubeMapPath
            if (GUILayout.Button("Pick Cube Map"))
            {
                CubemapPickerWindow.Show(cubemapPath =>
                {
                    cubemapPath = cubemapPath.ToLower();

                    string relativePath = cubemapPath.Split("/resources/")[1];
                    world.cubeMapPath = relativePath;
                });

            }*/
        }

    }
#endif

}