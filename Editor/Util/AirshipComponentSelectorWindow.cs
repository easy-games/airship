using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Luau;
using UnityEditor;
using UnityEditor.SearchService;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Action = Unity.Plastic.Newtonsoft.Json.Serialization.Action;
using Object = UnityEngine.Object;

internal class AirshipComponentSelectionContext {
    public AirshipType componentType { get; }
    public AirshipComponent currentObject { get; set; }
    public bool allowNoneSelection { get; }

    public AirshipComponentSelectionContext(AirshipType type, AirshipComponent selected, bool allowNone = true) {
        currentObject = selected;
        componentType = type;
        allowNoneSelection = allowNone;
    }
}

internal class AirshipComponentSelectorWindow : EditorWindow {
    internal class ComponentItemInfo  {
        public int instanceId;
        public GameObject gameObject;
        public AirshipComponent component;
        public GlobalObjectId globalObjectId;
    }

    static IEnumerable<ComponentItemInfo> FetchInSceneByType([CanBeNull] AirshipType filterType) {
        var components =
            Object.FindObjectsByType<AirshipComponent>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);

        foreach (var component in components) {
            if (filterType != null && !component.GetAirshipType().IsAssignableFrom(filterType)) continue;
            
            yield return new ComponentItemInfo() {
                instanceId =  component.GetInstanceID(),
                component = component,
                gameObject = component.gameObject,
                globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(component),
            };
        }
    }
    
    private static AirshipComponentSelectionContext _context;
    private static Action<AirshipComponent> _selectionChangedEvent;
    private static Action<AirshipComponent> _selectionClosedEvent;
    private static List<ComponentItemInfo> _allItems;
    private static List<ComponentItemInfo> _filteredItems;
    
    internal static AirshipComponentSelectorWindow instance { get; private set; }

    private ToolbarSearchField _searchField;
    private ListView _listView;

    internal ComponentItemInfo SelectedComponentItem;
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
                                    (item.gameObject == null || item.gameObject.name.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >= 0)));
        _listView.Rebuild();
    }

    private void OnEnable() {
        Init();

        _searchField = new ToolbarSearchField();
        _searchField.RegisterValueChangedCallback(SearchFilterChanged);
        _searchField.style.flexGrow = 1;
        _searchField.style.maxHeight = 16;
        _searchField.style.width = Length.Percent(100f);
        _searchField.style.marginRight = 4;
        rootVisualElement.Add(_searchField);

        _listView = new ListView(_filteredItems, 16, MakeItem, BindItem);
        _listView.selectionChanged += ItemSelectionChanged;
        _listView.itemsChosen += ItemsChosen;
        _listView.style.flexGrow = 1;
        rootVisualElement.Add(_listView);

        if (_context.currentObject != null) {
            var currentSelectedId = _context.currentObject.GetInstanceID();
            var selectedIndex = _filteredItems.FindIndex(item => item.instanceId == currentSelectedId);
            if (selectedIndex >= 0)
                _listView.selectedIndex = selectedIndex;
        } else if (_context.allowNoneSelection) {
            _listView.selectedIndex = 0;
        }

        FinishInit();
    }

    private void FinishInit() {
        
    }

    private void SearchFilterChanged(ChangeEvent<string> evt) {
        searchText = evt.newValue;
    }

    private void ItemSelectionChanged(IEnumerable<object> obj) {
        SelectedComponentItem = obj.FirstOrDefault() as ComponentItemInfo;
        _selectionChangedEvent?.Invoke(SelectedComponentItem?.component);
    }

    private void ItemsChosen(IEnumerable<object> obj) {
        SelectedComponentItem = obj.FirstOrDefault() as ComponentItemInfo;
        cancelled = false;
        _selectionClosedEvent?.Invoke(SelectedComponentItem?.component);
        Close();
    }

    private void BindItem(VisualElement listItem, int index) {
        if (index < 0 || index >= _filteredItems.Count)
            return;
        

        var target = _filteredItems[index];

        var label = listItem.Q<Label>("AssetText");
        if (target.component != null) {
            label.text = target.gameObject.name + " [" + target.component.GetAirshipType().NicifyName() + "]";
        
            var icon = listItem.Q<Label>("Icon");
            if (PrefabUtility.IsPartOfPrefabInstance(target.gameObject)) {
                icon.style.backgroundImage =
                    new StyleBackground(EditorGUIUtility.IconContent("d_Prefab Icon").image as Texture2D);
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

    public static void Show(AirshipComponentSelectionContext selectionContext, Action<AirshipComponent> onSelectionChanged,
        Action<AirshipComponent> onSelectorClosed) {
        _context = selectionContext;
        _selectionChangedEvent = onSelectionChanged;
        _selectionClosedEvent = onSelectorClosed;

        var window = CreateInstance<AirshipComponentSelectorWindow>();
        window.titleContent = new GUIContent($"Select {ObjectNames.NicifyVariableName(selectionContext.componentType.Name)}");
        instance = window;
        window.ShowAuxWindow();
    }
    
    void Init() {
        _searchText = "";
        _allItems = new List<ComponentItemInfo>();
        _filteredItems = new List<ComponentItemInfo>();
        
        if (_context.allowNoneSelection) {
            _allItems.Add(new ComponentItemInfo());
        }
        _allItems.AddRange(FetchInSceneByType(_context.componentType));
        _filteredItems.AddRange(_allItems);
    }
}