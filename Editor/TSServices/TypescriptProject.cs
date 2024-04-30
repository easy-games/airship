using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CsToTs.TypeScript;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

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
        
        public static TypescriptConfig ReadTsConfig(string dir, string tsconfig = "tsconfig.json") {
            return JsonConvert.DeserializeObject<TypescriptConfig>(File.ReadAllText(Path.Join(dir, tsconfig)));
        }
    }
    
    public struct TypescriptProblemItem {
        public TypescriptProject Project;
        public string FileLocation;
        public string Message;
        public int ErrorCode;
        public TypescriptProblemType ProblemType;
        public TypescriptLocation Location;
        
        public override int GetHashCode() {
            return $"{FileLocation}@{Location.Column},{Location.Line}:{ErrorCode}:{Message}".GetHashCode();
        }

        internal static TypescriptProblemItem ErrorFromWatchState(TypescriptCompilerWatchState watchState, string message, TypescriptLocation location) {
            return new TypescriptProblemItem() {
                Project = watchState.project,
                Message = message,
                Location = location,
                ProblemType = TypescriptProblemType.Error,
            };
        }

        private static readonly Regex errorRegex = new(@"(src\\.+[\\][^\\]+\.ts|src/.+[\/][^\/]+\.ts)(?::(\d+):(\d+)) - error (?:TS([0-9]+)|TS unity-ts): (.*)");
        
        internal static TypescriptProblemItem? Parse(string input) {
            var problemItem = new TypescriptProblemItem();
            
            input = TerminalFormatting.StripANSI(input);

            if (!errorRegex.IsMatch(input)) {
                Debug.Log($"Is not error item {input}");
                return null;
            }

            TypescriptLocation location;
            
            var values = errorRegex.Match(input);
            problemItem.FileLocation = values.Groups[1].Value;
            
            int.TryParse(values.Groups[2].Value, out location.Line);
            int.TryParse(values.Groups[3].Value, out location.Column);
            int.TryParse(values.Groups[4].Value, out problemItem.ErrorCode);
            
            problemItem.Message = values.Groups[5].Value;
            problemItem.Location = location;
            return problemItem;
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

        internal HashSet<TypescriptProblemItem> ProblemItems { get; private set; } = new();
        internal void AddProblemItem(TypescriptProblemItem item) {
            ProblemItems.Add(item);
        }

        internal void ClearProblemItems() {
            ProblemItems.Clear();
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