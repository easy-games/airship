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

namespace Editor.Accessories {

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
        private static readonly string AccessoryHumanEntityPrefabPath = "Assets/AirshipPackages/@Easy/Core/Prefabs/Character/CharacterDummy.prefab";
        private static GameObject HumanEntityPrefab;

        // Path to the accessory prefab editor asset:
        private static readonly string AccessoryPrefabEditorPath = "Packages/gg.easy.airship/Editor/Resources/AccessoryPrefabEditor.prefab";

        private PrefabStage _prefabStage;
        private AccessoryPrefabEditor prefabEditor;
        private GameObject characterGO;
        List<AccessoryComponent> allAccessories = new List<AccessoryComponent>();
        private AccessoryComponent _editingAccessoryComponent;
        private AccessoryComponent _referenceAccessoryComponent;
        private ListView _listPane;

        private Label _selectedItemLabel;
        private List<string> backdropOptions = new List<string>();
        private List<string> poseOptions = new List<string>();
        private int currentBackdropIndex = 0;
        private int currentPoseIndex = 1;

        private static void Log(string message){
            #if AIRSHIP_INTERNAL
            Debug.Log("AccEditor: " + message);
            #endif
        }

        private void OnDisable() {
            Log("OnDisable");
            if (_prefabStage != null && PrefabStageUtility.GetCurrentPrefabStage() == _prefabStage) {
                StageUtility.GoBackToPreviousStage();
            }

            _prefabStage = null;
            _editingAccessoryComponent = null;
            _referenceAccessoryComponent = null;
            
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

            if (!HumanEntityPrefab){
                HumanEntityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AccessoryHumanEntityPrefabPath);
            }

            Selection.activeGameObject = HumanEntityPrefab;
            SceneView.FrameLastActiveSceneView();

            var existingEntity = _prefabStage.prefabContentsRoot.transform.Find(HumanEntityPrefab.name);
            if (existingEntity != null) {
                DestroyImmediate(existingEntity.gameObject);
            }
        
            characterGO = Instantiate(HumanEntityPrefab, _prefabStage.prefabContentsRoot.transform);
            characterGO.name = HumanEntityPrefab.name;
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

            
            _listPane = new ListView(allAccessories, 32);
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
                var accessory = allAccessories[index];
                ((Label) item).text = accessory.gameObject.name;
            };
            _listPane.itemsSource = allAccessories;
            _listPane.selectionChanged += OnAccessorySelectionChanged;
            //_listPane.Sort((a, b) => string.Compare(((Label)a).text, ((Label)b).text, StringComparison.OrdinalIgnoreCase));
        }

        private void OnAccessorySelectionChanged(IEnumerable<object> selectedItems) {
            var selectionList = selectedItems.Cast<AccessoryComponent>().ToList();
            Log("New Selection: " + selectionList.Count);
            if (selectionList.Count == 0) {
                ClearCurrentAccessory();
            } else {
                var selection = selectionList[0];
                _selectedItemLabel.text = selection.ToString();
                BuildScene(selection);
            }
        }

        private void ClearCurrentAccessory() {
            Log("ClearCurrentAccessory");
            if (_editingAccessoryComponent) {
                DestroyImmediate(_editingAccessoryComponent.gameObject);
                _editingAccessoryComponent = null;
            }
            _selectedItemLabel.text = "No selected item";
        }

        private void BuildScene(AccessoryComponent accessoryComponent, bool forceRedraw = false) {
            var newItem = accessoryComponent != _referenceAccessoryComponent;
            Log("Building Scene. New Item: " + newItem + " acc: " + accessoryComponent?.gameObject.name + " oldAcc: " + _referenceAccessoryComponent?.gameObject.name);
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
                    _editingAccessoryComponent = go.GetComponent<AccessoryComponent>();
                    _referenceAccessoryComponent = accessoryComponent;
                    //accessoryComponent.gameObject.hideFlags = HideFlags.DontSave;
                    Selection.activeObject = go;
                    Selection.activeGameObject = go;
                    SceneView.FrameLastActiveSceneView();

                    _selectedItemLabel.text = accessoryComponent.name;
                }
            }
        }

        public void SetSelected(AccessoryComponent accessoryComponent) {
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

            if (_listPane != null) {
                OnAccessorySelectionChanged(_listPane.selectedItems);
            }

            //BuildScene(_referenceAccessoryComponent);
        }

        private void OnLostFocus() {
            Log("OnLostFocus");
        }

        private void OnDestroy() {
            this.ClearCurrentAccessory();
            this.DestroyStage();
        }

        private void SaveCurrentAccessory() {
            if (!_referenceAccessoryComponent) {
                Debug.LogError("Trying to save with an empty accessory component");
                return;
            }

            Log("Saving acc: " + _referenceAccessoryComponent.gameObject.name);
            Undo.RecordObject(_referenceAccessoryComponent, "Save Accessory");
            _referenceAccessoryComponent.Copy(_editingAccessoryComponent);
            PrefabUtility.RecordPrefabInstancePropertyModifications(_referenceAccessoryComponent);
            EditorUtility.SetDirty(_referenceAccessoryComponent);
            PrefabUtility.ApplyPrefabInstance(_editingAccessoryComponent.gameObject, InteractionMode.UserAction);
            AssetDatabase.SaveAssets();
        }

        private void ResetCurrentAccessory() {
            if(!_referenceAccessoryComponent){
                Debug.LogError("Trying to reset an empty accessory component");
                return;
            }
            Log("Resetting acc: " + _referenceAccessoryComponent.gameObject.name);
            Undo.RecordObject(_editingAccessoryComponent.transform, "ResetTransform");
            _editingAccessoryComponent.transform.SetLocalPositionAndRotation(_referenceAccessoryComponent.localPosition, _referenceAccessoryComponent.localRotation);
            _editingAccessoryComponent.localScale = _referenceAccessoryComponent.localScale;
            PrefabUtility.RevertPrefabInstance(_editingAccessoryComponent.gameObject, InteractionMode.UserAction);
        }

        private void SetPose(PoseType poseType){
            Log("Setting Pose: " + poseType);
        }
    }
}
