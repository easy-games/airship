using System.Collections.Generic;
using Luau;
using UnityEditor;
using UnityEngine;

namespace Editor {
    public static class BinaryFileDragDrop {
        [InitializeOnLoadMethod]
        private static void OnLoad() {
            DragAndDrop.AddDropHandler(OnInspectorDrop);
            DragAndDrop.AddDropHandler(OnHierarchyDrop);
            DragAndDrop.AddDropHandler(OnSceneDrop);
        }

        private static bool IsDraggingBinaryFile() {
            foreach (var obj in DragAndDrop.objectReferences) {
                if (obj is AirshipScript) {
                    return true;
                }
            }
            return false;
        }

        private static Dictionary<string, AirshipScript> GetDraggedBinaryFiles() {
            var binaryFiles = new Dictionary<string, AirshipScript>();
            for (var i = 0; i < DragAndDrop.objectReferences.Length; i++) {
                var obj = DragAndDrop.objectReferences[i];
                if (obj is AirshipScript binaryFile) {
                    var path = DragAndDrop.paths[i];
                    binaryFiles[path] = binaryFile;
                }
            }

            return binaryFiles;
        }

        private static DragAndDropVisualMode OnSceneDrop(object dropUpon, Vector3 worldPos, Vector2 viewportPos, Transform parent, bool perform) {
            if (!perform) {
                return IsDraggingBinaryFile() && dropUpon != null ? DragAndDropVisualMode.Move : DragAndDropVisualMode.None;
            }

            GameObject go = null;
            if (dropUpon is GameObject gameObj) {
                go = gameObj;
            }
            
            if (go == null) {
                return DragAndDropVisualMode.None;
            }
            
            var binaryFiles = GetDraggedBinaryFiles();
            if (binaryFiles.Count == 0) {
                return DragAndDropVisualMode.None;
            }
            
            AddAllScriptBindings(go, binaryFiles);
            
            return DragAndDropVisualMode.Move;
        }

        private static DragAndDropVisualMode OnHierarchyDrop(int instanceId, HierarchyDropFlags dropMode, Transform parent, bool perform) {
            if (!perform) {
                return IsDraggingBinaryFile() && dropMode == HierarchyDropFlags.DropUpon ? DragAndDropVisualMode.Move : DragAndDropVisualMode.None;
            }
            
            if (dropMode != HierarchyDropFlags.DropUpon) {
                return DragAndDropVisualMode.None;
            }

            // Grab the game object:
            GameObject go = null;
            if (instanceId != 0) {
                var target = EditorUtility.InstanceIDToObject(instanceId);
                if (target is GameObject gameObj) {
                    go = gameObj;
                }
            }
            else if (parent != null) {
                // If instanceId is 0, then we're dragging into a prefab and need to use the 'parent' transform:
                go = parent.gameObject;
            }

            if (go == null) {
                return DragAndDropVisualMode.None;
            }
            
            var binaryFiles = GetDraggedBinaryFiles();
            if (binaryFiles.Count == 0) {
                return DragAndDropVisualMode.None;
            }
            
            AddAllScriptBindings(go, binaryFiles);

            return DragAndDropVisualMode.Move;
        }

        private static DragAndDropVisualMode OnInspectorDrop(object[] targets, bool perform) {
            // If hovering, just quickly check if any of the grabbed items are a BinaryFile:
            if (!perform) {
                return IsDraggingBinaryFile() ? DragAndDropVisualMode.Move : DragAndDropVisualMode.None;
            }

            // Collect all dragged binary files:
            var binaryFiles = GetDraggedBinaryFiles();

            if (binaryFiles.Count == 0) {
                return DragAndDropVisualMode.None;
            }
            
            // Add the script bindings into the given targets:
            foreach (var target in targets) {
                if (target is GameObject go) {
                    AddAllScriptBindings(go, binaryFiles);
                }
            }

            return DragAndDropVisualMode.Move;
        }

        private static void AddAllScriptBindings(GameObject target, Dictionary<string, AirshipScript> binaryFiles) {
            foreach (var pair in binaryFiles) {
                AddScriptBinding(target, pair.Value, pair.Key);
            }
        }

        private static void AddScriptBinding(GameObject target, AirshipScript airshipScript, string path) {
            if (HasScriptBinding(target, path)) return;
            
            var scriptBinding = target.AddComponent<AirshipComponent>();
            scriptBinding.script = airshipScript;
            EditorUtility.SetDirty(scriptBinding);
        }

        private static bool HasScriptBinding(GameObject target, string path) {
            var scriptBindings = target.GetComponents<AirshipComponent>();
            foreach (var binding in scriptBindings) {
                if (binding.script != null && binding.script.m_path == path) {
                    return true;
                }
            }
            return false;
        }
    }
}
