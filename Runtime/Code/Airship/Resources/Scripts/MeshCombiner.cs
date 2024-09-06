using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;
using System.IO;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Airship {

    //You build up a MeshCombiner by submitting assets to it
    //It then contains a list of MeshCopyReferences, which can be enabled/disabled or have other operations
    //performed on them
    [ExecuteInEditMode]
    [LuauAPI]
    public class MeshCombiner : MonoBehaviour {
        private static bool runThreaded = true;
        private static bool debugText = false;
        public static readonly string MeshCombineSkinnedName = "MeshCombinerSkinned";
        public static readonly string MeshCombineStaticName = "MeshCombinerStatic";
        
        [SerializeField]
        public GameObject baseMesh;

        [SerializeField]
        public Transform rootBone;
        
        [SerializeField]
        public bool executeOnLoad = false;

        [SerializeField]
        public List<Transform> hiddenSurfaces = new();
         
        [SerializeField]
        public List<MeshCopyReference> sourceReferences = new List<MeshCopyReference>();

        [NonSerialized]
        Dictionary<string, Matrix4x4> allBindPoses = new();

        public override string ToString() {
            string value = "";
            foreach (var copy in sourceReferences) {
                value += copy.ToString() + "\n";
            }
            return value;
        }

        //MeshCopyReference is where to get the mesh data from (an asset, or a child game object)
        [System.Serializable]
        public class MeshCopyReference {
            [SerializeField]
            public string assetPath;
            [SerializeField]
            private Matrix4x4 _transform;

            [SerializeField]
            public string name;
            [SerializeField]
            public bool enabled = true;
            [SerializeField]
            public Transform transform = null;

            [SerializeField]
            public bool maskThisMesh = false;

            public override string ToString() {
                return transform == null ? "Asset: " + assetPath : "Transform: " + transform.name;
            }

            //Do not serialize
            [NonSerialized]
            public MeshCopy[] meshCopy = null;


            public MeshCopyReference(string assetPath, string name) {
                this.assetPath = assetPath;
                this.name = name;
            }

            public MeshCopyReference(Transform obj) {
                this.assetPath = null;
                this.name = obj.name;
                this.transform = obj;
            }

            public MeshCopyReference ManualClone() {
                MeshCopyReference output = new MeshCopyReference(this.assetPath, this.name);

                //Clone all the members
                output.enabled = this.enabled;
                output.transform = this.transform;

                if (this.meshCopy != null) {
                    output.meshCopy = new MeshCopy[this.meshCopy.Length];
                    for (int i = 0; i < this.meshCopy.Length; i++) {
                        output.meshCopy[i] = this.meshCopy[i].ManualClone();
                    }
                }

                return output;
            }

            public void LoadMeshCopy() {
                if (transform == null) {
                    meshCopy = MeshCopy.Load(assetPath, true).ToArray();
                }
                else {
                    meshCopy = MeshCopy.Load(transform, true).ToArray();
                }
            }

        }

        [NonSerialized]
        public SkinnedMeshRenderer combinedSkinnedMeshRenderer;
        [NonSerialized]
        public MeshRenderer combinedStaticMeshRenderer;
        [NonSerialized]
        public MeshFilter combinedStaticMeshFilter;

        [NonSerialized]
        private MeshCopy finalSkinnedMesh = new MeshCopy();
        [NonSerialized]
        private MeshCopy finalStaticMesh = new MeshCopy();

        private MeshCopyReference[] readOnlySourceReferences;
        [NonSerialized]
        private bool pendingUpdate = false;
        [NonSerialized]
        private bool runningUpdate = false;
        [NonSerialized]
        private bool newMeshReadyToUse = false;
        [NonSerialized]
        private Bounds skinnedMeshBounds;
        [NonSerialized]
        public Action OnCombineComplete;


        public float finalVertCount => finalStaticMesh.vertices.Count;
        public float finalSkinnedVertCount => finalSkinnedMesh.vertices.Count;
        public float finalMaterialCount => finalStaticMesh.subMeshes.Count;
        public float finalSkinnedMaterialCount => finalSkinnedMesh.subMeshes.Count;
        public float finalSkinnedBonesCount => finalSkinnedMesh.bones.Count;

        public MeshCopyReference AddMesh(string assetPath, string name, bool showError = false) {
            //Todo: Pull from a pool?
            //Todo: Allow for callback here to edit mesh before it's processed?
            MeshCopyReference meshCopyReference = new MeshCopyReference(assetPath, name);
            meshCopyReference.LoadMeshCopy();

            sourceReferences.Add(meshCopyReference);
            pendingUpdate = true;

            return meshCopyReference;
        }

        public MeshCopyReference GetMeshCopyReference(string name) {
            //loop through and find it
            foreach (MeshCopyReference meshCopyReference in sourceReferences) {
                if (meshCopyReference.name == name) {
                    return meshCopyReference;
                }
            }
            return null;
        }


        void WalkBones(MeshCopy mesh, Transform currentBone) {
            // Add the current bone to the dictionary

            string boneName = currentBone.name;

            if (mesh.boneMappings.ContainsKey(boneName) == false) {

                if (allBindPoses.ContainsKey(boneName) == true) {

                    int boneIndex = mesh.bones.Count;
                    mesh.bones.Add(currentBone);
                    mesh.boneMappings.Add(boneName, boneIndex);
                    mesh.boneNames.Add(boneName);
                    mesh.bindPoses.Add(allBindPoses[boneName]); 
                }
            }

            // Recursively walk through each child bone
            foreach (Transform childBone in currentBone) {
                WalkBones(mesh, childBone);
            }
        }

        void GetBindPoses(Transform currentBone, Matrix4x4 localToWorldMatrix) {
            // Add the current bone to the dictionary

            string boneName = currentBone.name;
            if (allBindPoses.ContainsKey(boneName) == false) {
                allBindPoses.Add(boneName, currentBone.worldToLocalMatrix * localToWorldMatrix);
            }
            
            // Recursively walk through each child bone
            foreach (Transform childBone in currentBone) {
                GetBindPoses(childBone, localToWorldMatrix);
            }
        }

        public void Start() {
            if (rootBone != null) {
                GetBindPoses(rootBone, rootBone.localToWorldMatrix);
            }
        }

        public void StartMeshUpdate() {
            if (pendingUpdate == false || runningUpdate) {
                return;
            }

            if (debugText == true) {
                Debug.Log("Starting Mesh Update");
            }

            pendingUpdate = false;
            runningUpdate = true;
            newMeshReadyToUse = false;

            //Because this is front facing (users can edit this at any time), duplicate everything
            readOnlySourceReferences = new MeshCopyReference[sourceReferences.Count];
            for (int i = 0; i < sourceReferences.Count; i++) {
                readOnlySourceReferences[i] = (MeshCopyReference)sourceReferences[i].ManualClone();

                foreach (Transform filter in hiddenSurfaces) {
                    if (filter != null) {
                        if (filter == sourceReferences[i].transform) {
                            readOnlySourceReferences[i].maskThisMesh = true;
                        }
                    }
                }
            }

            //Create the new meshes
            finalSkinnedMesh = new MeshCopy();
            finalSkinnedMesh.skinnedMesh = true;

            finalStaticMesh = new MeshCopy();
            finalStaticMesh.skinnedMesh = false;

            //Walk the rootSkeleton and add all the bones to finalSkinnedMesh
            if (rootBone != null) {
                if (allBindPoses.Count == 0) {
                    GetBindPoses(rootBone, rootBone.localToWorldMatrix);
                }
                WalkBones(finalSkinnedMesh, rootBone);
            }

            //Kick off a thread
#pragma warning disable CS0162
            if (runThreaded) {
                ThreadPool.QueueUserWorkItem(ThreadedUpdateMeshWrapper, this);
            }
            else {
                ThreadedUpdateMesh(this);
            }
#pragma warning restore CS0162

        }

        public void ThreadedUpdateMeshWrapper(System.Object state) {
            try {
                ThreadedUpdateMesh(state);
            }
            catch (Exception e) {
                Debug.LogError("Error in ThreadedUpdateMesh: " + e.Message);
            }
        }

        public void ThreadedUpdateMesh(System.Object state) {
            int startTime = System.DateTime.Now.Millisecond;

            //Build a bodyMask for the body
            int bodyMask = 0;
            foreach (MeshCopyReference meshCopyReference in readOnlySourceReferences) {
                if (meshCopyReference.enabled == true) {
                    foreach (MeshCopy meshCopy in meshCopyReference.meshCopy) {
                        bodyMask |= meshCopy.bodyMask;
                    }
                }
            }
            

            foreach (MeshCopyReference meshCopyReference in readOnlySourceReferences) {

                if (meshCopyReference.enabled == false) {
                    // print("meshCopyReference.enabld = false " + meshCopyReference.name);
                    continue;
                }

                //Loop through all the meshes
                foreach (MeshCopy meshCopy in meshCopyReference.meshCopy) {

                    //Duplicate it unpacked
                    //MeshCopy unpackedMeshCopy = meshCopy.ManualCloneUnpackedFat();
                    MeshCopy unpackedMeshCopy = meshCopy.ManualCloneUnpacked();

                    if (meshCopyReference.maskThisMesh) {

                        int numFaces = 0;
                        for (int i = 0; i < meshCopy.subMeshes.Count; i++) {
                            numFaces += meshCopy.subMeshes[i].triangles.Count / 3;
                        }

                        if (bodyMask > 0) {
                            unpackedMeshCopy.DeleteFacesBasedOnBodyMask(bodyMask);
                        }

                        //Count after
                        int afterFaces = 0;
                        for (int i = 0; i < unpackedMeshCopy.subMeshes.Count; i++) {
                            afterFaces += unpackedMeshCopy.subMeshes[i].triangles.Count / 3;
                        }

                        //Print the difference
                        if (debugText == true) {
                            if (numFaces != afterFaces) {
                                Debug.Log("Deleted " + (numFaces - afterFaces) + " faces from " + meshCopyReference.name);
                            }
                            else {
                                Debug.Log("Deleted 0 faces from " + meshCopyReference.name);
                            }
                        }
                    }
                    //Add the mesh
                    if (unpackedMeshCopy.skinnedMesh == true) {
                        finalSkinnedMesh.MergeMeshCopy(unpackedMeshCopy);
                    }
                    else {
                        finalStaticMesh.MergeMeshCopy(unpackedMeshCopy);
                    }

                }
                skinnedMeshBounds = finalSkinnedMesh.CalculateBoundsFromVertexData();
            }

            finalSkinnedMesh.RepackVertices();

            newMeshReadyToUse = true;
            if (debugText == true) {
                Debug.Log("MeshCombiner: Merge (threaded): " + (System.DateTime.Now.Millisecond - startTime) + " ms");
            }
        }

        public void UpdateMesh() {
            if (newMeshReadyToUse == false) {
                return;
            }
            newMeshReadyToUse = false;

            int startTime = System.DateTime.Now.Millisecond;

            //find our MeshCombiner gameObject child
            GameObject meshCombinerGameObjectStatic = null;
            GameObject meshCombinerGameObjectSkinned = null;

            foreach (Transform child in transform) {
                if (child.name == MeshCombineSkinnedName) {
                    meshCombinerGameObjectSkinned = child.gameObject;
                    break;
                }
            }
            foreach (Transform child in transform) {
                if (child.name == MeshCombineStaticName) {
                    meshCombinerGameObjectStatic = child.gameObject;
                    break;
                }
            }

            if (meshCombinerGameObjectSkinned == null) {
                meshCombinerGameObjectSkinned = new GameObject(MeshCombineSkinnedName);
                meshCombinerGameObjectSkinned.transform.parent = transform;

                meshCombinerGameObjectSkinned.transform.localPosition = Vector3.zero;
                meshCombinerGameObjectSkinned.transform.localRotation = Quaternion.Euler(0, 0, 0);
                meshCombinerGameObjectSkinned.transform.localScale = Vector3.one;
                meshCombinerGameObjectSkinned.layer = gameObject.layer;
                meshCombinerGameObjectSkinned.hideFlags = HideFlags.DontSave;
            }
            if (meshCombinerGameObjectStatic == null) {
                meshCombinerGameObjectStatic = new GameObject(MeshCombineStaticName);
                meshCombinerGameObjectStatic.transform.parent = transform;

                meshCombinerGameObjectStatic.transform.localPosition = Vector3.zero;
                meshCombinerGameObjectStatic.transform.localRotation = Quaternion.Euler(0, 0, 0);
                meshCombinerGameObjectStatic.transform.localScale = Vector3.one;
                meshCombinerGameObjectSkinned.layer = gameObject.layer;
                meshCombinerGameObjectStatic.hideFlags = HideFlags.DontSave;
            }

            //Do static mesh
            if (true) {
                combinedStaticMeshFilter = meshCombinerGameObjectStatic.GetComponent<MeshFilter>();
                if (combinedStaticMeshFilter == null) {
                    combinedStaticMeshFilter = meshCombinerGameObjectStatic.AddComponent<MeshFilter>();
                }

                combinedStaticMeshRenderer = meshCombinerGameObjectStatic.GetComponent<MeshRenderer>();
                if (combinedStaticMeshRenderer == null) {
                    combinedStaticMeshRenderer = meshCombinerGameObjectStatic.AddComponent<MeshRenderer>();
                }

                //Apply meshes
                Mesh mesh = new Mesh();
                mesh.name = $"{MeshCombineStaticName}Mesh";
                //Copy out of finalMesh
                mesh.SetVertices(finalStaticMesh.vertices);
                //more
                mesh.SetUVs(0, finalStaticMesh.uvs);
                mesh.SetUVs(1, finalStaticMesh.uvs2);

                mesh.SetNormals(finalStaticMesh.normals);
                mesh.SetTangents(finalStaticMesh.tangents);
                mesh.SetColors(finalStaticMesh.colors);

                //Create subMeshes
                mesh.subMeshCount = finalStaticMesh.subMeshes.Count;
                for (int i = 0; i < finalStaticMesh.subMeshes.Count; i++) {
                    mesh.SetTriangles(finalStaticMesh.subMeshes[i].triangles, i);
                }

                //Copy the materials to the renderer
                Material[] finalMaterials = new Material[finalStaticMesh.subMeshes.Count];
                for (int i = 0; i < finalStaticMesh.subMeshes.Count; i++) {
                    finalMaterials[i] = finalStaticMesh.subMeshes[i].material;
                }
                combinedStaticMeshRenderer.sharedMaterials = finalMaterials;

                combinedStaticMeshFilter.sharedMesh = mesh;
            }

            if (true) {
                //Same thing, but for skinned meshes
                combinedSkinnedMeshRenderer = meshCombinerGameObjectSkinned.GetComponent<SkinnedMeshRenderer>();
                if (combinedSkinnedMeshRenderer == null) {
                    combinedSkinnedMeshRenderer = meshCombinerGameObjectSkinned.AddComponent<SkinnedMeshRenderer>();
                }

                //Apply meshes
                Mesh mesh = new Mesh();
                mesh.name = $"{MeshCombineSkinnedName}Mesh";

                //Copy out of finalMesh
                mesh.SetVertices(finalSkinnedMesh.vertices);
                if (finalSkinnedMesh.vertices.Count == finalSkinnedMesh.boneWeights.Count) {
                    mesh.boneWeights = finalSkinnedMesh.boneWeights.ToArray();
                }
                else {
                    Debug.LogError($"Mismatch bone weights verts: {finalSkinnedMesh.vertices.Count} weights: {finalSkinnedMesh.boneWeights.Count}");
                }

                mesh.SetUVs(0, finalSkinnedMesh.uvs);
                mesh.SetUVs(1, finalSkinnedMesh.uvs2);

                mesh.SetNormals(finalSkinnedMesh.normals);
                mesh.SetTangents(finalSkinnedMesh.tangents);

                mesh.SetColors(finalSkinnedMesh.colors);

                //Create subMeshes
                mesh.subMeshCount = finalSkinnedMesh.subMeshes.Count;
                for (int i = 0; i < finalSkinnedMesh.subMeshes.Count; i++) {
                    mesh.SetTriangles(finalSkinnedMesh.subMeshes[i].triangles, i);
                }

                //Copy the materials to the renderer
                Material[] finalMaterials = new Material[finalSkinnedMesh.subMeshes.Count];
                for (int i = 0; i < finalSkinnedMesh.subMeshes.Count; i++) {
                    finalMaterials[i] = finalSkinnedMesh.subMeshes[i].material;

                }



                /*
                //Clone it because we might want to change things about it
                finalMaterials[i] = new Material(finalSkinnedMesh.subMeshes[i].material);

                if (finalSkinnedMesh.subMeshes[i].batchableMaterialData != null) {

                    finalMaterials[i] = new Material(Shader.Find("Shader Graphs/VertexColorURP"));

                    finalMaterials[i].SetColor("_BaseColor", Color.white);

                }
                else {
                    finalMaterials[i] = new Material(finalSkinnedMesh.subMeshes[i].material);
                */

                combinedSkinnedMeshRenderer.sharedMaterials = finalMaterials;
                combinedSkinnedMeshRenderer.sharedMesh = mesh;
                combinedSkinnedMeshRenderer.sharedMesh.bindposes = finalSkinnedMesh.bindPoses.ToArray();
                combinedSkinnedMeshRenderer.bones = finalSkinnedMesh.bones.ToArray();
                combinedSkinnedMeshRenderer.rootBone = finalSkinnedMesh.rootBone;
                combinedSkinnedMeshRenderer.localBounds = skinnedMeshBounds;

                //if theres instancing data on the materials, do that
                Renderer renderer = combinedSkinnedMeshRenderer;

                MaterialColorURP matColor = meshCombinerGameObjectSkinned.GetComponent<MaterialColorURP>();
                if (matColor == null) {
                    matColor = meshCombinerGameObjectSkinned.AddComponent<MaterialColorURP>();
                }
                matColor.Clear();

                matColor.DoUpdate(); //Forces it to re-add everything
                matColor.InitializeColorsFromCurrentMaterials();

                for (int i = 0; i < finalSkinnedMesh.subMeshes.Count; i++) {

                    if (finalSkinnedMesh.subMeshes[i].batchableMaterialData != null) {
                        MaterialColorURP.ColorSetting setting = matColor.colorSettings[i];
                        if (setting != null) {
                            setting.baseColor = finalSkinnedMesh.subMeshes[i].batchableMaterialData.color;
                        }
                    }

                }
                matColor.DoUpdate();

                /*
                int savingsCount = 0;
                for (int i = 0; i < renderer.sharedMaterials.Length; i++) {
                    MeshCopy.SubMesh subMesh = finalSkinnedMesh.subMeshes[i];
                    if (subMesh.batchableMaterialName != null) {
                        MaterialPropertyBlock block = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(block, i);

                        Vector4[] colorArray = new Vector4[16];
                        for (int j = 0; j < 16; j++) {
                            if (j < subMesh.batchableMaterialData.Count) {
                                colorArray[j] = subMesh.batchableMaterialData[j].color;
                            }
                            else {
                                colorArray[j] = Vector4.zero;
                            }
                        }

                        block.SetVectorArray("_ColorInstanceData", colorArray);
                        renderer.SetPropertyBlock(block, i);

                        //Flip the keyword on
                        renderer.sharedMaterials[i].EnableKeyword("INSTANCE_DATA_ON");
                        renderer.sharedMaterials[i].SetFloat("INSTANCE_DATA_ON", 1);
                        savingsCount += subMesh.batchableMaterialData.Count - 1;
                    }
                }*/
                if (debugText == true) {
                    Debug.Log("MeshCombiner: Finalize (mainthread): " + (System.DateTime.Now.Millisecond - startTime) + " ms");
                }
            }

            foreach (MeshCopyReference reference in readOnlySourceReferences) {
                if (reference.transform) {
                    MeshRenderer meshRenderer = reference.transform.gameObject.GetComponent<MeshRenderer>();
                    if (meshRenderer) {
                        meshRenderer.enabled = false;
                    }
                    reference.transform.gameObject.TryGetComponent<SkinnedMeshRenderer>(out var skinnedMeshRenderer);
                    if (skinnedMeshRenderer) {
                        skinnedMeshRenderer.enabled = false;
                    }
                }
            }

            //we're all done
            if (debugText == true) {
                Debug.Log("all done");
            }
            runningUpdate = false;
            OnCombineComplete?.Invoke();
        }

        public void Dirty() {
            if (debugText == true) {
                Debug.Log("Processing pendingUpdate:" + pendingUpdate + " runningUpdate:" + runningUpdate);
            }
            pendingUpdate = true;
        }


        //update in editor
        private void Update() {
            StartMeshUpdate();
            UpdateMesh();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void OnScriptsReloaded() {
            if (debugText == true) {
                Debug.Log("Scripts reloaded");
            }

            //Go through and reload all of the MeshCopyReferences 
            MeshCombiner[] meshCombiners = GameObject.FindObjectsOfType<MeshCombiner>();
            foreach (MeshCombiner meshCombiner in meshCombiners) {
                meshCombiner.CombineMesh();
            }
        }
#endif

        public void CombineMesh() {
            //reload  sourceMeshes
            foreach (MeshCopyReference reference in sourceReferences) {
                reference.LoadMeshCopy();
            }
            Dirty();
        }

        public void LoadMeshCopies() {
            foreach (MeshCopyReference reference in sourceReferences) {
                reference.LoadMeshCopy();
            }
        }

        public void RemoveBone(string boneName) {
            foreach (var meshRef in sourceReferences) {
                foreach (var meshCopy in meshRef.meshCopy) {
                    meshCopy.DeleteFacesBasedOnBone(boneName);
                }
            }
        }

        public void CombineMeshes() {
            Dirty();
        }

        internal void BuildReferencesFromBaseMesh() {

            if (baseMesh == null) {
                Debug.LogWarning("Base mesh is null");
                return;
            }

            int startTime = System.DateTime.Now.Millisecond;
            //Clear out the references
            sourceReferences.Clear();

            //Add all the children
            Renderer[] renderers = baseMesh.GetComponentsInChildren<Renderer>();

            foreach (Renderer renderer in renderers) {
                //if the child gameobject has a mesh or skinned mesh on it
                MeshFilter meshFilter = renderer.gameObject.GetComponent<MeshFilter>();
                SkinnedMeshRenderer skinnedMeshRenderer = renderer.gameObject.GetComponent<SkinnedMeshRenderer>();

                if (meshFilter != null || skinnedMeshRenderer != null) {
                    if (renderer.gameObject.name == MeshCombineSkinnedName) {
                        continue;
                    }
                    if (renderer.gameObject.name == MeshCombineStaticName) {
                        continue;
                    }

                    if (renderer.gameObject.activeInHierarchy == true) {
                        //Add a reference
                        MeshCopyReference reference = new MeshCopyReference(renderer.gameObject.transform);
                        reference.LoadMeshCopy();
                        sourceReferences.Add(reference);

                        if (renderer.gameObject.activeInHierarchy == false) {
                            reference.enabled = false;
                        }
                    }
                }
            }
            if (debugText == true) {
                Debug.Log("MeshCombiner: Setup (mainthread): " + (System.DateTime.Now.Millisecond - startTime) + " ms");

                //Print this go name
                Debug.Log("MeshCombiner: " + gameObject.name + " has " + sourceReferences.Count + " references");

            }
        }

    }

#if UNITY_EDITOR

    [CustomEditor(typeof(MeshCombiner))]
    public class MeshCombinerEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            //DrawDefaultInspector();
            serializedObject.Update();
            MeshCombiner meshCombinerScript = (MeshCombiner)target;

            //Add baseMesh picker
            meshCombinerScript.baseMesh = (GameObject)EditorGUILayout.ObjectField("Base Mesh", meshCombinerScript.baseMesh, typeof(GameObject), true);

            //Add rootBone picker
            meshCombinerScript.rootBone = (Transform)EditorGUILayout.ObjectField("Root Bone", meshCombinerScript.rootBone, typeof(Transform), true);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Vis", GUILayout.Width(20));
            EditorGUILayout.LabelField("Name");
            EditorGUILayout.EndHorizontal();

            //Draw a bar
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            for (int i = 0; i < meshCombinerScript.sourceReferences.Count; i++) {
                MeshCombiner.MeshCopyReference meshCopyReference = meshCombinerScript.sourceReferences[i];
                EditorGUILayout.BeginHorizontal();
                //add checkbox
                meshCopyReference.enabled = EditorGUILayout.Toggle(meshCopyReference.enabled, GUILayout.Width(20));

                EditorGUILayout.LabelField(meshCopyReference.name);
                if (GUILayout.Button("X", GUILayout.Width(20))) {
                    meshCombinerScript.sourceReferences.RemoveAt(i);
                    meshCombinerScript.Dirty();
                    UnityEditor.EditorUtility.SetDirty(meshCombinerScript);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            //Create an array editor for hidden surfaces
            SerializedProperty hiddenSurfaces = serializedObject.FindProperty("hiddenSurfaces");
            EditorGUILayout.PropertyField(hiddenSurfaces, true);
            

            if (GUILayout.Button("Initialize From BaseMesh")) {

                meshCombinerScript.BuildReferencesFromBaseMesh();
                meshCombinerScript.CombineMesh();
            }
            if (GUILayout.Button("Rebuild Mesh")) {
                meshCombinerScript.CombineMesh();
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.LabelField("Static Verts: " + meshCombinerScript.finalVertCount);
            EditorGUILayout.LabelField("Static Materials: " + meshCombinerScript.finalMaterialCount);
            EditorGUILayout.LabelField("Skinned Verts: " + meshCombinerScript.finalSkinnedVertCount);
            EditorGUILayout.LabelField("Skinned Materials: " + meshCombinerScript.finalSkinnedMaterialCount);
            EditorGUILayout.LabelField("Skinned Bones: " + meshCombinerScript.finalSkinnedBonesCount);
        }

    }
#endif
}
