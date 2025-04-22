using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Airship.Editor {
    internal enum CompilationState {
        Inactive,
        IsStandby,
        IsCompiling,
        HasErrors,
    }
    
    [Serializable]
    internal class TypescriptCompilerWatchState {
        [SerializeField]
        public int processId;
        [SerializeField]
        public string directory;
        internal CompilationState compilationState = CompilationState.Inactive;

        public Process CompilerProcess {
            get {
                if (processId == 0) return null;
                try {
                    return Process.GetProcessById(processId);
                }
                catch {
                    return null;
                }
            }
        }

        public bool IsActive => CompilerProcess is { HasExited: false };
        public bool IsCompiling => compilationState == CompilationState.IsCompiling;
        public bool HasErrors => compilationState == CompilationState.HasErrors;
        
        internal HashSet<TypescriptFileDiagnosticItem> problemItems = new();
        
        public int ErrorCount { get; internal set; }
        
        public TypescriptCompilerWatchState(TypescriptProject project) {
            this.directory = project.Directory;
        }
        
        public IEnumerator Watch(TypescriptCompilerBuildArguments arguments, NodeJsArguments nodeJsArguments = default) {
            compilationState = CompilationState.IsCompiling;

            if (TypescriptCompilationService.CompilerVersion == TypescriptCompilerVersion.UseLocalDevelopmentBuild) {
                Debug.LogWarning("You are using the development version of the typescript compiler");
            }

            var argList = new List<string>();
            var nodeJsArgs = nodeJsArguments.GetCommandString();
            if (!string.IsNullOrEmpty(nodeJsArgs)) {
                argList.Add(nodeJsArgs);
            }
            
            argList.Add(TypescriptCompilationService.TypescriptLocationCommandLine);
            argList.Add(arguments.GetCommandString(CompilerCommand.BuildWatch));
            
            var compilerProcess = TypescriptCompilationService.RunNodeCommand(directory, string.Join(" ", argList));
            TypescriptCompilationService.AttachWatchOutputToUnityConsole(this, arguments, compilerProcess);
            processId = compilerProcess.Id;
            
            TypescriptCompilationServicesState.instance.RegisterWatchCompiler(this);
            yield return null;
        }

        public void Stop() {
            try {
                var process = CompilerProcess ?? Process.GetProcessById(processId);
                process.Kill();
            }
            catch {
                Debug.LogWarning($"Failed to kill process {processId}");
            }
            TypescriptCompilationServicesState.instance.UnregisterWatchCompiler(this);
        }
    }
}