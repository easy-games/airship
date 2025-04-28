using Airship.Editor;
using UnityEditor;
using UnityEngine;

namespace Editor.Settings {
    public class AirshipScriptingSettingsProvider : SettingsProvider  {
        public const string Path = "Project/Airship/Typescript";

        private AirshipScriptingSettingsProvider(string path, SettingsScope scopes = SettingsScope.Project) : base(path, scopes) { }
        
        public override void OnGUI(string searchContext) {
            EditorGUILayout.Space(10);
            TypescriptOptions.RenderSettings(searchContext);
        }
        
        // Register the SettingsProvider
        [SettingsProvider]
        public static SettingsProvider CreateTypescriptSettingsProvider()
        {
            var provider = new AirshipScriptingSettingsProvider(Path) {
                keywords = new[] { "Github", "Airship", "Typescript", "Compiler", "Scripting", "Scripts", "Compiling" },
                label = "TypeScript Services",
            };
            return provider;
        }
    }
}