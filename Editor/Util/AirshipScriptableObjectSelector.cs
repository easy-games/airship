using System;
using System.Collections.Generic;
using Luau;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal class AirshipScriptableObjectSelector : AirshipSelectorWindow<AirshipScriptableObject> {
    internal class SelectorContext : AirshipSelectorContext<AirshipScriptableObject> {
        public AirshipType scriptableObjectType { get; }

        public SelectorContext(Object editingObject, AirshipType type, AirshipScriptableObject selected, bool allowNone = true) : base(editingObject, selected, allowNone) {
            scriptableObjectType = type;
        }
    }
    
    internal static AirshipScriptableObjectSelector instance { get; private set; }
    
    public static void Show(SelectorContext selectorContext, Action<AirshipScriptableObject> onSelectionChanged, Action<AirshipScriptableObject> onSelectionClosed) {
        _context = selectorContext;
        _selectionChangedEvent = onSelectionChanged;
        _selectionClosedEvent = onSelectionClosed;
        
        var window = CreateInstance<AirshipScriptableObjectSelector>();
        window.titleContent = new GUIContent($"Select {ObjectNames.NicifyVariableName(selectorContext.scriptableObjectType.Name)}");
        instance = window;
        
        window.ShowAuxWindow();
    }

    protected override IEnumerable<AirshipScriptableObject> FetchAssetObjects() {
        List<AirshipScriptableObject> objects = new();
        
        var guids = AssetDatabase.FindAssets($"t:{nameof(AirshipScriptableObject)}");
        foreach (var guid in guids) {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<AirshipScriptableObject>(path);
            if (asset.script == ((SelectorContext)_context).scriptableObjectType.Script) {
                objects.Add(asset);
            }
        }
        
        return objects.ToArray();
    }

    protected override IEnumerable<AirshipScriptableObject> FetchSceneObjects() {
        return null;
    }

    protected override Texture2D GetItemIcon(ItemInfo itemInfo) {
        return EditorGUIUtility.GetIconForObject(itemInfo.item);
    }

    protected override string GetItemDetails(ItemInfo info) {
        if (info.item.script == null) return "(No Script)";
        return ObjectNames.NicifyVariableName(info.item.script.m_metadata!.name);
    }

    protected override string GetItemPath(ItemInfo info) {
        return AssetDatabase.GetAssetPath(info.item) ?? "(Ephemeral)";
    }

    protected override string GetItemLabel(ItemInfo itemInfo) {
        return base.GetItemLabel(itemInfo);
    }

    protected override bool allowSceneObjects => false;
    protected override bool allowAssets => true;
}