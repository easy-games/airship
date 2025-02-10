using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    [FilePath("Temp/TypeScriptCompilationServicesState", FilePathAttribute.Location.ProjectFolder)]
    internal class TypescriptCompilationServicesState : ScriptableSingleton<TypescriptCompilationServicesState> {
        [SerializeField] 
        internal List<TypescriptCompilerWatchState> watchStates = new();
            
        public TypescriptNodeTarget nodeTarget = TypescriptNodeTarget.FindInstallOnSystem;
        
        public int CompilerCount => watchStates.Count(compiler => compiler.IsActive); // test

        internal void RegisterWatchCompiler(TypescriptCompilerWatchState watchState) {
            watchStates.Add(watchState);
            Update();
        }

        internal void UnregisterWatchCompiler(TypescriptCompilerWatchState watchState) {
            if (!watchStates.Contains(watchState)) return;
            
            watchStates.Remove(watchState);
            Update();
        }
            
        internal void Update() {
            Save(true);
        }
    }
}