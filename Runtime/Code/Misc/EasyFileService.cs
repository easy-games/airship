using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Profiling;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif
using UnityEngine.Scripting;

[LuauAPI][Preserve]
public class EasyFileService {
    private static string[] allFilesCache;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void OnLoad() {
        allFilesCache = null;
    }

    public static string[] GetFilesInPath(string path, string searchPattern = "*.lua") {
        Profiler.BeginSample("GetFilesInPath");
        path = path.ToLower();

#if UNITY_EDITOR
        var root = SystemRoot.Instance;
        if (root && !AssetBridge.useBundles) {
            if (allFilesCache != null) {
                var results = allFilesCache.Where((p) => {
                    return p.Contains(path) && Regex.IsMatch(p, searchPattern);
                }).ToArray();
                Profiler.EndSample();
                return results;
            } else {
                string[] guids = AssetDatabase.FindAssets("t: ScriptableObject");
                List<string> results = new();
                List<string> all = new();
                foreach (var guid in guids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid).ToLower();
                    if (p.Contains(path) && Regex.IsMatch(p, searchPattern))
                    {
                        results.Add(p);
                    }
                    all.Add(p);
                }

                allFilesCache = all.ToArray();
                Profiler.EndSample();
                return results.ToArray();
            }
        }
#endif

        if (allFilesCache == null) {
            allFilesCache = AssetBridge.Instance.GetAllAssets();
        }
        var paths = allFilesCache.Where((p) => {
            return p.Contains(path) && Regex.IsMatch(p, searchPattern);
        }).ToArray();
        Profiler.EndSample();
        return paths;
    }
}