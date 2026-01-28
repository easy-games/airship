#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Airship.Editor;
using Code.Luau;
using Editor.Packages;
using Luau;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AirshipBuildInfo))]

public class AirshipBuildInfoEditor : UnityEditor.Editor {
    private Vector2 pos = default;
    private string searchText = "";

    private Dictionary<string, bool> typeToExpandState = new();
    
    public void OnBehaviourMeta(AirshipBehaviourMeta airshipBehaviourMeta) {
        var type = AirshipBuildInfo.Instance.GetTypeByPathAndName(airshipBehaviourMeta.assetPath, airshipBehaviourMeta.className);
        if (type == null) return;
        {
            typeToExpandState.TryGetValue(type.UniqueId, out var toggleState);

            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            var nextToggleState = EditorGUI.BeginFoldoutHeaderGroup(rect, toggleState, new GUIContent(ObjectNames.NicifyVariableName(type.Name)));
            
            EditorGUI.LabelField(new Rect(rect) { width = 200, x = rect.xMin + rect.width - 200 }, type.DeclarationType.ToString(),new GUIStyle(EditorStyles.label) {
                alignment = TextAnchor.MiddleRight
            });
            
            if (nextToggleState) {
                EditorGUILayout.BeginVertical(new GUIStyle() {
                    margin = new RectOffset(2, 2, 2, 2),
                    // padding = new RectOffset(5,5,5,5),
                    fixedWidth = 0,
                });
                {
                    GUI.enabled = false;
                    EditorGUILayout.EnumPopup("Declaration Type", type.DeclarationType);
                    EditorGUILayout.LabelField("Asset Path", type.AssetPath);
                    EditorGUILayout.LabelField("Runtime Path", type.RuntimePath.ToLowerInvariant());
                    EditorGUILayout.ObjectField("Script", type.Script, typeof(AirshipScript));
                    
                    // if (airshipBehaviourMeta.extends.Count > 0) {
                    //     AirshipEditorGUI.Heading(new GUIContent("Inherits"));
                    //     foreach (var inheritance in airshipBehaviourMeta.extends) {
                    //         EditorGUILayout.LabelField(inheritance);
                    //     }
                    // }

                    if (type.BaseTypes == null) {
                        EditorGUILayout.HelpBox("Missing BaseTypes field", MessageType.Error);
                    }
                    
                    if (type.BaseTypes != null && type.BaseTypes.Length > 0) {
                        AirshipEditorGUI.Heading(new GUIContent("Inherits"));
                        foreach (var inheritance in type.BaseTypes) {
                            EditorGUILayout.LabelField(inheritance.Name);
                        }
                    }
                    GUI.enabled = true;
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (nextToggleState != toggleState) {
                if (!typeToExpandState.TryAdd(type.UniqueId, nextToggleState)) {
                    typeToExpandState[type.UniqueId] = nextToggleState;
                }
            }
        }
        
    }

    private int packageId = 0;
    public override void OnInspectorGUI() {
        var buildInfo = (AirshipBuildInfo)target;

        List<AirshipBehaviourMeta> gameMetas = new List<AirshipBehaviourMeta>();
        List<AirshipBehaviourMeta> packageMetas = new List<AirshipBehaviourMeta>();
        
        foreach (var info in buildInfo.data.airshipBehaviourMetas) {
            if (searchText != "" && !info.className.StartsWith(searchText, StringComparison.OrdinalIgnoreCase)) continue;
            
            if (info.assetPath.StartsWith("Assets/AirshipPackages/@", StringComparison.Ordinal)) {
                packageMetas.Add(info);
            } else {
                gameMetas.Add(info);
            }
        }
        
        foreach (var info in buildInfo.data.airshipScriptableObjectMetas) {
            if (searchText != "" && !info.className.StartsWith(searchText, StringComparison.OrdinalIgnoreCase)) continue;
            
            if (info.assetPath.StartsWith("Assets/AirshipPackages/@", StringComparison.Ordinal)) {
                packageMetas.Add(info);
            } else {
                gameMetas.Add(info);
            }
        }
        
        var prevEnabled = GUI.enabled;
        GUI.enabled = true;

        List<string> items = new List<string>();
        items.Add("Game");
        
        foreach (var package in GameConfig.Load().packages) {
            items.Add(package.id);
        }
        
        EditorGUILayout.BeginHorizontal();
        searchText = EditorGUILayout.TextField(searchText, EditorStyles.toolbarSearchField);
        EditorGUILayout.LabelField("Scope", GUILayout.Width(50));
        packageId = EditorGUILayout.Popup(packageId, items.ToArray(), GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();

        var selectedItem = items[packageId];
        
        if (selectedItem.StartsWith("@")) {
            if (packageMetas.Count > 0) {
                foreach (var info in packageMetas) {
                    if (info.assetPath.StartsWith("Assets/AirshipPackages/" + selectedItem, StringComparison.Ordinal)) OnBehaviourMeta(info);
                }
            }      
        } else {
            if (gameMetas.Count > 0) {
                foreach (var info in gameMetas) {
                    OnBehaviourMeta(info);
                }
            }
        }

        GUI.enabled = prevEnabled;
    }
}
#endif