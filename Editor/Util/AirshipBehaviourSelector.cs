
using System;
using Luau;
using UnityEditor;
using UnityEngine;

public class AirshipBehaviourSelector : EditorWindow {
    private static AirshipBehaviourSelector _airshipBehaviourSelector;
    public static AirshipBehaviourSelector get {
        get {
            if (_airshipBehaviourSelector == null) {
                var objects = Resources.FindObjectsOfTypeAll<AirshipBehaviourSelector>();
                if (objects != null && objects.Length != 0) {
                    _airshipBehaviourSelector = objects[0];         
                } else {
                    _airshipBehaviourSelector = ScriptableObject.CreateInstance<AirshipBehaviourSelector>();
                }
            }

            return _airshipBehaviourSelector;
        }
    }

    private void OnGUI() {
        // if (UnityEngine.Event.current.type == UnityEngine.EventType.KeyDown && UnityEngine.Event.current.keyCode == KeyCode.Escape)
        // {
        //     this.Cancel();
        // }

        GUILayout.BeginScrollView(new Vector2());
        
        GUILayout.EndScrollView();
    }

    internal void Show(UnityEngine.Object obj, string requiredTypePath) {
        base.Show();
    }
}
