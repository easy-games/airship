using System;
using System.IO;
using System.Linq;
using Editor;
using Luau;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    public static class AirshipScriptContextMenus {
        [MenuItem("CONTEXT/" + nameof(AirshipComponent) + "/Reset", priority = 0)]
        public static void Test(MenuCommand command) {
            var binding = command.context as AirshipComponent;
            if (binding == null || binding.metadata == null) return;
            foreach (var property in binding.metadata.properties.Where(property => property.modified)) {
                property.SetDefaultAsValue();
                property.modified = false;
            }
                
            EditorUtility.SetDirty(binding);
        }
        
        
        
        [MenuItem("CONTEXT/" + nameof(AirshipComponent) + "/Edit Script")]
        public static void EditScript(MenuCommand command) {
            var binding = command.context as AirshipComponent;
            if (binding == null || binding.metadata == null) return;

            TypescriptProjectsService.OpenFileInEditor(binding.script.assetPath);
        }

        [MenuItem("CONTEXT/" + nameof(AirshipComponent) + "/Edit Script", validate = true)]
        [MenuItem("CONTEXT/" + nameof(AirshipComponent) + "/Remove Script", validate = true)]
        public static bool ValidateRemoveScript(MenuCommand command) {
            var binding = command.context as AirshipComponent;
            return binding != null && binding.script != null;
        }
        
        [MenuItem("CONTEXT/" + nameof(AirshipComponent) + "/Remove Script")]
        public static void RemoveScript(MenuCommand command) {
            var binding = command.context as AirshipComponent;
            if (binding == null || binding.metadata == null) return;

            binding.script = null;
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(AirshipScript))]
    public class AirshipScriptFileEditor : AirshipScriptEditor<AirshipScript> {
        private const string IconOk = "Packages/gg.easy.airship/Editor/TypescriptAsset.png";
        private const string IconEmpty = "Packages/gg.easy.airship/Editor/TypescriptOff.png";
        private const string IconFail = "Packages/gg.easy.airship/Editor/TypescriptErr.png";
        
        private const string LuaIconOk = "Packages/gg.easy.airship/Editor/LuauAssetIcon.png";
        private const string LuaIconFail = "Packages/gg.easy.airship/Editor/LuauAssetIconError.png";
        
        // private BinaryFile script;
        // private IEnumerable<BinaryFile> scripts;
        
        private DeclarationFile declaration;
        private string assetGuid;
        
        private void UpdateSelection() {
            if (targets.Length > 1) {
                items = targets.Select(target => target as AirshipScript);
            }
            else {
                item = target as AirshipScript;
                var assetPath = AssetDatabase.GetAssetPath(item);
                assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                declaration = null;
                if (item.scriptLanguage == AirshipScriptLanguage.Luau) {
                    var declarationPath = assetPath.Replace(".lua", ".d.ts").Replace("init", "index");
                    if (File.Exists(declarationPath)) {
                        declaration = AssetDatabase.LoadAssetAtPath<DeclarationFile>(declarationPath);
                    }
                }
            
                CachePreview();        
            }
        }

        protected override void OnEnable() {
            UpdateSelection();
            base.OnEnable();
           
        }

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
                switch (item.scriptLanguage) {
                    case AirshipScriptLanguage.Luau: {
                        rect.y += 6;
                        GUI.Label(rect, ObjectNames.NicifyVariableName(item.name), "IN TitleText");
                        icon = LuaIconOk;
                        break;
                    }
                    case AirshipScriptLanguage.Typescript: {
                        if (item.airshipBehaviour && item.m_metadata != null) {
                            GUI.Label(rect, item.m_metadata.displayName, "IN TitleText");
                            GUI.Label(new RectOffset(2, 0, -10, 0).Add(rect), item.m_metadata.singleton ? "Airship Singleton" : "Airship Component");
                        }
                        else {
                            rect.y += 6;
                            GUI.Label(rect, ObjectNames.NicifyVariableName(item.name), "IN TitleText");
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
            var image = item.m_metadata?.displayIcon != null ? item.m_metadata?.displayIcon : AssetDatabase.LoadAssetAtPath<Texture2D>(icon);
            
            
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
                            AssetDatabase.ImportAsset(script.assetPath, ImportAssetOptions.Default);
                        }
                        AssetDatabase.StopAssetEditing();
                        
                        UpdateSelection();
                        return;
                    }
                } else if (item != null) {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Reimport", GUILayout.MaxWidth(100))) {
           
                        AssetDatabase.StartAssetEditing();
                        AssetDatabase.ImportAsset(item.assetPath, ImportAssetOptions.Default);
                        AssetDatabase.StopAssetEditing();
                        
                        UpdateSelection();
                        return;
                    }
                    
                    if (GUILayout.Button("Edit", GUILayout.MaxWidth(100))) {
                        TypescriptProjectsService.OpenFileInEditor(item.assetPath);
                    }
            
                    if (item.scriptLanguage == AirshipScriptLanguage.Typescript) {
                        GUI.enabled = item != null && item.m_compiled;
                        if (GUILayout.Button("View Compiled", GUILayout.MaxWidth(150)))
                        {
                            var project = TypescriptProjectsService.Project;
                            TypescriptProjectsService.OpenFileInEditor(project.GetOutputPath(item.assetPath));
                        }                   
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10f);
        }

        public (string typeName, bool isArray, bool isObject) GetType(LuauMetadataProperty property) {
            var typeName = property.type;
            var isArray = false;
            var isObject = false;

            if (property.type is "Array") {
                isArray = true;

                if (property.items.type is "AirshipBehaviour" or "object") {
                    typeName = property.items.objectType;
                } else if (property.type is "StringEnum" or "IntEnum") {
                    typeName = property.refPath.Split("@")[1];
                }
                else {
                    typeName = property.items.type;
                }
            } else if (property.type is "AirshipBehaviour" or "object") {
                typeName = property.objectType;
                isObject = true;
            } else if (property.type is "StringEnum" or "IntEnum") {
                typeName = property.refPath.Split("@")[1];
            }

            return (typeName, isArray, isObject);
        }

        public override void OnInspectorGUI() {
            GUI.enabled = true;

            if (item != null) {
                if (item.scriptLanguage == AirshipScriptLanguage.Typescript && item.airshipBehaviour) {
                    var project = TypescriptProjectsService.Project;
                    var errors = project.GetProblemsForFile(item.assetPath);
                    foreach (var error in errors) {
                        EditorGUILayout.HelpBox(error.ToString(), MessageType.Error);
                    }
                    
                    EditorGUILayout.Space(10);
                    GUILayout.Label("Component Details", EditorStyles.boldLabel);
                    
                    EditorGUILayout.LabelField("DisplayName", item.m_metadata!.displayName, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("ClassName", item.m_metadata.name, scriptTextMono);


                    
#if AIRSHIP_INTERNAL
                    EditorGUILayout.LabelField("OutFileHash", project.GetOutputFileHash(item.assetPath));
#endif

                    GUI.enabled = false;
                    EditorGUILayout.Toggle("Is Singleton", item.m_metadata.singleton);
#if AIRSHIP_INTERNAL
                    EditorGUILayout.Toggle("Requires Reimport", TypescriptImporter.RequiresRecompile(item.assetPath));
#endif
                    GUI.enabled = true;

                    EditorGUILayout.Space(10);
                    GUILayout.Label("Properties", EditorStyles.boldLabel);
                    foreach (var property in item.m_metadata.properties) {
                        var typeInfo = GetType(property);
                        if (typeInfo.isArray) {
                            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(property.name), $"{typeInfo.typeName}[]", scriptTextMono);
                        }
                        else {
                            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(property.name), typeInfo.typeName, scriptTextMono);
                        }
                    }
                } else if (item.scriptLanguage == AirshipScriptLanguage.Luau) {
                    if (declaration == null) {
                        EditorGUILayout.HelpBox("This Luau file has no Typescript declaration file!", MessageType.Warning);
                    }
                    
                    EditorGUILayout.Space(10);
                    GUILayout.Label("Script Details", EditorStyles.boldLabel);
                    
                    GUI.enabled = false;
                    EditorGUILayout.ObjectField("Declaration File", declaration, typeof(DeclarationFile));
                    GUI.enabled = true;
                }

                if (item.m_directives != null && item.m_directiveValues != null && item.m_directives.Length > 0 && item.m_directives.Length == item.m_directiveValues.Length) {
                    EditorGUILayout.Space(10);
                    GUILayout.Label("Directives", EditorStyles.boldLabel);
                    for (var i = 0; i < item.m_directives.Length; i++) {
                        EditorGUILayout.LabelField(item.m_directives[i], item.m_directiveValues[i], scriptTextMono);
                    }
                }
                
                DrawSourceText();
            }
            
            GUI.enabled = false;
        }
    }
}