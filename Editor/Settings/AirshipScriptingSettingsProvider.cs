using Airship.Editor;
using UnityEditor;
using UnityEngine;

namespace Editor.Settings {
    public class AirshipScriptingSettingsProvider : SettingsProvider  {
        private const string Path = "Project/Airship/Typescript Scripting";

        private bool showTypescriptEditorIntegrations = true;
        
        private AirshipScriptingSettingsProvider(string path, SettingsScope scopes = SettingsScope.Project) : base(path, scopes) { }
        
        public override void OnGUI(string searchContext) {
            EditorGUILayout.Space(10);
            showTypescriptEditorIntegrations = EditorGUILayout.BeginFoldoutHeaderGroup(showTypescriptEditorIntegrations, "Typescript");
            if (showTypescriptEditorIntegrations) {
                TypescriptOptions.RenderSettings();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        // Register the SettingsProvider
        [SettingsProvider]
        public static SettingsProvider CreateTypescriptSettingsProvider()
        {
            var provider = new AirshipScriptingSettingsProvider(Path, SettingsScope.Project);
            provider.keywords = new[] { "Github", "Airship", "Typescript", "Compiler", "Scripting", "Scripts", "Compiling" };
            return provider;
        }
    }
}