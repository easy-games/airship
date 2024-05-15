using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

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

        public Process CompilerProcess { get; private set; }
        public bool IsActive => CompilerProcess is { HasExited: false };
        public bool IsCompiling => compilationState == CompilationState.IsCompiling;
        public bool HasErrors => compilationState == CompilationState.HasErrors;
        
        internal HashSet<TypescriptProblemItem> problemItems = new();
        
        public int ErrorCount { get; internal set; }
        
        public TypescriptCompilerWatchState(TypescriptProject project) {
            this.directory = project.Directory;
        }

        public void RequestCompileFiles(params string[] files) {
            if (CompilerProcess != null) {
                CompilerProcess.StandardInput.WriteLine("ping");
            }
        }

        public bool Watch(TypescriptCompilerBuildArguments arguments) {
            return ThreadPool.QueueUserWorkItem(delegate {
                compilationState = CompilationState.IsCompiling;
                CompilerProcess = TypescriptCompilationService.RunNodeCommand(this.directory, $"{EditorIntegrationsConfig.TypeScriptLocation} {arguments.ToArgumentString(CompilerBuildMode.Watch)}");
                TypescriptCompilationService.AttachWatchOutputToUnityConsole(this, arguments, CompilerProcess);
                processId = this.CompilerProcess.Id;
                TypescriptCompilationServicesState.instance.Update();
            });
        }
    }
}