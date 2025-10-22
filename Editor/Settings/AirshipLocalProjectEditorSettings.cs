using System;
using System.Collections.Generic;
using Airship.Editor;
using Luau;
using UnityEditor;
using UnityEngine;

namespace Editor.Settings {
    /// <summary>
    /// The local artifact database for Airship's Editor - stored in <code>Library/AirshipArtifactDB</code>
    /// This contains the state of the scripts and components for the local project
    /// </summary>
    [FilePath("Library/AirshipLocalProjectEditorSettings", FilePathAttribute.Location.ProjectFolder)]
    internal class AirshipLocalProjectEditorSettings : ScriptableSingleton<AirshipLocalProjectEditorSettings> {
        [Serializable]
        public class AirshipTypeEditorState {
            public string typeName;
            public string typePath;
            public bool enabled;

            public bool IsEditorState(AirshipType editorInfo) {
                return typeName == editorInfo.Name && typePath == editorInfo.AssetPath;
            }

            public AirshipTypeEditorState(AirshipType typeInfo) {
                this.typeName = typeInfo.Name;
                this.typePath = typeInfo.AssetPath;
                this.enabled = true;
            }
        }

        [SerializeField]
        public List<AirshipTypeEditorState> states = new();

        public bool GetEditorSettings(AirshipCustomEditors.CustomEditorInfo editor) {
            foreach (var state in states) {
                if (state.IsEditorState(editor.AirshipType)) return state.enabled;
            }

            return true;
        }

        public void SetEditorEnabled(AirshipCustomEditors.CustomEditorInfo editor, bool enabled) {
            foreach (var state in states)
            {
                if (state.IsEditorState(editor.AirshipType)) {
                    state.enabled = enabled;
                    return;
                }
            }
            
            this.states.Add(new AirshipTypeEditorState(editor.AirshipType) { enabled = enabled });
        }

        public void Modify() {
            this.Save(true);
        }
    }
}