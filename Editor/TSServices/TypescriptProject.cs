using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            public ProjectType ProjectType = ProjectType.Game;
        }
        
        public CompilerOptions compilerOptions;
        
        [Obsolete] [CanBeNull] public AirshipConfig rbxts;
        public AirshipConfig airship;
        
        [CanBeNull] public string[] include;
        [CanBeNull] public string[] exclude;

        public ProjectType AirshipProjectType {
            get {
                if (airship != null) {
                    return airship.ProjectType;
                } else if (rbxts != null) {
                    return rbxts.ProjectType;
                }

                return ProjectType.Game;
            }
        }
        
        /// <summary>
        /// The directory of the project itself
        /// </summary>
        public string Directory { get; private set; }
        
        /// <summary>
        /// The file path of this project file
        /// </summary>
        public string ConfigFilePath { get; private set; }

        /// <summary>
        /// The output directory of this project
        /// </summary>
        public string OutDir => Path.Join(Directory, compilerOptions.outDir).Replace("\\", "/");

        /// <summary>
        /// The root directories of this project
        /// </summary>
        public string[] RootDirs {
            get {
                if (compilerOptions.rootDirs is {} rootDirs) {
                    return rootDirs.Select(dir => $"{Directory}/{dir}").ToArray();
                } 
                
                if (compilerOptions.rootDir is { } rootDir) {
                    return new [] { Directory + "/" + rootDir };
                }

                return new string[] { };
            }
        }

        public string GetOutputPath(string input) {
            foreach (var rootDir in RootDirs) {
                if (!input.StartsWith(rootDir)) continue;
                
                var output = input.Replace(rootDir, OutDir);
                return output.Replace(".ts", ".lua");
            }

            return input.Replace(".ts", ".lua");
        }

        public static TypescriptConfig ReadTsConfig(string dir, string tsconfig = "tsconfig.json") {
            var filePath = Path.Join(dir, tsconfig);
            var config = JsonConvert.DeserializeObject<TypescriptConfig>(File.ReadAllText(filePath));
            config.Directory = dir;
            config.ConfigFilePath = filePath;
            return config;
        }
    }
    
    public class TypescriptProject {
        public static IReadOnlyList<TypescriptProject> GetAllProjects() {
            List<TypescriptProject> projects = new();

            var typeScriptDirectories = TypeScriptDirFinder.FindTypeScriptDirectories();
            foreach (var directory in typeScriptDirectories) {
                TypescriptProject project = new TypescriptProject(directory);
                if (!project.IsCompilableTypescriptProject) continue;
                
                projects.Add(project);
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

        private bool IsCompilableTypescriptProject =>
            PackageJson is { DevDependencies: not null } && (PackageJson.DevDependencies.ContainsKey("@easy-games/unity-ts") || PackageJson.Dependencies.ContainsKey("@easy-games/unity-ts"));
        
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