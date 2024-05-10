using System.IO;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    public class TypescriptProjectMigration {
        private static void MigrateTypescriptFiles() {
            var projectFolder = "Assets/Typescript~";
            Debug.Log("Testing lmfao");
            
            if (Directory.Exists(projectFolder)) {
                Debug.Log("I have project folder");
            }
            else {
                Debug.LogWarning(" I don't hav eproject folder");
            }
        }
        
        [MenuItem("Airship/Migrate to Project V2...", priority = 10)]
        public static void MigrateProject() {
            if (!TypescriptConfig.HasTsConfig("Assets")) {
                // There's no root level tsconfig, should be an old project
            }
            else {
                // MigrateTypescriptFiles();
                EditorUtility.DisplayDialog("Upgrade", "Already using the new project format!", "OK");
            }
        }
    }
}