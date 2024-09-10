using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Luau;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    public abstract class AirshipScriptEditor<T> : UnityEditor.Editor where T : ScriptableObject {
        protected static GUIStyle scriptTextMono;
        
        protected const int maxCharacters = 7000;
        protected T item;
        protected IEnumerable<T> items;
        protected GUIContent cachedPreview;

        private string assetGuid;
        
        protected void UpdateSelection() {
            if (targets.Length > 1) {
                items = targets.Select(target => target as T);
            }
            else {
                item = target as T;
                var assetPath = AssetDatabase.GetAssetPath(item);
                assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                CachePreview();        
            }
        }
        
        protected virtual void OnEnable() {
            scriptTextMono ??= new GUIStyle("ScriptText") {
                font = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font,
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                normal = new GUIStyleState() {
                    textColor = new Color(0.8f, 0.8f, 0.8f)
                },
            };
        }

        protected void CachePreview() {
            string text = "";
            if (item != null) {
                text = File.ReadAllText(AssetDatabase.GetAssetPath(item));
            }

            if (text.Length >= maxCharacters) {
                text = text.Substring(0, maxCharacters) + "...\n\n<... Truncated ...>";
            }
            
            cachedPreview = new GUIContent(text);
        }

        protected void DrawSourceText() {
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
        
        protected virtual void OnDisable() {
            item = null;
            items = null;
        }
    }
}