using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

internal class AirshipSelectorContext<T> where  T : Object {
    public T currentObject { get; set; }
    public Object objectBeingEdited { get; }
    public bool allowNoneSelection { get; }
    
    public AirshipSelectorContext(Object editingObject, T selected, bool allowNone = true) {
        currentObject = selected;
        objectBeingEdited = editingObject;
        allowNoneSelection = allowNone;
    }
}



internal abstract class AirshipSelectorWindow<T> : EditorWindow where  T : Object {
    internal class ItemInfo {
        public int instanceId;
        public T item;
        public bool asset;
        public GlobalObjectId globalObjectId;
    }

    private ToolbarSearchField _searchField;
    private ListView _assetListView;
    private ListView _sceneListView;
    private TabView _tabView;
    private TwoPaneSplitView _splitView;
    private Label assetDetailsIcon;
    private Label assetDetailsText;
    private Label assetDetailsPath;
    
    
    protected static AirshipSelectorContext<T> _context;
    protected static Action<T> _selectionChangedEvent;
    protected static Action<T> _selectionClosedEvent;

    private static List<ItemInfo> _sceneItems;
    private static List<ItemInfo> _filteredSceneItems;
    
    private static List<ItemInfo> _assetItems;
    private static List<ItemInfo> _filteredAssetItems;
    
    internal ItemInfo SelectedComponentItem;
    internal bool cancelled;

    private string _searchText;
    public string searchText {
        get => _searchText;
        set {
            _searchText = value;
            FilterItems();
        }
    }

    private void FilterItems() {
        _filteredAssetItems.Clear();
        _filteredAssetItems.AddRange(_assetItems.Where(info => info == null || info.item.name.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >= 0));
        
        _filteredSceneItems.Clear();
        _filteredSceneItems.AddRange(_sceneItems.Where(info => info == null || info.item.name.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >= 0));
        
        _assetListView.Rebuild();
        _sceneListView.Rebuild();
    }

    private ItemInfo ToItemInfo(T obj, bool asset) {
        return new ItemInfo() {
            instanceId = obj.GetInstanceID(),
            item = obj,
            asset = asset,
            globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(obj),
        };
    }

    
    void Init() {
        _searchText = "";

        _sceneItems = new List<ItemInfo>();
        _assetItems = new List<ItemInfo>();
        _filteredSceneItems = new List<ItemInfo>();
        _filteredAssetItems = new List<ItemInfo>();

        if (_context.allowNoneSelection) {
            if (allowSceneObjects) _sceneItems.Add(new ItemInfo());
            if (allowAssets) _assetItems.Add(new ItemInfo());
        }

        var sceneObjects = FetchSceneObjects();
        if (sceneObjects != null) {
            foreach (var sceneObject in sceneObjects) {
                _sceneItems.Add(ToItemInfo(sceneObject, false));
            }
        }

        var assetObjects = FetchAssetObjects();
        if (assetObjects != null) {
            foreach (var assetObject in assetObjects) {
                _assetItems.Add(ToItemInfo(assetObject, true));
            }
        }
        
        _filteredSceneItems.AddRange(_sceneItems);
        _filteredAssetItems.AddRange(_assetItems);
    }

    private void SearchFilterChanged(ChangeEvent<string> evt) {
        searchText = evt.newValue;
    }
    
    private void OnEnable() {
        Init();

        if (showSearchBox) {
            _searchField = new ToolbarSearchField();
            _searchField.AddToClassList("component-search-field");
            _searchField.RegisterValueChangedCallback(SearchFilterChanged);
            _searchField.style.flexGrow = 1;
            _searchField.style.maxHeight = 16;
            _searchField.style.width = Length.Auto();
            _searchField.style.marginRight = 4;
            rootVisualElement.Add(_searchField);
        }
        
          _splitView = new TwoPaneSplitView(1, 100, TwoPaneSplitViewOrientation.Vertical);
        {
            _tabView = new TabView();
            
            if (allowSceneObjects)
            {
                var scenesTab = new Tab("Scene");
                {
                    scenesTab.AddToClassList("tab-view");
                    scenesTab.style.paddingTop = 5;
                    scenesTab.style.flexGrow = 1;
                    _sceneListView = new ListView(_filteredSceneItems, 16, MakeItem, BindAssetItem);
                    _sceneListView.selectionChanged += ItemSelectionChanged;
                    _sceneListView.itemsChosen += ItemsChosen;
                    _sceneListView.style.flexGrow = 1;
                    scenesTab.Add(_sceneListView);
                }
                _tabView.Add(scenesTab);
            }
            
            if (allowAssets)
            {
                var assetsTab = new Tab("Assets");
                {
                    assetsTab.AddToClassList("tab-view");
                    assetsTab.style.paddingTop = 5;
                    assetsTab.style.flexGrow = 1;
                    _assetListView = new ListView(_filteredAssetItems, 16, MakeItem, BindAssetItem);
                    _assetListView.selectionChanged += ItemSelectionChanged;
                    _assetListView.itemsChosen += ItemsChosen;
                    _assetListView.style.flexGrow = 1;
                    assetsTab.Add(_assetListView);
                }
                _tabView.Add(assetsTab);
            }
    
            _splitView.Add(_tabView);
            
            var details = new VisualElement();
            {
                details.AddToClassList("details-pane");
                assetDetailsIcon = new Label();
                assetDetailsIcon.AddToClassList("asset-details-icon");

                assetDetailsText = new Label();
                assetDetailsText.AddToClassList("asset-details-text");

                assetDetailsPath = new Label();
                assetDetailsPath.AddToClassList("asset-details-path");

                var assetInnerDetails = new VisualElement();
                assetInnerDetails.AddToClassList("asset-details-inner");
                assetInnerDetails.Add(assetDetailsText);
                assetInnerDetails.Add(assetDetailsPath);
                
                details.Add(assetDetailsIcon);
                details.Add(assetInnerDetails);
            }
            _splitView.Add(details);
            // _splitView.CollapseChild(1);
        }
        rootVisualElement.Add(_splitView);
        
        if (_context.currentObject != null) {
            var currentSelectedId = _context.currentObject.GetInstanceID();
            var selectedAssetIndex = _filteredAssetItems.FindIndex(item => item.instanceId == currentSelectedId);
            if (selectedAssetIndex >= 0)
                _assetListView.selectedIndex = selectedAssetIndex;
            
            var selectedSceneIndex = _filteredSceneItems.FindIndex(item => item.instanceId == currentSelectedId);
            if (selectedSceneIndex >= 0)
                _sceneListView.selectedIndex = selectedAssetIndex;
            
        } else if (_context.allowNoneSelection) {
            if (_assetListView != null) _assetListView.selectedIndex = 0;
            if (_sceneListView != null) _sceneListView.selectedIndex = 0;
        }
        
        rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/gg.easy.airship/Editor/StyleSheets/AirshipSelectorWindow.uss"));
        if (EditorGUIUtility.isProSkin) {
            rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/gg.easy.airship/Editor/StyleSheets/AirshipSelectorWindow.dark.uss"));
        }
    }

    protected virtual string GetItemPath(ItemInfo info) {
        return info.item.name;
    }

    protected virtual string GetItemDetails(ItemInfo info) {
        return "";
    }
    
    private void UpdateSelection(ItemInfo info) {
        if (info == null || info.item == null) {
            assetDetailsIcon.style.backgroundImage = null;
            assetDetailsPath.text = "None";
            assetDetailsText.text = "-";
        } else {
            assetDetailsIcon.style.backgroundImage = GetItemIcon(info);
            assetDetailsPath.text = GetItemPath(info);
            assetDetailsText.text = GetItemDetails(info); 
        }
    }
    
    private void ItemSelectionChanged(IEnumerable<object> obj) {
        SelectedComponentItem = obj.FirstOrDefault() as ItemInfo;
        UpdateSelection(SelectedComponentItem);
        _selectionChangedEvent?.Invoke(SelectedComponentItem?.item);
    }

    private void ItemsChosen(IEnumerable<object> obj) {
        SelectedComponentItem = obj.FirstOrDefault() as ItemInfo;
        cancelled = false;
        _selectionClosedEvent?.Invoke(SelectedComponentItem?.item);
        Close();
    }
    
    private void BindAssetItem(VisualElement listItem, int index) {
        if (index < 0 || index >= _filteredAssetItems.Count)
            return;
        

        var target = _filteredAssetItems[index];

        var label = listItem.Q<Label>("AssetText");
        if (target.item != null) {
            label.text = GetItemLabel(target);
        
            var iconLabel = listItem.Q<Label>("Icon");
            var iconTexture = GetItemIcon(target);
            if (iconTexture != null) {
                iconLabel.style.backgroundImage = iconTexture;
            }
        } else {
            label.text = "None";
            var icon = listItem.Q<Label>("Icon");
            icon.style.backgroundImage = null;
        }
    }
    
    private void BindSceneItem(VisualElement listItem, int index) {
        if (index < 0 || index >= _filteredSceneItems.Count)
            return;
        

        var target = _filteredSceneItems[index];

        var label = listItem.Q<Label>("AssetText");
        if (target.item != null) {
            label.text = GetItemLabel(target);
        
            var iconLabel = listItem.Q<Label>("Icon");
            var iconTexture = GetItemIcon(target);
            if (iconTexture != null) {
                iconLabel.style.backgroundImage = iconTexture;
            }
        } else {
            label.text = "None";
            var icon = listItem.Q<Label>("Icon");
            icon.style.backgroundImage = null;
        }
    }

    private VisualElement MakeItem() {
        var element =  new VisualElement();
        {
            element.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
            element.style.paddingLeft = new StyleLength(20);
            
            var label = new Label();
            label.name = "Icon";
            
            label.style.backgroundImage = new StyleBackground(EditorGUIUtility.IconContent("d_GameObject Icon").image as Texture2D);
            label.style.height = new StyleLength(16);
            label.style.width = new StyleLength(16);
            element.Add(label);

            var label2 = new Label();
            label2.name = "AssetText";
            element.Add(label2);
        }
        return element;
    }

    protected virtual bool showSearchBox => true;
    protected virtual bool allowSceneObjects => false;
    protected virtual bool allowAssets => true;
    
    protected abstract IEnumerable<T> FetchSceneObjects();
    protected abstract IEnumerable<T> FetchAssetObjects();

    protected virtual string GetItemLabel(ItemInfo itemInfo) {
        return ObjectNames.NicifyVariableName(itemInfo.item.name);
    }

    protected virtual Texture2D GetItemIcon(ItemInfo itemInfo) {
        return null;
    }
}