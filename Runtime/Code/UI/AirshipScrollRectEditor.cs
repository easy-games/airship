using UnityEditor;
using UnityEditor.UI;
using UnityEngine.UI;

[CustomEditor(typeof(AirshipScrollRect))]
public class AirshipScrollRectEditor : ScrollRectEditor {
    public override void OnInspectorGUI() {
        AirshipScrollRect scrollRect = (AirshipScrollRect)target;

        scrollRect.redirectScrollWheelInput = (ScrollRect)EditorGUILayout.ObjectField("Scroll Redirect", scrollRect.redirectScrollWheelInput, typeof(ScrollRect), true);
        scrollRect.disableScrollWheel =
            EditorGUILayout.Toggle("Disable Scroll Wheel", scrollRect.disableScrollWheel);

        base.OnInspectorGUI();
    }
}