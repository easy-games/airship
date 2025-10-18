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