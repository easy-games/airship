using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using Time = UnityEngine.Time;

[EditorTool("Material Color Tool")]
public class MaterialColorTool : EditorTool {
    private const float slerpMod = 50;
    [SerializeField] private MaterialColorToolData data;

    public static float cursorSize = 0;
    public static MaterialColor.ColorSetting brushSettings;
    public static int currentMaterialIndex;
    public static bool useEmissive;
    public static bool useMainColor;
    public static bool useMaterial;
    public static bool autoAddMaterialColor;


    GUIContent m_IconContent;

    // Reference to settings window
    private MaterialColorToolWindow window;
#pragma warning disable CS0414
    private bool holdingClick = false;
#pragma warning restore CS0414
    private MaterialColor previewObject;
    private GameObject[] ignoreList;

    //Called once by tool manager on scene initialize
    private void OnEnable() {
        brushSettings = new(Color.white, Color.black, 1, 0);
    }

    private void OnDisable() {
    }

    // Called when the active tool is set to this tool instance. Global tools are persisted by the ToolManager,
    // so usually you would use OnEnable and OnDisable to manage native resources, and OnActivated/OnWillBeDeactivated
    // to set up state. See also `EditorTools.{ activeToolChanged, activeToolChanged }` events.
    public override void OnActivated() {
        ShowNotification("Entering Material Color Tool", .1f);

        previewObject = GameObject.CreatePrimitive(PrimitiveType.Sphere).AddComponent<MaterialColor>();
        previewObject.gameObject.SetActive(false);
        ignoreList = new[] { previewObject.gameObject };
    }

    // Called before the active tool is changed, or destroyed. The exception to this rule is if you have manually
    // destroyed this tool (ex, calling `Destroy(this)` will skip the OnWillBeDeactivated invocation).
    public override void OnWillBeDeactivated() {
        if (previewObject) {
            DestroyImmediate(previewObject.gameObject);
        }
        SceneView.duringSceneGui -= OnSceneGUI;

        SceneView.duringSceneGui += OnSceneGUI;

        // Focus or create settings window
        window = MaterialColorToolWindow.OpenWindow();
    }

    public override GUIContent toolbarIcon {
        get {
            if (m_IconContent == null) {
                m_IconContent = new GUIContent() {
                    text = "Material Color Tool",
                    tooltip = "Paint colors and materials onto meshes"
                };
            }
            if (data) {
                m_IconContent.image = data.toolIcon;
            }
            return m_IconContent;
        }
    }

    // The second "context" argument accepts an EditorWindow type.
    [Shortcut("Activate Material Color Tool", typeof(SceneView), KeyCode.C)]
    static void SelectToolShortcut() {
        ToolManager.SetActiveTool<MaterialColorTool>();
    }

    private void OnSceneGUI(SceneView scene) {
        if (!ToolManager.IsActiveTool(this) || data == null || previewObject == null) {
            return;
        }

        int controlID = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlID);

        //Create a custom cursor
        //EditorGUIUtility.AddCursorRect(new Rect(0,0, 300,300), MouseCursor.ArrowPlus);
        //Cursor.SetCursor(data.cursorIcon, Vector2.zero, CursorMode.ForceSoftware);

        Event e = Event.current;

        //Dont paint when orbiting camera
        if (e.alt) {
            previewObject.gameObject.SetActive(false);
            return;
        }

        //Position Preview
        previewObject.gameObject.SetActive(true);
        previewObject.transform.localScale = new Vector3(cursorSize, cursorSize, .0001f);
        var screenPos = HandleUtility.GUIPointToScreenPixelCoordinate(e.mousePosition);
        previewObject.transform.position
            = Vector3.Slerp(previewObject.transform.position,
                SceneView.lastActiveSceneView.camera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, .1f)),
                    Time.deltaTime * slerpMod);

        //Draw onto the preview
        ApplyMaterial(previewObject, previewObject.gameObject.GetComponent<Renderer>(), 0, false);

        previewObject.transform.LookAt(SceneView.lastActiveSceneView.camera.transform.position);

        //Mouse Move
        if (e.type == EventType.MouseDrag) {
            if (e.button == 0) {
                OnClick(e.mousePosition);
            }
        }

        //Left click
        if (e.type == EventType.MouseDown && e.button == 0) {
            holdingClick = true;
            OnClick(e.mousePosition);
        }
        else if (e.type == EventType.MouseUp && e.button == 0) {
            holdingClick = false;
        }


    }

    private void OnClick(Vector2 mousePos) {
        var hitObject = HandleUtility.PickGameObject(mousePos, ignoreList, out var targetMaterialIndex);
        if (hitObject != null) {
            var materialColor = hitObject.GetComponent<MaterialColor>();
            var ren = hitObject.GetComponent<Renderer>();
            if (materialColor == null && ren != null && autoAddMaterialColor) {
                materialColor = hitObject.AddComponent<MaterialColor>();
            }
            if (materialColor == null) {
                //No renderer on this object
                return;
            }

            //Debug.Log("Coloring: " + hitObject + " " + materialIndex + " " + brushSettings.materialColor);

            //DRAW ONTO THE OBJECT
            ApplyMaterial(materialColor, ren, targetMaterialIndex);
        }
    }

    private void ApplyMaterial(MaterialColor materialColor, Renderer ren, int targetMaterialIndex = 0, bool addToUndoStack = true) {
        if (useMaterial) {
            if (addToUndoStack) {
                //Add this renderer to undo stack
                Undo.RegisterCompleteObjectUndo(ren, "Changed Material");
            }

            //Apply the new material
            var materials = ren.sharedMaterials;
            materials[targetMaterialIndex] = data.standardMaterials[currentMaterialIndex];
            ren.sharedMaterials = materials;

            if (addToUndoStack) {
                //Record changes to renderer for any prefab components
                PrefabUtility.RecordPrefabInstancePropertyModifications(ren);
            }
        }

        if (addToUndoStack) {
            Undo.RecordObject(materialColor, "Painted Material");
        }

        MaterialColor.ColorSetting usedSettings = materialColor.GetColorSettings(targetMaterialIndex);
        if (useMainColor) {
            //Apply material color
            usedSettings.materialColor = brushSettings.materialColor;
        }
        if (useEmissive) {
            //Apply emissive color
            usedSettings.emissiveColor = brushSettings.emissiveColor;
        }
        materialColor.SetColorSettings(targetMaterialIndex, usedSettings);

        if (addToUndoStack) {
            //Record changes to material color for any prefab components
            PrefabUtility.RecordPrefabInstancePropertyModifications(materialColor);
            //Add this to Undo Stack
            Undo.FlushUndoRecordObjects();
        }
    }

    public override void OnToolGUI(EditorWindow window) {
        if (!(window is SceneView sceneView)) {
            return;
        }

    }

    private void ShowNotification(string label, float duration = .2f) {
        if (SceneView.lastActiveSceneView) {
            SceneView.lastActiveSceneView.ShowNotification(new GUIContent(label), duration);
        }
    }
}