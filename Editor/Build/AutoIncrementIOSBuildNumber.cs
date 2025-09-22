using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Editor.Build {
    public class AutoIncrementIOSBuildNumber : IPreprocessBuildWithReport {
        // This makes sure the script runs before the build starts.
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) {
            if (report.summary.platform == BuildTarget.iOS) {
                // Get current build number
                int currentBuild = int.Parse(PlayerSettings.iOS.buildNumber);

                // Increment by 1
                int newBuild = currentBuild + 1;

                // Set the new build number
                PlayerSettings.iOS.buildNumber = newBuild.ToString();

                Debug.Log($"[iOS] Auto-incremented build number: {currentBuild} → {newBuild}");
            }
        }
    }
}