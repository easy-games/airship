using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;
using System.Diagnostics;
using System.IO;
using Code.Airship.Resources.Scripts;
using UnityEngine.Profiling;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Airship {

    class MeshCombinerCache {
        public Mesh mesh;
        public MeshCopy finalSkinnedMesh;
        public Bounds skinnedMeshBounds;

        public MeshCombinerCache(Mesh mesh, MeshCopy finalSkinnedMesh, Bounds skinnedMeshBounds) {
            this.mesh = mesh;
            this.finalSkinnedMesh = finalSkinnedMesh;
            this.skinnedMeshBounds = skinnedMeshBounds;
        }
    }

    //You build up a MeshCombiner by submitting assets to it
    //It then contains a list of MeshCopyReferences, which can be enabled/disabled or have other operations
    //performed on them
    [ExecuteInEditMode]
    [LuauAPI]
    public class MeshCombiner : MonoBehaviour {
        private static bool runThreaded = true;
        private static bool debugText = false;
        private static bool useCache = false;
        public static readonly string MeshCombineSkinnedName = "CombinedMeshRenderer";

        [SerializeField]
        public SkinnedMeshRenderer combinedSkinnedMeshRenderer;

        [SerializeField] public MaterialColorURP materialColorURP;

        private static Dictionary<string, MeshCombinerCache> meshCache = new();

        [SerializeField]
        public Transform rootBone;

        [SerializeField]
        public bool createOverlayMesh = false;

        [SerializeField]
        public List<Transform> hiddenSurfaces = new();
         
        [NonSerialized]
        private List<MeshCopyReference> sourceReferences = new List<MeshCopyReference>();

        [SerializeField] public CharacterRig rig;

        [NonSerialized]
        private Dictionary<string, Matrix4x4> allBindPoses = new();

        [NonSerialized]
        private MeshCopy finalSkinnedMesh = new MeshCopy();

        [NonSerialized] private MeshCopyReference[] readOnlySourceReferences;
        [NonSerialized] private bool pendingUpdate = false;
        [NonSerialized] private bool runningUpdate = false;
        [NonSerialized] private bool newMeshReadyToUse = false;
        [NonSerialized] private Bounds skinnedMeshBounds;
        [NonSerialized] public Action OnCombineComplete;

        [NonSerialized] public string cacheId = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        [HideFromTS]
        public static void OnLoad() {
            meshCache.Clear();
        }

        // Used by TS
        public static void RemoveMeshCache(string cacheId) {
            meshCache.Remove(cacheId);
        }

        public override string ToString() {
            string value = "Mesh Combiner. Meshes: ";
            foreach (var copy in sourceReferences) {
                value += "  - " + copy.ToString() + "\n";
            }
            return value;
        }


        // public float finalVertCount => finalStaticMesh.vertices.Count;
        public float finalSkinnedVertCount => finalSkinnedMesh.vertices.Count;
        // public float finalMaterialCount => finalStaticMesh.subMeshes.Count;
        public float finalSkinnedMaterialCount => finalSkinnedMesh.subMeshes.Count;
        public float finalSkinnedBonesCount => finalSkinnedMesh.bones.Count;

        public void AddSourceReference(ActiveAccessory activeAccessory) {
            this.sourceReferences.Add(new MeshCopyReference(activeAccessory));
        }

        public void ClearSourceReferences() {
            this.sourceReferences.Clear();
            // this.sourceReferences.RemoveRange(3, this.sourceReferences.Count - 3);

            // add base meshes
            this.sourceReferences.Add(new MeshCopyReference(this.rig.headMesh, this.rig.headColor));
            this.sourceReferences.Add(new MeshCopyReference(this.rig.bodyMesh, this.rig.bodyColor));
            this.sourceReferences.Add(new MeshCopyReference(this.rig.armsMesh, this.rig.armsColor));
        }

        private void WalkBones(MeshCopy mesh, Transform currentBone) {
            // Add the current bone to the dictionary
            string boneName = currentBone.name;
            if (!mesh.boneMappings.ContainsKey(boneName)) {
                if (allBindPoses.TryGetValue(boneName, out var pose)) {
                    int boneIndex = mesh.bones.Count;
                    mesh.bones.Add(currentBone);
                    mesh.boneMappings.Add(boneName, boneIndex);
                    mesh.boneNames.Add(boneName);
                    mesh.bindPoses.Add(pose);
                }
            }

            // Recursively walk through each child bone
            foreach (Transform childBone in currentBone) {
                WalkBones(mesh, childBone);
            }
        }

        private void GetBindPoses(Transform currentBone, Matrix4x4 localToWorldMatrix) {
            // Add the current bone to the dictionary

            string boneName = currentBone.name;
            allBindPoses.TryAdd(boneName, currentBone.worldToLocalMatrix * localToWorldMatrix);

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

        private void StartMeshUpdate() {
            if (pendingUpdate == false || runningUpdate) {
                return;
            }

            if (useCache && !string.IsNullOrEmpty(this.cacheId) && meshCache.TryGetValue(this.cacheId, out var cache)) {
                if (debugText) {
                    print("Skipping threaded because we have cache: " + this.cacheId);
                }
                this.pendingUpdate = false;
                this.runningUpdate = false;
                this.newMeshReadyToUse = true;
                return;
            }

            this.pendingUpdate = false;
            this.runningUpdate = true;
            this.newMeshReadyToUse = false;

            var st = Stopwatch.StartNew();
            this.LoadMeshCopies();

            // Because this is front facing (users can edit this at any time), duplicate everything
            this.readOnlySourceReferences = new MeshCopyReference[this.sourceReferences.Count];
            for (int i = 0; i < this.sourceReferences.Count; i++) {
                this.readOnlySourceReferences[i] = this.sourceReferences[i].ManualClone();

                foreach (Transform filter in this.hiddenSurfaces) {
                    if (filter != null) {
                        if (filter == this.sourceReferences[i].transform) {
                            this.readOnlySourceReferences[i].maskThisMesh = true;
                        }
                    }
                }
            }

            // Create the new meshes
            this.finalSkinnedMesh = new MeshCopy {
                skinnedMesh = true
            };

            // Walk the rootSkeleton and add all the bones to finalSkinnedMesh
            this.WalkBones(this.finalSkinnedMesh, this.rootBone);

            if (debugText) {
                Debug.Log("[MeshCombiner] First half main thread time: " + st.Elapsed.TotalMilliseconds + "ms");
            }


            // Kick off a thread
// #pragma warning disable CS0162
            if (runThreaded) {
                ThreadPool.QueueUserWorkItem(ThreadedUpdateMeshWrapper, this);
            } else {
                ThreadedUpdateMesh(this);
            }
// #pragma warning restore CS0162
        }

        private void ThreadedUpdateMeshWrapper(System.Object state) {
            try {
                Profiler.BeginThreadProfiling("MeshCombiner", "MeshCombiner");
                Profiler.BeginSample("ThreadedMeshCombinerUpdate");
                this.ThreadedUpdateMesh(state);
                Profiler.EndSample();
                Profiler.EndThreadProfiling();
            } catch (Exception e) {
                Debug.LogError("Error in ThreadedUpdateMesh: " + e.Message);
            }
        }

        private void ThreadedUpdateMesh(System.Object state) {
            var st = Stopwatch.StartNew();

            // Build a bodyMask for the body
            var bodyMask = 0;
            foreach (MeshCopyReference meshCopyReference in this.readOnlySourceReferences) {
                if (meshCopyReference.enabled == true) {
                    foreach (MeshCopy meshCopy in meshCopyReference.meshCopies) {
                        bodyMask |= meshCopy.bodyMask;
                    }
                }
            }

            foreach (MeshCopyReference meshCopyReference in this.readOnlySourceReferences) {
                if (meshCopyReference.enabled == false) {
                    // print("meshCopyReference.enabld = false " + meshCopyReference.name);
                    continue;
                }

                //Loop through all the meshes
                foreach (MeshCopy meshCopy in meshCopyReference.meshCopies) {
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
                        // if (debugText == true) {
                        //     if (numFaces != afterFaces) {
                        //         Debug.Log("Deleted " + (numFaces - afterFaces) + " faces from " + meshCopyReference.name);
                        //     }
                        //     else {
                        //         Debug.Log("Deleted 0 faces from " + meshCopyReference.name);
                        //     }
                        // }
                    }
                    //Add the mesh
                    if (unpackedMeshCopy.skinnedMesh) {
                        finalSkinnedMesh.MergeMeshCopy(unpackedMeshCopy);
                    }
                    // else {
                    //     Debug.LogWarning("Doing static mesh combine! If you see this, please tell Luke.");
                    //     finalStaticMesh.MergeMeshCopy(unpackedMeshCopy);
                    // }

                }
                skinnedMeshBounds = finalSkinnedMesh.CalculateBoundsFromVertexData();
            }

            finalSkinnedMesh.RepackVertices();

            newMeshReadyToUse = true;
            if (debugText) {
                Debug.Log($"MeshCombiner: Merge (threaded): {st.ElapsedMilliseconds} ms");
            }
        }

        private void UpdateMeshMainThread() {
            if (newMeshReadyToUse == false) {
                return;
            }
            newMeshReadyToUse = false;

            var st = Stopwatch.StartNew();
            {
                // Update output skinned mesh
                Mesh mesh;
                if (useCache && !string.IsNullOrEmpty(this.cacheId) && meshCache.TryGetValue(this.cacheId, out MeshCombinerCache cache)) {
                    var dupeSt = Stopwatch.StartNew();
                    mesh = Instantiate(cache.mesh);
                    this.finalSkinnedMesh = cache.finalSkinnedMesh;
                    this.finalSkinnedMesh.rootBone = this.rootBone;
                    this.skinnedMeshBounds = cache.skinnedMeshBounds;
                    this.WalkBones(this.finalSkinnedMesh, this.rootBone);
                    if (debugText) {
                        Debug.Log($"Duplicated cached mesh ({this.cacheId}) in {dupeSt.Elapsed.TotalMilliseconds} ms");
                    }
                } else {
                    var meshSt = Stopwatch.StartNew();

                    //Apply mesh
                    mesh = new Mesh {
                        name = $"{MeshCombineSkinnedName}Mesh",
                    };

                    //Copy out of finalMesh
                    mesh.SetVertices(this.finalSkinnedMesh.vertices);
                    if (this.finalSkinnedMesh.vertices.Count == this.finalSkinnedMesh.boneWeights.Count) {
                        mesh.boneWeights = this.finalSkinnedMesh.boneWeights.ToArray();
                    } else {
                        Debug.LogError($"Mismatch bone weights verts: {this.finalSkinnedMesh.vertices.Count} weights: {this.finalSkinnedMesh.boneWeights.Count}");
                    }

                    mesh.SetUVs(0, this.finalSkinnedMesh.uvs);
                    mesh.SetUVs(1, this.finalSkinnedMesh.uvs2);
                    mesh.SetNormals(this.finalSkinnedMesh.normals);
                    mesh.SetTangents(this.finalSkinnedMesh.tangents);
                    mesh.SetColors(this.finalSkinnedMesh.colors);

                    // Create subMeshes
                    mesh.subMeshCount = this.finalSkinnedMesh.subMeshes.Count;
                    for (int i = 0; i < this.finalSkinnedMesh.subMeshes.Count; i++) {
                        mesh.SetTriangles(this.finalSkinnedMesh.subMeshes[i].triangles, i);
                    }

                    // Create an extra sub mesh for rendering a full body material
                    if (createOverlayMesh) {
                        var subMeshCount = mesh.subMeshCount;
                        mesh.subMeshCount = subMeshCount + 1;
                        mesh.SetTriangles(mesh.triangles, subMeshCount);
                    }

                    if (!string.IsNullOrEmpty(this.cacheId)) {
                        meshCache[this.cacheId] = new MeshCombinerCache(mesh, this.finalSkinnedMesh, this.skinnedMeshBounds);
                    }

                    if (debugText) {
                        Debug.Log("Copy mesh time: " + meshSt.Elapsed.TotalMilliseconds + " ms");
                    }
                }

                // Copy the materials to the renderer
                Material[] finalMaterials = new Material[finalSkinnedMesh.subMeshes.Count];
                for (int i = 0; i < finalSkinnedMesh.subMeshes.Count; i++) {
                    finalMaterials[i] = finalSkinnedMesh.subMeshes[i].material;
                }

                this.combinedSkinnedMeshRenderer.sharedMaterials = finalMaterials;
                this.combinedSkinnedMeshRenderer.sharedMesh = mesh;
                this.combinedSkinnedMeshRenderer.sharedMesh.bindposes = this.finalSkinnedMesh.bindPoses.ToArray();
                this.combinedSkinnedMeshRenderer.bones = this.finalSkinnedMesh.bones.ToArray();
                this.combinedSkinnedMeshRenderer.rootBone = this.finalSkinnedMesh.rootBone;
                this.combinedSkinnedMeshRenderer.localBounds = this.skinnedMeshBounds;

                {
                    // Skin color
                    var matColorSt = Stopwatch.StartNew();
                    this.materialColorURP.RefreshVariables();
                    for (int i = 0; i < this.finalSkinnedMesh.subMeshes.Count; i++) {
                        if (this.finalSkinnedMesh.subMeshes[i].batchableMaterialData != null) {
                            MaterialColorURP.ColorSetting setting = this.materialColorURP.colorSettings[i];
                            if (setting != null) {
                                setting.baseColor = this.finalSkinnedMesh.subMeshes[i].batchableMaterialData.color;
                            }
                        }
                    }
                    this.materialColorURP.DoUpdate();
                    if (debugText) {
                        Debug.Log($"MaterialColorURP update: {matColorSt.Elapsed.TotalMilliseconds} ms.");
                    }
                }

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
            }

            // // Disable renderers we combined
            // foreach (MeshCopyReference reference in this.sourceReferences) {
            //     if (reference.activeAccessory != null) {
            //         foreach (var activeAccessoryRenderer in reference.activeAccessory.renderers) {
            //             activeAccessoryRenderer.enabled = false;
            //         }
            //     } else {
            //         // base mesh
            //         reference.transform.gameObject.SetActive(false);
            //     }
            // }

            if (debugText) {
                Debug.Log($"[{this.gameObject.GetInstanceID()}] MeshCombiner: Finalize (main thread): {st.Elapsed.TotalMilliseconds} ms");
            }

            // We're all done
            this.runningUpdate = false;
            this.OnCombineComplete?.Invoke();
        }

        private void Dirty() {
            pendingUpdate = true;
        }


        //update in editor
        private void Update() {
            StartMeshUpdate();
            UpdateMeshMainThread();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void OnScriptsReloaded() {
            //Go through and reload all of the MeshCopyReferences 
            MeshCombiner[] meshCombiners = GameObject.FindObjectsOfType<MeshCombiner>();
            foreach (MeshCombiner meshCombiner in meshCombiners) {
                meshCombiner.CombineMesh();
            }
        }
#endif

        public void CombineMesh() {
            //reload sourceMeshes
            // foreach (MeshCopyReference reference in sourceReferences) {
            //     reference.LoadMeshCopies();
            // }
            // Dirty();
        }

        public void LoadMeshCopies() {
            var stAccessories = Stopwatch.StartNew();
            var stBase = Stopwatch.StartNew();
            foreach (MeshCopyReference reference in this.sourceReferences) {
                if (reference.activeAccessory != null) {
                    stAccessories.Start();
                    stBase.Stop();
                    reference.LoadMeshCopiesByAccessory();
                } else {
                    stAccessories.Stop();
                    stBase.Start();
                    reference.LoadMeshCopiesAsBaseMesh();
                }
            }

            if (debugText) {
                Debug.Log($"LoadMeshCopies {stBase.Elapsed.TotalMilliseconds + stAccessories.Elapsed.TotalMilliseconds} ms (base: " + stBase.Elapsed.TotalMilliseconds + " ms" + ", accessories: " + stAccessories.Elapsed.TotalMilliseconds + " ms)");
            }
        }

        public void RemoveBone(string boneName) {
            foreach (var meshRef in sourceReferences) {
                foreach (var meshCopy in meshRef.meshCopies) {
                    meshCopy.DeleteFacesBasedOnBone(boneName);
                }
            }
        }

        public void CombineMeshes() {
            // Disable renderers we will combine
            this.rig.headMesh.gameObject.SetActive(false);
            this.rig.bodyMesh.gameObject.SetActive(false);
            this.rig.armsMesh.gameObject.SetActive(false);

            Dirty();
        }

        // internal void BuildReferencesFromBaseMesh() {
        //     if (baseMesh == null) {
        //         Debug.LogWarning("Base mesh is null");
        //         return;
        //     }
        //
        //     int startTime = System.DateTime.Now.Millisecond;
        //     //Clear out the references
        //     sourceReferences.Clear();
        //
        //     //Add all the children
        //     Renderer[] renderers = baseMesh.GetComponentsInChildren<Renderer>();
        //
        //     foreach (Renderer renderer in renderers) {
        //         //if the child gameobject has a mesh or skinned mesh on it
        //         MeshFilter meshFilter = renderer.gameObject.GetComponent<MeshFilter>();
        //         SkinnedMeshRenderer skinnedMeshRenderer = renderer.gameObject.GetComponent<SkinnedMeshRenderer>();
        //
        //         if (meshFilter != null || skinnedMeshRenderer != null) {
        //             if (renderer.gameObject.name == MeshCombineSkinnedName) {
        //                 continue;
        //             }
        //             if (renderer.gameObject.name == MeshCombineStaticName) {
        //                 continue;
        //             }
        //
        //             if (renderer.gameObject.activeInHierarchy == true) {
        //                 //Add a reference
        //                 MeshCopyReference reference = new MeshCopyReference(renderer.gameObject.transform);
        //                 reference.LoadMeshCopy();
        //                 sourceReferences.Add(reference);
        //
        //                 if (renderer.gameObject.activeInHierarchy == false) {
        //                     reference.enabled = false;
        //                 }
        //             }
        //         }
        //     }
        //     if (debugText == true) {
        //         // Debug.Log($"[{this.gameObject.GetInstanceID()}] MeshCombiner: Setup (main thread): " + (System.DateTime.Now.Millisecond - startTime) + " ms");
        //
        //         //Print this go name

        //         // Debug.Log("MeshCombiner: " + gameObject.name + " has " + sourceReferences.Count + " references");
        //     }
        // }

    }

// #if UNITY_EDITOR
//
//     [CustomEditor(typeof(MeshCombiner))]
//     public class MeshCombinerEditor : UnityEditor.Editor {
//         public override void OnInspectorGUI() {
//             //DrawDefaultInspector();
//             serializedObject.Update();
//             MeshCombiner meshCombinerScript = (MeshCombiner)target;
//
//             meshCombinerScript.combinedSkinnedMeshRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Combined Skinned Mesh Renderer", meshCombinerScript.combinedSkinnedMeshRenderer, typeof(SkinnedMeshRenderer), true);
//
//             meshCombinerScript.materialColorURP = (MaterialColorURP)EditorGUILayout.ObjectField("Material Color", meshCombinerScript.materialColorURP, typeof(MaterialColorURP), true);
//
//             //Add baseMesh picker
//             // meshCombinerScript.baseMesh = (GameObject)EditorGUILayout.ObjectField("Base Mesh", meshCombinerScript.baseMesh, typeof(GameObject), true);
//
//             //Add rootBone picker
//             meshCombinerScript.rootBone = (Transform)EditorGUILayout.ObjectField("Root Bone", meshCombinerScript.rootBone, typeof(Transform), true);
//
//             meshCombinerScript.createOverlayMesh = EditorGUILayout.Toggle("Create Overlay Mesh", meshCombinerScript.createOverlayMesh);
//             EditorGUILayout.BeginHorizontal();
//             EditorGUILayout.LabelField("Vis", GUILayout.Width(20));
//             EditorGUILayout.LabelField("Name");
//             EditorGUILayout.EndHorizontal();
//
//             //Draw a bar
//             EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
//
//             // for (int i = 0; i < meshCombinerScript.sourceReferences.Count; i++) {
//             //     MeshCombiner.MeshCopyReference meshCopyReference = meshCombinerScript.sourceReferences[i];
//             //     EditorGUILayout.BeginHorizontal();
//             //     //add checkbox
//             //     meshCopyReference.enabled = EditorGUILayout.Toggle(meshCopyReference.enabled, GUILayout.Width(20));
//             //
//             //     EditorGUILayout.LabelField(meshCopyReference.name);
//             //     if (GUILayout.Button("X", GUILayout.Width(20))) {
//             //         meshCombinerScript.sourceReferences.RemoveAt(i);
//             //         meshCombinerScript.Dirty();
//             //         UnityEditor.EditorUtility.SetDirty(meshCombinerScript);
//             //     }
//             //
//             //     EditorGUILayout.EndHorizontal();
//             // }
//             //
//             // EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
//             //
//             // //Create an array editor for hidden surfaces
//             // SerializedProperty hiddenSurfaces = serializedObject.FindProperty("hiddenSurfaces");
//             // EditorGUILayout.PropertyField(hiddenSurfaces, true);
//             //
//             //
//             // // if (GUILayout.Button("Initialize From BaseMesh")) {
//             // //     meshCombinerScript.BuildReferencesFromBaseMesh();
//             // //     meshCombinerScript.CombineMesh();
//             // // }
//             // // if (GUILayout.Button("Rebuild Mesh")) {
//             // //     meshCombinerScript.CombineMesh();
//             // // }
//             //
//             // serializedObject.ApplyModifiedProperties();
//             //
//             // EditorGUILayout.LabelField("Skinned Verts: " + meshCombinerScript.finalSkinnedVertCount);
//             // EditorGUILayout.LabelField("Skinned Materials: " + meshCombinerScript.finalSkinnedMaterialCount);
//             // EditorGUILayout.LabelField("Skinned Bones: " + meshCombinerScript.finalSkinnedBonesCount);
//         }
//
//     }
// #endif
}
