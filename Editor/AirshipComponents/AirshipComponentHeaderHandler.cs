using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;


[InitializeOnLoad]
public class AirshipComponentHeaderHandler {
    static AirshipComponentHeaderHandler() {
        UnityEditor.Editor.finishedDefaultHeaderGUI -= AfterInspectorRootEditorHeaderGUI;
        UnityEditor.Editor.finishedDefaultHeaderGUI += AfterInspectorRootEditorHeaderGUI;
    }

    private static void AfterGameObjectHeaderGUI(UnityEditor.Editor gameObjectEditor) {
        foreach((UnityEditor.Editor editor, IMGUIContainer header) editorAndHeader in EditorInspectors.GetComponentHeaderElementsFromEditorWindowOf(gameObjectEditor))
        {
            var onGUIHandler = editorAndHeader.header.onGUIHandler;


            var component = editorAndHeader.editor.target as Component;
            if (component is AirshipComponent binding && binding.scriptFile != null && binding.scriptFile.airshipBehaviour) {
                if(onGUIHandler.Method is MethodInfo onGUI && onGUI.Name == "DrawWrappedHeaderGUI")
                {
                    continue;
                }
                
                var componentHeaderWrapper = new AirshipComponentHeaderWrapper(editorAndHeader.header, binding);
                editorAndHeader.header.onGUIHandler = componentHeaderWrapper.DrawWrappedHeaderGUI;
            }
        }
    }

    private static void AfterInspectorRootEditorHeaderGUI(UnityEditor.Editor editor) {
        if (editor.target is GameObject) {
            AfterGameObjectHeaderGUI(editor);
        }
    }
}
