using System;
using System.Collections.Generic;
using System.IO;
using Luau;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    [CustomEditor(typeof(DeclarationFile))]
    public class AirshipDeclarationEditor : AirshipScriptEditor<DeclarationFile> {
        private const string IconDeclaration = "Packages/gg.easy.airship/Editor/TypescriptDeclaration.png";
        private string assetPath;
        private AirshipScript asset;
        
       protected override void OnHeaderGUI() {
            GUILayout.Space(10f);
            var rect = EditorGUILayout.GetControlRect(false, 40, "IN BigTitle");
        
            var textureImage = new Rect(rect);
            textureImage.y += 0;
            textureImage.x += 0;
            textureImage.width = 38;
            textureImage.height = 38;

            var icon = "";
            rect.x += 40;
            
            if (item != null) {
                GUI.Label(rect, ObjectNames.NicifyVariableName(Path.GetFileNameWithoutExtension(item.name)), "IN TitleText");

                var label = "Typescript Declaration";
                if (asset != null) {
                    label = "Typescript Luau Declaration";
                } else if (item.ambient) {
                    label = "Typescript Ambient Declaration";
                }
                
                GUI.Label(new RectOffset(2, 0, -10, 0).Add(rect), label);
                icon = IconDeclaration;
            }
            else {
                GUI.Label(rect, "Multiple Scripts", "IN TitleText");
                GUI.Label(new RectOffset(2, 0, -10, 0).Add(rect), "Multiple Scripts Selected");
                icon = IconDeclaration;
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
                if (items != null) {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Reimport All", GUILayout.MaxWidth(100))) {
                        AssetDatabase.StartAssetEditing();
                        foreach (var script in items) {
                            AssetDatabase.ImportAsset(script.scriptPath, ImportAssetOptions.Default);
                        }
                        AssetDatabase.StopAssetEditing();
                        
                        UpdateSelection();
                        return;
                    }
                } else if (item != null) {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Reimport", GUILayout.MaxWidth(100))) {
           
                        AssetDatabase.StartAssetEditing();
                        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.Default);
                        AssetDatabase.StopAssetEditing();
                        
                        UpdateSelection();
                        return;
                    }
                    
                    if (GUILayout.Button("Edit", GUILayout.MaxWidth(100))) {
                        TypescriptProjectsService.OpenFileInEditor(assetPath);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10f);
        }
        
        public override void OnInspectorGUI() {
            if (!item) return;
            GUI.enabled = true;
            
            GUILayout.Label("Declaration File", EditorStyles.boldLabel);
                
            GUI.enabled = false;
            EditorGUILayout.ObjectField("Luau File", asset, typeof(AirshipScript), false);

            GUI.enabled = true;
            DrawSourceText();
            GUI.enabled = false;
        }
        
        private void UpdateSelection() {
            if (targets.Length > 1) {
                // items = targets.Select(target => target as BinaryFile);
            }
            else {
                item = target as DeclarationFile;
                assetPath = AssetDatabase.GetAssetPath(item);
                
                var scriptPath = item.scriptPath;
                if (scriptPath != "") {
                    asset = AssetDatabase.LoadAssetAtPath<AirshipScript>(scriptPath);
                }
                else {
                    asset = null;
                }
            
                CachePreview();        
            }
            
            CachePreview();
        }

        protected override void OnEnable() {
            base.OnEnable();
            UpdateSelection();
        }

        protected override void OnDisable() {
            base.OnDisable();
            asset = null;
        }
    }
}