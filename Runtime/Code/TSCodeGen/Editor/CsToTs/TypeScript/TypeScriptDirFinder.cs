using System;
using System.Collections.Generic;
using System.IO;
using Code.GameBundle;

namespace CsToTs.TypeScript
{
    public static class TypeScriptDirFinder
    {
        public static string FindCorePackageDirectory()
        {
            var queue = new Queue<string>();
            queue.Enqueue("Assets");

            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();
                var tsDir = Path.Join(dir, "Core~");

                if (Directory.Exists(tsDir))
                {
                    return tsDir;
                }

                var subDirs = Directory.GetDirectories(dir);
                foreach (var subDir in subDirs)
                {
                    queue.Enqueue(subDir);
                }
            }

            return null;
        }

        public static string FindTypeScriptDirectoryByPackage(AirshipPackageDocument package) {
            var packagePath = AirshipPackageDocument.FindPathFromDocument(package);

            var queue = new Queue<string>();
            queue.Enqueue(packagePath);
            
            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();
                var packageJsonPath = Path.Join(dir, "tsconfig.json");

                if (File.Exists(packageJsonPath)) {
                    return dir;
                }

                var subDirs = Directory.GetDirectories(dir);
                foreach (var subDir in subDirs)
                {
                    queue.Enqueue(subDir);
                }
            }
            
            return packagePath;
        }
        
        public static string[] FindTypeScriptDirectories() {
            List<string> dirs = new();
            
            var queue = new Queue<string>();
            queue.Enqueue("Assets");

            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();
                var packageJsonPath = Path.Join(dir, "tsconfig.json");

                if (File.Exists(packageJsonPath))
                {
                    dirs.Add(dir);
                    continue;
                }

                var subDirs = Directory.GetDirectories(dir);
                foreach (var subDir in subDirs)
                {
                    queue.Enqueue(subDir);
                }
            }

            return dirs.ToArray();
        }
    }
}
