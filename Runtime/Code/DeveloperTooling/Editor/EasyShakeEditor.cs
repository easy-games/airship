#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EasyShake))]
public class EasyShakeEditor : Editor {
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();

        var easyShake = (EasyShake)target;
        if (GUILayout.Button("Shake")) {
            easyShake.Shake(easyShake.shakeDuration);
        }
    }
}
#endif