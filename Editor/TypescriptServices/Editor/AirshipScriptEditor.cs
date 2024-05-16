using System;
using System.IO;
using Editor;
using Luau;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Airship.Editor {
#if UNITY_EDITOR
    internal enum FileViewMode {
        Script,
        Compiled,
    }
    
    [CustomEditor(typeof(BinaryFile))]
    public class BinaryFileEditor : UnityEditor.Editor {
        private const string IconOk = "Packages/gg.easy.airship/Editor/TypescriptAsset.png";
        private const string IconEmpty = "Packages/gg.easy.airship/Editor/TypescriptOff.png";
        private const string IconFail = "Packages/gg.easy.airship/Editor/TypescriptErr.png";
        
        private const string LuaIconOk = "Packages/gg.easy.airship/Editor/LuauIcon.png";
        private const string LuaIconFail = "Packages/gg.easy.airship/Editor/LuauErrorIcon.png";
        
        private BinaryFile script;
        private DeclarationFile declaration;
        private string assetGuid;
        private GUIContent cachedPreview;
        private FileViewMode viewMode = FileViewMode.Script;

        private const int maxCharacters = 7000;
        
        private GUIStyle scriptTextMono;
        
        private void OnEnable() {
            if (scriptTextMono == null) {
                scriptTextMono = new GUIStyle("ScriptText") {
                    font = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font,
                    fontSize = 11,
                    fontStyle = FontStyle.Normal,
                    normal = new GUIStyleState() {
                        textColor = new Color(0.8f, 0.8f, 0.8f)
                    },
                };
            }
            
            script = target as BinaryFile;
            var assetPath = AssetDatabase.GetAssetPath(script);
            assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            declaration = null;
            if (script.scriptLanguage == AirshipScriptLanguage.Luau) {
                var declarationPath = assetPath.Replace(".lua", ".d.ts");
                if (File.Exists(declarationPath)) {
                    declaration = AssetDatabase.LoadAssetAtPath<DeclarationFile>(declarationPath);
                }
            }
            
            CachePreview();
        }

        private void CachePreview() {
            string text = "";
            if (script != null) {
                text = File.ReadAllText(AssetDatabase.GetAssetPath(script));
            }

            if (text.Length >= maxCharacters) {
                text = text.Substring(0, maxCharacters) + "...\n\n<... Truncated ...>";
            }
            
            cachedPreview = new GUIContent(text);
        }
        
        protected override void OnHeaderGUI() {
            GUILayout.Space(10f);
            var rect = EditorGUILayout.GetControlRect(false, 40, "IN BigTitle");
        
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
                    GUI.Label(new RectOffset(2, 0, -10, 0).Add(rect), "Airship Component");
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
                    TypescriptProjectsService.OpenFileInEditor(script.assetPath);
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
            GUI.enabled = true;
            
            #if AIRSHIP_INTERNAL
            GUILayout.Label("Internal Debugging", EditorStyles.boldLabel);
            EditorGUILayout.TextField("TS Path", script.assetPath);
            EditorGUILayout.TextField("Luau Path", TypescriptProjectsService.Project.GetOutputPath(script.assetPath));
            #endif

            if (script.scriptLanguage == AirshipScriptLanguage.Typescript && script.airshipBehaviour) {
                EditorGUILayout.Space(10);
                GUILayout.Label("Component Details", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("DisplayName", script.m_metadata.displayName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("ClassName", script.m_metadata.name, EditorStyles.boldLabel);

                EditorGUILayout.Space(10);
                GUILayout.Label("Properties", EditorStyles.boldLabel);
                foreach (var property in script.m_metadata.properties) {
                    if (property.type == "object") {
                        EditorGUILayout.LabelField(property.name, property.objectType, EditorStyles.boldLabel);
                    } else if (property.type == "Array") {
                        if (property.items.type == "object") {
                            EditorGUILayout.LabelField(property.name, $"{property.items.objectType}[]", EditorStyles.boldLabel);   
                        }
                        else {
                            EditorGUILayout.LabelField(property.name, $"{property.items.type}[]", EditorStyles.boldLabel);   
                        }
                    }
                    else {
                        EditorGUILayout.LabelField(property.name, property.type, EditorStyles.boldLabel);   
                    }
                }
            } else if (script.scriptLanguage == AirshipScriptLanguage.Luau) {
                if (declaration == null) {
                    EditorGUILayout.HelpBox("This Luau file has no Typescript declaration file!", MessageType.Warning);
                }
                
                EditorGUILayout.Space(10);
                GUILayout.Label("Script Details", EditorStyles.boldLabel);
                
                GUI.enabled = false;
                EditorGUILayout.ObjectField("Declaration File", declaration, typeof(DeclarationFile));
                GUI.enabled = true;
            }

            if (script != null) {
                EditorGUILayout.Space(10);
                GUILayout.Label("Source", EditorStyles.boldLabel);
                Rect rect = GUILayoutUtility.GetRect(cachedPreview, scriptTextMono);
                rect.x = 5;
                rect.width += 12;
                GUI.Box(rect, "");

                rect.x += 2;
                rect.width -= 4;
                EditorGUI.SelectableLabel(rect, cachedPreview.text, scriptTextMono);
            }
            
            GUI.enabled = false;
        }
    }

    
    public static class ScriptedAssetHandler {
        [MenuItem("Assets/Create/Airship Script", false, 50)]
        private static void CreateNewTypescriptFile()
        {
            ProjectWindowUtil.CreateAssetWithContent(
                "Script.ts",
                string.Empty);
        }
        
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