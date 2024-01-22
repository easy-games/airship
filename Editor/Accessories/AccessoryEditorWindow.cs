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
                "Assets/Bundles/@Easy/Core/Shared/Resources/Character/Character.prefab"));
        
        // Path to the accessory prefab editor asset:
        private static readonly string AccessoryPrefabEditorPath = "Packages/gg.easy.airship/Editor/Resources/AccessoryPrefabEditor.prefab";

        private PrefabStage _prefabStage;
        private GameObject _humanEntity;
        private AccessoryComponent _editingAccessoryComponent;
        private AccessoryComponent _referenceAccessoryComponent;
        private ListView _listPane;

        private Label _selectedItemLabel;

        private void OnDisable() {
            if (_prefabStage != null && PrefabStageUtility.GetCurrentPrefabStage() == _prefabStage) {
                StageUtility.GoBackToPreviousStage();
            }
            _prefabStage = null;
            _editingAccessoryComponent = null;
            _referenceAccessoryComponent = null;
            
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
            var allAccessoryGuids = AssetDatabase.FindAssets("t:Prefab");
            var allAccessories = new List<AccessoryComponent>();
            foreach (var guid in allAccessoryGuids) {
                var accessory = AssetDatabase.LoadAssetAtPath<AccessoryComponent>(AssetDatabase.GUIDToAssetPath(guid));
                if (accessory) {
                    allAccessories.Add(accessory);
                }
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
                if (_editingAccessoryComponent == null || _referenceAccessoryComponent == null) return;
                SaveCurrentAccessory();
            };

            // Reset button:
            var resetBtn = new Button();
            resetBtn.text = "Reset";
            buttonPanel.Add(resetBtn);
            resetBtn.clickable.clicked += () => {
                if (_editingAccessoryComponent == null || _referenceAccessoryComponent == null) return;
                ResetCurrentAccessory();
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
                ((Label) item).text = accessory.gameObject.name;
            };
            _listPane.itemsSource = allAccessories;
            _listPane.onSelectionChange += OnAccessorySelectionChanged;
            _listPane.Sort((a, b) => string.Compare(((Label)a).text, ((Label)b).text, StringComparison.OrdinalIgnoreCase));
        }

        private void OnAccessorySelectionChanged(IEnumerable<object> selectedItems) {
            var selectionList = selectedItems.Cast<AccessoryComponent>().ToList();
            if (selectionList.Count == 0) {
                ClearCurrentAccessory();
            } else {
                var selection = selectionList[0];
                _selectedItemLabel.text = selection.ToString();
                BuildScene(selection);
            }
        }

        private void ClearCurrentAccessory() {
            if (_editingAccessoryComponent) {
                DestroyImmediate(_editingAccessoryComponent.gameObject);
                _editingAccessoryComponent = null;
            }
            _selectedItemLabel.text = "No selected item";
        }

        private void BuildScene(AccessoryComponent accessoryComponent) {
            if (_prefabStage == null || _humanEntity == null) {
                CreateStage();
            }

            ClearCurrentAccessory();
            
            var parent = _prefabStage.prefabContentsRoot.transform;
            var itemKey = AccessoryBuilder.GetBoneItemKey(accessoryComponent.accessorySlot);
            var objectRefs = _humanEntity.GetComponent<GameObjectReferences>();
            
            if (!string.IsNullOrEmpty(itemKey)) {
                parent = objectRefs.GetValueTyped<Transform>(AccessoryBuilder.boneKey, itemKey);
            }

            if (parent == null) {
                Debug.LogWarning($"could not find bone for accessory {accessoryComponent}");
                return;
            }
            
            var go = (GameObject)PrefabUtility.InstantiatePrefab(accessoryComponent.gameObject, parent);
            _editingAccessoryComponent = go.GetComponent<AccessoryComponent>();
            _referenceAccessoryComponent = accessoryComponent;
            Selection.activeObject = go;
        }

        public void SetSelected(AccessoryComponent accessoryComponent) {
            var index = _listPane.itemsSource.IndexOf(accessoryComponent);
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

        public static void OpenWithAccessory(AccessoryComponent accessoryComponent) {
            OpenOrCreateWindow();
            var window = GetWindow<AccessoryEditorWindow>();
            window.SetSelected(accessoryComponent);
        }

        // Automatically create an Accessory Editor window when an accessory is opened:
        [OnOpenAsset(0)]
        public static bool OpenAccessoryWindow(int instanceId, int line) {
            var target = EditorUtility.InstanceIDToObject(instanceId);
        
            if (target is AccessoryComponent) {
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

            if (target is AccessoryComponent accessory) {
                var window = GetWindow<AccessoryEditorWindow>();
                window.SetSelected(accessory);
                
                return true;
            }
            
            return false;
        }

        private void SaveCurrentAccessory() {
            Undo.RecordObject(_referenceAccessoryComponent, "Save Accessory");
            _referenceAccessoryComponent.Copy(_editingAccessoryComponent);
            PrefabUtility.RecordPrefabInstancePropertyModifications(_referenceAccessoryComponent);
            EditorUtility.SetDirty(_referenceAccessoryComponent);
            AssetDatabase.SaveAssets();
        }

        private void ResetCurrentAccessory() {
            Undo.RecordObject(_editingAccessoryComponent.transform, "ResetTransform");
            _editingAccessoryComponent.transform.SetLocalPositionAndRotation(_referenceAccessoryComponent.localPosition, _referenceAccessoryComponent.localRotation);
            _editingAccessoryComponent.localScale = _referenceAccessoryComponent.localScale;
        }
    }
}
