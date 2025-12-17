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
            PBXProject project = new PBXProject();
            project.ReadFromFile(projectPath);

            string mainTarget = project.GetUnityMainTargetGuid();

            // Add Push Notifications capability
            project.AddCapability(
                mainTarget,
                PBXCapabilityType.PushNotifications
            );

            // Optional but common
            project.AddCapability(
                mainTarget,
                PBXCapabilityType.BackgroundModes
            );

            project.WriteToFile(projectPath);
        }
    }
#endif
}