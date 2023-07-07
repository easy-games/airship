#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace MTAssets.SkinnedMeshCombiner
{
    /*
     *  This class is responsible for the functioning of the "Skinned Mesh Bones Manager" component, and all its functions.
     */
    /*
     * The Skinned Mesh Combiner was developed by Marcos Tomaz in 2019.
     * Need help? Contact me (mtassets@windsoft.xyz)
     */

    [AddComponentMenu("MT Assets/Skinned Mesh Combiner/Skinned Mesh Bones Manager")] //Add this component in a category of addComponent menu
    public class SkinnedMeshBonesManager : MonoBehaviour
    {
        //Public variables of script
        ///<summary>[WARNING] Do not change the value of this variable. This is a variable used for internal tool operations.</summary> 
        [HideInInspector]
        public SkinnedMeshRenderer anotherBonesHierarchyCurrentInUse = null;

#if UNITY_EDITOR
        //Public variables of Interface
        private bool gizmosOfThisComponentIsDisabled = false;

        //Classes of this script, only disponible in Editor
        public class VerticeData
        {
            //This class store all data about a vertice influenced by a bone
            public BoneInfo influencerBone;
            public float weightOfInfluencer;
            public int indexOfThisVerticeInMesh;

            public VerticeData(BoneInfo boneInfo, float weightOfInfluencer, int indexOfThisVerticeInMesh)
            {
                this.influencerBone = boneInfo;
                this.weightOfInfluencer = weightOfInfluencer;
                this.indexOfThisVerticeInMesh = indexOfThisVerticeInMesh;
            }
        }
        public class BoneInfo
        {
            //This class store all data about a bone
            public GameObject gameObject;
            public Transform transform;
            public string name;
            public string transformPath;
            public int hierarchyIndex;
            public List<VerticeData> verticesOfThis = new List<VerticeData>();
        }

        //Public variables of editor 
        ///<summary>[WARNING] This variable is only available in the Editor and will not be included in the compilation of your project, in the final Build.</summary> 
        [HideInInspector]
        private List<BoneInfo> bonesCacheOfThisRenderer = new List<BoneInfo>();
        ///<summary>[WARNING] This variable is only available in the Editor and will not be included in the compilation of your project, in the final Build.</summary> 
        [HideInInspector]
        private BoneInfo boneInfoToShowVertices = null;
        ///<summary>[WARNING] This variable is only available in the Editor and will not be included in the compilation of your project, in the final Build.</summary> 
        [HideInInspector]
        public string currentBoneNameRendering = "";
        ///<summary>[WARNING] This variable is only available in the Editor and will not be included in the compilation of your project, in the final Build.</summary> 
        [HideInInspector]
        public float gizmosSizeInterface = 0.01f;
        ///<summary>[WARNING] This variable is only available in the Editor and will not be included in the compilation of your project, in the final Build.</summary> 
        [HideInInspector]
        public bool renderGizmoOfBone = true;
        ///<summary>[WARNING] This variable is only available in the Editor and will not be included in the compilation of your project, in the final Build.</summary> 
        [HideInInspector]
        public bool renderLabelOfBone = true;
        ///<summary>[WARNING] This variable is only available in the Editor and will not be included in the compilation of your project, in the final Build.</summary> 
        [HideInInspector]
        public bool pingBoneOnShowVertices = false;
        ///<summary>[WARNING] This variable is only available in the Editor and will not be included in the compilation of your project, in the final Build.</summary> 
        [HideInInspector]
        public SkinnedMeshRenderer meshRendererBonesToAnimateThis = null;
        ///<summary>[WARNING] This variable is only available in the Editor and will not be included in the compilation of your project, in the final Build.</summary> 
        [HideInInspector]
        public bool useRootBoneToo = true;

        //The UI of this component
        #region INTERFACE_CODE
        [UnityEditor.CustomEditor(typeof(SkinnedMeshBonesManager))]
        public class CustomInspector : UnityEditor.Editor
        {
            //Private variables of editor
            private Vector2 bonesListScroll = Vector2.zero;
            private Vector3 currentPostionOfVerticesText = Vector3.zero;
            private string currentTextOfVerticesText = "";
            private int currentSelectedVertice = -1;

            public override void OnInspectorGUI()
            {
                //Start the undo event support, draw default inspector and monitor of changes
                SkinnedMeshBonesManager script = (SkinnedMeshBonesManager)target;
                script.gizmosOfThisComponentIsDisabled = MTAssetsEditorUi.DisableGizmosInSceneView("SkinnedMeshBonesManager", script.gizmosOfThisComponentIsDisabled);

                //Try to load needed assets
                Texture selectedBone = (Texture)AssetDatabase.LoadAssetAtPath("Assets/MT Assets/Skinned Mesh Combiner/Editor/Images/SelectedBone.png", typeof(Texture));
                Texture unselectedBone = (Texture)AssetDatabase.LoadAssetAtPath("Assets/MT Assets/Skinned Mesh Combiner/Editor/Images/UnselectedBone.png", typeof(Texture));
                //If fails on load needed assets, locks ui
                if (selectedBone == null || unselectedBone == null)
                {
                    EditorGUILayout.HelpBox("Unable to load required files. Please reinstall Skinned Mesh Combiner to correct this problem.", MessageType.Error);
                    return;
                }

                //Try to get prefab parent, is different from null, if this is a prefab
                var parentPrefab = PrefabUtility.GetCorrespondingObjectFromSource(script.gameObject.transform);

                //Description
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("Remember to read the Skinned Mesh Combiner documentation to understand how to use it.\nGet support at: mtassets@windsoft.xyz", MessageType.None);
                GUILayout.Space(10);

                //If not exists a skinned mesh renderer or null mesh, stop this interface
                SkinnedMeshRenderer meshRenderer = script.GetComponent<SkinnedMeshRenderer>();
                if (meshRenderer == null)
                {
                    EditorGUILayout.HelpBox("A \"Skinned Mesh Renderer\" component could not be found in this GameObject. Please insert this manager into a Skinned Renderer.", MessageType.Error);
                    return;
                }
                if (meshRenderer != null && meshRenderer.sharedMesh == null)
                {
                    EditorGUILayout.HelpBox("It was not possible to find a mesh associated with the Skinned Mesh Renderer component of this GameObject. Please associate a valid mesh with this Skinned Mesh Renderer, so that you can manage the Bones.", MessageType.Error);
                    return;
                }

                //Verify if is playing
                if (Application.isPlaying == true)
                {
                    EditorGUILayout.HelpBox("The bone management interface is not available while the application is running, only the API for this component works during execution.", MessageType.Info);
                    return;
                }

                //If bone info not loaded automatically
                if (script.bonesCacheOfThisRenderer.Count == 0)
                {
                    script.bonesCacheOfThisRenderer.Clear();
                    script.UpdateBonesCacheAndGetAllBonesAndDataInList();
                }

                //Bones list
                EditorGUILayout.LabelField("All Bones Of This Mesh (" + meshRenderer.sharedMesh.name + ")", EditorStyles.boldLabel);
                GUILayout.Space(10);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("All Bones Found In This Skinned Mesh Renderer", GUILayout.Width(280));
                GUILayout.Space(MTAssetsEditorUi.GetInspectorWindowSize().x - 280);
                EditorGUILayout.LabelField("Size", GUILayout.Width(30));
                EditorGUILayout.IntField(script.bonesCacheOfThisRenderer.Count, GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
                GUILayout.BeginVertical("box");
                bonesListScroll = EditorGUILayout.BeginScrollView(bonesListScroll, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Width(MTAssetsEditorUi.GetInspectorWindowSize().x), GUILayout.Height(250));
                if (script.bonesCacheOfThisRenderer != null)
                {
                    //If is using another bones hierarchy
                    if (script.anotherBonesHierarchyCurrentInUse != null)
                        EditorGUILayout.HelpBox("This bone hierarchy belongs to the Skinned Mesh Renderer \"" + script.anotherBonesHierarchyCurrentInUse.transform.name + "\", however, it is the bone hierarchy of \"" + script.anotherBonesHierarchyCurrentInUse.transform.name + "\" that is animating this mesh. You can still see which bones control which vertices and so on.", MessageType.Warning);

                    //If have null bones in bones hierarchy
                    if (script.bonesCacheOfThisRenderer.Count == 0)
                        EditorGUILayout.HelpBox("The bone hierarchy of this mesh is apparently corrupted. One or more bones are null, nonexistent or have been deleted. The hierarchy of bones to which this mesh is linked may also no longer exist. Try to have this mesh animated by another bone hierarchy below.", MessageType.Warning);

                    //Create style of icon
                    GUIStyle estiloIcone = new GUIStyle();
                    estiloIcone.border = new RectOffset(0, 0, 0, 0);
                    estiloIcone.margin = new RectOffset(4, 0, 6, 0);

                    foreach (BoneInfo bone in script.bonesCacheOfThisRenderer)
                    {
                        //List each bone
                        if (bone.hierarchyIndex > 0)
                            GUILayout.Space(8);
                        EditorGUILayout.BeginHorizontal();
                        if (script.boneInfoToShowVertices == null || bone.hierarchyIndex != script.boneInfoToShowVertices.hierarchyIndex)
                            GUILayout.Box(unselectedBone, estiloIcone, GUILayout.Width(24), GUILayout.Height(24));
                        if (script.boneInfoToShowVertices != null && bone.hierarchyIndex == script.boneInfoToShowVertices.hierarchyIndex)
                            GUILayout.Box(selectedBone, estiloIcone, GUILayout.Width(24), GUILayout.Height(24));
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.LabelField("(" + bone.hierarchyIndex.ToString() + ") " + bone.name, EditorStyles.boldLabel);
                        GUILayout.Space(-3);
                        EditorGUILayout.LabelField("Influencing " + bone.verticesOfThis.Count + " vertices in this mesh.");
                        EditorGUILayout.EndVertical();
                        GUILayout.Space(20);
                        EditorGUILayout.BeginVertical();
                        GUILayout.Space(6);
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("Path", GUILayout.Height(20)))
                        {
                            if (bone.gameObject != null)
                                EditorUtility.DisplayDialog("Ping to \"" + bone.name + "\" bone of this mesh", "The path of GameObject/Transform of this bone is...\n\n" + bone.transformPath, "Ok");
                            if (bone.gameObject == null)
                                EditorUtility.DisplayDialog("Bone Error", "This bone transform, not found in this scene.", "Ok");
                            EditorGUIUtility.PingObject(bone.gameObject);
                        }
                        if (script.boneInfoToShowVertices == null || bone.hierarchyIndex != script.boneInfoToShowVertices.hierarchyIndex)
                            if (GUILayout.Button("Vertices", GUILayout.Height(20)))
                                if (EditorUtility.DisplayDialog("Show Vertices", "You are about to display all the vertices affected by this bone. This can be slow depending on the grandeur of your model and how many vertices are being affected. Do you wish to continue?", "Yes", "No") == true)
                                {
                                    //Change the bone info to view vertices, and reset editor data
                                    script.boneInfoToShowVertices = bone;
                                    script.currentBoneNameRendering = "Showing vertices influenceds by bone\n\"" + bone.name + "\"\nVertices Influenceds: " + bone.verticesOfThis.Count;
                                    currentSelectedVertice = -1;
                                    currentPostionOfVerticesText = Vector3.zero;
                                    currentTextOfVerticesText = "";
                                    if (script.pingBoneOnShowVertices)
                                        EditorGUIUtility.PingObject(bone.gameObject);
                                }
                        if (script.boneInfoToShowVertices != null && bone.hierarchyIndex == script.boneInfoToShowVertices.hierarchyIndex)
                            if (GUILayout.Button("--------", GUILayout.Height(20)))
                            {
                                script.boneInfoToShowVertices = null;
                                script.currentBoneNameRendering = "";
                                currentSelectedVertice = -1;
                                currentPostionOfVerticesText = Vector3.zero;
                                currentTextOfVerticesText = "";
                            }
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                        GUILayout.Space(2);
                    }
                }
                EditorGUILayout.EndScrollView();
                GUILayout.EndVertical();

                EditorGUILayout.HelpBox("" +
                   "In the list above, you can see all the bones that are linked to the mesh of this Skinned Mesh Renderer. You can also see the bone hierarchy that this mesh is using. All bones listed below are linked to this mesh and may or may not deform vertices of this mesh.", MessageType.Info);

                script.gizmosSizeInterface = EditorGUILayout.Slider(new GUIContent("Gizmos Size In Interface",
                         "The size that the Gizmos will be rendered in interface of this component."),
                         script.gizmosSizeInterface, 0.001f, 0.1f);

                script.renderGizmoOfBone = (bool)EditorGUILayout.Toggle(new GUIContent("Render Gizmo Of Bone",
                        "Render gizmo of bone selected to show vertices?"),
                        script.renderGizmoOfBone);

                script.renderLabelOfBone = (bool)EditorGUILayout.Toggle(new GUIContent("Render Label Of Bone",
                       "Render label of bone selected to show vertices?"),
                       script.renderLabelOfBone);

                script.pingBoneOnShowVertices = (bool)EditorGUILayout.Toggle(new GUIContent("Ping Bone On Show Vert.",
                      "Ping/Highlight bone transform in scene, everytime that you show vertices of the bone?"),
                      script.pingBoneOnShowVertices);

                GUILayout.Space(10);
                if (GUILayout.Button("Update And Show Info About Bones Hierarchy Of This Mesh", GUILayout.Height(40)))
                {
                    script.bonesCacheOfThisRenderer.Clear();
                    script.UpdateBonesCacheAndGetAllBonesAndDataInList();
                    Debug.Log("The information on the bone hierarchy of this mesh has been updated.");
                }
                GUILayout.Space(10);

                //Bones mangement
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Use Another Bone Hierarchy To Animate This", EditorStyles.boldLabel);
                GUILayout.Space(10);

                if (script.meshRendererBonesToAnimateThis != null && parentPrefab != null)
                    EditorGUILayout.HelpBox("It looks like this GameObject is a prefab, or part of one. Therefore, this component may not be able to place this mesh, next to the bone hierarchy when you make the switch, for the reason that Unity does not allow the reorganization of GameObjects in prefabs. When you make the switch so that this mesh is animated by another bone hierarchy, everything will work normally, however, you will need to organize this GameObject manually.", MessageType.Warning);

                //If not forneced the skinned mesh renderer
                if (script.meshRendererBonesToAnimateThis == null)
                    EditorGUILayout.HelpBox("Provide a Skinned Mesh Renderer so that your bones hierarchy is used instead.", MessageType.Info);

                //If forneced the skinned mesh renderer
                if (script.meshRendererBonesToAnimateThis != null)
                {
                    //Prepare the message
                    string errorMessage = "It is not possible for this Skinned Mesh Renderer to use the Skinned Mesh Renderer \"" + script.meshRendererBonesToAnimateThis.gameObject.name + "\" bone hierarchy, as the two hierarchies are not identical. Both Skinned Mesh Renderers must have an identical bone hierarchy to make it possible for this mesh to be animated by the bones of the desired Skinned Mesh Renderer.";

                    //Validate the mesh renderer
                    if (script.isValidTargetSkinnedMeshRendererBonesHierarchy(script.meshRendererBonesToAnimateThis, false) == false)
                        EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
                }

                script.meshRendererBonesToAnimateThis = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(new GUIContent("Bones Hierarchy To Use",
                    "This custom material will have its properties copied and will be associated with the merged mesh."),
                    script.meshRendererBonesToAnimateThis, typeof(SkinnedMeshRenderer), true, GUILayout.Height(16));

                //If forneced the skinned mesh renderer
                if (script.meshRendererBonesToAnimateThis != null)
                {
                    if (script.isValidTargetSkinnedMeshRendererBonesHierarchy(script.meshRendererBonesToAnimateThis, false) == true)
                    {
                        script.useRootBoneToo = (bool)EditorGUILayout.Toggle(new GUIContent("Use Root Bone Too",
                          "Use the same root bone of the Skinned Mesh Renderer too?"),
                          script.useRootBoneToo);

                        GUILayout.Space(10);

                        if (GUILayout.Button("Use Bones Hierarchy From That Skinned Mesh Renderer", GUILayout.Height(40)))
                        {
                            if (EditorUtility.DisplayDialog("Continue?", "You will no longer be able to undo this change. To obtain the Skinned Mesh Renderer and all its original information, it will be necessary to re-add this mesh to your scene again."
                                + "\n\nAfter performing this action, the mesh of this Skinned Mesh Renderer will be animated using the bones of the Skinned Mesh Renderer you provided.", "Continue", "Cancel") == true)
                                script.UseAnotherBoneHierarchyForAnimateThis(script.meshRendererBonesToAnimateThis, script.useRootBoneToo);
                        }
                    }
                }

                GUILayout.Space(10);
            }

            protected virtual void OnSceneGUI()
            {
                SkinnedMeshBonesManager script = (SkinnedMeshBonesManager)target;

                //Get this mesh renderer
                SkinnedMeshRenderer meshRenderer = script.GetComponent<SkinnedMeshRenderer>();

                //If not have components to worl
                if (meshRenderer == null || meshRenderer.sharedMesh == null)
                    return;

                //Render only if have a boneinfo to render
                if (meshRenderer != null && meshRenderer.sharedMesh != null && script.boneInfoToShowVertices == null)
                    return;

                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(target, "Undo Event");

                //Set the base color of gizmos
                Handles.color = Color.green;

                //Render each vertice if, bone info to show vertices is not null
                foreach (VerticeData vertice in script.boneInfoToShowVertices.verticesOfThis)
                {
                    //Color the gizmo according the weight
                    Handles.color = Color.Lerp(Color.green, Color.red, vertice.weightOfInfluencer);

                    //If is the selected vertice
                    if (vertice.indexOfThisVerticeInMesh == currentSelectedVertice)
                        Handles.color = Color.white;

                    //Draw current vertice
                    Vector3 currentVertice = script.transform.TransformPoint(meshRenderer.sharedMesh.vertices[vertice.indexOfThisVerticeInMesh]);
                    if (Handles.Button(currentVertice, Quaternion.identity, script.gizmosSizeInterface, script.gizmosSizeInterface, Handles.SphereHandleCap))
                    {
                        currentPostionOfVerticesText = currentVertice;
                        currentTextOfVerticesText = "Vertice Index: " + vertice.indexOfThisVerticeInMesh + "/" + meshRenderer.sharedMesh.vertices.Length + "\nInfluencer Bone: " + vertice.influencerBone.name + "\nWeight of Influence: " + vertice.weightOfInfluencer.ToString("F2");
                        currentSelectedVertice = vertice.indexOfThisVerticeInMesh;
                    }
                }

                //Prepare the text
                GUIStyle styleVerticeDetail = new GUIStyle();
                styleVerticeDetail.normal.textColor = Color.white;
                styleVerticeDetail.alignment = TextAnchor.MiddleCenter;
                styleVerticeDetail.fontStyle = FontStyle.Bold;
                styleVerticeDetail.contentOffset = new Vector2(-currentTextOfVerticesText.Substring(0, currentTextOfVerticesText.IndexOf("\n") + 1).Length * 1.8f, 30);

                //Draw the vertice text, if is desired
                if (currentPostionOfVerticesText != Vector3.zero)
                    Handles.Label(currentPostionOfVerticesText, currentTextOfVerticesText, styleVerticeDetail);

                //Render the bone, if is desired
                if (script.renderGizmoOfBone)
                {
                    Handles.color = Color.gray;
                    if (script.boneInfoToShowVertices.transform != null)
                        Handles.ArrowHandleCap(0, script.boneInfoToShowVertices.transform.position, Quaternion.identity * script.boneInfoToShowVertices.transform.rotation * Quaternion.AngleAxis(90, Vector3.left), script.gizmosSizeInterface * 18f, EventType.Repaint);
                }

                //Render the bone name, if is desired
                if (script.renderLabelOfBone)
                {
                    GUIStyle styleBoneName = new GUIStyle();
                    styleBoneName.normal.textColor = Color.white;
                    styleBoneName.alignment = TextAnchor.MiddleCenter;
                    styleBoneName.fontStyle = FontStyle.Bold;
                    styleBoneName.contentOffset = new Vector2(-script.currentBoneNameRendering.Substring(0, script.currentBoneNameRendering.IndexOf("\n") + 1).Length * 1.5f, 30);
                    if (string.IsNullOrEmpty(script.currentBoneNameRendering) == false)
                        Handles.Label(script.boneInfoToShowVertices.transform.position, script.currentBoneNameRendering, styleBoneName);
                }

                //Apply changes on script, case is not playing in editor
                if (GUI.changed == true && Application.isPlaying == false)
                {
                    EditorUtility.SetDirty(script);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(script.gameObject.scene);
                }
                if (EditorGUI.EndChangeCheck() == true)
                {
                    //Apply the change, if moved the handle
                    //script.transform.position = teste;
                }
                Repaint();
            }
        }

        private string GetGameObjectPath(Transform transform)
        {
            //Return the full path of a GameObject
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        private void UpdateBonesCacheAndGetAllBonesAndDataInList()
        {
            //Only update the bones cache of this renderer, if this renderer is updated
            if (bonesCacheOfThisRenderer.Count > 0)
                return;

            //Get the skinned mesh renderer
            SkinnedMeshRenderer meshRender = GetComponent<SkinnedMeshRenderer>();

            //Start the scan
            if (meshRender != null && meshRender.sharedMesh != null)
            {
                //Get all bones
                Transform[] allBonesTransform = meshRender.bones;

                //If have a null bone, stop the process
                foreach (Transform transform in allBonesTransform)
                    if (transform == null)
                    {
                        bonesCacheOfThisRenderer.Clear();
                        return;
                    }

                //Create all boneinfo
                for (int i = 0; i < allBonesTransform.Length; i++)
                {
                    BoneInfo boneInfo = new BoneInfo();
                    boneInfo.transform = allBonesTransform[i];
                    boneInfo.name = allBonesTransform[i].name;
                    boneInfo.gameObject = allBonesTransform[i].transform.gameObject;
                    boneInfo.transformPath = GetGameObjectPath(allBonesTransform[i]);
                    boneInfo.hierarchyIndex = i;

                    bonesCacheOfThisRenderer.Add(boneInfo);
                }

                //Associate each vertice influenced by each bone to respective key
                for (int i = 0; i < meshRender.sharedMesh.vertexCount; i++)
                {
                    //Verify if exists a weight of a possible bone X influencing this vertice. Create a vertice data that stores and link this vertice inside your respective BoneInfo
                    if (meshRender.sharedMesh.boneWeights[i].weight0 > 0)
                    {
                        int boneIndexOfInfluencerBoneOfThisVertice = meshRender.sharedMesh.boneWeights[i].boneIndex0;
                        bonesCacheOfThisRenderer[boneIndexOfInfluencerBoneOfThisVertice].verticesOfThis.Add(new VerticeData(bonesCacheOfThisRenderer[boneIndexOfInfluencerBoneOfThisVertice], meshRender.sharedMesh.boneWeights[i].weight0, i));
                    }
                    if (meshRender.sharedMesh.boneWeights[i].weight1 > 0)
                    {
                        int boneIndexOfInfluencerBoneOfThisVertice = meshRender.sharedMesh.boneWeights[i].boneIndex1;
                        bonesCacheOfThisRenderer[boneIndexOfInfluencerBoneOfThisVertice].verticesOfThis.Add(new VerticeData(bonesCacheOfThisRenderer[boneIndexOfInfluencerBoneOfThisVertice], meshRender.sharedMesh.boneWeights[i].weight1, i));
                    }
                    if (meshRender.sharedMesh.boneWeights[i].weight2 > 0)
                    {
                        int boneIndexOfInfluencerBoneOfThisVertice = meshRender.sharedMesh.boneWeights[i].boneIndex2;
                        bonesCacheOfThisRenderer[boneIndexOfInfluencerBoneOfThisVertice].verticesOfThis.Add(new VerticeData(bonesCacheOfThisRenderer[boneIndexOfInfluencerBoneOfThisVertice], meshRender.sharedMesh.boneWeights[i].weight2, i));
                    }
                    if (meshRender.sharedMesh.boneWeights[i].weight3 > 0)
                    {
                        int boneIndexOfInfluencerBoneOfThisVertice = meshRender.sharedMesh.boneWeights[i].boneIndex3;
                        bonesCacheOfThisRenderer[boneIndexOfInfluencerBoneOfThisVertice].verticesOfThis.Add(new VerticeData(bonesCacheOfThisRenderer[boneIndexOfInfluencerBoneOfThisVertice], meshRender.sharedMesh.boneWeights[i].weight3, i));
                    }
                }
            }
        }
        #endregion
#endif

        //Private methods for this component Interface and API.

        private bool isValidTargetSkinnedMeshRendererBonesHierarchy(SkinnedMeshRenderer targetMeshRenderer, bool launchLogs)
        {
            //Prepare the value
            bool isValid = true;

            //Get this mesh renderer
            SkinnedMeshRenderer thisMeshRenderer = GetComponent<SkinnedMeshRenderer>();

            //Validate
            if (thisMeshRenderer == null)
                isValid = false;
            if (targetMeshRenderer == null)
                isValid = false;
            if (thisMeshRenderer.bones.Length != targetMeshRenderer.bones.Length)
                isValid = false;

            //Validate if all bones of target is equal of bones of this, by name
            /*if (isValid == true)
            {
                for (int i = 0; i < thisMeshRenderer.bones.Length; i++)
                {
                    if (thisMeshRenderer.bones[i].name != targetMeshRenderer.bones[i].name)
                        isValid = false;
                }
            }*/

            //Launch logs if is desired
            if (launchLogs == true)
            {
                string errorMessage = "It is not possible for this Skinned Mesh Renderer to use the Skinned Mesh Renderer \"" + targetMeshRenderer.gameObject.name + "\" bone hierarchy, as the two hierarchies are not identical. Both Skinned Mesh Renderers must have an identical bone hierarchy to make it possible for this mesh to be animated by the bones of the desired Skinned Mesh Renderer.";

#if !UNITY_EDITOR
                if (isValid == false)
                    Debug.Log(errorMessage);
#endif
#if UNITY_EDITOR
                if (isValid == false && Application.isPlaying == false)
                    EditorUtility.DisplayDialog("Error", errorMessage, "Ok");
                if (isValid == false && Application.isPlaying == true)
                    Debug.Log(errorMessage);
#endif
            }

            //Return
            return isValid;
        }

        //Public API methods

        public bool UseAnotherBoneHierarchyForAnimateThis(SkinnedMeshRenderer meshRendererBonesToUse, bool useRootBoneToo)
        {
            //First validate the target skinned mesh renderer
            if (isValidTargetSkinnedMeshRendererBonesHierarchy(meshRendererBonesToUse, true) == false)
                return false;

            //Set a another bone hierarchy to animate this
            SkinnedMeshRenderer thisMeshRenderer = GetComponent<SkinnedMeshRenderer>();
            thisMeshRenderer.bones = meshRendererBonesToUse.bones;
            if (useRootBoneToo == true)
                thisMeshRenderer.rootBone = meshRendererBonesToUse.rootBone;

            //Move this gameobject to be parent of meshRendererToBonesUse
            this.gameObject.transform.parent = meshRendererBonesToUse.transform.parent;

            //Set the stats
            anotherBonesHierarchyCurrentInUse = meshRendererBonesToUse;

            //Return true for success
            return true;
        }
    }
}