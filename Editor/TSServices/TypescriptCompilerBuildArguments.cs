using System.Collections.Generic;

namespace Airship.Editor {
    public enum CompilerBuildMode {
        BuildOnly,
        Watch,
    }
    
    internal struct TypescriptCompilerBuildArguments {
        /// <summary>
        /// The location of package.json (aka --package)
        /// </summary>
        public string Package { get; set; }

        /// <summary>
        /// The location of tsconfig.json (aka -p or --project)
        /// </summary>
        public string Project { get; set; }

        public bool Json { get; set; }

        public bool Verbose { get; set; }

        /// <summary>
        /// Will output the arguments as a string
        /// </summary>
        /// <returns></returns>
        public string ToArgumentString(CompilerBuildMode compilerBuildMode) {
            var args = new List<string>();

            if (compilerBuildMode == CompilerBuildMode.Watch) {
                args.Add("build --watch");
            } else if (compilerBuildMode == CompilerBuildMode.BuildOnly) {
                args.Add("build");
            }
            
            if (Project != null) {
                args.Add($"--project {Project}");
            }
            
            if (Package != null) {
                args.Add($"--package {Package}");
            }

            if (Json) {
                args.Add("--json");
            } else if (Verbose) {
                args.Add("--verbose");
            }
            
            return string.Join(" ", args);
        }
    }
}