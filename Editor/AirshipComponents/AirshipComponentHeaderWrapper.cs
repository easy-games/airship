using System;
using System.ComponentModel;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Component = UnityEngine.Component;

internal static class AirshipComponentHeader {
    private static Color darkBg = new Color(62f / 255f, 62f / 255f, 62f / 255f);
    private static Color darkBgHover = new Color(71f / 255f, 71f / 255f, 71f / 255f);

    private static Color lightBg = new Color(203f / 255f, 203f / 255f, 203f / 255f);
    private static Color lightBgHover = new Color(214f / 255f, 214f / 255f, 214f / 255f);
    
    private static readonly GUIContent label = new GUIContent("");
    
    internal static float AfterComponentHeader(AirshipComponent component, Rect headerRect, bool isHeaderSelected) {
        var scriptFile = component.script;
        var metadata = scriptFile.m_metadata;

        if (metadata == null) return 0f;
        
        var tooltipRect = new Rect(headerRect);
        tooltipRect.x += 60f;
        tooltipRect.y += 2f;
        tooltipRect.height -= 4f;
        tooltipRect.width -= 120f;

        var isMouseOver = headerRect.Contains(Event.current.mousePosition);
        
        if (metadata.displayIcon != null) {
            var iconRect = new Rect(headerRect);
            iconRect.x += 20f;
            iconRect.y += 2f;
            iconRect.width = 18;
            iconRect.height = 18;
            
            EditorGUI.DrawRect(iconRect, isMouseOver ? darkBgHover : darkBg);
            GUI.Label(iconRect, new GUIContent("", metadata.displayIcon));
        }

        label.text = metadata.displayName;

        
        if (EditorGUIUtility.isProSkin) {
            EditorGUI.DrawRect(tooltipRect, isMouseOver ? darkBgHover : darkBg);
        }
        else {
            EditorGUI.DrawRect(tooltipRect, isMouseOver ? lightBgHover : lightBg);
        }
        
        // test
        
      
        GUI.Label(tooltipRect, label, EditorStyles.boldLabel);
        return 0f;
    }
}

internal class AirshipComponentHeaderWrapper {
    private readonly IMGUIContainer headerElement;
    private readonly AirshipComponent component;
    private readonly Action unityOnGUIHandler;

    public AirshipComponentHeaderWrapper(IMGUIContainer headerElement, AirshipComponent binding) {
        this.headerElement = headerElement;
        this.component = binding;
        unityOnGUIHandler = headerElement.onGUIHandler;
    }

    public void DrawWrappedHeaderGUI() {
        if (component == null || component.script == null || !component.script.airshipBehaviour) {
            RemoveOverrideHeader();
            return;
        }
        
        Rect headerRect = headerElement.contentRect;
        bool headerIsSelected = headerElement.focusController.focusedElement == headerElement;
        
        unityOnGUIHandler.Invoke();

        if (component.metadata.name != "") {
            AirshipComponentHeader.AfterComponentHeader(component, headerRect, headerIsSelected);
        }
    }
    
    private void RemoveOverrideHeader() {
        if (headerElement is null) {
            return;
        }

        headerElement.onGUIHandler = unityOnGUIHandler;
    }
}
