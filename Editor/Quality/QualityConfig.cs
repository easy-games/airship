#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Editor.Quality {
    [InitializeOnLoad]
    public class QualityConfig {
        private const string LOW_QUALITY_NAME = "Low";
        private const string NORMAL_QUALITY_NAME = "Normal";

#if AIRSHIP_INTERNAL
        [MenuItem("Airship/Quality/Config Mobile")]
#endif
        public static void ConfigureLowQualityLevel() {
            SwapToQualityLevel(LOW_QUALITY_NAME);
            ConfigureForMobile();
            SaveChangesToQualitySettings();
        }

#if AIRSHIP_INTERNAL
        [MenuItem("Airship/Quality/Config Normal")]
#endif
        public static void ConfigureNormalQualityLevel() {
            SwapToQualityLevel(NORMAL_QUALITY_NAME);
            ConfigureForNormal();
            SaveChangesToQualitySettings();
        }

        private static void ConfigureForMobile() {
            // Some sample quality settings from ChatGPT. Leaving in case it's useful for you Liam.
            // - Luke

            // Core toggles
            // QualitySettings.vSyncCount = 0; // let you drive FPS caps per-platform
            // QualitySettings.pixelLightCount = 1;
            // QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            // QualitySettings.antiAliasing = 0; // prefer TAA/FXAA in URP if needed
            // QualitySettings.softParticles = false;
            // QualitySettings.realtimeReflectionProbes = false;
            // QualitySettings.billboardsFaceCameraPosition = false;
            //
            // // LODs & textures
            // QualitySettings.lodBias = 0.6f;
            // QualitySettings.maximumLODLevel = 0; // 0 = use all LODs; increase to force lower-detail LODs
            // QualitySettings.globalTextureMipmapLimit = 1; // 0=full res, 1=half, 2=quarter (tune as needed)
            // QualitySettings.streamingMipmapsActive = true;
            //
            // // Shadows (you can enable HardOnly and short distance if you really need them)
            // QualitySettings.shadows = ShadowQuality.Disable;
            // QualitySettings.shadowDistance = 15f;
            // QualitySettings.shadowResolution = ShadowResolution.Low;
            // QualitySettings.shadowCascades = 0;

            // Skin weights
#if UNITY_2020_3_OR_NEWER
            QualitySettings.skinWeights = SkinWeights.TwoBones; // cheaper on mobile
#else
        QualitySettings.blendWeights = BlendWeights.TwoBones;
#endif

            // Assign mobile specific URP pipeline asset:
            // QualitySettings.renderPipeline = yourMobileURPAsset;
            // If using URP’s renderer features, also consider disabling costly features on your mobile RP asset (SSAO, Screen-Space Shadows, TAA, HDR).
        }

        private static void ConfigureForNormal() {

        }

        private static void SwapToQualityLevel(string name) {
            int index = GetQualityIndex(name);
            if (index < 0) {
                if (!TryAddQualityLevel(name, out index)) {
                    Debug.LogError("Failed to add Mobile quality level. See console for details.");
                    return;
                }
                Debug.Log($"Created quality level '{name}' at index {index}.");
            } else {
                Debug.Log($"Found existing quality level '{name}' at index {index}.");
            }

            // Switch to it so QualitySettings.* edits apply to the right level
            QualitySettings.SetQualityLevel(index, true);
        }

        private static void SaveChangesToQualitySettings() {
            // Save project settings to disk
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static int GetQualityIndex(string name) {
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++) {
                if (names[i] == name) {
                    return i;
                }
            }
            return -1;
        }

        // Adds a new quality level by cloning the lowest level’s serialized settings, then renaming it.
        // Warning: property names may break on newer Unity versions.
        private static bool TryAddQualityLevel(string newName, out int newIndex) {
            newIndex = -1;

            // Load the ProjectSettings asset that stores quality levels
            var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (objs == null || objs.Length == 0) {
                Debug.LogError("Could not load ProjectSettings/QualitySettings.asset");
                return false;
            }

            var root = new SerializedObject(objs[0]);
            var qualArray = root.FindProperty("m_QualitySettings");
            if (qualArray == null || !qualArray.isArray) {
                Debug.LogError("m_QualitySettings array not found; Unity may have changed the schema.");
                return false;
            }

            // Use the last (usually lowest) level as a template
            int templateIndex = Mathf.Max(0, qualArray.arraySize - 1);
            qualArray.InsertArrayElementAtIndex(qualArray.arraySize); // push a new slot
            newIndex = qualArray.arraySize - 1;

            // Copy the template element into the new slot (Unity duplicates values on insert, but we guard anyway)
            CopyElement(qualArray.GetArrayElementAtIndex(templateIndex), qualArray.GetArrayElementAtIndex(newIndex));

            // Set the name
            var newElem = qualArray.GetArrayElementAtIndex(newIndex);
            var nameProp = newElem.FindPropertyRelative("name");
            if (nameProp == null) {
                // Some Unity versions store it as 'm_Name'
                nameProp = newElem.FindPropertyRelative("m_Name");
            }
            if (nameProp == null) {
                Debug.LogWarning("Could not find name field on quality element; trying to proceed.");
            } else {
                nameProp.stringValue = newName;
            }

            root.ApplyModifiedPropertiesWithoutUndo();
            root.Update();

            // Update the runtime list so GetQualityIndex can find it immediately
            // (Unity rebuilds names automatically after Apply)
            return true;
        }

        private static void CopyElement(SerializedProperty src, SerializedProperty dst) {
            // SerializedProperty.CopyFromSerializedProperty only exists for certain kinds; we’ll field-by-field copy.
            // Fallback: iterate over children
            var srcIter = src.Copy();
            var end = src.GetEndProperty();
            var dstIter = dst.Copy();

            while (srcIter.Next(true) && !SerializedProperty.EqualContents(srcIter, end)) {
                var relativePath = srcIter.propertyPath.Substring(src.propertyPath.Length).TrimStart('.');
                var dstProp = dst.FindPropertyRelative(relativePath);
                if (dstProp == null) {
                    continue;
                }
                switch (srcIter.propertyType) {
                    case SerializedPropertyType.Integer: dstProp.intValue = srcIter.intValue; break;
                    case SerializedPropertyType.Boolean: dstProp.boolValue = srcIter.boolValue; break;
                    case SerializedPropertyType.Float: dstProp.floatValue = srcIter.floatValue; break;
                    case SerializedPropertyType.String: dstProp.stringValue = srcIter.stringValue; break;
                    case SerializedPropertyType.Enum: dstProp.enumValueIndex = srcIter.enumValueIndex; break;
                    case SerializedPropertyType.Color: dstProp.colorValue = srcIter.colorValue; break;
                    case SerializedPropertyType.ObjectReference: dstProp.objectReferenceValue = srcIter.objectReferenceValue; break;
                    case SerializedPropertyType.Vector2: dstProp.vector2Value = srcIter.vector2Value; break;
                    case SerializedPropertyType.Vector3: dstProp.vector3Value = srcIter.vector3Value; break;
                    case SerializedPropertyType.Vector4: dstProp.vector4Value = srcIter.vector4Value; break;
                    case SerializedPropertyType.Rect: dstProp.rectValue = srcIter.rectValue; break;
                    case SerializedPropertyType.Bounds: dstProp.boundsValue = srcIter.boundsValue; break;
                    case SerializedPropertyType.Quaternion: dstProp.quaternionValue = srcIter.quaternionValue; break;
                    default: break;
                }
            }
        }

        private static void RenameQualityLevel(int index, string newName) {
            // This edits the ProjectSettings asset where quality settings live
            var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (objs == null || objs.Length == 0) {
                Debug.LogError("Could not load QualitySettings.asset");
                return;
            }

            var so = new SerializedObject(objs[0]);
            var qualityArray = so.FindProperty("m_QualitySettings");

            if (qualityArray == null || !qualityArray.isArray || index < 0 || index >= qualityArray.arraySize) {
                Debug.LogError("Quality settings array not found or index out of range.");
                return;
            }

            var element = qualityArray.GetArrayElementAtIndex(index);
            var nameProp = element.FindPropertyRelative("name");
            if (nameProp == null) {
                nameProp = element.FindPropertyRelative("m_Name"); // fallback for older Unity versions
            }

            if (nameProp != null) {
                nameProp.stringValue = newName;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            } else {
                Debug.LogError("Could not find the name property for quality level.");
            }
        }

        static QualityConfig() {
            // All templates were using a single "Ultra" quality level.
            // This renames it to "Normal"
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++) {
                if (names[i] == "Ultra") {
                    RenameQualityLevel(i, "Normal");
                    return;
                }
            }
        }
    }
}
#endif