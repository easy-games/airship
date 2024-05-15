using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsToTs.TypeScript;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Airship.Editor {
    internal enum TypescriptDiagnosticCategory {
        Warning,
        Error,
        Suggestion,
        Message,
    }
    
    internal struct CompilerEditorFileDiagnosticEvent {
        [CanBeNull] public string FilePath;
        [CanBeNull] public string Message;
        public int? Code;
        public TypescriptDiagnosticCategory Category;
        public int? Position;
        [CanBeNull] public string Source;
        public int? Line;
        public int? Column;
        public int? Length;
        [CanBeNull] public string Text;
    }

    internal struct WatchReportEvent {
        public TypescriptDiagnosticCategory Category;
        public string MessageText;
    }

    internal struct CompilerFinishCompilationWithErrorsEvent {
        public int ErrorCount;
    }

    internal struct CompilerStartCompilationEvent {
        public bool Initial;
    }

    public class TypescriptProject {
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
            item.Project = this;
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
        
        /// <summary>
        /// Gets the output path for the given input path
        /// </summary>
        /// <param name="input">The input path relative to the project</param>
        /// <returns>The output path</returns>
        public string GetOutputPath(string input) {
            foreach (var rootDir in TsConfig.RootDirs) {
                if (!input.StartsWith(rootDir)) continue;
                
                var output = input.Replace(rootDir, TsConfig.OutDir);
                return TransformOutputPath(output);
            }

            return TransformOutputPath(input);
        }

        private string TransformOutputPath(string input) {
            if (input.EndsWith("index.ts")) {
                input = input.Replace("index.ts", "init.ts");
            }

            return input.Replace(".ts", ".lua");
        }
        
        public string Directory {
            get;
        }

        public TypescriptConfig TsConfig { get; private set; }
        
        public PackageJson Package { get; private set; }
        public bool HasNodeModules => System.IO.Directory.Exists(Path.Join(Directory, "node_modules"));

        public bool HasCompiler => System.IO.Directory.Exists(Path.Join(Directory, "node_modules", "@easy-games/unity-ts"));

        private bool IsCompilableTypescriptProject =>
            Package is { DevDependencies: not null } && (Package.DevDependencies.ContainsKey("@easy-games/unity-ts") || Package.Dependencies.ContainsKey("@easy-games/unity-ts"));
        
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
        
        public TypescriptProject(TypescriptConfig tsconfig, PackageJson package) {
            this.Directory = tsconfig.Directory;
            this.TsConfig = tsconfig;
            this.Package = package;
        }
        
        public static bool operator==(TypescriptProject lhs, TypescriptProject rhs) {
            return lhs?.Directory == rhs?.Directory;
        }

        public static bool operator !=(TypescriptProject lhs, TypescriptProject rhs) {
            return lhs?.Directory != rhs?.Directory;
        }
    }
}