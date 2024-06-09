using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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

        public Process CompilerProcess { get; private set; }
        public bool IsActive => CompilerProcess is { HasExited: false };
        public bool IsCompiling => compilationState == CompilationState.IsCompiling;
        public bool HasErrors => compilationState == CompilationState.HasErrors;
        
        internal HashSet<TypescriptProblemItem> problemItems = new();
        
        public int ErrorCount { get; internal set; }
        
        public TypescriptCompilerWatchState(TypescriptProject project) {
            this.directory = project.Directory;
        }

        public IEnumerator Watch(TypescriptCompilerBuildArguments arguments) {
            compilationState = CompilationState.IsCompiling;

            if (TypescriptCompilationService.CompilerVersion == TypescriptCompilerVersion.UseLocalDevelopmentBuild) {
                Debug.LogWarning("You are using the development version of the typescript compiler");
            }
            
            CompilerProcess = TypescriptCompilationService.RunNodeCommand(this.directory, $"{TypescriptCompilationService.TypeScriptLocation} {arguments.GetCommandString(CompilerCommand.BuildWatch)}");
            TypescriptCompilationService.AttachWatchOutputToUnityConsole(this, arguments, CompilerProcess);
            processId = this.CompilerProcess.Id;
            TypescriptCompilationServicesState.instance.Update();
            yield return null;
        }
    }
}