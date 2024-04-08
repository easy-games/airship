using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Editor.Build {
    internal class AirshipIOSBuildProcessor {

        [PostProcessBuild]
        public static void UpdateInfoPList(BuildTarget buildTarget, string pathToBuiltProject) {
            if (buildTarget != BuildTarget.iOS) {
                return;
            }

            var plistPath = pathToBuiltProject + "/Info.plist";
            var contents = File.ReadAllText(plistPath);
#if UNITY_IOS || UNITY_TVOS
            var plist = new UnityEditor.iOS.Xcode.PlistDocument();
            plist.ReadFromString(contents);
            var root = plist.root;
            var buildKey = "NSCameraUsageDescription";
            root.SetString(buildKey, "Use the camera to take profile pictures.");
            File.WriteAllText(plistPath, plist.WriteToString());
#endif
        }
    }
}