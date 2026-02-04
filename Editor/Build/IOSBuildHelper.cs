using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
using System.IO;
using System.Collections.Generic;

public class IOSBuildProcessor {
#if UNITY_IOS
    [PostProcessBuild]
    static void OnPostprocessBuild(BuildTarget buildTarget, string path)
    {
        // Read plist
        var plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        // Update value
        PlistElementDict rootDict = plist.root;
        rootDict.SetString("NSCameraUsageDescription", "Used for taking profile pictures.");

        // Write plist
        File.WriteAllText(plistPath, plist.WriteToString());

        // Push Notification Entitlements
        {
            string projectPath = PBXProject.GetPBXProjectPath(path);
            PBXProject pbx = new PBXProject();
            pbx.ReadFromFile(projectPath);

            string mainTargetGuid = pbx.GetUnityMainTargetGuid();

            // Put the entitlements file at the root of the Xcode project (common convention).
            string entitlementsFileName = "Entitlements.entitlements";

            // ProjectCapabilityManager will create/maintain the entitlements file and hook it up.
            var capManager = new ProjectCapabilityManager(projectPath, entitlementsFileName, "Unity-iPhone", mainTargetGuid);

            capManager.AddPushNotifications(true); // 'true' enables Background Modes -> Remote notifications too
            // If you don't want background remote notifications, use false:
            // capManager.AddPushNotifications(false);

            capManager.WriteToFile();
        }
    }
#endif
}