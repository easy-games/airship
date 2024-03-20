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

        public const string defaultCubemapPath = "@Easy/CoreMaterials/Shared/Resources/Skybox/BrightSky/bright_sky_2.png";

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
        public string cubeMapPath = defaultCubemapPath;

        public Color globalAmbientLight = new Color(1, 1, 1, 1);
        public float globalAmbientBrightness = 0.25f;
        public float globalAmbientOcclusion = 0.25f;

        public bool fogEnabled = true;
        public float fogStart = 75;
        public float fogEnd = 280;
        public Color fogColor = new Color(0.5f, 0.8f, 1, 1);

        public float shadowRange = 100;

        public bool postProcess = true;
        public bool doShadows = true;
        
        //Derived fields
        [NonSerialized]
        Cubemap _cubeMap;

        [NonSerialized]
        public string loadedCubemapPath;
        
        public Cubemap cubeMap {
            get {
                float3[] shData = cubeMapSHData; //trigger a load
                return _cubeMap;
            }
            set {
                _cubeMap = value;
            }
        }

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
        
        public void LoadCubemapSHData() {

            this.cubeMap = AssetBridge.Instance.LoadAssetInternal<Cubemap>(this.cubeMapPath, false);

            //load an xml file from this.cubeMapPath using AssetBridge, but without the extension
            //then load the data into this.cubeMapSHData
            if (this.cubeMap == null || this.cubeMapPath == "") {
                Debug.LogError("Failed to load cubemap at path: " + this.cubeMapPath + " - ambient lighting will look incorrect.");
                return;
            }

            //modify the path
            string xmlPath = this.cubeMapPath.Substring(0, this.cubeMapPath.Length - 4) + ".xml";

            TextAsset text = AssetBridge.Instance.LoadAssetInternal<TextAsset>(xmlPath, false);
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
                loadedCubemapPath = this.cubeMapPath;
            }
            else {
                Debug.LogError("Failed to load cubemap XML at path: " + xmlPath + " - ambient lighting will look incorrect.");
            }
        }

        private void Awake() {
            RegisterAirshipRenderSettings();
        }
        private void OnEnable() {
            RegisterAirshipRenderSettings();
        }

        private void OnDisable() {
            UnregisterAirshipRenderSettings();
        }
        

        private void OnDestroy() {
            UnregisterAirshipRenderSettings();
        }

        private void Start() {
            RegisterAirshipRenderSettings();
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

            foreach(var value in list) {
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

        public override void OnInspectorGUI() {

            settings = (AirshipRenderSettings)target;

            //Draw gizmos for all the render settings
            if (settings != null) {

                EditorGUILayout.LabelField("Lighting Settings", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                //Add a divider
                GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
                //Draw a textField for the settings path, and make it read only


                settings.sunBrightness = EditorGUILayout.Slider("Sun Brightness", settings.sunBrightness, 0, 2);
                settings.sunShadow = EditorGUILayout.Slider("Sun Shadow Alpha", settings.sunShadow, 0, 1);
                settings.globalAmbientBrightness = EditorGUILayout.Slider("Global Ambient Brightness", settings.globalAmbientBrightness, 0, 2);

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                settings.sunDirection = EditorGUILayout.Vector3Field("Sun Direction", settings.sunDirection);

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                settings.cubeMapPath = EditorGUILayout.TextField("Sky Cubemap Path", settings.cubeMapPath);
                settings.skySaturation = EditorGUILayout.Slider("Sky Cubemap Saturation", settings.skySaturation, 0, 1);

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

            }

            if (GUI.changed) {
                if (settings.loadedCubemapPath != settings.cubeMapPath) {
                    settings.cubeMap = null;
                    settings.loadedCubemapPath = "";
                    settings.cubeMapSHData = new float3[9];
                    settings.LoadCubemapSHData();
                }
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