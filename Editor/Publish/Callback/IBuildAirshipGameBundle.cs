using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Editor.Publish.Callback {
    public interface IBuildAirshipGameBundle : IOrderedCallback {
        /// <summary>
        /// Fired before building asset bundles for game
        /// </summary>
        void OnPreBuildGameBundle(BuildTarget buildTarget);
    }
    
    [InitializeOnLoad]
    public static class BuildAirshipGameBundleProcessor {
        private static List<IBuildAirshipGameBundle> callbacks;
        
        static BuildAirshipGameBundleProcessor() {
            callbacks = GetCallbackInstances();
        }
        
        public static void InvokePreBuildGameBundle(BuildTarget target) {
            foreach (var callback in callbacks) {
                try {
                    callback.OnPreBuildGameBundle(target);
                } catch (Exception e) {
                    Debug.LogError(e);
                }
            }
        }
        
        /// <summary>
        /// Create instances of each implementation of IBuildAirshipGameBundle
        /// </summary>
        private static List<IBuildAirshipGameBundle> GetCallbackInstances() {
            var callbackType = typeof(IBuildAirshipGameBundle);
            var instances = new List<IBuildAirshipGameBundle>();
            
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) {
                var types = assembly.GetTypes()
                    .Where(t => callbackType.IsAssignableFrom(t) && 
                                t.IsClass && 
                                !t.IsAbstract);
                
                foreach (var type in types) {
                    var instance = (IBuildAirshipGameBundle) Activator.CreateInstance(type);
                    instances.Add(instance);
                }
            }
            
            return instances.OrderBy(i => i.callbackOrder).ToList();
        }
    }
}