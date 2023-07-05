using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.Profiling;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#endif
using UnityEngine.Scripting;

[LuauAPI][Preserve]
public class EasyFileService {
    public static string[] GetFilesInPath(string path, string searchPattern = "*.lua") {
        Profiler.BeginSample("GetFilesInPath");
        path = path.ToLower();

#if UNITY_EDITOR
        var root = AssetBridge.GetRoot();
        if (root && !root.IsUsingBundles())
        {
            string[] guids = AssetDatabase.FindAssets("t: ScriptableObject");
            List<string> results = new();
            foreach (var guid in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid).ToLower();
                if (p.Contains(path) && Regex.IsMatch(p, searchPattern))
                {
                    results.Add(p);
                }
            }

            Profiler.EndSample();
            return results.ToArray();
        }
#endif

        var paths = AssetBridge.GetAllAssets();
        paths = paths.Where((p) => {
            return p.Contains(path) && Regex.IsMatch(p, searchPattern);
        }).ToArray();
        Profiler.EndSample();
        return paths;
    }
}