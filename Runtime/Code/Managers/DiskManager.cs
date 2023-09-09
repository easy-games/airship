using System;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;

[LuauAPI]
public class DiskManager : Singleton<DiskManager> {
    [CanBeNull]
    public static async Task<string> ReadFileAsync(string path) {
        var fullPath = Path.Join(Application.persistentDataPath, path);
        if (!File.Exists(fullPath)) {
            return null;
        }

        var contents = await File.ReadAllTextAsync(fullPath);
        return contents;
    }

    public static async Task<bool> WriteFileAsync(string path, string contents) {
        var fullPath = Path.Join(Application.persistentDataPath, path);
        try {
            await File.WriteAllTextAsync(fullPath, contents);
            return true;
        } catch (Exception e) {
            Debug.LogError(e);
            return false;
        }
    }
}