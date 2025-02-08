using System;
using System.IO;
using System.Threading.Tasks;
using Codice.Client.Common;
using JetBrains.Annotations;
using UnityEngine;

namespace Code.Managers {
    [LuauAPI(LuauContext.Protected)]
    public class DiskManager : Singleton<DiskManager> {
        [CanBeNull]
        public static async Task<string> ReadFileAsync(string path) {
            var fullPath = Path.Join(Application.persistentDataPath, path);
            if (!File.Exists(fullPath)) {
                return "";
            }

            try {
                var contents = await File.ReadAllTextAsync(fullPath);
                return contents;
            } catch (Exception e) {
                Debug.LogError(e);
                return "";
            }
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

        public static void EnsureDirectory(string path) {
            var p = Path.Join(Application.persistentDataPath, path);
            if (!Directory.Exists(p)) {
                Directory.CreateDirectory(p);
            }
        }
    }
}