using System;
using System.Collections.Generic;
using System.IO;
using CsToTs.TypeScript;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Airship.Editor {
    public class TypescriptConfig {
        public class CompilerOptions {
            [CanBeNull] public string rootDir;
            [CanBeNull] public string[] rootDirs;
            [CanBeNull] public string outDir;
        }

        [JsonConverter(typeof(StringEnumConverter))]  
        public enum ProjectType {
            AirshipBundle,
            Game,
        }

        public class AirshipConfig {
            public ProjectType ProjectType;
        }
        
        public CompilerOptions compilerOptions;
        
        [Obsolete] [CanBeNull] public AirshipConfig rbxts;
        public AirshipConfig airship;
        
        [CanBeNull] public string[] include;
        [CanBeNull] public string[] exclude;
        
        public static TypescriptConfig ReadTsConfig(string dir, string tsconfig = "tsconfig.json") {
            return JsonConvert.DeserializeObject<TypescriptConfig>(File.ReadAllText(Path.Join(dir, tsconfig)));
        }
    }
    
    public class TypescriptProject {
        public static IReadOnlyList<TypescriptProject> GetAllProjects() {
            List<TypescriptProject> projects = new();

            var typeScriptDirectories = TypeScriptDirFinder.FindTypeScriptDirectories();
            foreach (var directory in typeScriptDirectories) {
                projects.Add(new TypescriptProject(directory));
            }

            return projects;
        }
        
        public string Directory {
            get;
        }

        public TypescriptConfig TsConfigJson => TypescriptConfig.ReadTsConfig(Directory);
        
        public PackageJson PackageJson => NodePackages.ReadPackageJson(Directory);
        public bool HasNodeModules => System.IO.Directory.Exists(Path.Join(Directory, "node_modules"));

        public bool HasCompiler => System.IO.Directory.Exists(Path.Join(Directory, "node_modules", "@easy-games/unity-ts"));

        public Semver CompilerVersion {
            get {
                var packageInfo = NodePackages.GetPackageInfo(Directory, "@easy-games/unity-ts");
                return Semver.Parse(packageInfo.Version);
            }
        }
        
        public Semver CompilerTypesVersion {
            get {
                var packageInfo = NodePackages.GetPackageInfo(Directory, "@easy-games/compiler-types");
                return Semver.Parse(packageInfo.Version);
            }
        }
        
        public Semver FlameworkVersion {
            get {
                var packageInfo = NodePackages.GetPackageInfo(Directory, "@easy-games/unity-flamework-transformer");
                return Semver.Parse(packageInfo.Version);
            }
        }
        
        public TypescriptProject(string directory) {
            this.Directory = directory;
        }
        
        public static bool operator==(TypescriptProject lhs, TypescriptProject rhs) {
            return lhs.Directory == rhs.Directory;
        }

        public static bool operator !=(TypescriptProject lhs, TypescriptProject rhs) {
            return lhs.Directory != rhs.Directory;
        }
    }
}