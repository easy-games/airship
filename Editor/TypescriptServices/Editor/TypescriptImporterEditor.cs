#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Editor;
using Luau;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.UIElements;

namespace Airship.Editor {
    [CanEditMultipleObjects]
    [CustomEditor(typeof(TypescriptImporter))]
    public class TypescriptImporterEditor : AssetImporterEditor {
        private const string IconAsset = "Packages/gg.easy.airship/Editor/AirshipScriptIcon.png";
        private static Texture2D AssetIcon;
        
        public TypescriptImporter importer;
        public AirshipScriptable script;
        public IEnumerable<AirshipScriptable> scripts;

        public override void OnEnable() {
            base.OnEnable();
            
            if (assetTargets.Length > 1) {
                scripts = assetTargets.Select(target => target as AirshipScriptable);
            }
            else {
                script = assetTarget as AirshipScriptable;
                importer = target as TypescriptImporter;
            }
        }

        public override void OnDisable() {
            scripts = null;
            script = null;
            importer = null;
            base.OnDisable();
        }

        public override void OnInspectorGUI() {
            importer.scriptType = (ScriptType) EditorGUILayout.EnumPopup("Script Type", importer.scriptType);
            if (GUI.changed) {
                var value = this.serializedObject.FindProperty("scriptType");
                if (value != null) {
                    value.enumValueIndex = (int) importer.scriptType;
                }
            }
        
            ApplyRevertGUI();
        }
        
        protected override bool needsApplyRevert => true;

        protected override void OnHeaderGUI() {
            var rect = EditorGUILayout.GetControlRect(false, 50, "IN BigTitle");
            
            if (!AssetIcon) {
                AssetIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAsset);
            }
            
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(38f);
            
                GUILayout.BeginVertical();
                {
                    
                    
                    var textureImage = new Rect(rect);
                    textureImage.y += 5;
                    textureImage.x += 5;
                    textureImage.width = 38;
                    textureImage.height = 38;
                    GUI.Label(textureImage, AssetIcon);
        
                    rect.x += 45;
                    if (scripts != null) {
                        EditorGUI.LabelField(rect, "Airship Script Assets", EditorStyles.boldLabel);
                    }
                    else {
                        EditorGUI.LabelField(rect,"Airship Script Asset", EditorStyles.boldLabel);
                    }
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
            // base.OnHeaderGUI();
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(LuauImporter))]
    public class LuauImporterEditor : TypescriptImporterEditor {}
}
#endif