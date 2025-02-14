#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Luau;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public static class AdvancedDropdownExtensions {
    public static void Show(this AdvancedDropdown dropdown, Rect buttonRect, float maxHeight) {
        dropdown.Show(buttonRect);
        SetMaxHeightForOpenedPopup(buttonRect, maxHeight);
    }

    private static void SetMaxHeightForOpenedPopup(Rect buttonRect, float maxHeight) {
        var window = EditorWindow.focusedWindow;

        if (window == null) {
            Debug.LogWarning("EditorWindow.focusedWindow was null.");
            return;
        }

        if (!string.Equals(window.GetType().Namespace, typeof(AdvancedDropdown).Namespace)) {
            Debug.LogWarning("EditorWindow.focusedWindow " + EditorWindow.focusedWindow.GetType().FullName +
                             " was not in expected namespace.");
            return;
        }

        var position = window.position;

        position.height = maxHeight;
        position.width = buttonRect.width;
        window.minSize = position.size;
        window.maxSize = position.size;
        window.position = position;
        window.ShowAsDropDown(GUIUtility.GUIToScreenRect(buttonRect), position.size);
    }
}

public class AirshipComponentDropdown : AdvancedDropdown {
    private const string IconAsset = "Packages/gg.easy.airship/Editor/AirshipScriptIcon.png";
    private static Texture2D _assetIcon;
    
    public static Texture2D AssetIcon {
        get
        {
            if (_assetIcon == null) {
                _assetIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAsset);
            }

            return _assetIcon;
        }
    }
    
    public class AirshipScriptItem : AdvancedDropdownItem {
        public AirshipScript file;
        
        public AirshipScriptItem(AirshipScript file, string name) : base(name) {
            this.file = file;
        }
    }

    private readonly Action<AirshipScript> binaryFileSelected;
    
    public AirshipComponentDropdown(AdvancedDropdownState state, Action<AirshipScript> binaryFileSelected) : base(state) {
        this.binaryFileSelected = binaryFileSelected;
    }

    protected override void ItemSelected(AdvancedDropdownItem item) {
        if (item is AirshipScriptItem binaryFileItem) {
            binaryFileSelected.Invoke(binaryFileItem.file);
        }
    }

    internal interface ITreeNode {
        public string DisplayName { get; }
        AdvancedDropdownItem Build();
    }

    private class ScriptNode : ITreeNode {
        public AirshipScript Script { get; }
        public string DisplayName { get; }

        public ScriptNode(AirshipScript script, string name) {
            Script = script;
            DisplayName = name;
        }

        public AdvancedDropdownItem Build() {
            var child = new AirshipScriptItem(Script, DisplayName);
            
            if (Script.m_metadata?.displayIcon != null) {
                child.icon = Script.m_metadata.displayIcon;
            }
            else {
                child.icon = AssetIcon;
            }
            
            return child;
        }
    }

    internal class FolderNode : ITreeNode {
        public string DisplayName { get; }

        private readonly List<ScriptNode> _files = new();
        private readonly List<FolderNode> _folders = new();

        private IEnumerable<ITreeNode> Nodes {
            get {
                List<ITreeNode> nodes = new();
                nodes.AddRange(_folders);
                nodes.AddRange(_files);
                return nodes;
            }
        }

        public FolderNode(string name) {
            DisplayName = name;
        }

        private void AddScript(AirshipScript script, string name) {
            _files.Add(new ScriptNode(script, name));
        }
        
        private FolderNode AddFolder(string name) {
            foreach (var node in _folders) {
                if (node.DisplayName == name) return node;
            }

            var newNode = new FolderNode(name);
            this._folders.Add(newNode);
            return newNode;
        }
        
        public void AddScriptPath(AirshipScript script, string[] folders, string componentName) {
            if (folders.Length == 0) {
                AddScript(script, componentName);
                return;
            }

            var rootFolder = folders[0];
            if (folders.Length > 0) {
                var relativeItem = AddFolder(rootFolder);
                relativeItem = folders[1..].Aggregate(relativeItem, (current, nextItem) => current.AddFolder(nextItem));
                relativeItem.AddScript(script, componentName);
            }
            else {
                var folder = AddFolder(rootFolder);
                folder.AddScript(script, componentName);
            }
        }

        public AdvancedDropdownItem Build() {
            var item = new AdvancedDropdownItem(DisplayName);
            foreach (var child in Nodes) {
                item.AddChild(child.Build());
            }
            return item;
        }
    }

    protected override AdvancedDropdownItem BuildRoot() {
        List<AirshipScript> binaryFiles = new();
        var guids = AssetDatabase.FindAssets("t:AirshipScript");
        foreach (var guid in guids) {
            var airshipScript = AssetDatabase.LoadAssetAtPath<AirshipScript>(AssetDatabase.GUIDToAssetPath(guid));
            if (airshipScript.airshipBehaviour) {
                binaryFiles.Add(airshipScript);
            }
        }

        var rootNode = new FolderNode("Airship Components");
        foreach (var binaryFile in binaryFiles) {
            if (binaryFile.m_metadata == null) continue;
            
            var airshipComponentMenu = binaryFile.m_metadata.GetDecorators().Find(f => f.name == "AirshipComponentMenu");
            if (airshipComponentMenu != null && airshipComponentMenu.parameters[0].TryGetString(out var customPath)) {
                if (customPath == "") continue; // ignore empty names :)
                
                var pathComponents = customPath.Split("/");
                var path = pathComponents.Last();

                rootNode.AddScriptPath(binaryFile, pathComponents[..^1], path);
            }
            else {
                var isPackage = binaryFile.m_path.StartsWith("Assets/AirshipPackages/@");
                if (isPackage) {
                    var packagePath = string.Join(" ", binaryFile.m_path["Assets/AirshipPackages/@".Length..].Split("/")[0..2]);
                    
                    rootNode.AddScriptPath(binaryFile, new []{ packagePath }, binaryFile.m_metadata.displayName);
                }
                else {
                    rootNode.AddScriptPath(binaryFile, new []{ "Scripts" }, binaryFile.m_metadata.displayName);
                }
            }
        }
        
        return rootNode.Build();
    }
}
#endif