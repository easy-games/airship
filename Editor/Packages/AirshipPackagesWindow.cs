using System;
using UnityEditor;
using UnityEngine;

namespace Editor.Packages {
    public class AirshipPackagesWindow : EditorWindow {
        [MenuItem ("Window/Airship Packages")]
        public static void  ShowWindow () {
            EditorWindow.GetWindow(typeof(AirshipPackagesWindow));
        }

        private void OnGUI() {
            GUILayout.Button("Download All");
        }
    }
}