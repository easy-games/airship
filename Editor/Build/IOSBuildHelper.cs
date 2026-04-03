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

    /// <summary>
    /// Fixes absolute paths in the notificationservice extension target added by
    /// com.unity.services.push-notifications. That package passes the build output path
    /// (which Unity resolves to absolute) into AddAppExtension and AddFile, baking
    /// machine-specific paths into the Xcode project. This breaks when the Xcode project
    /// is built on a different machine (e.g. CI: Unity in Docker, xcodebuild on macOS).
    /// Must run after the push-notifications post-process (order 1).
    /// </summary>
    [PostProcessBuild(2)]
    static void FixNotificationServiceAbsolutePaths(BuildTarget buildTarget, string path)
    {
        if (buildTarget != BuildTarget.iOS)
            return;

        string pbxprojPath = PBXProject.GetPBXProjectPath(path);
        string pbxproj = File.ReadAllText(pbxprojPath);

        string absolutePrefix = path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (pbxproj.Contains(absolutePrefix))
        {
            pbxproj = pbxproj.Replace(absolutePrefix, "");
            File.WriteAllText(pbxprojPath, pbxproj);
        }
    }
#endif
}