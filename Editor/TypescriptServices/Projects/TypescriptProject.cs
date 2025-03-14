using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CsToTs.TypeScript;
using Editor.Util;
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

        internal TypeScriptCompileFlags CompileFlags = 0;
    }
    
    public class TypescriptProject {
        protected bool Equals(TypescriptProject other) {
            return Equals(CompilationState, other.CompilationState) && ProgressId == other.ProgressId && Equals(FileProblemItems, other.FileProblemItems) && Directory == other.Directory && Equals(TsConfig, other.TsConfig) && Equals(Package, other.Package);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == this.GetType() && Equals((TypescriptProject)obj);
        }

        public override int GetHashCode() {
            return HashCode.Combine(CompilationState, ProgressId, FileProblemItems, Directory, TsConfig, Package);
        }

        internal TypescriptCrashProblemItem CrashProblemItem { get; set; }
        private Dictionary<string, HashSet<TypescriptFileDiagnosticItem>> FileProblemItems { get; set; } = new();
        internal TypescriptProjectCompileState CompilationState = new();

        internal IEnumerable<TypescriptFileDiagnosticItem> GetProblemsForFile(string file) {
            if (file.StartsWith("Assets/")) {
                file = file[7..];
            }
            return ProblemItems.OfType<TypescriptFileDiagnosticItem>().Where(diagnostic => diagnostic.FileLocation == file);
        }

        internal TypescriptProblemType? HighestProblemType {
            get {
                TypescriptProblemType? problemType = null;
                foreach (var problemItem in ProblemItems) {
                    if (problemType == null || problemItem.ProblemType > problemType) {
                        problemType = problemItem.ProblemType;
                    }
                }
                
                return problemType;
            }
        }
        
        /// <summary>
        /// The progress id of this project (if applicable)
        /// </summary>
        internal int ProgressId;
        
        /// <summary>
        /// Problematic items in this Typescript Project
        /// </summary>
        internal IReadOnlyList<ITypescriptProblemItem> ProblemItems {
            get {
                HashSet<ITypescriptProblemItem> problemItems = new();

                if (CrashProblemItem != null) {
                    problemItems.Add(CrashProblemItem);
                }
                
                foreach (var pair in FileProblemItems) {
                    foreach (var item in pair.Value) {
                        problemItems.Add(item);
                    }
                }

                return problemItems.ToList();
            }
        }

        internal int ErrorCount => ProblemItems.Count(item => item.ProblemType == TypescriptProblemType.Error);
        internal bool HasCrashed => CrashProblemItem != null;
            
        internal void AddProblemItem(string file, TypescriptFileDiagnosticItem item) {
            item.Project = this;
            if (FileProblemItems.TryGetValue(file, out var items)) {
                items.Add(item);
            }
            else {
                items = new HashSet<TypescriptFileDiagnosticItem>();
                items.Add(item);
                FileProblemItems.Add(file, items);
            }
        }

        internal void ClearAllProblems() {
            FileProblemItems.Clear();
            CrashProblemItem = null;
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
        /// <param name="inputFilePath">The input path relative to the project</param>
        /// <param name="outputFileType">The output type</param>
        /// <returns>The output path</returns>
        public string GetOutputPath(string inputFilePath, OutputFileType outputFileType = OutputFileType.Lua) {
            foreach (var rootDir in TsConfig.RootDirs) {
                if (!inputFilePath.StartsWith(rootDir)) continue;
                
                var output = inputFilePath.Replace(rootDir, TsConfig.OutDir);
                return TransformOutputPath(output, InputFileType.Typescript, outputFileType);
            }

            return TransformOutputPath(inputFilePath, InputFileType.Typescript, outputFileType);
        }

        public string GetOutputFileHash(string inputFilePath) {
            var outFile = GetOutputPath(inputFilePath);
            if (!File.Exists(outFile)) {
                return null;
            }
            
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(outFile);
            var hash = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            return hash;
        }
        
        /// <summary>
        /// Gets a list of possible input paths for the given output path
        /// </summary>
        /// <param name="outputFilePath">The output path</param>
        /// <returns></returns>
        public IEnumerable<string> GetPossibleInputPaths(string outputFilePath) {
            var paths = new List<string>();
            if (!outputFilePath.StartsWith(TsConfig.OutDir)) return paths;
            
            foreach (var rootDir in TsConfig.RootDirs) {
                var inputFilePath = outputFilePath.Replace(TsConfig.OutDir, rootDir);
                
                if (FileExtensions.EndsWith(inputFilePath, FileExtensions.Lua)) {
                    paths.Add(inputFilePath.Replace(FileExtensions.Lua, FileExtensions.Typescript));
                } else if (FileExtensions.EndsWith(inputFilePath, FileExtensions.TypescriptDeclaration)) {
                    paths.Add(inputFilePath.Replace(FileExtensions.TypescriptDeclaration, FileExtensions.Typescript));
                    paths.Add(inputFilePath);
                } else if (FileExtensions.EndsWith(inputFilePath, FileExtensions.AirshipComponentMeta)) {
                    paths.Add(inputFilePath.Replace(FileExtensions.AirshipComponentMeta, FileExtensions.Typescript));
                }
            }
            
            return paths;
        }
        
        private static string TransformOutputPath(string input, InputFileType inputFileType, OutputFileType outputFileType) {
            return FileExtensions.Transform(input, FileExtensions.GetExtensionForInputType(inputFileType),
                FileExtensions.GetExtensionForOutputType(outputFileType));
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

        /// <summary>
        /// The path of the build info file for this project
        /// </summary>
        public string BuildInfoPath {
            get {
                var userBuildInfoFilePath = TsConfig.compilerOptions.tsBuildInfoFile;
                return userBuildInfoFilePath != null ? PosixPath.Join(TsConfig.Directory, userBuildInfoFilePath) : PosixPath.Join(TsConfig.OutDir, "tsconfig.tsbuildinfo");
            }
        }
        
        public void EnforceDefaultConfigurationSettings() {
            var modified = false;
            
            var compilerOptions = TsConfig.compilerOptions;
            if (compilerOptions.strictPropertyInitialization is null or true) {
                compilerOptions.strictPropertyInitialization = false;
                modified = true;
            }

            if (modified) {
                TsConfig.Modify();
            }
        }
    }
}