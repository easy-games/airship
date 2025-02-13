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
        foreach((UnityEditor.Editor editor, IMGUIContainer header) editorAndHeader in EditorInspectors.GetComponentHeaderElementsFromEditorWindowOf(gameObjectEditor)) {
            var onGUIHandler = editorAndHeader.header.onGUIHandler;
            
            var inspectorModeField = editorAndHeader.editor.GetType().GetProperty("inspectorMode", BindingFlags.NonPublic | BindingFlags.Instance);
            var inspectorMode = (InspectorMode) (inspectorModeField?.GetValue(editorAndHeader.editor) ??
                                                InspectorMode.Normal);
            if (inspectorMode == InspectorMode.Debug) return;
   
            var component = editorAndHeader.editor.target as Component;
            if (component is not AirshipComponent binding || binding.script == null || !binding.script.airshipBehaviour) continue;
            
            if(onGUIHandler.Method is { Name: "DrawWrappedHeaderGUI" }) continue;
                
            var componentHeaderWrapper = new AirshipComponentHeaderWrapper(editorAndHeader.header, binding);
            editorAndHeader.header.onGUIHandler = componentHeaderWrapper.DrawWrappedHeaderGUI;
        }
    }

    private static void AfterInspectorRootEditorHeaderGUI(UnityEditor.Editor editor) {
        if (editor.target is GameObject) {
            AfterGameObjectHeaderGUI(editor);
        }
    }
}
