#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Editor;
using Luau;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Airship.Editor {
    [CanEditMultipleObjects]
    [CustomEditor(typeof(TypescriptImporter))]
    public class TypescriptImporterEditor : AssetImporterEditor {
        public TypescriptImporter importer;
        public BinaryFile script;
        public IEnumerable<BinaryFile> scripts;
        
        public override void OnEnable() {
            base.OnEnable();
            if (assetTargets.Length > 1) {
                scripts = assetTargets.Select(target => target as BinaryFile);
            }
            else {
                script = assetTarget as BinaryFile;
                importer = target as TypescriptImporter;
            }
        }

        public override void OnDisable() {
            base.OnDisable();
            scripts = null;
            script = null;
            importer = null;
        }

        public override void OnInspectorGUI() {
            // base.OnInspectorGUI();
            this.ApplyRevertGUI();
        }

        protected override bool needsApplyRevert => false;

        protected override void OnHeaderGUI() {
            GUILayout.BeginHorizontal("IN BigTitle");
            {
                GUILayout.Space(38f);
            
                GUILayout.BeginVertical();
                {
                    if (scripts != null) {
                        EditorGUILayout.LabelField("Airship Script Assets", EditorStyles.boldLabel);
                    }
                    else {
                        EditorGUILayout.LabelField("Airship Script Asset", EditorStyles.boldLabel);
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