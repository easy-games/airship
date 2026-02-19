using System;
using System.Collections.Generic;
using System.Linq;
using Code.Player.Accessories;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Easy.Airship.Editor.Accessories {

    public class AccessoryEditorWindow : EditorWindow {
        private enum BackdropType{
            NONE = 0,
            WHITE_FLAT,
            LIGHT_3D,
            DARK_3D,
        }
        private enum PoseType{
            TPOSE,
            APOSE,
            RUNNING
        }
        // Path to the human entity asset:
        private static readonly string CharacterDummyPrefabPath = "Assets/AirshipPackages/@Easy/Core/Prefabs/Character/CharacterDummy.prefab";
        private static GameObject CharacterPrefab;

        // Path to the accessory prefab editor asset:
        private static readonly string AccessoryPrefabEditorPath = "Packages/gg.easy.airship/Editor/Resources/AccessoryPrefabEditor.prefab";

        private PrefabStage _prefabStage;
        private AccessoryPrefabEditor prefabEditor;
        private GameObject characterGO;
        private readonly List<AccessoryComponent> allAccessories = new List<AccessoryComponent>();
        private readonly List<AccessoryComponent> filteredAccessories = new List<AccessoryComponent>();
        private AccessoryComponent editingAccessoryComponent;
        private AccessoryComponent referenceAccessoryComponent;
        private ListView _listPane;

        private Label _selectedItemLabel;
        private Label _resultsLabel;
        private ToolbarSearchField _searchField;
        private Button _saveBtn;
        private Button _resetBtn;
        private string _searchText = string.Empty;
        private List<string> backdropOptions = new List<string>();
        private List<string> poseOptions = new List<string>();
        private int currentBackdropIndex = 0;
        private int currentPoseIndex = 1;

        private static void Log(string message){
            #if AIRSHIP_INTERNAL
            // Debug.Log("AccEditor: " + message);
            #endif
        }

        private void OnDisable() {
            Log("OnDisable");
            if (_prefabStage != null && PrefabStageUtility.GetCurrentPrefabStage() == _prefabStage) {
                StageUtility.GoBackToPreviousStage();
            }

            _prefabStage = null;
            editingAccessoryComponent = null;
            referenceAccessoryComponent = null;
            
            if (characterGO) {
                DestroyImmediate(characterGO);
                characterGO = null;
            }
        }

        private void CreateStage() {
            Log("Creating STAGE");
            _prefabStage = PrefabStageUtility.OpenPrefab(AccessoryPrefabEditorPath, null, PrefabStage.Mode.InIsolation);
            prefabEditor = _prefabStage.prefabContentsRoot.GetComponent<AccessoryPrefabEditor>();
            prefabEditor.SetBackdrop(currentBackdropIndex);
            if (!_prefabStage){
                Debug.LogError("Unable to load Accessory Editor Prefab at: " + AccessoryPrefabEditorPath);
                return;
            }

            if (!CharacterPrefab){
                CharacterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterDummyPrefabPath);
            }

            Selection.activeGameObject = CharacterPrefab;
            SceneView.FrameLastActiveSceneView();

            var existingEntity = _prefabStage.prefabContentsRoot.transform.Find(CharacterPrefab.name);
            if (existingEntity != null) {
                DestroyImmediate(existingEntity.gameObject);
            }
        
            characterGO = Instantiate(CharacterPrefab, _prefabStage.prefabContentsRoot.transform);
            characterGO.name = CharacterPrefab.name;
            characterGO.hideFlags = HideFlags.DontSave;
            OnFocus();
        }

        private void DestroyStage(){
            if(_prefabStage){
                StageUtility.GoBackToPreviousStage();
                _prefabStage = null;
                characterGO = null;
            }
        }

        private void CreateGUI() {
            Log("Creating GUI");
            titleContent = new GUIContent("Accessory Editor");

            var split = new TwoPaneSplitView(0, 98, TwoPaneSplitViewOrientation.Vertical);
            rootVisualElement.Add(split);
            
            var editPane = new VisualElement();
            editPane.style.paddingTop = new StyleLength(new Length(5, LengthUnit.Pixel));
            editPane.style.paddingBottom = new StyleLength(new Length(5, LengthUnit.Pixel));
            editPane.style.paddingRight = new StyleLength(new Length(5, LengthUnit.Pixel));
            editPane.style.paddingLeft = new StyleLength(new Length(5, LengthUnit.Pixel));
            split.Add(editPane);

            var selectedHeader = new Label("Selected Accessory");
            selectedHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            selectedHeader.style.marginBottom = new StyleLength(new Length(2, LengthUnit.Pixel));
            editPane.Add(selectedHeader);

            _selectedItemLabel = new Label("No accessory selected");
            _selectedItemLabel.style.marginBottom = new StyleLength(new Length(4, LengthUnit.Pixel));
            editPane.Add(_selectedItemLabel);
            
            var buttonPanel = new VisualElement();
            buttonPanel.style.paddingTop = new StyleLength(new Length(4, LengthUnit.Pixel));
            buttonPanel.style.paddingBottom = new StyleLength(new Length(4, LengthUnit.Pixel));
            buttonPanel.style.flexDirection = FlexDirection.Row;
            editPane.Add(buttonPanel);
            
            // Save button:
            _saveBtn = new Button();
            _saveBtn.text = "Save";
            buttonPanel.Add(_saveBtn);
            _saveBtn.clickable.clicked += () => {
                if (editingAccessoryComponent == null || referenceAccessoryComponent == null) return;
                SaveCurrentAccessory();
            };

            // Reset button:
            _resetBtn = new Button();
            _resetBtn.text = "Reset";
            buttonPanel.Add(_resetBtn);
            _resetBtn.clickable.clicked += () => {
                if (editingAccessoryComponent == null || referenceAccessoryComponent == null) return;
                ResetCurrentAccessory();
            };

            var searchRow = new VisualElement();
            searchRow.style.flexDirection = FlexDirection.Row;
            searchRow.style.alignItems = Align.Center;
            editPane.Add(searchRow);

            _searchField = new ToolbarSearchField();
            _searchField.style.flexGrow = 1;
            _searchField.value = _searchText;
            _searchField.RegisterValueChangedCallback(evt => {
                _searchText = evt.newValue ?? string.Empty;
                ApplyFilter(true);
            });
            searchRow.Add(_searchField);

            _resultsLabel = new Label();
            _resultsLabel.style.marginLeft = new StyleLength(new Length(8, LengthUnit.Pixel));
            _resultsLabel.style.minWidth = new StyleLength(new Length(70, LengthUnit.Pixel));
            searchRow.Add(_resultsLabel);

            // Backdrop
            // backdropOptions.Clear();
            // foreach(var name in Enum.GetNames(typeof(BackdropType))){
            //     backdropOptions.Add(name);
            // }
            // buttonPanel.Add(new ToolbarSpacer());
            // var backdropEnum =  new DropdownField("Backdrop", backdropOptions, 0);
            // backdropEnum.RegisterValueChangedCallback((e)=>{
            //     int i=0;
            //     foreach(var enumValue in Enum.GetNames(typeof(BackdropType))){
            //         if(e.newValue == enumValue){
            //             prefabEditor.SetBackdrop(i);
            //             break;
            //         }
            //         i++;
            //     }
            // });
            // buttonPanel.Add(backdropEnum);

            //Poses
            // poseOptions.Clear();
            // foreach(var name in Enum.GetNames(typeof(PoseType))){
            //     poseOptions.Add(name);
            // }
            // buttonPanel.Add(new ToolbarSpacer());
            // var poseEnum =  new DropdownField("Pose", poseOptions, 0);
            // poseEnum.RegisterValueChangedCallback((e)=>{
            //     int i=0;
            //     foreach(var enumValue in Enum.GetNames(typeof(PoseType))){
            //         if(e.newValue == enumValue){
            //             SetPose((PoseType)i);
            //             break;
            //         }
            //         i++;
            //     }
            // });
            // buttonPanel.Add(poseEnum);

            
            _listPane = new ListView(filteredAccessories, 28);
            split.Add(_listPane);

            // Set up the left list view to show accessories:
            _listPane.selectionType = SelectionType.Single;
            _listPane.makeItem = () => {
                var label = new Label();
                label.style.paddingTop = new StyleLength(new Length(10, LengthUnit.Pixel));
                label.style.paddingBottom = new StyleLength(new Length(10, LengthUnit.Pixel));
                label.style.paddingRight = new StyleLength(new Length(5, LengthUnit.Pixel));
                label.style.paddingLeft = new StyleLength(new Length(5, LengthUnit.Pixel));
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                return label;
            };
            _listPane.bindItem = (item, index) => {
                var accessory = filteredAccessories[index];
                ((Label) item).text = accessory.gameObject.name;
            };
            _listPane.itemsSource = filteredAccessories;
            _listPane.selectionChanged += OnAccessorySelectionChanged;
            UpdateResultLabel();
            UpdateActionButtons();
            ApplyFilter(false, referenceAccessoryComponent);
        }

        private void OnAccessorySelectionChanged(IEnumerable<object> selectedItems) {
            var selectionList = selectedItems.Cast<AccessoryComponent>().ToList();
            Log("New Selection: " + selectionList.Count);
            if (selectionList.Count == 0) {
                ClearCurrentAccessory();
            } else {
                var selection = selectionList[0];
                _selectedItemLabel.text = selection.gameObject.name;
                BuildScene(selection);
            }
        }

        private void ClearCurrentAccessory() {
            Log("ClearCurrentAccessory");
            if (editingAccessoryComponent) {
                DestroyImmediate(editingAccessoryComponent.gameObject);
                editingAccessoryComponent = null;
            }

            if (_selectedItemLabel != null) {
                _selectedItemLabel.text = "No accessory selected";
            }
            UpdateActionButtons();
        }


        private bool hasFramedView = false;
        private void BuildScene(AccessoryComponent accessoryComponent, bool forceRedraw = false) {
            var newItem = accessoryComponent != referenceAccessoryComponent;
            Log("Building Scene. New Item: " + newItem + " acc: " + accessoryComponent?.gameObject.name + " oldAcc: " + referenceAccessoryComponent?.gameObject.name);
            if (_prefabStage == null || characterGO == null) {
                CreateStage();
            }

            if(accessoryComponent && (forceRedraw || newItem)){
                ClearCurrentAccessory();

                var parent = _prefabStage.prefabContentsRoot.transform;
                var rig = characterGO.GetComponentInChildren<CharacterRig>();
                if(rig){
                    parent = rig.GetSlotTransform(accessoryComponent.accessorySlot);
                }else{
                    Debug.LogError("Unable to get rig component on human entity");
                }

                if (parent == null) {
                    Debug.LogWarning($"could not find bone for accessory {accessoryComponent}");
                    return;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(accessoryComponent.gameObject, parent);
                if (go != null) {
                    this.editingAccessoryComponent = go.GetComponent<AccessoryComponent>();
                    this.referenceAccessoryComponent = accessoryComponent;
                    //accessoryComponent.gameObject.hideFlags = HideFlags.DontSave;
                    Selection.activeObject = go;
                    Selection.activeGameObject = go;
                    // SceneView.FrameLastActiveSceneView();

                    _selectedItemLabel.text = accessoryComponent.name;

                    if (this.editingAccessoryComponent.skinnedToCharacter) {
                        var skinnedMeshRenderers = go.GetComponentsInChildren<SkinnedMeshRenderer>();
                        foreach (var skinnedMeshRenderer in skinnedMeshRenderers) {
                            skinnedMeshRenderer.rootBone = rig.bodyMesh.rootBone;
                            skinnedMeshRenderer.bones = rig.bodyMesh.bones;
                        }
                    }
                    UpdateActionButtons();
                }
            }
        }

        public void SetSelected(AccessoryComponent accessoryComponent) {
            if (accessoryComponent == null || _listPane == null) return;

            if (!string.IsNullOrWhiteSpace(_searchText) &&
                accessoryComponent.gameObject.name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0) {
                _searchText = string.Empty;
                if (_searchField != null) {
                    _searchField.value = _searchText;
                }
                ApplyFilter(false);
            }

            var index = _listPane.itemsSource.IndexOf(accessoryComponent);
            if (index == -1) return;
            
            _listPane.SetSelection(index);
        }

        public static void OpenOrCreateWindow() {
            var windowOpen = HasOpenInstances<AccessoryEditorWindow>();
            if (windowOpen) {
                Log("Open existing");
                FocusWindowIfItsOpen<AccessoryEditorWindow>();
            } else {
                Log("Open new");
                CreateWindow<AccessoryEditorWindow>();
            }
            // var window = GetWindow<AccessoryEditorWindow>();
            // if(window){
            //     window.ClearCurrentAccessory();
            // }
        }

        public static void OpenWithAccessory(AccessoryComponent accessoryComponent) {
            OpenOrCreateWindow();
            var window = GetWindow<AccessoryEditorWindow>();
            window.SetSelected(accessoryComponent);
            window.BuildScene(accessoryComponent, true);
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

        private void OnFocus() {
            Log("OnFocus");
            var previousSelection = referenceAccessoryComponent;

            // Find and collect all accessories
            allAccessories.Clear();
            var allAccessoryGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in allAccessoryGuids) {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                #if !AIRSHIP_INTERNAL
                    //Ignore package accessories that you can't change
                    if(assetPath.Contains("AirshipPackages")){
                        continue;
                    }
                #endif
                var accessory = AssetDatabase.LoadAssetAtPath<AccessoryComponent>(assetPath);
                if (accessory) {
                    allAccessories.Add(accessory);
                }
            }

            //Sort names alphebetically 
            allAccessories.Sort((a,b)=>{
                return a.gameObject.name.CompareTo(b.gameObject.name);
            });

            ApplyFilter(false, previousSelection);
        }

        private void OnLostFocus() {
            Log("OnLostFocus");
        }

        private void OnDestroy() {
            this.ClearCurrentAccessory();
            this.DestroyStage();
        }

        private void SaveCurrentAccessory() {
            if (!referenceAccessoryComponent) {
                Debug.LogError("Trying to save with an empty accessory component");
                return;
            }

            Log("Saving acc: " + referenceAccessoryComponent.gameObject.name);
            Undo.RecordObject(referenceAccessoryComponent, "Save Accessory");
            referenceAccessoryComponent.Copy(editingAccessoryComponent);
            PrefabUtility.RecordPrefabInstancePropertyModifications(referenceAccessoryComponent);
            EditorUtility.SetDirty(referenceAccessoryComponent);
            PrefabUtility.ApplyPrefabInstance(editingAccessoryComponent.gameObject, InteractionMode.UserAction);
            AssetDatabase.SaveAssets();
        }

        private void ResetCurrentAccessory() {
            if(!referenceAccessoryComponent){
                Debug.LogError("Trying to reset an empty accessory component");
                return;
            }
            Log("Resetting acc: " + referenceAccessoryComponent.gameObject.name);
            Undo.RecordObject(editingAccessoryComponent.transform, "ResetTransform");
            editingAccessoryComponent.transform.SetLocalPositionAndRotation(referenceAccessoryComponent.localPosition, referenceAccessoryComponent.localRotation);
            editingAccessoryComponent.localScale = referenceAccessoryComponent.localScale;
            PrefabUtility.RevertPrefabInstance(editingAccessoryComponent.gameObject, InteractionMode.UserAction);
        }

        private void SetPose(PoseType poseType){
            Log("Setting Pose: " + poseType);
        }

        private void ApplyFilter(bool clearSelectionWhenEmpty, AccessoryComponent preferredSelection = null) {
            if (_listPane == null) {
                return;
            }

            if (preferredSelection == null && _listPane.selectedIndex >= 0 && _listPane.selectedIndex < filteredAccessories.Count) {
                preferredSelection = filteredAccessories[_listPane.selectedIndex];
            }

            filteredAccessories.Clear();
            foreach (var accessory in allAccessories) {
                if (string.IsNullOrWhiteSpace(_searchText) ||
                    accessory.gameObject.name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0) {
                    filteredAccessories.Add(accessory);
                }
            }

            _listPane.Rebuild();
            UpdateResultLabel();

            if (preferredSelection != null) {
                var preferredIndex = filteredAccessories.IndexOf(preferredSelection);
                if (preferredIndex >= 0) {
                    _listPane.SetSelection(preferredIndex);
                    return;
                }
            }

            if (clearSelectionWhenEmpty) {
                _listPane.ClearSelection();
                ClearCurrentAccessory();
            } else if (filteredAccessories.Count > 0) {
                _listPane.SetSelection(0);
            } else {
                _listPane.ClearSelection();
                ClearCurrentAccessory();
            }
        }

        private void UpdateResultLabel() {
            if (_resultsLabel != null) {
                _resultsLabel.text = $"{filteredAccessories.Count} items";
            }
        }

        private void UpdateActionButtons() {
            var hasSelection = editingAccessoryComponent != null && referenceAccessoryComponent != null;
            _saveBtn?.SetEnabled(hasSelection);
            _resetBtn?.SetEnabled(hasSelection);
        }
    }
}
