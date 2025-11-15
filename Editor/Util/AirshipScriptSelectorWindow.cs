using System;
using System.Collections.Generic;
using System.Linq;
using Luau;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

internal class AirshipScriptSelectionContext {
    public AirshipScript currentScript { get; set; }
    public Object objectBeingEdited { get; }
    public bool allowNoneSelection { get; }
    public AirshipScriptType targetScriptType { get; }

    public AirshipScriptSelectionContext(AirshipScriptType scriptType, Object editingObject, AirshipScript selected, bool allowNone = false) {
        currentScript = selected;
        targetScriptType = scriptType;
        objectBeingEdited = editingObject;
        allowNoneSelection = allowNone;
    }
}

internal class AirshipScriptSelectorWindow : EditorWindow {
    internal class AirshipScriptInfo  {
        public int instanceId;
        public AirshipScript script;
        public GlobalObjectId globalObjectId;
    }
    
    static IEnumerable<AirshipScriptInfo> FetchAssetsOfType(AirshipScriptType scriptType) {
        var scriptGuids = AssetDatabase.FindAssets($"t:{typeof(AirshipScript)}");
        foreach (var guid in scriptGuids) {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<AirshipScript>(path);
            if (asset.scriptType == scriptType) {
                yield return new AirshipScriptInfo() {
                    instanceId = asset.GetInstanceID(),
                    script = asset,
                    globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(asset),
                };
            }
        }
    }
    
    private static AirshipScriptSelectionContext _context;
    private static Action<AirshipScript> _selectionChangedEvent;
    private static Action<AirshipScript> _selectionClosedEvent;
    private static List<AirshipScriptInfo> _allItems;
    private static List<AirshipScriptInfo> _filteredItems;
    
    internal static AirshipScriptSelectorWindow instance { get; private set; }
    
    private ToolbarSearchField _searchField;
    private ListView _listView;
    private TabView _tabView;
    private TwoPaneSplitView _splitView;
    private Label assetDetailsIcon;
    private Label assetDetailsText;
    private Label assetDetailsPath;
    
    internal AirshipScriptInfo SelectedComponentItem;
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
        _filteredItems.Clear();
        _filteredItems.AddRange(
            _allItems.Where(item => string.IsNullOrEmpty(searchText) || 
                                    (item.script == null || item.script.name.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >= 0)));
        _listView.Rebuild();
    }
    
    void Init() {
        _searchText = "";
        _allItems = new List<AirshipScriptInfo>();
        _filteredItems = new List<AirshipScriptInfo>();
        
        if (_context.allowNoneSelection) {
            _allItems.Add(new AirshipScriptInfo());
        }
        _allItems.AddRange(FetchAssetsOfType(_context.targetScriptType));
        _filteredItems.AddRange(_allItems);
    }
    
       private void OnEnable() {
        Init();

        _searchField = new ToolbarSearchField();
        _searchField.AddToClassList("component-search-field");
        _searchField.RegisterValueChangedCallback(SearchFilterChanged);
        _searchField.style.flexGrow = 1;
        _searchField.style.maxHeight = 16;
        _searchField.style.width = Length.Auto();
         _searchField.style.marginRight = 4;
        rootVisualElement.Add(_searchField);

        _splitView = new TwoPaneSplitView(1, 100, TwoPaneSplitViewOrientation.Vertical);
        {
            _tabView = new TabView();
            
            {
                var sceneTab = new Tab("Assets");
                {
                    sceneTab.AddToClassList("tab-view");
                    sceneTab.style.paddingTop = 5;
                    sceneTab.style.flexGrow = 1;
                    _listView = new ListView(_filteredItems, 16, MakeItem, BindItem);
                    _listView.selectionChanged += ItemSelectionChanged;
                    _listView.itemsChosen += ItemsChosen;
                    _listView.style.flexGrow = 1;
                    sceneTab.Add(_listView);
                }
                _tabView.Add(sceneTab);
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
        
        if (_context.currentScript != null) {
            var currentSelectedId = _context.currentScript.GetInstanceID();
            var selectedIndex = _filteredItems.FindIndex(item => item.instanceId == currentSelectedId);
            if (selectedIndex >= 0)
                _listView.selectedIndex = selectedIndex;
        } else if (_context.allowNoneSelection) {
            _listView.selectedIndex = 0;
        }
        
        

        rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/gg.easy.airship/Editor/StyleSheets/AirshipSelectorWindow.uss"));
        if (EditorGUIUtility.isProSkin) {
            rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/gg.easy.airship/Editor/StyleSheets/AirshipSelectorWindow.dark.uss"));
        }
        
        FinishInit();
    }
       
    private void FinishInit() {

    }
   
    private void SearchFilterChanged(ChangeEvent<string> evt) {
        searchText = evt.newValue; 
    }

   private void UpdateSelection(AirshipScriptInfo info) {
       if (info.script == null) {
           assetDetailsIcon.style.backgroundImage = null;
           assetDetailsPath.text = "None";
           assetDetailsText.text = "(No Script)";
       } else {

           assetDetailsIcon.style.backgroundImage = AirshipComponentDropdown.AssetIcon;
           assetDetailsPath.text = info.script.assetPath;
           assetDetailsText.text = info.script.m_metadata != null ? ObjectNames.NicifyVariableName(info.script.m_metadata.name) : "Script"; 
       }
   }
       
    private void ItemSelectionChanged(IEnumerable<object> obj) {
        SelectedComponentItem = obj.FirstOrDefault() as AirshipScriptInfo;
        UpdateSelection(SelectedComponentItem);
        _selectionChangedEvent?.Invoke(SelectedComponentItem?.script);
    }

    private void ItemsChosen(IEnumerable<object> obj) {
        SelectedComponentItem = obj.FirstOrDefault() as AirshipScriptInfo;
        cancelled = false;
        _selectionClosedEvent?.Invoke(SelectedComponentItem?.script);
        Close();
    }

    private void BindItem(VisualElement listItem, int index) {
        if (index < 0 || index >= _filteredItems.Count)
            return;
        

        var target = _filteredItems[index];

        var label = listItem.Q<Label>("AssetText");
        if (target.script != null) {
            label.text = target.script.name;
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
            
            //label.style.backgroundImage = new StyleBackground(EditorGUIUtility.IconContent("d_GameObject Icon").image as Texture2D);
            label.style.backgroundImage = AirshipComponentDropdown.AssetIcon;
            label.style.height = new StyleLength(16);
            label.style.width = new StyleLength(16);
            element.Add(label);

            var label2 = new Label();
            label2.name = "AssetText";
            element.Add(label2);
        }
        return element;
    }
    
    public static void Show(AirshipScriptSelectionContext selectionContext, Action<AirshipScript> onSelectionChanged,
        Action<AirshipScript> onSelectorClosed) {
        _context = selectionContext;
        _selectionChangedEvent = onSelectionChanged;
        _selectionClosedEvent = onSelectorClosed;

        var window = CreateInstance<AirshipScriptSelectorWindow>();
        window.titleContent = new GUIContent(selectionContext.targetScriptType switch {
            AirshipScriptType.ScriptableObject => "Select Airship Scriptable Object Class",
            AirshipScriptType.Behaviour => "Select Airship Component Class",
            AirshipScriptType.Script => "Select Airship Script",
            _ => throw new ArgumentOutOfRangeException()
        });
        instance = window;
        
        window.ShowAuxWindow();
    }
}