using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
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

    public static void ClearCache() {
        allFilesCache = null;
    }

    public static string[] GetFilesInPath(string path, string searchPattern = "*.ts") {
        path = path.ToLower();

        // code.zip
        var luaOnlyPattern = searchPattern.Replace(".ts", ".lua");
        var root = SystemRoot.Instance;
        if (root && root.luauFiles.Count > 0) {
            List<string> results = new();
            foreach (var pair in root.luauFiles) {
                foreach (var filePair in pair.Value) {
                    var p = filePair.Key.ToLower();
                    if (p.Contains(path) && Regex.IsMatch(p, luaOnlyPattern)) {
                        results.Add(p);
                    }
                }
            }

            return results.ToArray();
        }

#if UNITY_EDITOR
        if (!AssetBridge.useBundles) {
            if (allFilesCache != null) {
                var results = allFilesCache.Where((p) => {
                    return p.Contains(path) && Regex.IsMatch(p, searchPattern);
                }).ToArray();
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
        return paths;
    }
}