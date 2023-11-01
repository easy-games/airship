using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;
using System.IO;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Airship
{

    //You build up a MeshCombiner by submitting assets to it
    //It then contains a list of MeshCopyReferences, which can be enabled/disabled or have other operations
    //performed on them
    [ExecuteInEditMode]
    [LuauAPI]
    public class MeshCombiner : MonoBehaviour
    {
        private static bool runThreaded = true;
        public static readonly string MeshCombineSkinnedName = "MeshCombinerSkinned";
        public static readonly string MeshCombineStaticName = "MeshCombinerStatic";
        
        [SerializeField]
        public List<MeshCopyReference> sourceReferences = new List<MeshCopyReference>();

        public override string ToString() {
            string value = "";
            foreach (var copy in sourceReferences) {
                value += copy.ToString() + "\n";
            }
            return value;
        }


        //MeshCopyReference is where to get the mesh data from (an asset, or a child game object)
        [System.Serializable]
        public class MeshCopyReference
        {
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
 

            public override string ToString() {
                return transform == null ? "Asset: " + assetPath : "Transform: " + transform.name;
            }

           

            //Do not serialize
            [NonSerialized]
            public MeshCopy[] meshCopy = null;
            

            public MeshCopyReference(string assetPath, string name)
            {
                this.assetPath = assetPath;
                this.name = name;
            }
            
            public MeshCopyReference(Transform obj)
            {
                this.assetPath = null;
                this.name = obj.name;
                this.transform = obj;
            }
            
            public MeshCopyReference ManualClone()
            {
                MeshCopyReference output = new MeshCopyReference(this.assetPath, this.name);

                //Clone all the members
                output.enabled = this.enabled;
                output.transform = this.transform;
                
                if (this.meshCopy != null)
                {
                    output.meshCopy = new MeshCopy[this.meshCopy.Length];
                    for (int i = 0; i < this.meshCopy.Length; i++)
                    {
                        output.meshCopy[i] = this.meshCopy[i].ManualClone();
                    }
                }
                
                return output;
            } 

            public void LoadMeshCopy()
            {
                if (transform == null)
                {
                    meshCopy = MeshCopy.Load(assetPath, true).ToArray();
                }
                else
                {
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

        public Action OnCombineComplete;


        public float finalVertCount => finalStaticMesh.vertices.Count;
        public float finalSkinnedVertCount => finalSkinnedMesh.vertices.Count;
        public float finalMaterialCount => finalStaticMesh.subMeshes.Count;
        public float finalSkinnedMaterialCount => finalSkinnedMesh.subMeshes.Count;
        public float finalSkinnedBonesCount => finalSkinnedMesh.bones.Count;
 
        public MeshCopyReference AddMesh(string assetPath, string name, bool showError = false)
        {
            //Todo: Pull from a pool?
            //Todo: Allow for callback here to edit mesh before it's processed?
            MeshCopyReference meshCopyReference = new MeshCopyReference(assetPath, name);
            meshCopyReference.LoadMeshCopy();

            sourceReferences.Add(meshCopyReference);
            pendingUpdate = true;

            return meshCopyReference;
        }
 
        public MeshCopyReference GetMeshCopyReference(string name)
        {
            //loop through and find it
            foreach(MeshCopyReference meshCopyReference in sourceReferences)
            {
                if (meshCopyReference.name == name)
                {
                    return meshCopyReference;
                }
            }
            return null;
        }

        public void StartMeshUpdate()
        {
            if (pendingUpdate == false || runningUpdate)
            {
                return;
            }
            
            //Debug.Log("Starting Mesh Update");

            pendingUpdate = false;
            runningUpdate = true;
            newMeshReadyToUse = false;

            //Because this is front facing (users can edit this at any time), duplicate everything
            readOnlySourceReferences = new MeshCopyReference[sourceReferences.Count];
            for(int i = 0; i < sourceReferences.Count; i++)
            {
                readOnlySourceReferences[i] = (MeshCopyReference)sourceReferences[i].ManualClone();
            }
            
            //Kick off a thread
#pragma warning disable CS0162
            if (runThreaded)
            {
                ThreadPool.QueueUserWorkItem(ThreadedUpdateMesh, this);
            }
            else
            {
                ThreadedUpdateMesh(this);
            }
#pragma warning restore CS0162       
            
        }

        public void ThreadedUpdateMesh(System.Object state)
        {
            int startTime = System.DateTime.Now.Millisecond;
            finalSkinnedMesh = new MeshCopy();
            finalSkinnedMesh.skinnedMesh = true;
            
            finalStaticMesh = new MeshCopy();
            finalStaticMesh.skinnedMesh = false;
            
            foreach (MeshCopyReference meshCopyReference in readOnlySourceReferences)
            {
    
                if (meshCopyReference.enabled == false)
                {
                    continue;
                }

                //Loop through all the meshes
                foreach (MeshCopy meshCopy in meshCopyReference.meshCopy)
                {
                    
                    //Duplicate it unpacked

                    MeshCopy unpackedMeshCopy = meshCopy.ManualCloneUnpackedFat();

                    //Add the mesh
                    if (unpackedMeshCopy.skinnedMesh == true)
                    {
                        finalSkinnedMesh.MergeMeshCopy(unpackedMeshCopy);
                    }
                    else
                    {
                        finalStaticMesh.MergeMeshCopy(unpackedMeshCopy);
                    }
                }
                skinnedMeshBounds = finalSkinnedMesh.CalculateBoundsFromVertexData();
                
            }
            
            newMeshReadyToUse = true;
            Debug.Log("MeshCombiner: Merge (threaded): " + (System.DateTime.Now.Millisecond - startTime) + " ms");
        }

        public void UpdateMesh()
        {
            if (newMeshReadyToUse == false)
            {
                return;
            }
            newMeshReadyToUse = false;

            int startTime = System.DateTime.Now.Millisecond;

            //find our MeshCombiner gameObject child
            GameObject meshCombinerGameObjectStatic = null;
            GameObject meshCombinerGameObjectSkinned = null;

            foreach (Transform child in transform)
            {
                if (child.name == MeshCombineSkinnedName)
                {
                    meshCombinerGameObjectSkinned = child.gameObject;
                    break;
                }
            }
            foreach (Transform child in transform)
            {
                if (child.name == MeshCombineStaticName)
                {
                    meshCombinerGameObjectStatic = child.gameObject;
                    break;
                }
            }
            

            if (meshCombinerGameObjectSkinned == null)
            {
                meshCombinerGameObjectSkinned = new GameObject(MeshCombineSkinnedName);
                meshCombinerGameObjectSkinned.transform.parent = transform;

                meshCombinerGameObjectSkinned.transform.localPosition = Vector3.zero;
                meshCombinerGameObjectSkinned.transform.localRotation = Quaternion.Euler(0, 0, 0);
                meshCombinerGameObjectSkinned.transform.localScale = Vector3.one;
                meshCombinerGameObjectSkinned.layer = gameObject.layer;
                meshCombinerGameObjectSkinned.hideFlags = HideFlags.DontSave;
            }
            if (meshCombinerGameObjectStatic == null)
            {
                meshCombinerGameObjectStatic = new GameObject(MeshCombineStaticName);
                meshCombinerGameObjectStatic.transform.parent = transform;

                meshCombinerGameObjectStatic.transform.localPosition = Vector3.zero;
                meshCombinerGameObjectStatic.transform.localRotation = Quaternion.Euler(0, 0, 0);
                meshCombinerGameObjectStatic.transform.localScale = Vector3.one;
                meshCombinerGameObjectSkinned.layer = gameObject.layer;
                meshCombinerGameObjectStatic.hideFlags = HideFlags.DontSave;
            }


            //Do static mesh
            if (true)
            {
                combinedStaticMeshFilter = meshCombinerGameObjectStatic.GetComponent<MeshFilter>();
                if (combinedStaticMeshFilter ==null)
                {
                    combinedStaticMeshFilter = meshCombinerGameObjectStatic.AddComponent<MeshFilter>();
                }

                combinedStaticMeshRenderer = meshCombinerGameObjectStatic.GetComponent<MeshRenderer>();
                if (combinedStaticMeshRenderer == null)
                {
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
                if (finalStaticMesh.instanceData.Count > 0)
                {
                    mesh.SetUVs(7, finalStaticMesh.instanceData);
                }


                mesh.SetNormals(finalStaticMesh.normals);
                mesh.SetTangents(finalStaticMesh.tangents);
                mesh.SetColors(finalStaticMesh.colors);
 
                //Create subMeshes
                mesh.subMeshCount = finalStaticMesh.subMeshes.Count;
                for (int i = 0; i < finalStaticMesh.subMeshes.Count; i++)
                {
                    mesh.SetTriangles(finalStaticMesh.subMeshes[i].triangles, i);
                }

                //Copy the materials to the renderer
                Material[] finalMaterials = new Material[finalStaticMesh.subMeshes.Count];
                for (int i = 0; i < finalStaticMesh.subMeshes.Count; i++)
                {
                    finalMaterials[i] = finalStaticMesh.subMeshes[i].material;
                }
                combinedStaticMeshRenderer.sharedMaterials = finalMaterials;

                combinedStaticMeshFilter.sharedMesh = mesh;
            }

            if (true)
            {
                //Same thing, but for skinned meshes
                combinedSkinnedMeshRenderer = meshCombinerGameObjectSkinned.GetComponent<SkinnedMeshRenderer>();
                if (combinedSkinnedMeshRenderer == null)
                {
                    combinedSkinnedMeshRenderer = meshCombinerGameObjectSkinned.AddComponent<SkinnedMeshRenderer>();
                }

                //Apply meshes
                Mesh mesh = new Mesh();
                mesh.name = $"{MeshCombineSkinnedName}Mesh";

                //Copy out of finalMesh
                mesh.SetVertices(finalSkinnedMesh.vertices);
                if (finalSkinnedMesh.vertices.Count == finalSkinnedMesh.boneWeights.Count) {
                    mesh.boneWeights = finalSkinnedMesh.boneWeights.ToArray();
                } else {
                    Debug.LogError($"Mismatch bone weights verts: {finalSkinnedMesh.vertices.Count} weights: {finalSkinnedMesh.boneWeights.Count}");
                }
                
                //more
                mesh.SetUVs(0, finalSkinnedMesh.uvs);
                mesh.SetUVs(1, finalSkinnedMesh.uvs2);
                if (finalSkinnedMesh.instanceData.Count > 0)
                {
                    mesh.SetUVs(7, finalSkinnedMesh.instanceData);
                }

                mesh.SetNormals(finalSkinnedMesh.normals);
                mesh.SetTangents(finalSkinnedMesh.tangents);
                mesh.SetColors(finalSkinnedMesh.colors);


                //Create subMeshes
                mesh.subMeshCount = finalSkinnedMesh.subMeshes.Count;
                for (int i = 0; i < finalSkinnedMesh.subMeshes.Count; i++)
                {
                    mesh.SetTriangles(finalSkinnedMesh.subMeshes[i].triangles, i);
                }

                //Copy the materials to the renderer
                Material[] finalMaterials = new Material[finalSkinnedMesh.subMeshes.Count];
                for (int i = 0; i < finalSkinnedMesh.subMeshes.Count; i++)
                {
                    //finalMaterials[i] = finalSkinnedMesh.subMeshes[i].material;
                    //Clone it because we might want to change things about it
                    finalMaterials[i] = new Material(finalSkinnedMesh.subMeshes[i].material);
                }
 
                combinedSkinnedMeshRenderer.sharedMaterials = finalMaterials;
                combinedSkinnedMeshRenderer.sharedMesh = mesh;
                combinedSkinnedMeshRenderer.sharedMesh.bindposes = finalSkinnedMesh.bindPoses.ToArray();
                combinedSkinnedMeshRenderer.bones = finalSkinnedMesh.bones.ToArray();
                combinedSkinnedMeshRenderer.rootBone = finalSkinnedMesh.rootBone;
                combinedSkinnedMeshRenderer.localBounds = skinnedMeshBounds;


                //if theres instancing data on the materials, do that
                Renderer renderer = combinedSkinnedMeshRenderer;

                int savingsCount = 0;
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    MeshCopy.SubMesh subMesh = finalSkinnedMesh.subMeshes[i];
                    MaterialPropertyBlock block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, i);

                    Vector4[] colorArray = new Vector4[16];
                    for (int j = 0; j < 16; j++)
                    {
                        if (j < subMesh.batchableMaterialData.Count)
                        {
                            colorArray[j] = subMesh.batchableMaterialData[j].color;
                        }
                        else
                        {
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
                Debug.Log("MeshCombiner: Finalize (mainthread): " + (System.DateTime.Now.Millisecond - startTime) + " ms - (Custom Instancing is saving " + savingsCount + " drawcalls)");
            }

            foreach(MeshCopyReference reference in readOnlySourceReferences)
            {
                if (reference.transform)
                {
                    MeshRenderer meshRenderer = reference.transform.gameObject.GetComponent<MeshRenderer>();
                    if (meshRenderer)
                    {
                        meshRenderer.enabled = false;
                    }
                    SkinnedMeshRenderer skinnedMeshRenderer = reference.transform.gameObject.GetComponent<SkinnedMeshRenderer>();
                    if (skinnedMeshRenderer)
                    {
                        skinnedMeshRenderer.enabled = false;
                    }
                    
                }
            }
            
            //we're all done
            runningUpdate = false;
            OnCombineComplete?.Invoke();
        }

        public void Dirty()
        {
            pendingUpdate = true;
        }
 

        //update in editor
        private void Update()
        {
            StartMeshUpdate();
            UpdateMesh();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void OnScriptsReloaded()
        {
            Debug.Log("Scripts reloaded");

            //Go through and reload all of the MeshCopyReferences 
            MeshCombiner[] meshCombiners = GameObject.FindObjectsOfType<MeshCombiner>();
            foreach (MeshCombiner meshCombiner in meshCombiners)
            {
                meshCombiner.ReloadMeshCopyReferences();
            }
        }
#endif

        public void ReloadMeshCopyReferences()
        {
            //reload  sourceMeshes
            
            foreach (MeshCopyReference reference in sourceReferences)
            {
                reference.LoadMeshCopy();
            }
            Dirty();

        }

        internal void BuildReferencesFromChildren()
        {
            int startTime = System.DateTime.Now.Millisecond;
            //Clear out the references
            sourceReferences.Clear();

            //Add all the children
            Renderer[] renderers = transform.gameObject.GetComponentsInChildren<Renderer>();

            foreach (Renderer renderer in renderers) 
            {
                //if the child gameobject has a mesh or skinned mesh on it
                MeshFilter meshFilter = renderer.gameObject.GetComponent<MeshFilter>();
                SkinnedMeshRenderer skinnedMeshRenderer = renderer.gameObject.GetComponent<SkinnedMeshRenderer>();

                if (meshFilter != null || skinnedMeshRenderer != null)
                {
                    if (renderer.gameObject.name == MeshCombineSkinnedName)
                    {
                        continue;
                    }
                    if (renderer.gameObject.name == MeshCombineStaticName)
                    {
                        continue;
                    }

                    if (renderer.gameObject.activeInHierarchy == true)
                    {
                        //Add a reference
                        MeshCopyReference reference = new MeshCopyReference(renderer.gameObject.transform);
                        reference.LoadMeshCopy();
                        sourceReferences.Add(reference);

                        if (renderer.gameObject.activeInHierarchy == false)
                        {
                            reference.enabled = false;
                        }
                    }

                }

            }
            Debug.Log("MeshCombiner: Setup (mainthread): " + (System.DateTime.Now.Millisecond - startTime) + " ms");
        }
    }

#if UNITY_EDITOR

    [CustomEditor(typeof(MeshCombiner))]
    public class MeshCombinerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            //DrawDefaultInspector();

            MeshCombiner meshCombinerScript = (MeshCombiner)target;
       
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Vis", GUILayout.Width(20));
            EditorGUILayout.LabelField("Name");
            EditorGUILayout.EndHorizontal();
             
            //Draw a bar
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            for (int i = 0; i < meshCombinerScript.sourceReferences.Count; i++)
            {
                MeshCombiner.MeshCopyReference meshCopyReference = meshCombinerScript.sourceReferences[i];
                EditorGUILayout.BeginHorizontal();
                //add checkbox
                meshCopyReference.enabled = EditorGUILayout.Toggle(meshCopyReference.enabled, GUILayout.Width(20));

                EditorGUILayout.LabelField(meshCopyReference.name );
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    meshCombinerScript.sourceReferences.RemoveAt(i);
                    meshCombinerScript.Dirty();
                    UnityEditor.EditorUtility.SetDirty(meshCombinerScript);
                }
                 
                EditorGUILayout.EndHorizontal(); 
            }

            
            /*
            //Todo: This works, but we're disabling it for now until the UX is resolved
            if (GUILayout.Button("Add from .Prefab"))
            {
                //Pick an asset
                string assetPath = EditorUtility.OpenFilePanel("Pick a prefab", "", "prefab");
                if (assetPath.Length != 0)
                {
                    //Get the name
                    string name = Path.GetFileNameWithoutExtension(assetPath);
                    string shortPath = StripAssetsFolder(assetPath);
                    //Add it
                    meshCombinerScript.AddMesh(shortPath, name, true);

                    meshCombinerScript.Dirty();
                    UnityEditor.EditorUtility.SetDirty(meshCombinerScript);
                }
            }*/
            
            if (GUILayout.Button("Initialize From Children"))
            {
                meshCombinerScript.BuildReferencesFromChildren();
                meshCombinerScript.ReloadMeshCopyReferences();
            }
            if (GUILayout.Button("Rebuild Mesh"))
            {
                meshCombinerScript.ReloadMeshCopyReferences();
            }

            EditorGUILayout.LabelField("Static Verts: " + meshCombinerScript.finalVertCount);
            EditorGUILayout.LabelField("Static Materials: " + meshCombinerScript.finalMaterialCount);
            EditorGUILayout.LabelField("Skinned Verts: " + meshCombinerScript.finalSkinnedVertCount);
            EditorGUILayout.LabelField("Skinned Materials: " + meshCombinerScript.finalSkinnedMaterialCount);
            EditorGUILayout.LabelField("Skinned Bones: " + meshCombinerScript.finalSkinnedBonesCount);

          
        }

        private string StripAssetsFolder(string filePath)
        {
            //Trasnform something like this: D:/EasyGG/bedwars-airship/Assets/Bundles/Imports/Core/Shared/Resources/VoxelWorld/Meshes/Tilesets/OakLeaf/Leaf1x1x1.prefab
            //Into something like this: Imports/Core/Shared/Resources/VoxelWorld/Meshes/Tilesets/OakLeaf/Leaf1x1x1

            //strip the extension
            string extension = Path.GetExtension(filePath);
            filePath = filePath.Substring(0, filePath.Length - extension.Length);

            string checkString = "/Assets/Bundles/";
            int bundlesIndex = string.IsNullOrEmpty(filePath) ? -1 : filePath.IndexOf(checkString);
            if (bundlesIndex >= 0)
            {
                filePath = filePath.Substring(bundlesIndex + checkString.Length);
            }
            return filePath;
        }
    }
#endif
}
