using System.IO;
using Editor;
using Luau;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Airship.Editor {
#if UNITY_EDITOR
    [CustomEditor(typeof(BinaryFile))]
    public class BinaryFileEditor : UnityEditor.Editor {
        private const string IconOk = "Packages/gg.easy.airship/Editor/TypescriptAsset.png";
        private const string IconEmpty = "Packages/gg.easy.airship/Editor/TypescriptOff.png";
        private const string IconFail = "Packages/gg.easy.airship/Editor/TypescriptErr.png";
        
        private const string LuaIconOk = "Packages/gg.easy.airship/Editor/LuauIcon.png";
        private const string LuaIconFail = "Packages/gg.easy.airship/Editor/LuauErrorIcon.png";
        
        protected override void OnHeaderGUI() {
            GUILayout.Space(10f);

            var rect = EditorGUILayout.GetControlRect(false, 40, "IN BigTitle");
            var script = (BinaryFile)target;
        
            var textureImage = new Rect(rect);
            textureImage.y += 2;
            textureImage.x += 5;
            textureImage.width = 30;
            textureImage.height = 30;

            var icon = "";
            
            rect.x += 40;
            
            if (script.scriptLanguage == AirshipScriptLanguage.Luau) {
                rect.y += 6;
                GUI.Label(rect, "Luau Script", "IN TitleText");
                icon = LuaIconOk;
            } else if (script.scriptLanguage == AirshipScriptLanguage.Typescript) {
                if (script.airshipBehaviour && script.m_metadata != null) {
                    GUI.Label(rect, script.m_metadata.displayName, "IN TitleText");
                    GUI.Label(new RectOffset(0, 0, -10, 0).Add(rect), "Airship Component");
                }
                else {
                    rect.y += 6;
                    GUI.Label(rect, "Typescript Script", "IN TitleText");
                }
                
                icon = IconOk;
            }
            
            bool flag = AssetPreview.IsLoadingAssetPreview(this.target.GetInstanceID());
            Texture2D image = AssetDatabase.LoadAssetAtPath<Texture2D>(icon);
            if (!(bool) (UnityEngine.Object) image)
            {
                if (flag)
                    this.Repaint();
                image = AssetPreview.GetMiniThumbnail(this.target);
            }
            
            GUI.Label(textureImage, image);
            
            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reimport", GUILayout.MaxWidth(100))) {
                    AssetDatabase.StartAssetEditing();
                    AssetDatabase.ImportAsset(script.assetPath, ImportAssetOptions.Default);
                    AssetDatabase.StopAssetEditing();
                }
                
                if (GUILayout.Button("Edit", GUILayout.MaxWidth(100))) {
                    TypescriptProjectsService.OpenFileInEditor(script.m_path);
                }
        
                if (script.scriptLanguage == AirshipScriptLanguage.Typescript) {
                    GUI.enabled = script.m_compiled;
                    if (GUILayout.Button("View Compiled", GUILayout.MaxWidth(150)))
                    {
                        var project = TypescriptProjectsService.Project;
                        TypescriptProjectsService.OpenFileInEditor(project.GetOutputPath(script.assetPath));
                    }                   
                }
                // GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10f);
        }

        public override void OnInspectorGUI() {
            var script = (BinaryFile)target;
            GUI.enabled = true;
            
            #if AIRSHIP_INTERNAL
            EditorGUILayout.LabelField("TS Path", script.assetPath);
            EditorGUILayout.LabelField("Luau Path", TypescriptProjectsService.Project.GetOutputPath(script.assetPath));
            #endif
            
            GUI.enabled = false;
        }
    }

    
    public static class ScriptedAssetOpenFileHandler {
        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            var target = EditorUtility.InstanceIDToObject(instanceID);
            
            switch (target) {
                case BinaryFile: {
                    var path = AssetDatabase.GetAssetPath(instanceID);
                    TypescriptProjectsService.OpenFileInEditor(path);
                    return true;
                }
                case DeclarationFile: {
                    var path = AssetDatabase.GetAssetPath(instanceID);
                    TypescriptProjectsService.OpenFileInEditor(path);
                    return true;   
                }
                default:
                    return false;
            }
        }
    }
#endif
}