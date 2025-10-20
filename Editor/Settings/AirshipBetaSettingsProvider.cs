using System.Linq;
using Airship.Editor;
using UnityEditor;
using UnityEngine;

namespace Editor.Settings {
    public class AirshipBetaSettingsProvider : SettingsProvider {
         public const string Path = "Project/Airship/Betas";

        private AirshipBetaSettingsProvider(string path, SettingsScope scopes = SettingsScope.Project) : base(path, scopes) { }

        private void BetaCategoryBegin(GUIContent header, GUIContent desc = null) {
            EditorGUILayout.Space(10);
            AirshipEditorGUI.HorizontalLine();
            
            EditorGUILayout.BeginVertical(new GUIStyle() { padding = new RectOffset(10, 10, 10, 10)});
            GUILayout.Label(header, EditorStyles.whiteLargeLabel);

            if (desc != null) {
                GUILayout.Label(desc, EditorStyles.label);
                EditorGUILayout.Space(5);
            }
        }

        private void BetaCategoryEnd() {
            EditorGUILayout.EndVertical();
        }

        private Vector2 editorScroll;
        private bool listEditors;
        
        public override void OnGUI(string searchContext) {
            EditorGUILayout.HelpBox("You should not touch these settings unless you know what you're doing. Opting into the betas is accepting you are testing these features.", MessageType.Warning);
            
            BetaCategoryBegin(new GUIContent("Reconciler Changes"), new GUIContent("Changes how AirshipComponent properties are updated in the Editor."));
            {
                EditorGUILayout.BeginHorizontal();
                AirshipReconciliationService.ReconcilerVersion = (ReconcilerVersion) EditorGUILayout.EnumPopup(
                    new GUIContent("New Reconciler", "This is an experimental feature and subject to change: Changes how the properties on your components are reconciled (updated)"), 
                    EditorIntegrationsConfig.instance.useProjectReconcileOption ? EditorIntegrationsConfig.instance.projectReconcilerVersion : AirshipLocalArtifactDatabase.instance.reconcilerVersion);
                EditorGUILayout.EndHorizontal();
            
                var result = EditorGUILayout.Popup(new GUIContent("Reconciliation Beta Target", "How to test this feature"), 
                    EditorIntegrationsConfig.instance.useProjectReconcileOption ? 1 : 0, new[] { "Local Instance (Only you)", "Project-wide (All users)" });
                EditorIntegrationsConfig.instance.useProjectReconcileOption = result == 1;

            }
            BetaCategoryEnd();
            
            BetaCategoryBegin(
                new GUIContent("Airship Component Inspector Rework & Custom Inspectors"), 
                new GUIContent("Changes how AirshipComponent properties are displayed in the editor, as well if custom editors are supported"));
            {
                if (AirshipCustomEditors.EditorInspectorMode == EditorInspectorMode.UseNewInspector) {
                    EditorGUILayout.HelpBox("Using the new inspectors, please report any issues to @Vorlias on discord. Custom editor API is subject to change.", MessageType.Warning);
                    EditorGUILayout.Space(5);
                }
                
                AirshipCustomEditors.EditorInspectorMode = (EditorInspectorMode) EditorGUILayout.EnumPopup(
                    new GUIContent("Use AirshipEditors", "Use the new Airship editor system which allows custom editors and the new property API"),
                    EditorIntegrationsConfig.instance.editorInspectorMode);

                if (AirshipCustomEditors.EditorInspectorMode == EditorInspectorMode.UseNewInspector) {

                    listEditors = EditorGUILayout.BeginFoldoutHeaderGroup(listEditors, "Active Custom Editors");
                    if (listEditors) {
                        var codeStyle = new GUIStyle() {
                            font = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font,
                            fontSize = 11,
                            fontStyle = FontStyle.Normal,
                            normal = new GUIStyleState() {
                                textColor = new Color(0.8f, 0.8f, 0.8f)
                            },
                        };
                        
                        editorScroll = EditorGUILayout.BeginScrollView(editorScroll, GUILayout.MaxHeight(250));

                        foreach (var editor in AirshipCustomEditors.Editors) {
                            EditorGUILayout.BeginHorizontal(new GUIStyle("FrameBox")
                                { margin = new RectOffset(5, 5, 5, 5) });
                            {
                                EditorGUILayout.BeginVertical(GUILayout.Width(20));
                                EditorGUILayout.Space();
                                GUI.enabled = false; // editor.EditorAttribute is not CustomAirshipCoreEditorAttribute;
                                EditorGUILayout.Toggle(true, GUILayout.Width(20));
                                GUI.enabled = true;
                                EditorGUILayout.Space();
                                EditorGUILayout.EndVertical();
                            
                                EditorGUILayout.BeginVertical();
                                {
                                    EditorGUILayout.LabelField(editor.AirshipType.UniqueId, new GUIStyle(codeStyle) {
                                        fontStyle = FontStyle.Bold,
                                        normal = new GUIStyleState() {
                                            textColor = new Color(1, 1, 1)
                                        },
                                    });
                                    EditorGUILayout.LabelField($"{editor.EditorType.FullName} ({editor.EditorType.Assembly.FullName})"
                                        , codeStyle);

               
                                }
                                EditorGUILayout.EndVertical();
                                
                                var editors = AirshipCustomEditors.GetEditors(editor);
                                var count = editors.Count();
                                
                                EditorGUILayout.LabelField($"{count} editor{(count == 1 ? "" : "s")}", codeStyle);
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                    
                        EditorGUILayout.EndScrollView();
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();
                }
            }
            BetaCategoryEnd();
            
            if (GUI.changed) {
                AirshipLocalArtifactDatabase.instance.Modify();
                EditorIntegrationsConfig.instance.Modify();
            }
        }
        
        // Register the SettingsProvider
        [SettingsProvider]
        public static SettingsProvider CreateBetasSettingsProvider()
        {
            var provider = new AirshipBetaSettingsProvider(Path) {
                keywords = new[] { "Airship", "Beta", "Experimental" },
                label = "Editor Features",
            };
            return provider;
        }
    }
}