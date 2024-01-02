using UnityEditor;
#if UNITY_EDITOR
using UnityEditor.EditorTools;
#endif
using UnityEngine;

// Tool Settings Window
public class MaterialColorToolWindow : EditorWindow {
    private const float cursorMod = 1000;
    [SerializeField] private MaterialColorToolData data;
    
    private const string PlayerPrefPrefix = "MaterialColor_";
    private const string MainColorToggleKey = "MainColor_Toggle";
    private const string MainColorKey = "MainColor";
    private const string EmissiveColorToggleKey = "EmissiveColor_Toggle";
    private const string EmissiveColorKey = "EmissiveColor";
    private const string MaterialIndexToggleKey = "MaterialIndex_Toggle";
    private const string MaterialIndexKey = "MaterialIndex";
    private const string CursorSizeKey = "CursorSize";
    private const string AutoAddToggleKey = "AutoAdd_Toggle";
    
    [MenuItem("Airship/Misc/Material Color Window")]
    public static MaterialColorToolWindow OpenWindow() {
        var window = GetWindow<MaterialColorToolWindow>();
        return window;
    }

    private void OnEnable() {
        MaterialColorTool.cursorSize = GetFloat(CursorSizeKey, .1f);
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        // Check if EditorTool is currently active and disable it when window is closed
        // MagicTool requires this window to be open as long as it's active
        if (ToolManager.activeToolType == typeof(MaterialColorTool))
        {
            // Try to activate previously used tool
            ToolManager.RestorePreviousPersistentTool();
        }
#endif
    }

    private void OnGUI() {
        if (MaterialColorTool.brushSettings == null || data == null) {
            return;
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("MATERIAL TOOL SETTINGS");
        if (GUI.Button(new Rect(175, 0, 50, 50), new GUIContent(data.toolIcon))) {
            ToolManager.SetActiveTool(typeof(MaterialColorTool));
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Brush");
        
        //Material Color Toggle
        bool useMainColor = GetBool(MainColorToggleKey, true);
        useMainColor = EditorGUILayout.Toggle("Use Main Color", useMainColor);
        if (useMainColor != MaterialColorTool.useMainColor) {
            MaterialColorTool.useMainColor = useMainColor;
            SetBool(MainColorToggleKey, useMainColor);
        }
        
        //Material Color
        Color currentColor = GetColor(MainColorKey);
        currentColor = EditorGUILayout.ColorField("Main Color", currentColor);
        if (!MaterialColorTool.brushSettings.materialColor.Equals(currentColor)) {
            MaterialColorTool.brushSettings.materialColor = currentColor;
            SetColor(MainColorKey, currentColor);
        }

        //Emissive Toggle
        bool useEmissive = GetBool(EmissiveColorToggleKey, false);
        useEmissive = EditorGUILayout.Toggle("Use Emissive", useEmissive);
        if (useEmissive != MaterialColorTool.useEmissive) {
            MaterialColorTool.useEmissive = useEmissive;
            SetBool(EmissiveColorToggleKey, useEmissive);
        }
        
        //Emissive Color
        Color currentEmissive = GetColor(EmissiveColorKey);
        currentEmissive = EditorGUILayout.ColorField("Main Color", currentEmissive);
        if (!MaterialColorTool.brushSettings.emissiveColor.Equals(currentEmissive)) {
            MaterialColorTool.brushSettings.emissiveColor = currentEmissive;
            SetColor(EmissiveColorKey, currentEmissive);
        }

        //Material Toggle
        bool useMaterial = GetBool(MaterialIndexToggleKey, false);
        useMaterial = EditorGUILayout.Toggle("Use Material", useMaterial);
        if (useMaterial != MaterialColorTool.useMaterial) {
            MaterialColorTool.useMaterial = useMaterial;
            SetBool(MaterialIndexToggleKey, useMaterial);
        }
        
        //Material Color
        for (int i = 0; i < data.standardMaterials.Length; i++) {
            
        }
        
        //GUILayout.SelectionGrid()
        int currentMaterialIndex = (int)GetFloat(MaterialIndexKey);
        currentMaterialIndex = EditorGUILayout.IntField("MaterialIndex", currentMaterialIndex);
        if (!MaterialColorTool.currentMaterialIndex.Equals(currentMaterialIndex)) {
            MaterialColorTool.currentMaterialIndex = currentMaterialIndex;
            SetFloat(MaterialIndexKey, currentMaterialIndex);
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Editor");

        //Material Toggle
        bool autoAdd = GetBool(AutoAddToggleKey, false);
        autoAdd = EditorGUILayout.Toggle("Auto Add Material Color Script", autoAdd);
        if (autoAdd != MaterialColorTool.autoAddMaterialColor) {
            MaterialColorTool.autoAddMaterialColor = autoAdd;
            SetBool(AutoAddToggleKey, autoAdd);
        }
        
        //Cursor Size
        var newCursorSize = EditorGUILayout.Slider("Cursor Size",MaterialColorTool.cursorSize * cursorMod, 1, 10)/cursorMod;
        if (!newCursorSize.Equals(MaterialColorTool.cursorSize)) {
            MaterialColorTool.cursorSize = newCursorSize;
            SetFloat(CursorSizeKey, newCursorSize);
        }
    }

    private void SetColor(string key, Color value) {
        PlayerPrefs.SetFloat(PlayerPrefPrefix + key + "_R", value.r);
        PlayerPrefs.SetFloat(PlayerPrefPrefix + key + "_G", value.g);
        PlayerPrefs.SetFloat(PlayerPrefPrefix + key + "_B", value.b);
        PlayerPrefs.SetFloat(PlayerPrefPrefix + key + "_A", value.a);
    }

    private Color GetColor(string key) {
       return new Color(
            PlayerPrefs.GetFloat(PlayerPrefPrefix + key + "_R", 0),
            PlayerPrefs.GetFloat(PlayerPrefPrefix + key + "_G", 0),
            PlayerPrefs.GetFloat(PlayerPrefPrefix + key + "_B", 0),
            PlayerPrefs.GetFloat(PlayerPrefPrefix + key + "_A", 1));
    }
    
    private void SetFloat(string key, float value) {
        PlayerPrefs.SetFloat(PlayerPrefPrefix + key, value);
    }

    private float GetFloat(string key, float defaultValue = 0) {
        return PlayerPrefs.GetFloat(PlayerPrefPrefix + key, defaultValue);
    }
    
    private void SetBool(string key, bool value) {
        PlayerPrefs.SetInt(PlayerPrefPrefix + key, value?1:0);
    }

    private bool GetBool(string key, bool defaultValue) {
        return PlayerPrefs.GetInt(PlayerPrefPrefix + key, defaultValue ? 1:0) == 1;
    }

}