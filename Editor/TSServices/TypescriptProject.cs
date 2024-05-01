using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    
    public class TypescriptProblemItem : IEquatable<TypescriptProblemItem> {
        public readonly TypescriptProject Project;
        public readonly string FileLocation;
        public readonly string Message;
        public readonly int ErrorCode;
        public readonly TypescriptProblemType ProblemType;
        public readonly TypescriptLocation Location;

        private TypescriptProblemItem(TypescriptProject project, string fileLocation, string message, int errorCode, TypescriptLocation location, TypescriptProblemType problemType) {
            Project = project;
            FileLocation = fileLocation;
            Message = message;
            ErrorCode = errorCode;
            ProblemType = problemType;
            Location = location;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TypescriptProblemItem)obj);
        }

        public static bool operator ==(TypescriptProblemItem left, TypescriptProblemItem right) {
            return left?.GetHashCode() == right?.GetHashCode();
        }
        
        public static bool operator !=(TypescriptProblemItem left, TypescriptProblemItem right) {
            return left?.GetHashCode() != right?.GetHashCode();
        }

        public override string ToString() {
            return $"{FileLocation}:{Location.Line}:{Location.Column}: {Message}";
        }

        public override int GetHashCode() {
            return HashCode.Combine(Project, FileLocation, Message, ErrorCode, (int)ProblemType, Location);
        }

        private static readonly Regex errorRegex = new(@"(src\\.+[\\][^\\]+\.ts|src/.+[\/][^\/]+\.ts)(?::(\d+):(\d+)) - error (?:TS([0-9]+)|TS unity-ts): (.*)");
        
        [CanBeNull]
        internal static TypescriptProblemItem Parse(string input) {
            
            
            input = TerminalFormatting.StripANSI(input);

            if (!errorRegex.IsMatch(input)) {
                Debug.Log($"Is not error item {input}");
                return null;
            }

            TypescriptLocation location;
            
            var values = errorRegex.Match(input);
            var fileLocation = values.Groups[1].Value;
            
            int.TryParse(values.Groups[2].Value, out location.Line);
            int.TryParse(values.Groups[3].Value, out location.Column);
            int.TryParse(values.Groups[4].Value, out var errorCode);
            
            var message = values.Groups[5].Value;
            
            var problemItem = new TypescriptProblemItem(null, fileLocation, message, errorCode, location, TypescriptProblemType.Error);
            return problemItem;
        }

        public bool Equals(TypescriptProblemItem other) {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Project, other.Project) && FileLocation == other.FileLocation && Message == other.Message && ErrorCode == other.ErrorCode && ProblemType == other.ProblemType && Location.Equals(other.Location);
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

        internal Dictionary<string, HashSet<TypescriptProblemItem>> FileProblemItems { get; private set; } = new();

        internal IReadOnlyList<TypescriptProblemItem> ProblemItems {
            get {
                HashSet<TypescriptProblemItem> problemItems = new();
                foreach (var pair in FileProblemItems) {
                    foreach (var item in pair.Value) {
                        problemItems.Add(item);
                    }
                }

                return problemItems.ToList();
            }
        }
            
        internal void AddProblemItem(string file, TypescriptProblemItem item) {
            if (FileProblemItems.TryGetValue(file, out var items)) {
                items.Add(item);
            }
            else {
                items = new HashSet<TypescriptProblemItem>();
                items.Add(item);
                FileProblemItems.Add(file, items);
            }
        }

        internal void ClearAllProblems() {
            FileProblemItems.Clear();
        }
        
        internal void ClearProblemItemsForFile(string file) {
            if (FileProblemItems.TryGetValue(file, out var items)) {
                items.Clear();
                FileProblemItems.Remove(file);
            }
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