using System;
using System.IO;

namespace Editor.Util {
    public static class PosixPath {
        public static string Join(ReadOnlySpan<char> path1, ReadOnlySpan<char> path2) {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
                return Path.Join(path1, path2).Replace("\\", "/");
#else
                return Path.Join(path1, path2);
#endif
        }

        public static string GetRelativePath(string relativeTo, string path) {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
            return Path.GetRelativePath(relativeTo, path).Replace("\\", "/");
#else
            return Path.GetRelativePath(relativeTo, path);
#endif
        }

        public static string ToPosix(string path) {
            return path.Replace("\\", "/");
        }
    }
}