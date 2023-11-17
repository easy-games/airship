using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor.Accessories {
    public class AccessoryEditorWindow : EditorWindow {
        // Path to the human entity asset:
        private static readonly Lazy<GameObject> HumanEntityPrefab = new(() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Bundles/@Easy/Core/Shared/Resources/Entity/HumanEntity/HumanEntity.prefab"));
        
        // Path to the accessory prefab editor asset:
        private static readonly string AccessoryPrefabEditorPath = "Packages/gg.easy.airship/Editor/Resources/AccessoryPrefabEditor.prefab";

        private PrefabStage _prefabStage;
        private GameObject _humanEntity;
        private GameObject _editingAccessory;
        private AccessoryTransform _accessoryTransform;
        private ListView _listPane;

        private Label _selectedItemLabel;

        private void OnDisable() {
            if (_prefabStage != null && PrefabStageUtility.GetCurrentPrefabStage() == _prefabStage) {
                StageUtility.GoBackToPreviousStage();
            }
            _prefabStage = null;
            _editingAccessory = null;
            
            if (_humanEntity) {
                DestroyImmediate(_humanEntity);
                _humanEntity = null;
            }
        }

        private void CreateStage() {
            _prefabStage = PrefabStageUtility.OpenPrefab(AccessoryPrefabEditorPath, null, PrefabStage.Mode.InIsolation);

            var existingEntity = _prefabStage.prefabContentsRoot.transform.Find(HumanEntityPrefab.Value.name);
            if (existingEntity != null) {
                DestroyImmediate(existingEntity.gameObject);
            }
        
            _humanEntity = Instantiate(HumanEntityPrefab.Value, _prefabStage.prefabContentsRoot.transform);
            _humanEntity.name = HumanEntityPrefab.Value.name;
        }

        private void CreateGUI() {
            titleContent = new GUIContent("Accessory Editor");
            
            // Find and collect all accessories:
            var allAccessoryGuids = AssetDatabase.FindAssets("t:Accessory");
            var allAccessories = new List<Accessory>();
            foreach (var guid in allAccessoryGuids) {
                allAccessories.Add(AssetDatabase.LoadAssetAtPath<Accessory>(AssetDatabase.GUIDToAssetPath(guid)));
            }

            var split = new TwoPaneSplitView(0, 50, TwoPaneSplitViewOrientation.Vertical);
            rootVisualElement.Add(split);
            
            var editPane = new VisualElement();
            editPane.style.paddingTop = new StyleLength(new Length(5, LengthUnit.Pixel));
            editPane.style.paddingBottom = new StyleLength(new Length(5, LengthUnit.Pixel));
            editPane.style.paddingRight = new StyleLength(new Length(5, LengthUnit.Pixel));
            editPane.style.paddingLeft = new StyleLength(new Length(5, LengthUnit.Pixel));
            split.Add(editPane);

            _selectedItemLabel = new Label();
            _selectedItemLabel.text = "No selected item";
            editPane.Add(_selectedItemLabel);
            
            var buttonPanel = new VisualElement();
            buttonPanel.style.paddingTop = new StyleLength(new Length(5, LengthUnit.Pixel));
            buttonPanel.style.flexDirection = FlexDirection.Row;
            editPane.Add(buttonPanel);
            
            // Save button:
            var saveBtn = new Button();
            saveBtn.text = "Save";
            buttonPanel.Add(saveBtn);
            saveBtn.clickable.clicked += () => {
                if (_editingAccessory == null) return;
                var accTransform = _accessoryTransform;
                if (accTransform == null) return;
                accTransform.SetFromTransform(_editingAccessory.transform);
                accTransform.Save();
            };

            // Reset button:
            var resetBtn = new Button();
            resetBtn.text = "Reset";
            buttonPanel.Add(resetBtn);
            resetBtn.clickable.clicked += () => {
                if (_editingAccessory == null) return;
                var accTransform = _accessoryTransform;
                if (accTransform == null) return;
                Undo.RecordObject(_editingAccessory.transform, "ResetTransform");
                _editingAccessory.transform.SetLocalPositionAndRotation(accTransform.Position, Quaternion.Euler(accTransform.Rotation));
                _editingAccessory.transform.localScale = accTransform.Scale;
            };
            
            _listPane = new ListView();
            split.Add(_listPane);

            // Set up the left list view to show accessories:
            _listPane.selectionType = SelectionType.Single;
            _listPane.makeItem = () => {
                var label = new Label();
                label.style.paddingTop = new StyleLength(new Length(5, LengthUnit.Pixel));
                label.style.paddingBottom = new StyleLength(new Length(5, LengthUnit.Pixel));
                label.style.paddingRight = new StyleLength(new Length(5, LengthUnit.Pixel));
                label.style.paddingLeft = new StyleLength(new Length(5, LengthUnit.Pixel));
                return label;
            };
            _listPane.bindItem = (item, index) => {
                var accessory = allAccessories[index];
                ((Label) item).text = accessory.name;
            };
            _listPane.itemsSource = allAccessories;
            _listPane.onSelectionChange += OnAccessorySelectionChanged;
            _listPane.Sort((a, b) => string.Compare(((Label)a).text, ((Label)b).text, StringComparison.OrdinalIgnoreCase));
        }

        private void OnAccessorySelectionChanged(IEnumerable<object> selectedItems) {
            var selectionList = selectedItems.Cast<Accessory>().ToList();
            if (selectionList.Count == 0) {
                if (_editingAccessory) {
                    DestroyImmediate(_editingAccessory);
                    _editingAccessory = null;
                    _selectedItemLabel.text = "No selected item";
                }
            } else {
                var selection = selectionList[0];
                _selectedItemLabel.text = selection.DisplayName;
                BuildScene(selection);
            }
        }

        private void BuildScene(Accessory accessory, AccessoryTransform accessoryTransform = null) {
            if (_prefabStage == null) {
                CreateStage();
            }

            if (_editingAccessory) {
                DestroyImmediate(_editingAccessory);
                _editingAccessory = null;
            }
            
            var parent = _prefabStage.prefabContentsRoot.transform;
            var itemKey = AccessoryBuilder.GetBoneItemKey(accessory.AccessorySlot);
            var objectRefs = _humanEntity.GetComponent<GameObjectReferences>();

            if (accessoryTransform == null) {
                accessoryTransform = new AccessoryTransform(accessory);
            }
            _accessoryTransform = accessoryTransform;
            
            if (!string.IsNullOrEmpty(itemKey)) {
                parent = objectRefs.GetValueTyped<Transform>(AccessoryBuilder.boneKey, itemKey);
            }

            if (parent == null) {
                Debug.LogWarning($"could not find bone for accessory {accessory.DisplayName}");
                return;
            }
            
            var go = Instantiate(accessory.Prefab, parent);
            go.transform.localPosition = accessoryTransform.Position;
            go.transform.localScale = accessoryTransform.Scale;
            go.transform.localRotation = Quaternion.Euler(accessoryTransform.Rotation);
            _editingAccessory = go;

            Selection.activeObject = go;
        }

        public void SetSelected(Accessory accessory) {
            var index = _listPane.itemsSource.IndexOf(accessory);
            if (index == -1) return;
            
            _listPane.SetSelection(index);
        }

        public static void OpenOrCreateWindow() {
            var windowOpen = HasOpenInstances<AccessoryEditorWindow>();
            if (windowOpen) {
                Debug.Log("Open existing");
                FocusWindowIfItsOpen<AccessoryEditorWindow>();
            } else {
                Debug.Log("Open new");
                CreateWindow<AccessoryEditorWindow>();
            }
        }

        public static void OpenWithAccessory(Accessory accessory) {
            OpenOrCreateWindow();
            var window = GetWindow<AccessoryEditorWindow>();
            window.SetSelected(accessory);
        }

        // Automatically create an Accessory Editor window when an accessory is opened:
        [OnOpenAsset(0)]
        public static bool OpenAccessoryWindow(int instanceId, int line) {
            var target = EditorUtility.InstanceIDToObject(instanceId);
        
            if (target is Accessory) {
                OpenOrCreateWindow();
            } else if (target is AccessoryPrefabEditor) {
                OpenOrCreateWindow();
            }

            return false;
        }
        
        // Give the accessory over to the Accessory Editor after the window is opened or created:
        [OnOpenAsset(1)]
        public static bool LoadAccessoryWindow(int instanceId, int line) {
            var target = EditorUtility.InstanceIDToObject(instanceId);

            if (target is Accessory accessory) {
                var window = GetWindow<AccessoryEditorWindow>();
                window.SetSelected(accessory);
                
                return true;
            }

            return false;
        }
    }
}
