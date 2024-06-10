using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    [FilePath("Temp/TypeScriptCompilationServicesState", FilePathAttribute.Location.ProjectFolder)]
    internal class TypescriptCompilationServicesState : ScriptableSingleton<TypescriptCompilationServicesState> {
        [SerializeField] 
        internal List<TypescriptCompilerWatchState> watchStates = new();
            
        public int CompilerCount => watchStates.Count(compiler => compiler.IsActive); // test

        internal void RegisterWatchCompiler(TypescriptCompilerWatchState watchState) {
            Debug.Log($"Register watch compiler at {watchState.processId}");
            watchStates.Add(watchState);
            Update();
        }

        internal void UnregisterWatchCompiler(TypescriptCompilerWatchState watchState) {
            Debug.Log($"Unregister watch compiler at {watchState.processId}");
            watchStates.Remove(watchState);
            Update();
        }
            
        internal void Update() {
            Save(true);
        }
    }
}