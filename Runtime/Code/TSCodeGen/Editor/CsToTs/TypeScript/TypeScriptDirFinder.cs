using System;
using System.Collections.Generic;
using System.IO;
using Code.GameBundle;

namespace CsToTs.TypeScript
{
    [Flags]
    public enum TypescriptDirectorySearchFlags {
        None,
        NodeModules = 1 << 0,
    }
    
    public static class TypeScriptDirFinder
    {
        public static string FindCorePackageDirectory()
        {
            var queue = new Queue<string>();
            queue.Enqueue("Assets");

            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();
                var tsDir = Path.Join(dir, "Core");

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
        
        public static string[] FindTypeScriptDirectories(TypescriptDirectorySearchFlags includeFlags = 0, TypescriptDirectorySearchFlags excludeFlags = 0) {
            var installedOnly = (includeFlags & TypescriptDirectorySearchFlags.NodeModules) != 0;
            var uninstalledOnly = !installedOnly && (excludeFlags & TypescriptDirectorySearchFlags.NodeModules) != 0;
            
            List<string> dirs = new();
            
            var queue = new Queue<string>();
            queue.Enqueue("Assets");

            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();
                var tsconfigPath = Path.Join(dir, "tsconfig.json");
                var packageJsonPath = Path.Join(dir, "package.json");
                var nodeModulesPath = Path.Join(dir, "node_modules");

                bool valid = true;
                
                if (uninstalledOnly && Directory.Exists(nodeModulesPath)) {
                    valid = false;
                } else if (installedOnly && !Directory.Exists(nodeModulesPath)) {
                    valid = false;
                }
                
                if (File.Exists(tsconfigPath) && File.Exists(packageJsonPath) && valid)
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
