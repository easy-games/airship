using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class AirshipEditorUtil {
    public static BuildTarget[] AllBuildTargets = new BuildTarget[]
        { BuildTarget.StandaloneWindows64, BuildTarget.StandaloneOSX, BuildTarget.StandaloneLinux64 };

    public static BuildTarget GetLocalBuildTarget() {
        return EditorUserBuildSettings.activeBuildTarget;
    }

    public static bool AllRequestsDone(List<UnityWebRequestAsyncOperation> requests)
    {
        // A little Linq magic
        // returns true if All requests are done
        return requests.All(r => r.isDone);
    }
}