using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Easy.Airship.Editor.Publish.Callback {
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
            var instances = new List<IBuildAirshipGameBundle>();

            var derivedTypes = TypeCache.GetTypesDerivedFrom<IBuildAirshipGameBundle>();
            foreach (var type in derivedTypes) {
                var instance = (IBuildAirshipGameBundle) Activator.CreateInstance(type);
                instances.Add(instance);   
            }
            return instances.OrderBy(i => i.callbackOrder).ToList();
        }
    }
}