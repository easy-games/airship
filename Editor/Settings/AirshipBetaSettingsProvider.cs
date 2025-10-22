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

        private int tab = 0;
        
        public override void OnGUI(string searchContext) {
            this.tab = AirshipEditorGUI.BeginTabs(this.tab, new[] {
                new GUIContent(" User Settings", EditorGUIUtility.IconContent("BuildSettings.Standalone.Small").image), 
                // new GUIContent(" Project Settings", EditorGUIUtility.IconContent("Project").image)
            });
            
            
            if (this.tab == 0) {
                BetaCategoryBegin(
                    new GUIContent("Airship Component Inspector Rework & Custom Inspectors (User)"), 
                    new GUIContent("Changes how AirshipComponent properties are displayed in the editor, as well if custom editors are supported"));
                {
      
                    
                    AirshipCustomEditors.UserInspectorMode = (EditorInspectorMode) EditorGUILayout.EnumPopup(
                        new GUIContent("AirshipEditors v2", "Use the new Airship editor system which allows custom editors and the new property API"),
                        AirshipCustomEditors.UserInspectorMode);

                    if (AirshipCustomEditors.UseNewInspector) {
                        EditorGUILayout.HelpBox("Using the new inspectors, please report any issues to @Vorlias on discord. Custom editor API is subject to change.", MessageType.Warning);
                        EditorGUILayout.Space(5);
                        
                        
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

                            var defaultEditors = AirshipCustomEditors.AllEditors.Where(editor =>
                                editor.GetType() == typeof(DefaultAirshipComponentEditor));
                            EditorGUILayout.LabelField("Default Editors", defaultEditors.Count().ToString());
                            
                            foreach (var editor in AirshipCustomEditors.CustomEditors) {
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
            } else if (this.tab == 1) {

            }
            
            AirshipEditorGUI.EndTabs();
            
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
                label = "Editor Beta Features",
            };
            return provider;
        }
    }
}