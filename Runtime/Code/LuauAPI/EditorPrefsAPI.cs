#if UNITY_EDITOR
using System;
using UnityEditor;

[LuauAPI]
public class EditorPrefsAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(EditorPrefs);
    }
}
#endif