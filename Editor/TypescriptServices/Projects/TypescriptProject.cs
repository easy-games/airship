using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsToTs.TypeScript;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

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

    internal struct CompiledFileEvent {
        public string fileName;
    }

    internal struct CompilerStartCompilationEvent {
        public bool Initial;
        public int Count;
    }

    public class TypescriptProjectCompileState {
        /// <summary>
        /// Whether or not this project requires an initial compile
        /// </summary>
        internal bool RequiresInitialCompile;
        
        /// <summary>
        /// The total files that needed to be compiled in the last compile
        /// </summary>
        internal int FilesToCompileCount;
        /// <summary>
        /// The total number of files that were compiled in the last compile
        /// </summary>
        internal int CompiledFileCount;
    }
    
    public class TypescriptProject {
        protected bool Equals(TypescriptProject other) {
            return Equals(CompilationState, other.CompilationState) && ProgressId == other.ProgressId && Equals(FileProblemItems, other.FileProblemItems) && Directory == other.Directory && Equals(TsConfig, other.TsConfig) && Equals(Package, other.Package);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TypescriptProject)obj);
        }

        public override int GetHashCode() {
            return HashCode.Combine(CompilationState, ProgressId, FileProblemItems, Directory, TsConfig, Package);
        }

        internal Dictionary<string, HashSet<TypescriptProblemItem>> FileProblemItems { get; private set; } = new();
        internal TypescriptProjectCompileState CompilationState = new();
        
        /// <summary>
        /// The progress id of this project (if applicable)
        /// </summary>
        internal int ProgressId;
        
        /// <summary>
        /// Problematic items in this Typescript Project
        /// </summary>
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

        internal int ErrorCount => ProblemItems.Count(item => item.ProblemType == TypescriptProblemType.Error);
            
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

        public string[] GetInputPaths(string output) {
            List<string> paths = new List<string>();
            foreach (var rootDir in TsConfig.RootDirs) {
                if (!output.StartsWith(TsConfig.OutDir)) continue;
                
                var input = output.Replace(TsConfig.OutDir, rootDir).Replace(".lua", ".ts");
                paths.Add(input);
            }
            
            return paths.ToArray();
        }

        private string TransformOutputPath(string input) {
            return input.Replace(".ts", ".lua");
        }
        
        /// <summary>
        /// The directory of this typescript project
        /// </summary>
        public string Directory {
            get;
        }

        public string Name => Package.Name;

        /// <summary>
        /// The typescript configuration for this project
        /// </summary>
        public TypescriptConfig TsConfig { get; }
        
        /// <summary>
        /// The node package.json configuration for this project
        /// </summary>
        public PackageJson Package { get; }
        
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