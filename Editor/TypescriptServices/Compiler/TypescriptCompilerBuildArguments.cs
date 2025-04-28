using System.Collections.Generic;
using UnityEngine;

namespace Airship.Editor {
    public enum CompilerCommand {
        BuildOnly,
        BuildWatch,
    }

    internal struct TypescriptCompilerBuildArguments {
        /// <summary>
        /// The location of package.json (aka --package)
        /// </summary>
        public string Package { get; set; }

        /// <summary>
        /// If true, tell the compiler we're compiling for a publish
        /// </summary>
        public bool Publishing { get; set; }

        /// <summary>
        /// The location of tsconfig.json (aka -p or --project)
        /// </summary>
        public string Project { get; set; }

        /// <summary>
        /// Use JSON event messaging
        /// </summary>
        public bool Json { get; set; }

        /// <summary>
        /// Flag to enable incremental mode
        /// </summary>
        public bool Incremental { get; set; }

        /// <summary>
        /// Use verbose messages
        /// </summary>
        public bool Verbose { get; set; }

        /// <summary>
        /// Only write changed files
        /// </summary>
        public bool WriteOnlyChanged { get; set; }

        /// <summary>
        /// Will output the arguments as a string
        /// </summary>
        /// <returns></returns>
        public string GetCommandString(CompilerCommand compilerCommand) {
            var args = new List<string>();

            if (compilerCommand == CompilerCommand.BuildWatch) {
                args.Add("build --watch");
            } else if (compilerCommand == CompilerCommand.BuildOnly) {
                args.Add("build");
            }

            if (WriteOnlyChanged) {
                args.Add("--writeOnlyChanged");
            }
            
            if (Project != null) {
                args.Add($"--project {Project}");
            }

            if (Incremental) {
                args.Add("--incremental");
            }
            
            if (Package != null) {
                args.Add($"--package {Package}");
            }

            if (Json) {
                args.Add("--json");
            }

            if (Publishing) {
                args.Add("--publish");
            }
            
            if (Verbose) {
                args.Add("--verbose");
            }

            return string.Join(" ", args);
        }
    }
}