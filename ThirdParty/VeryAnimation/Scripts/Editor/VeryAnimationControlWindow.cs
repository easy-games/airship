//#define Enable_Profiler

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace VeryAnimation
{
    public class VeryAnimationControlWindow : EditorWindow
    {
        public static VeryAnimationControlWindow instance;

        private VeryAnimationWindow vaw { get { return VeryAnimationWindow.instance; } }
        private VeryAnimationEditorWindow vae { get { return VeryAnimationEditorWindow.instance; } }
        private VeryAnimation va { get { return VeryAnimation.instance; } }

        #region Textures
        private Texture2D avatarHead;
        private Texture2D avatarTorso;
        private Texture2D avatarLeftArm;
        private Texture2D avatarLeftFingers;
        private Texture2D avatarLeftLeg;
        private Texture2D avatarRightArm;
        private Texture2D avatarRightFingers;
        private Texture2D avatarRightLeg;
        private Texture2D avatarHeadZoom;
        private Texture2D avatarLeftHandZoom;
        private Texture2D avatarRightHandZoom;
        private Texture2D avatarBodysilhouette;
        private Texture2D avatarHeadzoomsilhouette;
        private Texture2D avatarLefthandzoomsilhouette;
        private Texture2D avatarRighthandzoomsilhouette;
        private Texture2D avatarRoot;
        private Texture2D avatarLeftFeetIk;
        private Texture2D avatarRightFeetIk;
        private Texture2D avatarLeftFingersIk;
        private Texture2D avatarRightFingersIk;
        private Texture2D avatarBodyPartPicker;
        private Texture2D dotfill;
        private Texture2D dotframe;
        private Texture2D dotframedotted;
        private Texture2D dotselection;
        #endregion

        #region GUIStyles
        private GUIStyle guiStyleBackgroundBox;
        private GUIStyle guiStyleVerticalToolbar;
        private GUIStyle guiStyleBoneButton;
        #endregion

        #region Editor
        public enum HumanoidAvatarPartsMode
        {
            Body,
            Head,
            LeftHand,
            RightHand,
        }
        private readonly string[] HumanoidAvatarPartsModeStrings =
        {
            "Body",
            "Head",
            "Left Hand",
            "Right Hand",
        };
        public HumanoidAvatarPartsMode humanoidAvatarPartsMode { get; private set; }

        private readonly Color GlayColor = new Color(0.2f, 0.2f, 0.2f);
        private readonly Color GreenColor = new Color(0.2f, 0.8f, 0.2f);
        private readonly Color BlueColor = new Color32(102, 178, 255, 255);

        private Vector2 windowScrollPosition;

        private bool guiAnimatorIkFoldout;
        private bool guiOriginalIkFoldout;
        private bool guiHumanoidFoldout;
        private bool guiSelectionFoldout;
        private bool guiHierarchyFoldout;

        private bool guiAnimatorIkVisible = true;
        private bool guiOriginalIkVisible = true;
        private bool guiHumanoidVisible = true;
        private bool guiSelectionVisible = true;
        private bool guiHierarchyVisible = true;

        private bool guiAnimatorIkHelp;
        private bool guiOriginalIkHelp;
        private bool guiHumanoidHelp;
        private bool guiSelectionHelp;
        private bool guiHierarchyHelp;

        private List<HumanBodyBones> selectionGameObjectsHumanoidIndex;
        private Dictionary<HumanBodyBones, Vector2> controlBoneList;
        private AvatarMaskBodyPart selectionAvatarMaskBodyPart;

        private Color[] maskBodyPartPicker;

        private enum SelectionType
        {
            List,
            Popup,
        }
        private static readonly string[] SelectionTypeString =
        {
            SelectionType.List.ToString(),
            SelectionType.Popup.ToString(),
        };
        private SelectionType selectionType;
        private bool updateSelectionList = true;
        private bool updateSelectionPopup = true;
        private int selectionSetIndex = -1;
        private ReorderableList selectionSetList;
        private string[] selectionSetStrings;

        public bool mirrorObject { get; private set; }
        public bool humanoidName { get; private set; }
        #endregion

        #region Hierarchy
        private class HierarchyTreeView : TreeView
        {
            private VeryAnimationWindow vaw { get { return VeryAnimationWindow.instance; } }
            private VeryAnimation va { get { return VeryAnimation.instance; } }
            private VeryAnimationControlWindow vcw { get { return VeryAnimationControlWindow.instance; } }

            private Dictionary<Type, Texture2D> typeIconDic;
            
            private Texture2D GetIconTexture(Type type)
            {
                if (typeIconDic == null)
                    typeIconDic = new Dictionary<Type, Texture2D>();
                Texture2D tex;
                if (!typeIconDic.TryGetValue(type, out tex))
                {
                    tex = vaw.uEditorGUIUtility.LoadIcon(type.Name + " icon");
                    typeIconDic.Add(type, tex);
                }
                return tex;
            }

            public HierarchyTreeView(TreeViewState state) : base(state)
            {
                extraSpaceBeforeIconAndLabel = 18f;
            }

            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem(int.MinValue, -1, "Root");
                if (instance == null || va == null || va.isEditError)
                {
                    root.children = new List<TreeViewItem>();
                    return root;
                }
                Func<Transform, int, TreeViewItem> CreateTreeViewItem = null;
                CreateTreeViewItem = (t, depth) =>
                {
                    var hi = va.HumanoidBonesIndexOf(t.gameObject);
                    var name = t.gameObject.name;
                    if (va.isHuman && instance.humanoidName)
                    {
                        if (vaw.gameObject == t.gameObject)
                            name = "Root";
                        else if (hi >= 0)
                            name = hi.ToString();
                    }
                    var item = new TreeViewItem(t.gameObject.GetInstanceID(), depth, name);
                    {
                        var boneIndex = va.BonesIndexOf(t.gameObject);
                        var tex = boneIndex >= 0 ? GetIconTexture(va.GetBoneType(boneIndex)) : null;
                        if (tex == null)
                            tex = GetIconTexture(typeof(Transform));
                        item.icon = tex;
                    }
                    item.children = new List<TreeViewItem>(t.childCount);
                    for (int i = 0; i < t.childCount; i++)
                    {
                        item.children.Add(CreateTreeViewItem(t.GetChild(i), depth + 1));
                    }
                    return item;
                };
                root.children = (new TreeViewItem[] { CreateTreeViewItem(vaw.gameObject.transform, 0) }).ToList();

                return root;
            }

            protected override void SelectionChanged(IList<int> selectedIds)
            {
                if (va == null || va.isEditError) return;

                HashSet<GameObject> selection = new HashSet<GameObject>();
                Func<int, GameObject> FindGameObject = (instanceID) =>
                {
                    Func<Transform, GameObject> FindChild = null;
                    FindChild = (t) =>
                    {
                        if (t.gameObject.GetInstanceID() == instanceID)
                            return t.gameObject;
                        for (int i = 0; i < t.childCount; i++)
                        {
                            var go = FindChild(t.GetChild(i));
                            if (go != null) return go;
                        }
                        return null;
                    };
                    return FindChild(vaw.gameObject.transform);
                };
                foreach (var instanceID in selectedIds)
                {
                    var go = FindGameObject(instanceID);
                    if (go != null)
                        selection.Add(go);
                }
                if (Event.current.alt)
                {
                    var lastGo = FindGameObject(state.lastClickedID);
                    var lastBoneIndex = va.BonesIndexOf(lastGo);
                    va.ActionAllBoneChildren(lastBoneIndex, (boneIndex) =>
                    {
                        selection.Add(va.bones[boneIndex]);
                    });
                }
                {
                    var lastGo = FindGameObject(state.lastClickedID);
                    if (lastGo != null)
                        Selection.activeGameObject = lastGo;
                }
                va.SelectGameObjects(selection.ToArray());
            }

            protected override void DoubleClickedItem(int id)
            {
                if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.FrameSelected();
            }
            protected override bool CanStartDrag(CanStartDragArgs args)
            {
                return true;
            }
            protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
            {
                DragAndDrop.PrepareStartDrag();
                {
                    var list = new List<GameObject>();
                    foreach (var id in args.draggedItemIDs)
                    {
                        var go = EditorUtility.InstanceIDToObject(id) as GameObject;
                        if (go == null) continue;
                        list.Add(go);
                    }
                    DragAndDrop.objectReferences = list.ToArray();
                }
                DragAndDrop.StartDrag("Dragging GameObject");
            }
            protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
            {
                return DragAndDropVisualMode.Link;
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                if (va == null || va.isEditError || instance == null) return;

                var gameObject = EditorUtility.InstanceIDToObject(args.item.id) as GameObject;
                if (gameObject != null)
                {
                    var boneIndex = va.BonesIndexOf(gameObject);
                    if (boneIndex >= 0)
                    {
                        {
                            Rect toggleRect = args.rowRect;
                            toggleRect.x += GetContentIndent(args.item);
                            toggleRect.width = 16f;
                            {
                                EditorGUI.BeginChangeCheck();
                                var flag = EditorGUI.Toggle(toggleRect, va.boneShowFlags[boneIndex]);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(vaw, "Change bone show flag");
                                    va.boneShowFlags[boneIndex] = flag;
                                    if (Event.current.alt && args.item.hasChildren)
                                    {
                                        Action<TreeViewItem> SetChildren = null;
                                        SetChildren = (item) =>
                                        {
                                            var go = EditorUtility.InstanceIDToObject(item.id) as GameObject;
                                            if (go != null)
                                            {
                                                var bi = va.BonesIndexOf(go);
                                                if (bi >= 0)
                                                    va.boneShowFlags[bi] = flag;
                                            }
                                            if (item.hasChildren)
                                            {
                                                foreach (var i in item.children)
                                                    SetChildren(i);
                                            }
                                        };
                                        foreach (var i in args.item.children)
                                            SetChildren(i);
                                    }
                                    va.OnBoneShowFlagsUpdated.Invoke();
                                    InternalEditorUtility.RepaintAllViews();
                                }
                            }
                        }
                        float currentX = 0f;
                        if (vcw.mirrorObject)
                        {
                            Rect r = args.rowRect;
                            r.width = args.rowRect.width / 3f;
                            r.x = args.rowRect.width - r.width;
                            var mirrorBone = va.mirrorBoneIndexes[boneIndex] >= 0 ? va.bones[va.mirrorBoneIndexes[boneIndex]] : null;
                            EditorGUI.BeginDisabledGroup((va.isHuman && va.boneIndex2humanoidIndex[boneIndex] >= 0) || boneIndex == 0 || boneIndex == va.rootMotionBoneIndex);
                            EditorGUI.BeginChangeCheck();
                            var changeBone = EditorGUI.ObjectField(r, mirrorBone, typeof(GameObject), true) as GameObject;
                            if (EditorGUI.EndChangeCheck())
                            {
                                va.ChangeBonesMirror(boneIndex, va.BonesIndexOf(changeBone));
                            }
                            EditorGUI.EndDisabledGroup();
                            currentX += r.width;
                        }
                        if (va.mirrorBoneIndexes[boneIndex] >= 0)
                        {
                            const int IconWidth = 13;
                            const int IconHeight = 14;
                            {
                                var tex = vaw.mirrorTex;
                                Rect r = args.rowRect;
                                float Margin = (r.height - IconHeight) / 2f;
                                r.height -= Margin * 2f;
                                r.y += Margin;
                                r.width = IconWidth;
                                r.x = args.rowRect.width - (currentX + r.width);
                                if (GUI.Button(r, new GUIContent("", string.Format("Mirror bone: '{0}'", va.bones[va.mirrorBoneIndexes[boneIndex]].name)), vaw.guiStyleMirrorButton))
                                {
                                    va.SelectGameObject(va.bones[va.mirrorBoneIndexes[boneIndex]]);
                                }
                                currentX += r.width;
                            }
                        }
                    }
                }
                base.RowGUI(args);
            }
        };

        private TreeViewState hierarchyTreeState;
        private SearchField hierarchyTreeSearchField;
        private HierarchyTreeView hierarchyTreeView;

        private void UpdateHierarchyTree()
        {
            var expandList = hierarchyTreeView.GetExpanded();

            hierarchyTreeView.Reload();

            hierarchyTreeView.CollapseAll();
            hierarchyTreeView.SetExpanded(expandList);
        }

        private bool hierarchyButtonAll;
        private bool hierarchyButtonWeight;
        private bool hierarchyButtonRenderer;
        private bool hierarchyButtonRendererParent;
        private bool hierarchyButtonBody;
        private bool hierarchyButtonFace;
        private bool hierarchyButtonLeftHand;
        private bool hierarchyButtonRightHand;

        public void ActionAllExpand(Action<GameObject> action)
        {
            foreach (var id in hierarchyTreeView.GetExpanded())
            {
                var go = EditorUtility.InstanceIDToObject(id) as GameObject;
                if (go != null)
                {
                    action(go);
                }
            }
        }
        public void CollapseAll()
        {
            hierarchyTreeView.CollapseAll();
        }
        public void SetExpand(GameObject go, bool expanded)
        {
            hierarchyTreeView.SetExpanded(go.GetInstanceID(), expanded);
        }
        #endregion

        #region SelectionRect
        private struct SelectionRect
        {
            public void Reset()
            {
                Enable = false;
                start = Vector2.zero;
                end = Vector2.zero;
                distance = 0f;
                if (calcList == null)
                    calcList = new List<HumanBodyBones>();
                else
                    calcList.Clear();
                beforeSelection = null;
            }
            public void SetStart(Vector2 add)
            {
                Enable = true;
                start = add;
                end = add;
                distance = 0f;
            }
            public void SetEnd(Vector2 add)
            {
                distance += Vector2.Distance(end, add);
                end = add;
            }
            public bool Enable { get; private set; }
            public Vector2 min { get { return Vector2.Min(start, end); } }
            public Vector2 max { get { return Vector2.Max(start, end); } }
            public Rect rect { get { return new Rect(min.x, min.y, max.x - min.x, max.y - min.y); } }

            public Vector2 start { get; private set; }
            public Vector2 end { get; private set; }
            public float distance { get; private set; }

            public List<HumanBodyBones> calcList;
            public HumanBodyBones[] beforeSelection;
        }
        private SelectionRect selectionRect;
        #endregion

        private bool initialized;

        void OnEnable()
        {
            if (vaw == null || va == null) return;

            instance = this;

            titleContent = new GUIContent("VA Control");
            avatarHead = EditorGUIUtility.IconContent("avatarinspector/head").image as Texture2D;
            avatarTorso = EditorGUIUtility.IconContent("avatarinspector/torso").image as Texture2D;
            avatarLeftArm = EditorGUIUtility.IconContent("avatarinspector/leftarm").image as Texture2D;
            avatarLeftFingers = EditorGUIUtility.IconContent("avatarinspector/leftfingers").image as Texture2D;
            avatarLeftLeg = EditorGUIUtility.IconContent("avatarinspector/leftleg").image as Texture2D;
            avatarRightArm = EditorGUIUtility.IconContent("avatarinspector/rightarm").image as Texture2D;
            avatarRightFingers = EditorGUIUtility.IconContent("avatarinspector/rightfingers").image as Texture2D;
            avatarRightLeg = EditorGUIUtility.IconContent("avatarinspector/rightleg").image as Texture2D;
            avatarHeadZoom = EditorGUIUtility.IconContent("avatarinspector/headzoom").image as Texture2D;
            avatarLeftHandZoom = EditorGUIUtility.IconContent("avatarinspector/lefthandzoom").image as Texture2D;
            avatarRightHandZoom = EditorGUIUtility.IconContent("avatarinspector/righthandzoom").image as Texture2D;
            avatarBodysilhouette = EditorGUIUtility.IconContent("avatarinspector/bodysilhouette").image as Texture2D;
            avatarHeadzoomsilhouette = EditorGUIUtility.IconContent("avatarinspector/headzoomsilhouette").image as Texture2D;
            avatarLefthandzoomsilhouette = EditorGUIUtility.IconContent("avatarinspector/lefthandzoomsilhouette").image as Texture2D;
            avatarRighthandzoomsilhouette = EditorGUIUtility.IconContent("avatarinspector/righthandzoomsilhouette").image as Texture2D;
            avatarRoot = EditorGUIUtility.IconContent("avatarinspector/MaskEditor_Root").image as Texture2D;
            avatarLeftFeetIk = EditorGUIUtility.IconContent("avatarinspector/leftfeetik").image as Texture2D;
            avatarRightFeetIk = EditorGUIUtility.IconContent("avatarinspector/rightfeetik").image as Texture2D;
            avatarLeftFingersIk = EditorGUIUtility.IconContent("avatarinspector/leftfingersik").image as Texture2D;
            avatarRightFingersIk = EditorGUIUtility.IconContent("avatarinspector/rightfingersik").image as Texture2D;
            avatarBodyPartPicker = EditorGUIUtility.IconContent("avatarinspector/bodypartpicker").image as Texture2D;
            dotfill = EditorGUIUtility.IconContent("avatarinspector/dotfill").image as Texture2D;
            dotframe = EditorGUIUtility.IconContent("avatarinspector/dotframe").image as Texture2D;
            dotframedotted = EditorGUIUtility.IconContent("avatarinspector/dotframedotted").image as Texture2D;
            dotselection = EditorGUIUtility.IconContent("avatarinspector/dotselection").image as Texture2D;

            {
                var uBodyMaskEditor = new UBodyMaskEditor();
                maskBodyPartPicker = uBodyMaskEditor.GetMaskBodyPartPicker();
            }
            
            {
                hierarchyTreeState = new TreeViewState();
                hierarchyTreeSearchField = new SearchField();
                hierarchyTreeView = new HierarchyTreeView(hierarchyTreeState);
                hierarchyTreeSearchField.downOrUpArrowKeyPressed += hierarchyTreeView.SetFocusAndEnsureSelectedItem;
            }

            selectionGameObjectsHumanoidIndex = new List<HumanBodyBones>();
            controlBoneList = new Dictionary<HumanBodyBones, Vector2>();
            selectionAvatarMaskBodyPart = (AvatarMaskBodyPart)(-1);

            va.OnHierarchyUpdated += UpdateHierarchyTree;
            va.OnBoneShowFlagsUpdated += UpdateHierarchyFlags;

            GUIStyleClear();

            OnSelectionChange();

            Undo.undoRedoPerformed += UndoRedoPerformed;
        }
        void OnDisable()
        {
            if (vaw == null || va == null) return;

            Release();

            va.OnHierarchyUpdated -= UpdateHierarchyTree;
            va.OnBoneShowFlagsUpdated -= UpdateHierarchyFlags;

            Undo.undoRedoPerformed -= UndoRedoPerformed;

            GUIStyleClear();

            instance = null;

            if (vaw != null)
            {
                vaw.Release();
            }
        }
        void OnDestroy()
        {
            if (vaw != null)
            {
                vaw.Release();
            }
        }

        public void Initialize()
        {
            Release();

            #region EditorPref
            {
                guiAnimatorIkFoldout = EditorPrefs.GetBool("VeryAnimation_Control_AnimatorIK", false);
                guiOriginalIkFoldout = EditorPrefs.GetBool("VeryAnimation_Control_OriginalIK", false);
                guiHumanoidFoldout = EditorPrefs.GetBool("VeryAnimation_Control_Humanoid", true);
                guiSelectionFoldout = EditorPrefs.GetBool("VeryAnimation_Control_Selection", false);
                guiHierarchyFoldout = EditorPrefs.GetBool("VeryAnimation_Control_Hierarchy", true);

                guiAnimatorIkVisible = EditorPrefs.GetBool("VeryAnimation_Control_AnimatorIKVisible", true);
                guiOriginalIkVisible = EditorPrefs.GetBool("VeryAnimation_Control_OriginalIKVisible", true);
                guiHumanoidVisible = EditorPrefs.GetBool("VeryAnimation_Control_HumanoidVisible", true);
                guiSelectionVisible = EditorPrefs.GetBool("VeryAnimation_Control_SelectionVisible", true);
                guiHierarchyVisible = EditorPrefs.GetBool("VeryAnimation_Control_HierarchyVisible", true);

                selectionType = (SelectionType)EditorPrefs.GetInt("VeryAnimation_Control_SelectionType", 0);
                mirrorObject = EditorPrefs.GetBool("VeryAnimation_Control_HierarchyMirrorObject", false);
                humanoidName = EditorPrefs.GetBool("VeryAnimation_Control_HierarchyHumanoidName", true);
            }
            #endregion

            updateSelectionList = true;
            updateSelectionPopup = true;

            UpdateHierarchyTree();
            UpdateHierarchyFlags();

            hierarchyTreeView.ExpandAll();

            initialized = true;
        }
        private void Release()
        {
            if (!initialized) return;

            #region EditorPref
            {
                EditorPrefs.SetBool("VeryAnimation_Control_AnimatorIK", guiAnimatorIkFoldout);
                EditorPrefs.SetBool("VeryAnimation_Control_OriginalIK", guiOriginalIkFoldout);
                EditorPrefs.SetBool("VeryAnimation_Control_Humanoid", guiHumanoidFoldout);
                EditorPrefs.SetBool("VeryAnimation_Control_Selection", guiSelectionFoldout);
                EditorPrefs.SetBool("VeryAnimation_Control_Hierarchy", guiHierarchyFoldout);

                EditorPrefs.SetBool("VeryAnimation_Control_AnimatorIKVisible", guiAnimatorIkVisible);
                EditorPrefs.SetBool("VeryAnimation_Control_OriginalIKVisible", guiOriginalIkVisible);
                EditorPrefs.SetBool("VeryAnimation_Control_HumanoidVisible", guiHumanoidVisible);
                EditorPrefs.SetBool("VeryAnimation_Control_SelectionVisible", guiSelectionVisible);
                EditorPrefs.SetBool("VeryAnimation_Control_HierarchyVisible", guiHierarchyVisible);
            }
            #endregion
        }

        void OnSelectionChange()
        {
            if (va == null || va.isEditError) return;

            if (hierarchyTreeState != null)
            {
                List<int> selectedIDs = new List<int>();
                foreach (var go in Selection.gameObjects)
                {
                    selectedIDs.Add(go.GetInstanceID());
                    if (vaw.editorSettings.settingHierarchyExpandSelectObject)
                    {
                        var tmp = go.transform.parent;
                        while (tmp != null)
                        {
                            SetExpand(tmp.gameObject, true);
                            tmp = tmp.transform.parent;
                        }
                    }
                }
                hierarchyTreeState.selectedIDs = selectedIDs;

                if (vaw.editorSettings.settingHierarchyExpandSelectObject &&
                    Selection.activeGameObject != null)
                {
                    try
                    {
                        hierarchyTreeView.FrameItem(Selection.activeInstanceID);
                    }
                    catch
                    {
                    }
                }
            }

            if (guiSelectionFoldout)
            {
                EditorApplication.delayCall += () =>
                {
                    UpdateSelection();
                };
            }

            Repaint();
        }

        void OnInspectorUpdate()
        {
            if (vaw == null || va == null || !vaw.initialized || vae == null)
            {
                Close();
                return;
            }
        }

        void OnGUI()
        {
            if (va == null || va.isEditError || !vaw.guiStyleReady) return;

#if Enable_Profiler
            Profiler.BeginSample("****VeryAnimationControlWindow.OnGUI");
#endif

            GUIStyleReady();

            Event e = Event.current;
            bool repaint = false;

            #region Event
            switch (e.type)
            {
            case EventType.MouseUp:
                SceneView.RepaintAll();
                break;
            }
            #endregion

            windowScrollPosition = EditorGUILayout.BeginScrollView(windowScrollPosition);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                if (va.isHuman && guiAnimatorIkVisible)
                {
                    guiAnimatorIkFoldout = GUILayout.Toggle(guiAnimatorIkFoldout, "Animator IK", EditorStyles.toolbarButton);
                }
                if (guiOriginalIkVisible)
                {
                    guiOriginalIkFoldout = GUILayout.Toggle(guiOriginalIkFoldout, "Original IK", EditorStyles.toolbarButton);
                }
                if (va.isHuman && guiHumanoidVisible)
                {
                    guiHumanoidFoldout = GUILayout.Toggle(guiHumanoidFoldout, "Humanoid", EditorStyles.toolbarButton);
                }
                if (guiSelectionVisible)
                {
                    EditorGUI.BeginChangeCheck();
                    guiSelectionFoldout = GUILayout.Toggle(guiSelectionFoldout, "Selection", EditorStyles.toolbarButton);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (guiSelectionFoldout)
                            UpdateSelection();
                    }
                }
                if (guiHierarchyVisible)
                {
                    guiHierarchyFoldout = GUILayout.Toggle(guiHierarchyFoldout, "Hierarchy", EditorStyles.toolbarButton);
                }
                if (EditorGUILayout.DropdownButton(vaw.uEditorGUI.GetTitleSettingsIcon(), FocusType.Passive, vaw.guiStyleIconButton, GUILayout.Width(19)))
                {
                    GenericMenu menu = new GenericMenu();
                    if (va.isHuman)
                        menu.AddItem(new GUIContent("Animator IK"), guiAnimatorIkVisible, () => { guiAnimatorIkVisible = !guiAnimatorIkVisible; guiAnimatorIkFoldout = guiAnimatorIkVisible; });
                    menu.AddItem(new GUIContent("Original IK"), guiOriginalIkVisible, () => { guiOriginalIkVisible = !guiOriginalIkVisible; guiOriginalIkFoldout = guiOriginalIkVisible; });
                    if (va.isHuman)
                        menu.AddItem(new GUIContent("Humanoid"), guiHumanoidVisible, () => { guiHumanoidVisible = !guiHumanoidVisible; guiHumanoidFoldout = guiHumanoidVisible; });
                    menu.AddItem(new GUIContent("Selection"), guiSelectionVisible, () => { guiSelectionVisible = !guiSelectionVisible; guiSelectionFoldout = guiSelectionVisible; });
                    menu.AddItem(new GUIContent("Hierarchy"), guiHierarchyVisible, () => { guiHierarchyVisible = !guiHierarchyVisible; guiHierarchyFoldout = guiHierarchyVisible; });
                    menu.ShowAsContext();
                }
            }
            EditorGUILayout.EndHorizontal();

            #region AnimatorIK
            if (va.isHuman && guiAnimatorIkFoldout && guiAnimatorIkVisible)
            {
                EditorGUILayout.BeginHorizontal();
                guiAnimatorIkFoldout = EditorGUILayout.Foldout(guiAnimatorIkFoldout, "Animator IK", true, vaw.guiStyleBoldFoldout);
                {
                    EditorGUILayout.Space();
                    if (GUILayout.Button(vaw.uEditorGUI.GetHelpIcon(), guiAnimatorIkHelp ? vaw.guiStyleIconActiveButton : vaw.guiStyleIconButton, GUILayout.Width(19)))
                    {
                        guiAnimatorIkHelp = !guiAnimatorIkHelp;
                    }
#if VERYANIMATION_ANIMATIONRIGGING
                    if (EditorGUILayout.DropdownButton(vaw.uEditorGUI.GetTitleSettingsIcon(), FocusType.Passive, vaw.guiStyleIconButton, GUILayout.Width(19)))
                    {
                        var rigEnable = va.animationRigging.isValid;
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(Language.GetContent(Language.Help.AnimatorIKAnimationRiggingEnable), rigEnable, () =>
                        {
                            Undo.RecordObject(vaw, "Animation Rigging Enable");
                            va.animationRigging.Enable();
                        });
                        menu.AddItem(Language.GetContent(Language.Help.AnimatorIKAnimationRiggingDisable), !rigEnable, () =>
                        {
                            Undo.RecordObject(vaw, "Animation Rigging Disable");
                            va.animationRigging.Disable();
                        });
                        menu.ShowAsContext();
                    }
#endif
                }
                EditorGUILayout.EndHorizontal();
                {
                    if (guiAnimatorIkHelp)
                    {
                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.HelpAnimatorIK), MessageType.Info);
                    }
                    va.animatorIK.ControlGUI();
                }
            }
            #endregion

            #region OriginalIK
            if (guiOriginalIkFoldout && guiOriginalIkVisible)
            {
                EditorGUILayout.BeginHorizontal();
                guiOriginalIkFoldout = EditorGUILayout.Foldout(guiOriginalIkFoldout, "Original IK", true, vaw.guiStyleBoldFoldout);
                {
                    EditorGUILayout.Space();
                    if (GUILayout.Button(vaw.uEditorGUI.GetHelpIcon(), guiOriginalIkHelp ? vaw.guiStyleIconActiveButton : vaw.guiStyleIconButton, GUILayout.Width(19)))
                    {
                        guiOriginalIkHelp = !guiOriginalIkHelp;
                    }
                }
                EditorGUILayout.EndHorizontal();
                {
                    if (guiOriginalIkHelp)
                    {
                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.HelpOriginalIK), MessageType.Info);
                    }
                    va.originalIK.ControlGUI();
                }
            }
            #endregion

            #region Humanoid
            if (va.isHuman)
            {
                if (guiHumanoidFoldout && guiHumanoidVisible)
                {
                    if (e.type == EventType.Layout)
                    {
                        selectionGameObjectsHumanoidIndex.Clear();
                        if (va.SelectionGameObjectsIndexOf(vaw.gameObject) >= 0)
                            selectionGameObjectsHumanoidIndex.Add((HumanBodyBones)(-1));
                        foreach (var hi in va.SelectionGameObjectsHumanoidIndex())
                            selectionGameObjectsHumanoidIndex.Add(hi);
                    }
                    controlBoneList.Clear();
                    //
                    EditorGUILayout.BeginHorizontal();
                    guiHumanoidFoldout = EditorGUILayout.Foldout(guiHumanoidFoldout, "Humanoid", true, vaw.guiStyleBoldFoldout);
                    {
                        EditorGUILayout.Space();
                        if (GUILayout.Button(vaw.uEditorGUI.GetHelpIcon(), guiHumanoidHelp ? vaw.guiStyleIconActiveButton : vaw.guiStyleIconButton, GUILayout.Width(19)))
                        {
                            guiHumanoidHelp = !guiHumanoidHelp;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    {
                        if (guiHumanoidHelp)
                        {
                            EditorGUILayout.HelpBox(Language.GetText(Language.Help.HelpHumanoid), MessageType.Info);
                        }

                        Rect backgroundRect;
                        {
                            backgroundRect = EditorGUILayout.GetControlRect();
                            backgroundRect.height = avatarBodyPartPicker.height;
                            GUI.Box(backgroundRect, "", guiStyleBackgroundBox);
                            GUILayout.Space(backgroundRect.height - 16);
                        }

                        var saveGUIColor = GUI.color;
                        if (humanoidAvatarPartsMode == HumanoidAvatarPartsMode.Body)
                        {
                            #region Body
                            #region Root
                            GUI.color = va.SelectionGameObjectsIndexOf(vaw.gameObject) < 0 ? GreenColor : BlueColor;
                            GUI.DrawTexture(backgroundRect, avatarRoot, ScaleMode.ScaleToFit);
                            #endregion
                            #region BackGround
                            GUI.color = GlayColor;
                            GUI.DrawTexture(backgroundRect, avatarBodysilhouette, ScaleMode.ScaleToFit);
                            GUI.color = GreenColor;
                            GUI.DrawTexture(backgroundRect, avatarHead, ScaleMode.ScaleToFit);
                            GUI.DrawTexture(backgroundRect, avatarTorso, ScaleMode.ScaleToFit);
                            GUI.DrawTexture(backgroundRect, avatarLeftArm, ScaleMode.ScaleToFit);
                            GUI.DrawTexture(backgroundRect, avatarLeftFingers, ScaleMode.ScaleToFit);
                            GUI.DrawTexture(backgroundRect, avatarLeftLeg, ScaleMode.ScaleToFit);
                            GUI.DrawTexture(backgroundRect, avatarRightArm, ScaleMode.ScaleToFit);
                            GUI.DrawTexture(backgroundRect, avatarRightFingers, ScaleMode.ScaleToFit);
                            GUI.DrawTexture(backgroundRect, avatarRightLeg, ScaleMode.ScaleToFit);
                            #endregion
                            #region IK
                            {
                                Func<AnimatorIKCore.IKTarget, Color> GetIKTargetColor = (t) =>
                                {
                                    if (!va.animatorIK.ikData[(int)t].enable)
                                        return GlayColor;
                                    else if (va.animatorIK.ikTargetSelect != null && EditorCommon.ArrayContains(va.animatorIK.ikTargetSelect, t))
                                        return BlueColor;
                                    else
                                        return GreenColor;
                                };
                                Action<AnimatorIKCore.IKTarget, Vector2> IKTragetToggle = (t, position) =>
                                {
                                    Rect rect = new Rect(position, new Vector2(GUI.skin.toggle.border.horizontal, GUI.skin.toggle.border.vertical));
                                    GUI.color = Color.white;
                                    EditorGUI.BeginChangeCheck();
                                    EditorGUI.Toggle(rect, va.animatorIK.ikData[(int)t].enable);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        va.animatorIK.ChangeTargetIK(t);
                                    }
                                };
                                {
                                    GUI.color = GetIKTargetColor(AnimatorIKCore.IKTarget.LeftFoot);
                                    GUI.DrawTexture(backgroundRect, avatarLeftFeetIk, ScaleMode.ScaleToFit);
                                    IKTragetToggle(AnimatorIKCore.IKTarget.LeftFoot, new Vector2(backgroundRect.center.x + 86, backgroundRect.y + 355));
                                }
                                {
                                    GUI.color = GetIKTargetColor(AnimatorIKCore.IKTarget.RightFoot);
                                    GUI.DrawTexture(backgroundRect, avatarRightFeetIk, ScaleMode.ScaleToFit);
                                    IKTragetToggle(AnimatorIKCore.IKTarget.RightFoot, new Vector2(backgroundRect.center.x - 100, backgroundRect.y + 355));
                                }
                                {
                                    GUI.color = GetIKTargetColor(AnimatorIKCore.IKTarget.LeftHand);
                                    GUI.DrawTexture(backgroundRect, avatarLeftFingersIk, ScaleMode.ScaleToFit);
                                    IKTragetToggle(AnimatorIKCore.IKTarget.LeftHand, new Vector2(backgroundRect.center.x + 76, backgroundRect.y + 220));
                                }
                                {
                                    GUI.color = GetIKTargetColor(AnimatorIKCore.IKTarget.RightHand);
                                    GUI.DrawTexture(backgroundRect, avatarRightFingersIk, ScaleMode.ScaleToFit);
                                    IKTragetToggle(AnimatorIKCore.IKTarget.RightHand, new Vector2(backgroundRect.center.x - 90, backgroundRect.y + 220));
                                }
                                {
                                    GUI.color = GetIKTargetColor(AnimatorIKCore.IKTarget.Head);
                                    var rect = backgroundRect;
                                    rect.center -= new Vector2(-12f, 212f);
                                    GUI.DrawTexture(rect, avatarRightFingersIk, ScaleMode.ScaleToFit);
                                    IKTragetToggle(AnimatorIKCore.IKTarget.Head, new Vector2(backgroundRect.center.x - 79, backgroundRect.y + 8));
                                }
                            }
                            #endregion
                            #region Bone
                            {
                                var position = backgroundRect.center;
                                position.y = backgroundRect.y - 19;
                                HumanoidControlBoneGUI(new Vector2(position.x, position.y + 191), HumanBodyBones.Hips);
                                HumanoidControlBoneGUI(new Vector2(position.x, position.y + 170), HumanBodyBones.Spine);
                                HumanoidControlBoneGUI(new Vector2(position.x, position.y + 140), HumanBodyBones.Chest);
                                HumanoidControlBoneGUI(new Vector2(position.x, position.y + 112), HumanBodyBones.UpperChest);
                                HumanoidControlBoneGUI(new Vector2(position.x, position.y + 82), HumanBodyBones.Neck);
                                HumanoidControlBoneGUI(new Vector2(position.x, position.y + 63), HumanBodyBones.Head);
                                HumanoidControlBoneGUI(new Vector2(position.x + 12, position.y + 93), HumanBodyBones.LeftShoulder);
                                HumanoidControlBoneGUI(new Vector2(position.x - 12, position.y + 93), HumanBodyBones.RightShoulder);
                                HumanoidControlBoneGUI(new Vector2(position.x + 27, position.y + 99), HumanBodyBones.LeftUpperArm);
                                HumanoidControlBoneGUI(new Vector2(position.x + 43, position.y + 150), HumanBodyBones.LeftLowerArm);
                                HumanoidControlBoneGUI(new Vector2(position.x + 59, position.y + 201), HumanBodyBones.LeftHand);
                                HumanoidControlBoneGUI(new Vector2(position.x - 27, position.y + 99), HumanBodyBones.RightUpperArm);
                                HumanoidControlBoneGUI(new Vector2(position.x - 43, position.y + 150), HumanBodyBones.RightLowerArm);
                                HumanoidControlBoneGUI(new Vector2(position.x - 59, position.y + 201), HumanBodyBones.RightHand);
                                HumanoidControlBoneGUI(new Vector2(position.x + 14, position.y + 205), HumanBodyBones.LeftUpperLeg);
                                HumanoidControlBoneGUI(new Vector2(position.x + 18, position.y + 282), HumanBodyBones.LeftLowerLeg);
                                HumanoidControlBoneGUI(new Vector2(position.x + 20, position.y + 358), HumanBodyBones.LeftFoot);
                                HumanoidControlBoneGUI(new Vector2(position.x - 14, position.y + 205), HumanBodyBones.RightUpperLeg);
                                HumanoidControlBoneGUI(new Vector2(position.x - 18, position.y + 282), HumanBodyBones.RightLowerLeg);
                                HumanoidControlBoneGUI(new Vector2(position.x - 20, position.y + 358), HumanBodyBones.RightFoot);
                                HumanoidControlBoneGUI(new Vector2(position.x + 23, position.y + 375), HumanBodyBones.LeftToes);
                                HumanoidControlBoneGUI(new Vector2(position.x - 23, position.y + 375), HumanBodyBones.RightToes);

                                controlBoneList.Add((HumanBodyBones)(-1), new Vector2(position.x, position.y + 372));   //Root
                            }
                            #endregion
                            #endregion
                        }
                        else if (humanoidAvatarPartsMode == HumanoidAvatarPartsMode.Head)
                        {
                            #region Head
                            #region BackGround
                            GUI.color = GlayColor;
                            GUI.DrawTexture(backgroundRect, avatarHeadzoomsilhouette, ScaleMode.ScaleToFit);
                            //base
                            {
                                GUI.color = GreenColor;
                                GUI.DrawTexture(backgroundRect, avatarHeadZoom, ScaleMode.ScaleToFit);
                            }
                            #endregion
                            #region Bone
                            {
                                var position = backgroundRect.center;
                                position.y = backgroundRect.y - 19;
                                HumanoidControlBoneGUI(new Vector2(position.x - 14, position.y + 263), HumanBodyBones.Head);
                                HumanoidControlBoneGUI(new Vector2(position.x - 18, position.y + 324), HumanBodyBones.Neck);
                                HumanoidControlBoneGUI(new Vector2(position.x + 56, position.y + 176), HumanBodyBones.LeftEye);
                                HumanoidControlBoneGUI(new Vector2(position.x + 13, position.y + 176), HumanBodyBones.RightEye);
                                HumanoidControlBoneGUI(new Vector2(position.x + 40, position.y + 282), HumanBodyBones.Jaw);
                            }
                            #endregion
                            #endregion
                        }
                        else if (humanoidAvatarPartsMode == HumanoidAvatarPartsMode.LeftHand)
                        {
                            #region LeftHand
                            #region BackGround
                            GUI.color = GlayColor;
                            GUI.DrawTexture(backgroundRect, avatarLefthandzoomsilhouette, ScaleMode.ScaleToFit);
                            //base
                            GUI.color = va.humanoidHasLeftHand ? GreenColor : GlayColor;
                            GUI.DrawTexture(backgroundRect, avatarLeftHandZoom, ScaleMode.ScaleToFit);
                            #endregion
                            #region Bone
                            {
                                var position = backgroundRect.center;
                                position.y = backgroundRect.y - 19;
                                HumanoidControlBoneGUI(new Vector2(position.x - 42, position.y + 186), HumanBodyBones.LeftThumbProximal);
                                HumanoidControlBoneGUI(new Vector2(position.x - 20, position.y + 162), HumanBodyBones.LeftThumbIntermediate);
                                HumanoidControlBoneGUI(new Vector2(position.x - 4, position.y + 144), HumanBodyBones.LeftThumbDistal);
                                HumanoidControlBoneGUI(new Vector2(position.x + 22, position.y + 186), HumanBodyBones.LeftIndexProximal);
                                HumanoidControlBoneGUI(new Vector2(position.x + 54, position.y + 179), HumanBodyBones.LeftIndexIntermediate);
                                HumanoidControlBoneGUI(new Vector2(position.x + 78, position.y + 175), HumanBodyBones.LeftIndexDistal);
                                HumanoidControlBoneGUI(new Vector2(position.x + 26, position.y + 207), HumanBodyBones.LeftMiddleProximal);
                                HumanoidControlBoneGUI(new Vector2(position.x + 62, position.y + 207), HumanBodyBones.LeftMiddleIntermediate);
                                HumanoidControlBoneGUI(new Vector2(position.x + 88, position.y + 207), HumanBodyBones.LeftMiddleDistal);
                                HumanoidControlBoneGUI(new Vector2(position.x + 19, position.y + 229), HumanBodyBones.LeftRingProximal);
                                HumanoidControlBoneGUI(new Vector2(position.x + 54, position.y + 230), HumanBodyBones.LeftRingIntermediate);
                                HumanoidControlBoneGUI(new Vector2(position.x + 79, position.y + 232), HumanBodyBones.LeftRingDistal);
                                HumanoidControlBoneGUI(new Vector2(position.x + 10, position.y + 250), HumanBodyBones.LeftLittleProximal);
                                HumanoidControlBoneGUI(new Vector2(position.x + 35, position.y + 251), HumanBodyBones.LeftLittleIntermediate);
                                HumanoidControlBoneGUI(new Vector2(position.x + 54, position.y + 253), HumanBodyBones.LeftLittleDistal);
                            }
                            #endregion
                            #endregion
                        }
                        else if (humanoidAvatarPartsMode == HumanoidAvatarPartsMode.RightHand)
                        {
                            #region RightHand
                            #region BackGround
                            GUI.color = GlayColor;
                            GUI.DrawTexture(backgroundRect, avatarRighthandzoomsilhouette, ScaleMode.ScaleToFit);
                            //base
                            GUI.color = va.humanoidHasRightHand ? GreenColor : GlayColor;
                            GUI.DrawTexture(backgroundRect, avatarRightHandZoom, ScaleMode.ScaleToFit);
                            #endregion
                            #region Bone
                            {
                                var position = backgroundRect.center;
                                position.y = backgroundRect.y - 19;
                                HumanoidControlBoneGUI(new Vector2(position.x + 42, position.y + 186), HumanBodyBones.RightThumbProximal);
                                HumanoidControlBoneGUI(new Vector2(position.x + 20, position.y + 162), HumanBodyBones.RightThumbIntermediate);
                                HumanoidControlBoneGUI(new Vector2(position.x + 4, position.y + 144), HumanBodyBones.RightThumbDistal);
                                HumanoidControlBoneGUI(new Vector2(position.x - 22, position.y + 186), HumanBodyBones.RightIndexProximal);
                                HumanoidControlBoneGUI(new Vector2(position.x - 54, position.y + 179), HumanBodyBones.RightIndexIntermediate);
                                HumanoidControlBoneGUI(new Vector2(position.x - 78, position.y + 175), HumanBodyBones.RightIndexDistal);
                                HumanoidControlBoneGUI(new Vector2(position.x - 26, position.y + 207), HumanBodyBones.RightMiddleProximal);
                                HumanoidControlBoneGUI(new Vector2(position.x - 62, position.y + 207), HumanBodyBones.RightMiddleIntermediate);
                                HumanoidControlBoneGUI(new Vector2(position.x - 88, position.y + 207), HumanBodyBones.RightMiddleDistal);
                                HumanoidControlBoneGUI(new Vector2(position.x - 19, position.y + 229), HumanBodyBones.RightRingProximal);
                                HumanoidControlBoneGUI(new Vector2(position.x - 54, position.y + 230), HumanBodyBones.RightRingIntermediate);
                                HumanoidControlBoneGUI(new Vector2(position.x - 79, position.y + 232), HumanBodyBones.RightRingDistal);
                                HumanoidControlBoneGUI(new Vector2(position.x - 10, position.y + 250), HumanBodyBones.RightLittleProximal);
                                HumanoidControlBoneGUI(new Vector2(position.x - 35, position.y + 251), HumanBodyBones.RightLittleIntermediate);
                                HumanoidControlBoneGUI(new Vector2(position.x - 54, position.y + 253), HumanBodyBones.RightLittleDistal);
                            }
                            #endregion
                            #endregion
                        }
                        GUI.color = saveGUIColor;

                        #region Toolbar
                        {
                            Rect rect = backgroundRect;
                            {
                                rect.position = new Vector2(backgroundRect.position.x + 5, backgroundRect.position.y + 308);
                                rect.width = 70;
                                rect.height = 64;
                            }
                            humanoidAvatarPartsMode = (HumanoidAvatarPartsMode)GUI.SelectionGrid(rect, (int)humanoidAvatarPartsMode, HumanoidAvatarPartsModeStrings, 1, guiStyleVerticalToolbar);
                        }
                        #endregion

                        #region Event
                        switch (e.type)
                        {
                        case EventType.MouseDown:
                            if (e.button == 0)
                            {
                                selectionRect.Reset();
                                selectionAvatarMaskBodyPart = (AvatarMaskBodyPart)(-1);
                                if (GUIUtility.hotControl == 0 && backgroundRect.Contains(e.mousePosition))
                                {
                                    var pos = e.mousePosition - backgroundRect.min;
                                    pos.x -= (backgroundRect.width - avatarBodyPartPicker.width) / 2f;
                                    if (humanoidAvatarPartsMode == HumanoidAvatarPartsMode.Body &&
                                        pos.x >= 0f && pos.x < avatarBodyPartPicker.width &&
                                        pos.y >= 0f && pos.x < avatarBodyPartPicker.height)
                                    {
                                        var pixel = avatarBodyPartPicker.GetPixel((int)pos.x, avatarBodyPartPicker.height - (int)pos.y);
                                        selectionAvatarMaskBodyPart = (AvatarMaskBodyPart)EditorCommon.ArrayIndexOf(maskBodyPartPicker, pixel);
                                        switch (selectionAvatarMaskBodyPart)
                                        {
                                        case AvatarMaskBodyPart.Root:
                                            va.SelectGameObjectPlusKey(vaw.gameObject);
                                            break;
                                        case AvatarMaskBodyPart.LeftFootIK:
                                            if (va.animatorIK.ikData[(int)AnimatorIKCore.IKTarget.LeftFoot].enable)
                                                va.SelectAnimatorIKTargetPlusKey(AnimatorIKCore.IKTarget.LeftFoot);
                                            break;
                                        case AvatarMaskBodyPart.RightFootIK:
                                            if (va.animatorIK.ikData[(int)AnimatorIKCore.IKTarget.RightFoot].enable)
                                                va.SelectAnimatorIKTargetPlusKey(AnimatorIKCore.IKTarget.RightFoot);
                                            break;
                                        case AvatarMaskBodyPart.LeftHandIK:
                                            if (va.animatorIK.ikData[(int)AnimatorIKCore.IKTarget.LeftHand].enable)
                                                va.SelectAnimatorIKTargetPlusKey(AnimatorIKCore.IKTarget.LeftHand);
                                            break;
                                        case AvatarMaskBodyPart.RightHandIK:
                                            if (va.animatorIK.ikData[(int)AnimatorIKCore.IKTarget.RightHand].enable)
                                                va.SelectAnimatorIKTargetPlusKey(AnimatorIKCore.IKTarget.RightHand);
                                            break;
                                        case AvatarMaskBodyPart.LastBodyPart:
                                            if (va.animatorIK.ikData[(int)AnimatorIKCore.IKTarget.Head].enable)
                                                va.SelectAnimatorIKTargetPlusKey(AnimatorIKCore.IKTarget.Head);
                                            break;
                                        default:
                                            va.SelectGameObject(null);
                                            break;
                                        }
                                    }
                                    {
                                        selectionRect.SetStart(e.mousePosition);
                                        if (Shortcuts.IsKeyControl(e) || e.shift)
                                        {
                                            selectionRect.beforeSelection = selectionGameObjectsHumanoidIndex.ToArray();
                                        }
                                    }
                                    e.Use();
                                    repaint = true;
                                }
                            }
                            break;
                        case EventType.MouseUp:
                            if (e.button == 0)
                            {
                                if (backgroundRect.Contains(e.mousePosition))
                                {
                                    if (selectionAvatarMaskBodyPart < 0 && (!selectionRect.Enable || selectionRect.distance <= 0f) && selectionRect.beforeSelection == null)
                                    {
                                        va.SelectGameObject(null);
                                    }
                                    selectionRect.Reset();
                                    selectionAvatarMaskBodyPart = (AvatarMaskBodyPart)(-1);
                                    repaint = true;
                                }
                                else if (selectionRect.Enable)
                                {
                                    selectionRect.Reset();
                                    selectionAvatarMaskBodyPart = (AvatarMaskBodyPart)(-1);
                                    repaint = true;
                                }
                            }
                            break;
                        case EventType.MouseDrag:
                            if (e.button == 0)
                            {
                                if (selectionRect.Enable)
                                {
                                    if (GUIUtility.hotControl == 0)
                                    {
                                        {
                                            var rect = position;
                                            rect.position -= rect.position;
                                            if (rect.Contains(e.mousePosition - windowScrollPosition))
                                            {
                                                var pos = e.mousePosition;
                                                pos.x = Mathf.Clamp(pos.x, backgroundRect.xMin, backgroundRect.xMax);
                                                pos.y = Mathf.Clamp(pos.y, backgroundRect.yMin, backgroundRect.yMax);
                                                selectionRect.SetEnd(pos);
                                            }
                                            else
                                            {
                                                selectionRect.Reset();
                                            }
                                        }
                                        #region Selection
                                        if (selectionRect.Enable)
                                        {
                                            List<HumanBodyBones> oldCalcList = new List<HumanBodyBones>(selectionRect.calcList);
                                            selectionRect.calcList.Clear();
                                            var rect = selectionRect.rect;
                                            foreach (var pair in controlBoneList)
                                            {
                                                if (rect.Contains(pair.Value))
                                                    selectionRect.calcList.Add(pair.Key);
                                            }
                                            if ((Shortcuts.IsKeyControl(e) || e.shift) && selectionRect.beforeSelection != null)
                                            {
                                                if (e.shift)
                                                {
                                                    foreach (var hi in selectionRect.beforeSelection)
                                                    {
                                                        if (!selectionRect.calcList.Contains(hi))
                                                            selectionRect.calcList.Add(hi);
                                                    }
                                                }
                                                else if (Shortcuts.IsKeyControl(e))
                                                {
                                                    foreach (var hi in selectionRect.beforeSelection)
                                                    {
                                                        if (!controlBoneList.ContainsKey(hi)) continue;
                                                        if (!rect.Contains(controlBoneList[hi]))
                                                        {
                                                            if (!selectionRect.calcList.Contains(hi))
                                                                selectionRect.calcList.Add(hi);
                                                        }
                                                        else
                                                        {
                                                            selectionRect.calcList.Remove(hi);
                                                        }
                                                    }
                                                }
                                            }
                                            bool selectionChange = oldCalcList.Count != selectionRect.calcList.Count;
                                            if (!selectionChange)
                                            {
                                                for (int i = 0; i < selectionRect.calcList.Count; i++)
                                                {
                                                    if (oldCalcList[i] != selectionRect.calcList[i])
                                                    {
                                                        selectionChange = true;
                                                        break;
                                                    }
                                                }
                                            }
                                            if (selectionChange)
                                            {
                                                va.SelectHumanoidBones(selectionRect.calcList.ToArray());
                                                ForceSelectionChange();
                                            }
                                        }
                                        #endregion
                                    }
                                    else
                                    {
                                        selectionRect.Reset();
                                    }
                                }
                                repaint = true;
                            }
                            break;
                        }

                        #region SelectionRect
                        if (selectionRect.Enable && selectionRect.rect.width > 0f && selectionRect.rect.height > 0f)
                        {
                            GUI.Box(selectionRect.rect, "", "SelectionRect");
                        }
                        #endregion
                        #endregion
                    }
                }
            }
            #endregion

            #region Selection
            if (guiSelectionFoldout && guiSelectionVisible)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                guiSelectionFoldout = EditorGUILayout.Foldout(guiSelectionFoldout, "Selection", true, vaw.guiStyleBoldFoldout);
                if (EditorGUI.EndChangeCheck())
                {
                    if (guiSelectionFoldout)
                        UpdateSelection();
                }
                EditorGUILayout.Space();
                {
                    EditorGUI.BeginChangeCheck();
                    var type = (SelectionType)GUILayout.Toolbar((int)selectionType, SelectionTypeString, EditorStyles.miniButton);
                    if (EditorGUI.EndChangeCheck())
                    {
                        selectionType = type;
                        EditorPrefs.SetInt("VeryAnimation_Control_SelectionType", (int)selectionType);
                        switch (selectionType)
                        {
                        case SelectionType.List:
                            updateSelectionList = true;
                            break;
                        case SelectionType.Popup:
                            updateSelectionPopup = true;
                            break;
                        }
                    }
                }
                {
                    if (GUILayout.Button(vaw.uEditorGUI.GetHelpIcon(), guiSelectionHelp ? vaw.guiStyleIconActiveButton : vaw.guiStyleIconButton, GUILayout.Width(19)))
                    {
                        guiSelectionHelp = !guiSelectionHelp;
                    }
                }
                EditorGUILayout.EndHorizontal();
                {
                    if (guiSelectionHelp)
                    {
                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.HelpSelectionSet), MessageType.Info);
                    }
                    else
                    {
                        GUILayout.Space(2f);
                    }
                    SelectionGUI();
                }
            }
            #endregion

            #region Hierarchy
            if (guiHierarchyFoldout && guiHierarchyVisible)
            {
                EditorGUILayout.BeginHorizontal();
                guiHierarchyFoldout = EditorGUILayout.Foldout(guiHierarchyFoldout, "Hierarchy", true, vaw.guiStyleBoldFoldout);
                {
                    EditorGUILayout.Space();
                    if (GUILayout.Button(vaw.uEditorGUI.GetHelpIcon(), guiHierarchyHelp ? vaw.guiStyleIconActiveButton : vaw.guiStyleIconButton, GUILayout.Width(19)))
                    {
                        guiHierarchyHelp = !guiHierarchyHelp;
                    }
                    if (EditorGUILayout.DropdownButton(vaw.uEditorGUI.GetTitleSettingsIcon(), FocusType.Passive, vaw.guiStyleIconButton, GUILayout.Width(19)))
                    {
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(Language.GetContent(Language.Help.HierarchyMirrorObject), mirrorObject, () =>
                        {
                            mirrorObject = !mirrorObject;
                            EditorPrefs.SetBool("VeryAnimation_Control_HierarchyMirrorObject", mirrorObject);
                        });
                        menu.AddItem(Language.GetContent(Language.Help.HierarchyMirrorAutomap), false, () =>
                        {
                            va.BonesMirrorAutomap();
                            InternalEditorUtility.RepaintAllViews();
                        });
                        menu.AddItem(Language.GetContent(Language.Help.HierarchyMirrorClear), false, () =>
                        {
                            va.BonesMirrorInitialize();
                            InternalEditorUtility.RepaintAllViews();
                        });
                        if (va.isHuman)
                        {
                            menu.AddSeparator(string.Empty);
                            menu.AddItem(Language.GetContent(Language.Help.HierarchyHumanoidName), humanoidName, () =>
                            {
                                humanoidName = !humanoidName;
                                EditorPrefs.SetBool("VeryAnimation_Control_HierarchyHumanoidName", humanoidName);
                                UpdateHierarchyTree();
                            });
                        }
                        menu.ShowAsContext();
                    }
                }
                EditorGUILayout.EndHorizontal();
                {
                    if (guiHierarchyHelp)
                    {
                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.HelpHierarchy), MessageType.Info);
                    }
                    else
                    {
                        GUILayout.Space(2f);
                    }
                    HierarchyToolBarGUI();
                    {
                        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                        hierarchyTreeView.searchString = hierarchyTreeSearchField.OnToolbarGUI(hierarchyTreeView.searchString);
                        EditorGUILayout.LabelField(new GUIContent(string.Format("{0} / {1}", va.boneShowCount, va.boneShowFlags.Length), "Show / All"), vaw.guiStyleMiddleRightMiniLabel, GUILayout.Width(60f));
                        EditorGUILayout.EndHorizontal();
                    }
                    {
                        var rect = EditorGUILayout.GetControlRect(false, 0);
                        rect.height = Math.Max(position.height - rect.y, 0);
                        hierarchyTreeView.OnGUI(rect);
                    }
                }
            }
            #endregion

            EditorGUILayout.EndScrollView();

            if (repaint)
            {
                Repaint();
            }

#if Enable_Profiler
            Profiler.EndSample();
#endif
        }

        private void GUIStyleReady()
        {
            if (guiStyleBackgroundBox == null)
            {
                guiStyleBackgroundBox = new GUIStyle("CurveEditorBackground");
            }
            if (guiStyleVerticalToolbar == null)
            {
                guiStyleVerticalToolbar = new GUIStyle(GUI.skin.button);
                guiStyleVerticalToolbar.margin = new RectOffset(0, 0, 0, 0);
                guiStyleVerticalToolbar.fontSize = 9;
            }
            if (guiStyleBoneButton == null)
            {
                guiStyleBoneButton = new GUIStyle(GUI.skin.button);
                guiStyleBoneButton.border = new RectOffset(0, 0, 0, 0);
                guiStyleBoneButton.margin = new RectOffset(0, 0, 0, 0);
                guiStyleBoneButton.overflow = new RectOffset(0, 0, 0, 0);
                guiStyleBoneButton.padding = new RectOffset(0, 0, 0, 0);
                guiStyleBoneButton.active = guiStyleBoneButton.normal;
            }
        }
        private void GUIStyleClear()
        {
            guiStyleBackgroundBox = null;
            guiStyleVerticalToolbar = null;
            guiStyleBoneButton = null;
        }
        
        private void SelectionGUI()
        {
            var e = Event.current;

            EditorGUILayout.BeginVertical(vaw.guiStyleSkinBox);
            {
                if (selectionType == SelectionType.List)
                {
                    #region List
                    if (Event.current.type == EventType.Layout && updateSelectionList)
                    {
                        #region SelectionSet
                        selectionSetList = null;
                        if (va.selectionSetList != null)
                        {
                            selectionSetList = new ReorderableList(va.selectionSetList, typeof(VeryAnimationSaveSettings.SelectionData), true, true, true, true);
                            selectionSetList.drawHeaderCallback = (Rect rect) =>
                            {
                                float x = rect.x;
                                {
                                    const float ButtonWidth = 100f;
                                    #region Add
                                    {
                                        var r = rect;
                                        r.width = ButtonWidth;
                                        if (GUI.Button(r, Language.GetContent(Language.Help.SelectionSetTemplate), EditorStyles.toolbarDropDown))
                                        {
                                            var selectionTemplates = new Dictionary<string, string>();
                                            {
                                                var guids = AssetDatabase.FindAssets("t:selectiontemplate");
                                                for (int i = 0; i < guids.Length; i++)
                                                {
                                                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                                                    var name = path.Remove(0, "Assets/".Length);
                                                    selectionTemplates.Add(name, path);
                                                }
                                            }

                                            var menu = new GenericMenu();
                                            {
                                                var enu = selectionTemplates.GetEnumerator();
                                                while (enu.MoveNext())
                                                {
                                                    var value = enu.Current.Value;
                                                    menu.AddItem(new GUIContent(enu.Current.Key), false, () =>
                                                    {
                                                        var selectionTemplate = AssetDatabase.LoadAssetAtPath<SelectionTemplate>(value);
                                                        if (selectionTemplate != null)
                                                        {
                                                            Undo.RecordObject(vaw, "Template Selection");
                                                            va.selectionSetList.AddRange(va.LoadSelectionSaveSettings(selectionTemplate.selectionData));
                                                            updateSelectionList = true;
                                                        }
                                                    });
                                                }
                                            }
                                            menu.ShowAsContext();
                                        }
                                    }
                                    #endregion
                                    #region Clear
                                    {
                                        var r = rect;
                                        r.xMin += ButtonWidth;
                                        r.width = ButtonWidth;
                                        if (GUI.Button(r, "Clear", EditorStyles.toolbarButton))
                                        {
                                            Undo.RecordObject(vaw, "Clear Selection Set");
                                            va.selectionSetList.Clear();
                                            selectionSetList.index = -1;
                                            updateSelectionList = true;
                                        }
                                    }
                                    #endregion
                                    #region Save as
                                    {
                                        var r = rect;
                                        r.width = ButtonWidth;
                                        r.x = rect.xMax - r.width;
                                        if (GUI.Button(r, Language.GetContent(Language.Help.SelectionSetSaveAs), EditorStyles.toolbarButton))
                                        {
                                            string path = EditorUtility.SaveFilePanel("Save as Selection Template", vae.templateSaveDefaultDirectory, string.Format("{0}_Selection.asset", vaw.gameObject.name), "asset");
                                            if (!string.IsNullOrEmpty(path))
                                            {
                                                if (!path.StartsWith(Application.dataPath))
                                                {
                                                    EditorCommon.SaveInsideAssetsFolderDisplayDialog();
                                                }
                                                else
                                                {
                                                    vae.templateSaveDefaultDirectory = Path.GetDirectoryName(path);
                                                    path = FileUtil.GetProjectRelativePath(path);
                                                    var selectionTemplate = ScriptableObject.CreateInstance<SelectionTemplate>();
                                                    {
                                                        selectionTemplate.selectionData = va.SaveSelectionSaveSettings();
                                                    }
                                                    try
                                                    {
                                                        VeryAnimationWindow.CustomAssetModificationProcessor.Pause();
                                                        AssetDatabase.CreateAsset(selectionTemplate, path);
                                                    }
                                                    finally
                                                    {
                                                        VeryAnimationWindow.CustomAssetModificationProcessor.Resume();
                                                    }
                                                    Focus();
                                                }
                                            }
                                        }
                                    }
                                    #endregion
                                }
                            };
                            selectionSetList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                            {
                                if (index >= va.selectionSetList.Count)
                                    return;

                                float x = rect.x;
                                {
                                    const float Rate = 0.7f;
                                    var r = rect;
                                    r.x = x;
                                    r.y += 2;
                                    r.height -= 4;
                                    r.width = rect.width * Rate;
                                    x += r.width;
                                    if (index == selectionSetList.index)
                                    {
                                        EditorGUI.BeginChangeCheck();
                                        var text = EditorGUI.TextField(r, va.selectionSetList[index].name);
                                        if (EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(text))
                                        {
                                            Undo.RecordObject(vaw, "Change Selection Set");
                                            va.selectionSetList[index].name = text;
                                        }
                                    }
                                    else
                                    {
                                        EditorGUI.LabelField(r, va.selectionSetList[index].name);
                                    }
                                }
                                {
                                    const float Rate = 0.2f;
                                    var r = rect;
                                    r.width = rect.width * Rate;
                                    r.x = rect.xMax - r.width;
                                    r.y += 2;
                                    r.height -= 4;
                                    EditorGUI.LabelField(r, va.selectionSetList[index].count.ToString(), vaw.guiStyleCenterAlignLabel);
                                }
                            };
                            selectionSetList.onSelectCallback = (ReorderableList list) =>
                            {
                                if (list.index < 0 || list.index >= va.selectionSetList.Count)
                                    return;
                                if (va.selectionSetList[list.index].bones.Length > 0)
                                    Selection.activeGameObject = va.selectionSetList[list.index].bones[0];
                                va.SelectGameObjects(va.selectionSetList[list.index].bones, va.selectionSetList[list.index].virtualBones);
                                InternalEditorUtility.RepaintAllViews();
                            };
                            selectionSetList.onCanAddCallback = (ReorderableList list) =>
                            {
                                return (va.selectionGameObjects != null && va.selectionGameObjects.Count > 0) || (va.selectionHumanVirtualBones != null && va.selectionHumanVirtualBones.Count > 0);
                            };
                            selectionSetList.onAddCallback = (ReorderableList list) =>
                            {
                                if ((va.selectionGameObjects == null || va.selectionGameObjects.Count <= 0) && (va.selectionHumanVirtualBones == null || va.selectionHumanVirtualBones.Count <= 0))
                                    return;

                                Undo.RecordObject(vaw, "Add Selection Set");
                                {
                                    var data = new VeryAnimationSaveSettings.SelectionData()
                                    {
                                        name = "New Set",
                                        bones = va.selectionGameObjects.ToArray(),
                                        virtualBones = va.selectionHumanVirtualBones != null ? va.selectionHumanVirtualBones.ToArray() : new HumanBodyBones[0],
                                    };
                                    if (va.selectionActiveGameObject != null)
                                        data.name = va.selectionActiveGameObject.name;
                                    else if(va.selectionHumanVirtualBones != null && va.selectionHumanVirtualBones.Count > 0)
                                        data.name = "Virtual" + va.selectionHumanVirtualBones[0].ToString();
                                    va.selectionSetList.Add(data);
                                }
                                updateSelectionList = true;
                            };
                            selectionSetList.onRemoveCallback = (ReorderableList list) =>
                            {
                                if (list.index < 0 || list.index >= va.selectionSetList.Count)
                                    return;
                                Undo.RecordObject(vaw, "Remove Selection Set");
                                va.selectionSetList.RemoveAt(list.index);
                                list.index = -1;
                                updateSelectionList = true;
                            };
                        }
                        #endregion
                        updateSelectionList = false;
                        UpdateSelection();
                        Repaint();
                    }
                    if (selectionSetList != null)
                    {
                        selectionSetList.DoLayoutList();
                    }
                    #endregion
                }
                else if (selectionType == SelectionType.Popup)
                {
                    #region Popup
                    if (Event.current.type == EventType.Layout && updateSelectionPopup)
                    {
                        selectionSetStrings = new string[va.selectionSetList.Count];
                        for (int i = 0; i < va.selectionSetList.Count; i++)
                        {
                            selectionSetStrings[i] = va.selectionSetList[i].name;
                        }
                        updateSelectionPopup = false;
                        UpdateSelection();
                        Repaint();
                    }
                    if (selectionSetStrings != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        selectionSetIndex = EditorGUILayout.Popup("Selection Set", selectionSetIndex, selectionSetStrings);
                        if (EditorGUI.EndChangeCheck())
                        {
                            if (selectionSetIndex >= 0 && selectionSetIndex < va.selectionSetList.Count)
                            {
                                if (va.selectionSetList[selectionSetIndex].bones.Length > 0)
                                    Selection.activeGameObject = va.selectionSetList[selectionSetIndex].bones[0];
                                va.SelectGameObjects(va.selectionSetList[selectionSetIndex].bones, va.selectionSetList[selectionSetIndex].virtualBones);
                                InternalEditorUtility.RepaintAllViews();
                            }
                        }
                    }
                    #endregion
                }

                #region Move select
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginDisabledGroup(va.selectionActiveBone < 0);
                    EditorGUILayout.PrefixLabel(Language.GetContent(Language.Help.MoveSelect));
                    if (GUILayout.Button("Upper"))
                    {
                        #region Upper
                        HashSet<GameObject> selectBones;
                        if (Shortcuts.IsKeyControl(e) || e.shift)
                            selectBones = new HashSet<GameObject>(va.selectionGameObjects);
                        else
                            selectBones = new HashSet<GameObject>();
                        foreach (var boneIndex in va.selectionBones)
                        {
                            if (va.parentBoneIndexes[boneIndex] >= 0)
                                selectBones.Add(va.bones[va.parentBoneIndexes[boneIndex]]);
                        }
                        if (va.selectionActiveGameObject != null)
                        {
                            GameObject activeGo = null;
                            if (selectBones.Contains(va.selectionActiveGameObject))
                            {
                                activeGo = va.selectionActiveGameObject;
                            }
                            else
                            {
                                var pt = va.selectionActiveGameObject.transform.parent;
                                if (pt != null && selectBones.Contains(pt.gameObject))
                                {
                                    activeGo = pt.gameObject;
                                }
                            }
                            if (activeGo != null)
                            {
                                Selection.activeGameObject = activeGo;
                            }
                        }
                        va.SelectGameObjects(selectBones.ToArray());
                        #endregion
                    }
                    if (GUILayout.Button("Lower"))
                    {
                        #region Lower
                        HashSet<GameObject> selectBones;
                        if (Shortcuts.IsKeyControl(e) || e.shift)
                            selectBones = new HashSet<GameObject>(va.selectionGameObjects);
                        else
                            selectBones = new HashSet<GameObject>();
                        foreach (var boneIndex in va.selectionBones)
                        {
                            for (int i = 0; i < va.bones.Length; i++)
                            {
                                if (boneIndex == va.parentBoneIndexes[i])
                                    selectBones.Add(va.bones[i]);
                            }
                        }
                        if (va.selectionActiveGameObject != null)
                        {
                            GameObject activeGo = null;
                            if (selectBones.Contains(va.selectionActiveGameObject))
                            {
                                activeGo = va.selectionActiveGameObject;
                            }
                            else
                            {
                                for (int i = 0; i < va.selectionActiveGameObject.transform.childCount; i++)
                                {
                                    var ct = va.selectionActiveGameObject.transform.GetChild(i);
                                    if (selectBones.Contains(ct.gameObject))
                                    {
                                        activeGo = ct.gameObject;
                                        break;
                                    }
                                }
                            }
                            if(activeGo != null)
                            {
                                Selection.activeGameObject = activeGo;
                            }
                        }
                        va.SelectGameObjects(selectBones.ToArray());
                        #endregion
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndHorizontal();
                }
                #endregion
            }
            EditorGUILayout.EndVertical();
        }
        private void UpdateSelection()
        {
            selectionSetIndex = -1;
            if (va.selectionSetList != null)
            {
                for (int i = 0; i < va.selectionSetList.Count; i++)
                {
                    #region Bone
                    {
                        if ((va.selectionSetList[i].bones != null ? va.selectionSetList[i].bones.Length : 0) != (va.selectionGameObjects != null ? va.selectionGameObjects.Count : 0))
                            continue;
                        if (va.selectionSetList[i].bones != null && va.selectionSetList[i].bones.Length > 0)
                        {
                            if (va.selectionSetList[i].bones[0] != va.selectionActiveGameObject)
                                continue;
                        }
                        if (va.selectionGameObjects != null)
                        {
                            bool contain = true;
                            foreach (var bone in va.selectionGameObjects)
                            {
                                if (!EditorCommon.ArrayContains(va.selectionSetList[i].bones, bone))
                                {
                                    contain = false;
                                    break;
                                }
                            }
                            if (!contain) continue;
                        }
                    }
                    #endregion
                    #region VirtualBone
                    {
                        if ((va.selectionSetList[i].virtualBones != null ? va.selectionSetList[i].virtualBones.Length : 0) != (va.selectionHumanVirtualBones != null ? va.selectionHumanVirtualBones.Count : 0))
                            continue;
                        if (va.selectionHumanVirtualBones != null)
                        {
                            bool contain = true;
                            foreach (var bone in va.selectionHumanVirtualBones)
                            {
                                if (!EditorCommon.ArrayContains(va.selectionSetList[i].virtualBones, bone))
                                {
                                    contain = false;
                                    break;
                                }
                            }
                            if (!contain) continue;
                        }
                    }
                    #endregion
                    selectionSetIndex = i;
                    break;
                }
            }
            if (selectionSetList != null)
                selectionSetList.index = selectionSetIndex;
            Repaint();
        }

        private void HierarchyToolBarGUI()
        {
            if (va == null || va.isEditError) return;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                #region All
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = GUILayout.Toggle(hierarchyButtonAll, Language.GetContent(Language.Help.HierarchyToolbarAll), EditorStyles.toolbarButton);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change bone show flag");
                        va.ActionBoneShowFlagsAll((index) =>
                        {
                            va.boneShowFlags[index] = flag;
                        });
                        va.OnBoneShowFlagsUpdated.Invoke();
                        InternalEditorUtility.RepaintAllViews();
                    }
                }
                #endregion
                #region Weight
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = GUILayout.Toggle(hierarchyButtonWeight, Language.GetContent(Language.Help.HierarchyToolbarWeight), EditorStyles.toolbarButton);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change bone show flag");
                        va.ActionBoneShowFlagsHaveWeight((index) =>
                        {
                            va.boneShowFlags[index] = flag;
                        });
                        va.OnBoneShowFlagsUpdated.Invoke();
                        InternalEditorUtility.RepaintAllViews();
                    }
                }
                #endregion
                if (va.isHuman)
                {
                    #region Body
                    {
                        EditorGUI.BeginChangeCheck();
                        var flag = GUILayout.Toggle(hierarchyButtonBody, Language.GetContent(Language.Help.HierarchyToolbarBody), EditorStyles.toolbarButton);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change bone show flag");
                            va.ActionBoneShowFlagsHumanoidBody((index) =>
                            {
                                va.boneShowFlags[index] = flag;
                            });
                            va.OnBoneShowFlagsUpdated.Invoke();
                            InternalEditorUtility.RepaintAllViews();
                        }
                    }
                    #endregion
                    #region Face
                    {
                        EditorGUI.BeginDisabledGroup(va.editHumanoidBones[(int)HumanBodyBones.LeftEye] == null && 
                                                        va.editHumanoidBones[(int)HumanBodyBones.RightEye] == null &&
                                                        va.editHumanoidBones[(int)HumanBodyBones.Jaw] == null);
                        EditorGUI.BeginChangeCheck();
                        var flag = GUILayout.Toggle(hierarchyButtonFace, Language.GetContent(Language.Help.HierarchyToolbarFace), EditorStyles.toolbarButton);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change bone show flag");
                            va.ActionBoneShowFlagsHumanoidFace((index) =>
                            {
                                va.boneShowFlags[index] = flag;
                            });
                            va.OnBoneShowFlagsUpdated.Invoke();
                            InternalEditorUtility.RepaintAllViews();
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                    #endregion
                    #region LeftHand
                    {
                        EditorGUI.BeginDisabledGroup(!va.humanoidHasLeftHand);
                        EditorGUI.BeginChangeCheck();
                        var flag = GUILayout.Toggle(hierarchyButtonLeftHand, Language.GetContent(Language.Help.HierarchyToolbarLeftHand), EditorStyles.toolbarButton);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change bone show flag");
                            va.ActionBoneShowFlagsHumanoidLeftHand((index) =>
                            {
                                va.boneShowFlags[index] = flag;
                            });
                            va.OnBoneShowFlagsUpdated.Invoke();
                            InternalEditorUtility.RepaintAllViews();
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                    #endregion
                    #region RightHand
                    {
                        EditorGUI.BeginDisabledGroup(!va.humanoidHasRightHand);
                        EditorGUI.BeginChangeCheck();
                        var flag = GUILayout.Toggle(hierarchyButtonRightHand, Language.GetContent(Language.Help.HierarchyToolbarRightHand), EditorStyles.toolbarButton);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change bone show flag");
                            va.ActionBoneShowFlagsHumanoidRightHand((index) =>
                            {
                                va.boneShowFlags[index] = flag;
                            });
                            va.OnBoneShowFlagsUpdated.Invoke();
                            InternalEditorUtility.RepaintAllViews();
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                    #endregion
                }
                else
                {
                    #region Renderer
                    {
                        EditorGUI.BeginChangeCheck();
                        var flag = GUILayout.Toggle(hierarchyButtonRenderer, Language.GetContent(Language.Help.HierarchyToolbarRenderer), EditorStyles.toolbarButton);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change bone show flag");
                            va.ActionBoneShowFlagsHaveRenderer((index) =>
                            {
                                va.boneShowFlags[index] = flag;
                            });
                            va.OnBoneShowFlagsUpdated.Invoke();
                            InternalEditorUtility.RepaintAllViews();
                        }
                    }
                    #endregion
                    #region RendererParent
                    {
                        EditorGUI.BeginChangeCheck();
                        var flag = GUILayout.Toggle(hierarchyButtonRendererParent, Language.GetContent(Language.Help.HierarchyToolbarRendererParent), EditorStyles.toolbarButton);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change bone show flag");
                            va.ActionBoneShowFlagsHaveRendererParent((index) =>
                            {
                                va.boneShowFlags[index] = flag;
                            });
                            va.OnBoneShowFlagsUpdated.Invoke();
                            InternalEditorUtility.RepaintAllViews();
                        }
                    }
                    #endregion
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        private void HumanoidControlBoneGUI(Vector2 position, HumanBodyBones select)
        {
            if (va.IsIKBone(select))
                return;

            var bone = va.humanoidBones[(int)select];
            if (bone == null && VeryAnimation.HumanVirtualBones[(int)select] == null)
                return;

            var e = Event.current;

            var saveGUIColor = GUI.color;
            GUI.color = GreenColor;

            var selected = (selectionGameObjectsHumanoidIndex != null && selectionGameObjectsHumanoidIndex.Contains(select));

            Texture2D frameTex = bone != null ? dotframe : dotframedotted;
            Rect rect = new Rect(new Vector2(position.x - frameTex.width / 2f, position.y - frameTex.height / 2f), new Vector2(frameTex.width, frameTex.height));

            guiStyleBoneButton.normal.background = frameTex;
            guiStyleBoneButton.normal.scaledBackgrounds = null;
            guiStyleBoneButton.active.background = frameTex;
            guiStyleBoneButton.active.scaledBackgrounds = null;
            if (GUI.Button(rect, dotfill, guiStyleBoneButton))
            {
                if (bone != null)
                    va.SelectGameObjectPlusKey(bone);
                else
                    va.SelectVirtualBonePlusKey(select);
                ForceSelectionChange();
            }

            if (selected)
            {
                GUI.color = BlueColor;
                GUI.DrawTexture(rect, dotselection, ScaleMode.ScaleToFit);
            }

            GUI.color = saveGUIColor;

            controlBoneList.Add(select, position);
        }

        private void UndoRedoPerformed()
        {
            if (va == null || va.isEditError) return;

            UpdateHierarchyFlags();
        }

        private void UpdateHierarchyFlags()
        {
            if (va == null || va.isEditError) return;

            {
                hierarchyButtonAll = true;
                va.ActionBoneShowFlagsAll((index) =>
                {
                    if (!va.boneShowFlags[index])
                        hierarchyButtonAll = false;
                });
            }
            {
                hierarchyButtonWeight = true;
                va.ActionBoneShowFlagsHaveWeight((index) =>
                {
                    if (!va.boneShowFlags[index])
                        hierarchyButtonWeight = false;
                });
            }
            if (va.isHuman)
            {
                {
                    hierarchyButtonBody = true;
                    va.ActionBoneShowFlagsHumanoidBody((index) =>
                    {
                        if (!va.boneShowFlags[index])
                            hierarchyButtonBody = false;
                    });
                }
                {
                    hierarchyButtonFace = true;
                    va.ActionBoneShowFlagsHumanoidFace((index) =>
                    {
                        if (!va.boneShowFlags[index])
                            hierarchyButtonFace = false;
                    });
                }
                {
                    hierarchyButtonLeftHand = true;
                    va.ActionBoneShowFlagsHumanoidLeftHand((index) =>
                    {
                        if (!va.boneShowFlags[index])
                            hierarchyButtonLeftHand = false;
                    });
                }
                {
                    hierarchyButtonRightHand = true;
                    va.ActionBoneShowFlagsHumanoidRightHand((index) =>
                    {
                        if (!va.boneShowFlags[index])
                            hierarchyButtonRightHand = false;
                    });
                }
            }
            else
            {
                {
                    hierarchyButtonRenderer = true;
                    va.ActionBoneShowFlagsHaveRenderer((index) =>
                    {
                        if (!va.boneShowFlags[index])
                            hierarchyButtonRenderer = false;
                    });
                }
                {
                    hierarchyButtonRendererParent = true;
                    va.ActionBoneShowFlagsHaveRendererParent((index) =>
                    {
                        if (!va.boneShowFlags[index])
                            hierarchyButtonRendererParent = false;
                    });
                }
            }
        }

        public static void ForceRepaint()
        {
            if (instance == null) return;
            instance.Repaint();
        }

        public static void ForceSelectionChange()
        {
            if (instance == null) return;
            if (instance.guiSelectionFoldout)
            {
                instance.UpdateSelection();
            }
            ForceRepaint();
        }
    }
}
