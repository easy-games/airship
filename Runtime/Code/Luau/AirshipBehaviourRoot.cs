﻿using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Luau {
    [DisallowMultipleComponent]
    public class AirshipBehaviourRoot : MonoBehaviour {
        private static int _idGen = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() {
            _idGen = 0;
        }

        public int Id { get; } = _idGen++;
    }

    // Matches same enum order in AirshipComponent.h plugin file
    public enum AirshipComponentUpdateType {
        AirshipUpdate,
        AirshipEnabled,
        AirshipDisabled,
        AirshipLateUpdate,
        AirshipFixedUpdate,
        AirshipStart,
        AirshipAwake,
        AirshipDestroy,
        AirshipCollisionEnter,
        AirshipCollisionStay,
        AirshipCollisionExit,
        AirshipCollisionEnter2D,
        AirshipCollisionStay2D,
        AirshipCollisionExit2D,
        AirshipTriggerEnter,
        AirshipTriggerStay,
        AirshipTriggerExit,
        AirshipTriggerEnter2D,
        AirshipTriggerStay2D,
        AirshipTriggerExit2D,
    }
    
#if UNITY_EDITOR && AIRSHIP_INTERNAL
    [CustomEditor(typeof(AirshipBehaviourRoot))]
    public class AirshipBehaviourRootDebugEditor : Editor {
        public override void OnInspectorGUI() {
            EditorGUILayout.HelpBox("This is internal debugging information for AirshipComponents", MessageType.Info);
            var behaviourRoot = (AirshipBehaviourRoot) target;
            GUI.enabled = false;
            EditorGUILayout.TextField("Instance Id", behaviourRoot.Id.ToString());
            EditorGUILayout.Foldout(true, "Components");
            EditorGUI.indentLevel += 1;
            foreach (var binding in behaviourRoot.gameObject.GetComponents<AirshipComponent>()) {
                if (binding.IsAirshipComponent) {
                    EditorGUILayout.Foldout(true, binding.m_metadata.name);
                    EditorGUILayout.TextField("Id", binding.GetAirshipComponentId().ToString());
                    EditorGUILayout.Toggle("Awoken", binding.didAwake);
                    EditorGUILayout.Toggle("Started", binding.didStart);
                }
                
                
                if (binding.Dependencies.Count > 0 ){
                    EditorGUILayout.Foldout(true, "Dependencies");
                    EditorGUI.indentLevel++;
                    
                    foreach (var dependency in binding.Dependencies) {
                        EditorGUILayout.ObjectField(dependency.name, dependency, typeof(AirshipComponent));
                    }
                    EditorGUI.indentLevel--;
                }
            }


            EditorGUI.indentLevel -= 1;
            GUI.enabled = true;
        }
    }
#endif
}
