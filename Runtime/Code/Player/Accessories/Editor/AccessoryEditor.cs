using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// PreviewRenderUtility Docs: https://github.com/CyberFoxHax/Unity3D_PreviewRenderUtility_Documentation/wiki/PreviewRenderUtility

namespace Code.Player.Accessories.Editor {
    public class AccessoryEditor : EditorWindow {
        private ListView _leftPane;
        private VisualElement _rightPane;
        private VisualElement _meshEditPane;
        private VisualElement _meshPane;

        private Vector3Field _positionField;
        private Vector3Field _rotationField;
        private Vector3Field _scaleField;

        private Rect _renderRect;
        private PreviewRenderUtility _preview;
        private Texture _outputTexture;
        private bool _sceneDirty;

        private AccessoryEditorCamera _camera;
        
        private bool _mouseDown;

        private List<Accessory> _accessories = new();
        private readonly List<AccessoryTransform> _accessoryTransforms = new();

        private Vector3 _queuedPositionInput;
        private Vector3 _queuedRotationInput;
        private Vector3 _queuedScaleInput;
        private bool _queuedChange;

        private AccessoryInputTracker _inputTracker;

        // Path to the human entity asset:
        private static readonly Lazy<GameObject> HumanEntityPrefab = new(() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Bedwars/Bundles/Shared/Resources/Entity/HumanEntity/HumanEntity.prefab"));

        /// <summary>
        /// Find a descendant transform with the given <c>name</c> within the <c>parent</c> transform.
        /// This will return <c>null</c> if nothing is found.
        /// </summary>
        private static Transform FindRecursive(Transform parent, string name) {
            foreach (Transform child in parent) {
                if (child.name == name) {
                    return child;
                }
                var found = FindRecursive(child, name);
                if (found != null) {
                    return found;
                }
            }
            return null;
        }

        private void OnUndoRedoPerformed() {
            // If the undo/redo process affected the Input Tracker, change the input fields to match:
            if (_inputTracker.DidUndoRedoAffectPosition()) {
                _positionField.SetValueWithoutNotify(_inputTracker.Position);
            }
            if (_inputTracker.DidUndoRedoAffectRotation()) {
                _rotationField.SetValueWithoutNotify(_inputTracker.Rotation);
            }
            if (_inputTracker.DidUndoRedoAffectScale()) {
                _scaleField.SetValueWithoutNotify(_inputTracker.Scale);
            }
            
            // Reset internal tracking of input if needed:
            _inputTracker.CheckAfterUndoRedo();
        }

        /// <summary>
        /// Build the 3D scene from scratch.
        /// </summary>
        private void BuildScene(IEnumerable<Accessory> accessories, List<AccessoryTransform> accessoryTransforms = null) {
            _preview?.Cleanup();
            _preview = new PreviewRenderUtility();

            _accessories = accessories.ToList();

            var hasAccessoryTransforms = accessoryTransforms != null;
            if (hasAccessoryTransforms) {
                if (accessoryTransforms != _accessoryTransforms) {
                    _accessoryTransforms.Clear();
                    _accessoryTransforms.AddRange(accessoryTransforms);
                }
            } else {
                _accessoryTransforms.Clear();
            }
            
            // Set camera properties:
            var cam = _preview.camera;
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            cam.backgroundColor = new Color(179f/255, 210f/255, 255f/255);

            // All scene objects will go inside the `rootGo`:
            var rootGo = new GameObject();
            rootGo.name = "RootGameObject";
            _preview.AddSingleGO(rootGo);

            // Create entity model for the scene:
            var humanEntity = Instantiate(HumanEntityPrefab.Value, rootGo.transform);
            var objectRefs = humanEntity.GetComponent<GameObjectReferences>();
            
            // Add all selected accessories:
            foreach (var accessory in _accessories) {
                var parent = rootGo.transform;
                string itemKey = AccessoryBuilder.GetBoneItemKey(accessory.AccessorySlot);
                if(!string.IsNullOrEmpty(itemKey)){
                    parent = objectRefs.GetValueTyped<Transform>(AccessoryBuilder.boneKey, itemKey);
                }

                if (parent == null) {
                    Debug.LogWarning($"Could not find bone for accessory {accessory.DisplayName}");
                    continue;
                }
                
                var accTransform = _accessoryTransforms.Find(transform => transform.Accessory == accessory);
                if (accTransform == null) {
                    accTransform = new AccessoryTransform(accessory);
                    _accessoryTransforms.Add(accTransform);
                }

                // Instantiate and set orientation of the accessory:
                var go = Instantiate(accessory.Prefab, parent);
                go.transform.localPosition = accTransform.Position;
                go.transform.localScale = accTransform.Scale;
                go.transform.localRotation = Quaternion.Euler(accTransform.Rotation);
            }

            // Find center point given all 3D objects in the scene:
            var boundsMin = Vector3.zero;
            var boundsMax = Vector3.zero;
            foreach (var renderer in rootGo.GetComponentsInChildren<Renderer>()) {
                boundsMin = Vector3.Min(boundsMin, renderer.bounds.min);
                boundsMax = Vector3.Max(boundsMax, renderer.bounds.max);
            }
            var center = Vector3.Lerp(boundsMin, boundsMax, 0.5f);
            center.x = 0;
            center.z = 0;
            _camera.CameraCenter = center;

            // Set lights:
            _preview.lights[0].transform.localEulerAngles = new Vector3(30, 250, 0);
            _preview.lights[0].intensity = 1f;
            
            _preview.lights[1].transform.localEulerAngles = new Vector3(30, 30, 0);
        }

        /// <summary>
        /// Calculate the camera position, render the camera, and retrieve a
        /// RenderTexture of the scene.
        /// </summary>
        private RenderTexture CreatePreviewTexture() {
            _preview.BeginPreview(_renderRect, GUIStyle.none);
            _camera.SetCameraPosition(_preview.camera);
            return (RenderTexture) _preview.EndPreview();
        }

        /// <summary>
        /// Create the <c>_outputTexture</c> by rendering the scene. The internal
        /// scene preview will automatically clean up any existing texture.
        /// </summary>
        private void Draw() {
            _outputTexture = CreatePreviewTexture();
        }

        private void OnEnable() {
            _inputTracker = CreateInstance<AccessoryInputTracker>();
            _camera = new AccessoryEditorCamera(7f, 65f, 100f);
            _mouseDown = false;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnDisable() {
            if (_preview != null) {
                _preview.Cleanup();
                _preview = null;
            }
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            UnregisterInputCallbacks();
        }

        private void OnGUI() {
            if (_accessories.Count == 0) return;

            // Find the `rect` that represents the drawing area:
            var leftPaneRect = _leftPane.contentRect;
            var meshEditPaneRect = _meshEditPane.layout;
            var meshPaneRect = _meshPane.contentRect;
            var rect = new Rect(leftPaneRect.width, meshEditPaneRect.height, meshPaneRect.width, meshPaneRect.height);

            var hasPreview = _preview != null;

            // Draw the scene if needed:
            if ((rect != _renderRect || _sceneDirty) && hasPreview) {
                _sceneDirty = false;
                _renderRect = rect;
                Draw();
            }

            // Draw the rendered 3D scene texture if available:
            if (hasPreview && _outputTexture != null) {
                GUI.DrawTexture(rect, _outputTexture);
            }
        }

        private void OnPositionInputChanged(ChangeEvent<Vector3> evt) {
            Undo.RecordObject(_inputTracker, "Position Changed");
            _inputTracker.Position = evt.newValue;
        }

        private void OnRotationInputChanged(ChangeEvent<Vector3> evt) {
            Undo.RecordObject(_inputTracker, "Rotation Changed");
            _inputTracker.Rotation = evt.newValue;
        }

        private void OnScaleInputChanged(ChangeEvent<Vector3> evt) {
            Undo.RecordObject(_inputTracker, "Scale Changed");
            _inputTracker.Scale = evt.newValue;
        }

        private void Update() {
            if (_accessoryTransforms.Count != 1) return;
            
            var accTransform = _accessoryTransforms[0];
            var changed = false;

            // Update the accessory transform trackers if any of the inputs have changed:
            if (_inputTracker.TryUpdatePosition(out var pos)) {
                if (pos != accTransform.Position) {
                    changed = true;
                    _positionField.label = pos == accTransform.OriginalPosition ? "Position" : "Position *";
                    accTransform.Position = pos;
                }
            }
            if (_inputTracker.TryUpdateRotation(out var rot)) {
                if (rot != accTransform.Rotation) {
                    changed = true;
                    _rotationField.label = rot == accTransform.OriginalRotation ? "Rotation" : "Rotation *";
                    accTransform.Rotation = rot;
                }
            }
            if (_inputTracker.TryUpdateScale(out var scale)) {
                if (scale != accTransform.Scale) {
                    changed = true;
                    _scaleField.label = scale == accTransform.OriginalScale ? "Scale" : "Scale *";
                    accTransform.Scale = scale;
                }
            }
            
            // Rebuild the scene if any inputs have changed:
            if (changed) {
                BuildScene(_accessories, _accessoryTransforms);
                _sceneDirty = true;
                _meshPane.MarkDirtyRepaint();
            }
        }

        private void CreateGUI() {
            titleContent = new GUIContent("Accessory Editor");
            
            // Find and collect all accessories:
            var allAccessoryGuids = AssetDatabase.FindAssets("t:Accessory");
            var allAccessories = new List<Accessory>();
            foreach (var guid in allAccessoryGuids) {
                allAccessories.Add(AssetDatabase.LoadAssetAtPath<Accessory>(AssetDatabase.GUIDToAssetPath(guid)));
            }

            // Top-level split view:
            var splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            
            rootVisualElement.Add(splitView);

            _leftPane = new ListView();
            _rightPane = new TwoPaneSplitView(0, 120, TwoPaneSplitViewOrientation.Vertical);
            splitView.Add(_leftPane);
            splitView.Add(_rightPane);

            // Mesh Edit Pane is the pane with various inputs:
            _meshEditPane = new VisualElement();
            _meshEditPane.style.paddingTop = new StyleLength(new Length(5, LengthUnit.Pixel));
            _meshEditPane.style.paddingBottom = new StyleLength(new Length(5, LengthUnit.Pixel));
            _meshEditPane.style.paddingRight = new StyleLength(new Length(5, LengthUnit.Pixel));
            _meshEditPane.style.paddingLeft = new StyleLength(new Length(5, LengthUnit.Pixel));
            
            // Mesh Pane contains the area where the scene render will be drawn:
            _meshPane = new VisualElement();
            _meshPane.focusable = true;
            
            _rightPane.Add(_meshEditPane);
            _rightPane.Add(_meshPane);

            // Set up the left list view to show accessories:
            _leftPane.selectionType = SelectionType.Multiple;
            _leftPane.makeItem = () => {
                var label = new Label();
                label.style.paddingTop = new StyleLength(new Length(5, LengthUnit.Pixel));
                label.style.paddingBottom = new StyleLength(new Length(5, LengthUnit.Pixel));
                label.style.paddingRight = new StyleLength(new Length(5, LengthUnit.Pixel));
                label.style.paddingLeft = new StyleLength(new Length(5, LengthUnit.Pixel));
                return label;
            };
            _leftPane.bindItem = (item, index) => {
                var accessory = allAccessories[index];
                ((Label) item).text = accessory.DisplayName == "" ? accessory.Prefab.name : accessory.DisplayName;
            };
            _leftPane.itemsSource = allAccessories;
            _leftPane.onSelectionChange += OnAccessorySelectionChanged;

            // Create the position, rotation, and scale input fields:
            _positionField = new Vector3Field("Position");
            _rotationField = new Vector3Field("Rotation");
            _scaleField = new Vector3Field("Scale");
            
            _positionField.focusable = false;
            _rotationField.focusable = false;
            _scaleField.focusable = false;
            
            _positionField.style.maxWidth = new StyleLength(new Length(500, LengthUnit.Pixel));
            _rotationField.style.maxWidth = new StyleLength(new Length(500, LengthUnit.Pixel));
            _scaleField.style.maxWidth = new StyleLength(new Length(500, LengthUnit.Pixel));
            
            _positionField.style.paddingBottom = new StyleLength(new Length(2, LengthUnit.Pixel));
            _rotationField.style.paddingBottom = new StyleLength(new Length(2, LengthUnit.Pixel));
            _scaleField.style.paddingBottom = new StyleLength(new Length(2, LengthUnit.Pixel));
            
            _meshEditPane.Add(_positionField);
            _meshEditPane.Add(_rotationField);
            _meshEditPane.Add(_scaleField);

            // Create the button panel below the input fields:
            var buttonPanel = new VisualElement();
            buttonPanel.style.paddingTop = new StyleLength(new Length(5, LengthUnit.Pixel));
            buttonPanel.style.flexDirection = FlexDirection.Row;
            _meshEditPane.Add(buttonPanel);
            
            // Save button:
            var saveBtn = new Button();
            saveBtn.text = "Save";
            buttonPanel.Add(saveBtn);
            saveBtn.clickable.clicked += () => {
                if (_accessoryTransforms.Count != 1) return;
                var accTransform = _accessoryTransforms[0];
                accTransform.Save();
                _positionField.value = accTransform.Position;
                _rotationField.value = accTransform.Rotation;
                _scaleField.value = accTransform.Scale;
                _positionField.label = "Position";
                _rotationField.label = "Rotation";
                _scaleField.label = "Scale";
            };

            // Reset button:
            var resetBtn = new Button();
            resetBtn.text = "Reset";
            buttonPanel.Add(resetBtn);
            resetBtn.clickable.clicked += () => {
                if (_accessoryTransforms.Count != 1) return;
                var accTransform = _accessoryTransforms[0];
                _positionField.value = accTransform.OriginalPosition;
                _rotationField.value = accTransform.OriginalRotation;
                _scaleField.value = accTransform.OriginalScale;
            };

            // Handle rotating and panning the view based on mouse and key input:
            var shiftDown = false;
            var isPanning = false;
            _meshPane.RegisterCallback<MouseDownEvent>((evt) => {
                if (evt.button != 0) return;
                _mouseDown = true;
                isPanning = shiftDown;
            });
            _meshPane.RegisterCallback<MouseUpEvent>((evt) => {
                if (evt.button != 0) return;
                _mouseDown = false;
            });
            _meshPane.RegisterCallback<MouseMoveEvent>((evt) => {
                const float deltaRotationSensitivity = 500f;
                const float deltaPanSensitivity = 10f;
                if (!_mouseDown) return;

                var delta = evt.mouseDelta;
                delta.x /= _renderRect.width;
                delta.y /= _renderRect.height;

                if (isPanning) {
                    delta *= deltaPanSensitivity * (_camera.CameraDistance / 10f);
                    _camera.Pan(_preview.camera, delta);
                } else {
                    delta *= deltaRotationSensitivity;
                    _camera.Increment(delta.y, delta.x);
                }

                _sceneDirty = true;
                _meshPane.MarkDirtyRepaint();
            });
            _meshPane.RegisterCallback<WheelEvent>((evt) => {
                const float scrollSensitivity = 0.4f;
                var zoom = evt.delta.y * scrollSensitivity;
                _camera.Zoom(zoom);
                _sceneDirty = true;
                _meshPane.MarkDirtyRepaint();
            });
            _meshPane.RegisterCallback<KeyDownEvent>((evt) => {
                if (evt.keyCode == KeyCode.LeftShift) {
                    shiftDown = true;
                } else if (evt.keyCode == KeyCode.Space) {
                    _camera.ResetPosition();
                    _sceneDirty = true;
                    _meshPane.MarkDirtyRepaint();
                }
            }, TrickleDown.TrickleDown);
            _meshPane.RegisterCallback<KeyUpEvent>((evt) => {
                if (evt.keyCode == KeyCode.LeftShift) {
                    shiftDown = false;
                }
            }, TrickleDown.TrickleDown);
        }

        private void RegisterInputCallbacks() {
            UnregisterInputCallbacks();
            _positionField.RegisterCallback<ChangeEvent<Vector3>>(OnPositionInputChanged);
            _rotationField.RegisterCallback<ChangeEvent<Vector3>>(OnRotationInputChanged);
            _scaleField.RegisterCallback<ChangeEvent<Vector3>>(OnScaleInputChanged);
        }

        private void UnregisterInputCallbacks() {
            _positionField?.UnregisterCallback<ChangeEvent<Vector3>>(OnPositionInputChanged);
            _rotationField?.UnregisterCallback<ChangeEvent<Vector3>>(OnRotationInputChanged);
            _scaleField?.UnregisterCallback<ChangeEvent<Vector3>>(OnScaleInputChanged);
            if (_positionField != null) _positionField.label = "Position";
            if (_rotationField != null) _rotationField.label = "Rotation";
            if (_scaleField != null) _scaleField.label = "Scale";
        }

        private void OnAccessorySelectionChanged(IEnumerable<object> selectedItems) {
            // Zero out the scene:
            _meshPane.Clear();
            UnregisterInputCallbacks();
            _positionField.SetValueWithoutNotify(Vector3.zero);
            _rotationField.SetValueWithoutNotify(Vector3.zero);
            _scaleField.SetValueWithoutNotify(Vector3.zero);
            _positionField.focusable = false;
            _rotationField.focusable = false;
            _scaleField.focusable = false;

            var selection = selectedItems.Cast<Accessory>().ToList();

            // If nothing is selected, clean up the scene and stop:
            if (selection.Count == 0) {
                if (_preview != null) {
                    _preview.Cleanup();
                    _preview = null;
                }
                return;
            }

            // If only one accessory is selected, allow the input fields to be editable:
            if (selection.Count == 1) {
                var selectedAccessory = selection[0];
                
                _positionField.SetValueWithoutNotify(selectedAccessory.Position);
                _rotationField.SetValueWithoutNotify(selectedAccessory.Rotation);
                _scaleField.SetValueWithoutNotify(selectedAccessory.Scale);
                _positionField.focusable = true;
                _rotationField.focusable = true;
                _scaleField.focusable = true;
                _inputTracker.SetWithoutDirty(selectedAccessory.Position, selectedAccessory.Rotation, selectedAccessory.Scale);
                
                RegisterInputCallbacks();
            }
            
            // If more than one accessory is selected, the fields will not be able to be edited, essentially
            // becoming a read-only version of viewing various accessories together.
            
            BuildScene(selection);
            _sceneDirty = true;
        }

        /// <summary>
        /// Force an accessory to be selected in the list.
        /// </summary>
        public void SetSelected(Accessory accessory) {
            var index = _leftPane.itemsSource.IndexOf(accessory);
            if (index == -1) return;
            
            _leftPane.SetSelection(index);
        }

        /// <summary>
        /// Open up the Accessory Editor window with the given accessory selected.
        /// </summary>
        public static void OpenWithAccessory(Accessory accessory) {
            OpenOrCreateWindow();
            var window = GetWindow<AccessoryEditor>();
            window.SetSelected(accessory);
        }

        public static void OpenOrCreateWindow() {
            var windowIsOpen = HasOpenInstances<AccessoryEditor>();
            if (windowIsOpen) {
                FocusWindowIfItsOpen<AccessoryEditor>();
            } else {
                CreateWindow<AccessoryEditor>();
            }
        }

        // Automatically create an Accessory Editor window when an accessory is opened:
        [OnOpenAsset(0)]
        public static bool OpenAccessoryWindow(int instanceId, int line) {
            var target = EditorUtility.InstanceIDToObject(instanceId);
        
            if (target is Accessory) {
                OpenOrCreateWindow();
            }

            return false;
        }

        // Give the accessory over to the Accessory Editor after the window is opened or created:
        [OnOpenAsset(1)]
        public static bool LoadAccessoryWindow(int instanceId, int line) {
            var target = EditorUtility.InstanceIDToObject(instanceId);

            if (target is Accessory accessory) {
                var window = GetWindow<AccessoryEditor>();
                window.SetSelected(accessory);
                
                return true;
            }

            return false;
        }
    }
}
