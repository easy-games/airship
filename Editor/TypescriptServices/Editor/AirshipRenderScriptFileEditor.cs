using Code.Luau;
using Luau;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    //[CanEditMultipleObjects]
    [CustomEditor(typeof(AirshipRenderPassScript))]
    public class AirshipRenderScriptFileEditor : AirshipScriptEditor<AirshipRenderPassScript> {
        private const string IconOk = "Packages/gg.easy.airship/Editor/TypescriptAsset.png";
        
        public override void OnInspectorGUI() {
            GUI.enabled = true;

            if (item != null) {
                DrawSourceText();
            }
            
            GUI.enabled = false;
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
            
            if (item.m_metadata != null) {
                GUI.Label(rect, item.m_metadata.name, "IN TitleText");
                GUI.Label(new RectOffset(2, 0, -10, 0).Add(rect), "Airship Render Pass");
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
            
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10f);
        }

        protected override void OnEnable() {
            UpdateSelection();
            base.OnEnable();
           
        }
    }
}