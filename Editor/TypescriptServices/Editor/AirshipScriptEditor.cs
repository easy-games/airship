using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Luau;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    [CanEditMultipleObjects]
    [CustomEditor(typeof(BinaryFile))]
    public class AirshipScriptEditor : UnityEditor.Editor {
        private const string IconOk = "Packages/gg.easy.airship/Editor/TypescriptAsset.png";
        private const string IconEmpty = "Packages/gg.easy.airship/Editor/TypescriptOff.png";
        private const string IconFail = "Packages/gg.easy.airship/Editor/TypescriptErr.png";
        
        private const string LuaIconOk = "Packages/gg.easy.airship/Editor/LuauIcon.png";
        private const string LuaIconFail = "Packages/gg.easy.airship/Editor/LuauErrorIcon.png";
        
        private BinaryFile script;
        private IEnumerable<BinaryFile> scripts;
        
        private DeclarationFile declaration;
        private string assetGuid;
        private GUIContent cachedPreview;
        private const int maxCharacters = 7000;
        
        private GUIStyle scriptTextMono;

        private void OnDisable() {
            script = null;
            scripts = null;
        }

        private void UpdateSelection() {
            if (targets.Length > 1) {
                scripts = targets.Select(target => target as BinaryFile);
            }
            else {
                script = target as BinaryFile;
                var assetPath = AssetDatabase.GetAssetPath(script);
                assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                declaration = null;
                if (script.scriptLanguage == AirshipScriptLanguage.Luau) {
                    var declarationPath = assetPath.Replace(".lua", ".d.ts").Replace("init", "index");
                    if (File.Exists(declarationPath)) {
                        declaration = AssetDatabase.LoadAssetAtPath<DeclarationFile>(declarationPath);
                    }
                }
            
                CachePreview();        
            }
        }
        
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

            UpdateSelection();
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
            
            if (script != null) {
                switch (script.scriptLanguage) {
                    case AirshipScriptLanguage.Luau: {
                        rect.y += 6;
                        GUI.Label(rect, ObjectNames.NicifyVariableName(script.name), "IN TitleText");
                        icon = LuaIconOk;
                        break;
                    }
                    case AirshipScriptLanguage.Typescript: {
                        if (script.airshipBehaviour && script.m_metadata != null) {
                            GUI.Label(rect, script.m_metadata.displayName, "IN TitleText");
                            GUI.Label(new RectOffset(2, 0, -10, 0).Add(rect), "Airship Component");
                        }
                        else {
                            rect.y += 6;
                            GUI.Label(rect, ObjectNames.NicifyVariableName(script.name), "IN TitleText");
                        }
                    
                        icon = IconOk;
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else {
                GUI.Label(rect, "Multiple Scripts", "IN TitleText");
                GUI.Label(new RectOffset(2, 0, -10, 0).Add(rect), "Multiple Scripts Selected");
                icon = IconOk;
            }
            
            var flag = AssetPreview.IsLoadingAssetPreview(this.target.GetInstanceID());
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(icon);
            if (!(bool) (UnityEngine.Object) image)
            {
                if (flag)
                    Repaint();
                image = AssetPreview.GetMiniThumbnail(this.target);
            }
            
            GUI.Label(textureImage, image);
            
            EditorGUILayout.BeginHorizontal();
            {
                if (scripts != null) {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Reimport All", GUILayout.MaxWidth(100))) {
                        AssetDatabase.StartAssetEditing();
                        foreach (var script in scripts) {
                            AssetDatabase.ImportAsset(script.assetPath, ImportAssetOptions.Default);
                        }
                        AssetDatabase.StopAssetEditing();
                        
                        UpdateSelection();
                        return;
                    }
                } else if (script != null) {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Reimport", GUILayout.MaxWidth(100))) {
           
                        AssetDatabase.StartAssetEditing();
                        AssetDatabase.ImportAsset(script.assetPath, ImportAssetOptions.Default);
                        AssetDatabase.StopAssetEditing();
                        
                        UpdateSelection();
                        return;
                    }
                    
                    if (GUILayout.Button("Edit", GUILayout.MaxWidth(100))) {
                        TypescriptProjectsService.OpenFileInEditor(script.assetPath);
                    }
            
                    if (script.scriptLanguage == AirshipScriptLanguage.Typescript) {
                        GUI.enabled = script != null && script.m_compiled;
                        if (GUILayout.Button("View Compiled", GUILayout.MaxWidth(150)))
                        {
                            var project = TypescriptProjectsService.Project;
                            TypescriptProjectsService.OpenFileInEditor(project.GetOutputPath(script.assetPath));
                        }                   
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10f);
        }

        public override void OnInspectorGUI() {
            GUI.enabled = true;

            if (script != null) {
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
}