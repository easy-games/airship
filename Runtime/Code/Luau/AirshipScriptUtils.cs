using System;
using System.IO;

namespace Code.Luau {
    public static class AirshipScriptUtils {
        public static string CleanupFilePath(string path) {
            string extension = Path.GetExtension(path);

            if (extension == "") {
                path += ".lua";
            }

            path = path.ToLower();
            if (path.StartsWith("assets/", StringComparison.Ordinal)) {
                path = path.Substring("assets/".Length);
            }

            return path;
        }
    }
}