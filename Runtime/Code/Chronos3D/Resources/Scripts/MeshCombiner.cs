using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using System.Threading;
using System.IO;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif
 
namespace Airship
{

    //This class exists because we cannot manipulate actual UnityEngine.Meshs in a thread
    //So we copy and cache assets here first
    
    public class MeshCopy
    {
        public class SubMesh 
        {
            public List<int> triangles = new();
            public Material material = null;
            
            public SubMesh ManualClone()
            {
                SubMesh output = new SubMesh();
                output.triangles = new List<int>(triangles);
                output.material = material;
                return output;
            }
        }
        
        //List of vertices uvs etc
        public List<Vector3> vertices = new();
        public List<Vector3> normals = new();
        public List<Vector4> tangents = new();
        public List<Vector3> binormals = new();
        public List<Vector2> uvs = new();
        public List<Vector2> uvs2 = new();
        public List<Color> colors = new();
        public List<Bone> bones = new();
        public List<Matrix4x4> bindPoses = new();
        public List<BoneWeight> boneWeights = new();
        public List<SubMesh> subMeshes = new();
                
        public Matrix4x4 transformUsed;

        public MeshCopy(Mesh mesh, Material[] materials,  Matrix4x4 worldMatrix)
        {
            //Copy the data to our local arrays
            mesh.GetVertices(vertices);
            mesh.GetNormals(normals);
            mesh.GetTangents(tangents);
            
            //transform the verts and normals
            if (worldMatrix.isIdentity == false)
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    vertices[i] = worldMatrix.MultiplyPoint3x4(vertices[i]);
                    normals[i] = worldMatrix.MultiplyVector(normals[i]);
                
                    Vector3 tangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                    tangent = worldMatrix.MultiplyVector(tangent).normalized;
                    tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
                }
            }
            
            //Enforce all meshes having UVs, Uv2s and Colors
            //Todo: Escalate this based on an survey of whats actually needed
            mesh.GetUVs(0, uvs);
            if (uvs.Count == 0)
            {
                uvs = new List<Vector2>(new Vector2[vertices.Count]);
            }

            mesh.GetUVs(1, uvs2);
            if (uvs2.Count == 0)
            {
                uvs2 = new List<Vector2>(new Vector2[vertices.Count]);
            }

            mesh.GetColors(colors);
            if (colors.Count == 0)
            {
                colors = new List<Color>(vertices.Count);
                for (int i = 0; i < vertices.Count; i++)
                {
                    colors.Add(Color.white);
                }
            }
                        
            mesh.GetBoneWeights(boneWeights);
            mesh.GetBindposes(bindPoses);
            
            
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                SubMesh subMesh = new();

                Material mat = null;
                if (i < materials.Length)
                {
                    mat = materials[i];
                }
                else
                {
                    //default material
                    mat = new Material(Shader.Find("Standard"));
                }
                subMesh.material = mat;
                mesh.GetTriangles(subMesh.triangles, i);
                subMeshes.Add(subMesh);
            }

            transformUsed = worldMatrix;
        }
        public MeshCopy()
        {
        }    

        public MeshCopy ManualClone()
        {
            //Don't forget to update this when you add new fields
            MeshCopy copy = new();
            copy.vertices = new List<Vector3>(vertices);
            copy.normals = new List<Vector3>(normals);
            copy.tangents = new List<Vector4>(tangents);
            copy.uvs = new List<Vector2>(uvs);
            copy.uvs2 = new List<Vector2>(uvs2);
            copy.colors = new List<Color>(colors);
            copy.bones = new List<Bone>(bones);
            copy.bindPoses = new List<Matrix4x4>(bindPoses);
            copy.boneWeights = new List<BoneWeight>(boneWeights);
            copy.subMeshes = new List<SubMesh>(subMeshes.Count);
            for (int i = 0; i < subMeshes.Count; i++)
            {
                copy.subMeshes.Add(subMeshes[i].ManualClone());
            }

            copy.transformUsed = transformUsed;
            return copy;
        }

        public void MergeMeshCopy(MeshCopy source)
        {
            //Take the contents of another meshCopy and absorb it


            //Todo: this is very wrong.
            int currentVertexCount = vertices.Count;
            
            vertices.AddRange(source.vertices);
            normals.AddRange(source.normals);
            tangents.AddRange(source.tangents);
            uvs.AddRange(source.uvs);
            uvs2.AddRange(source.uvs2);
            colors.AddRange(source.colors);

            if (colors.Count != vertices.Count)
            {
                //warn!
                Debug.LogWarning("MeshCopy: Color count does not match vertex count");
            }
            
            bones.AddRange(source.bones);
            bindPoses.AddRange(source.bindPoses);
            boneWeights.AddRange(source.boneWeights);
            
            
            for (int i = 0; i < source.subMeshes.Count; i++)
            {
                SubMesh sourceMesh = source.subMeshes[i];
                if (sourceMesh.material == null)
                {
                    sourceMesh.material = new Material(Shader.Find("Hidden/ChronosErrorShader"));
                }
                
                int hash = sourceMesh.material.GetHashCode();

                //find a submesh with a matching material
                SubMesh target = null;
                for (int j = 0; j < subMeshes.Count; j++)
                {
                    if (subMeshes[j].material.GetHashCode() == hash)
                    {
                        target = subMeshes[j];
                        break;
                    }
                }

                if (target == null)
                {
                    //no submesh with this material, add it
                    target = new SubMesh();
                    target.material = sourceMesh.material;
                    subMeshes.Add(target);
                }

                //Add all the triangle indices to the target, but increment them by the current vertex count
                //
                //This is because we are merging two meshes, and the triangle indices in the source mesh
                //are relative to the source mesh, not the target mesh
                for (int j = 0; j < source.subMeshes[i].triangles.Count; j++)
                {
                    target.triangles.Add(source.subMeshes[i].triangles[j] + currentVertexCount);
                }
            }
            

        }

        public static List<MeshCopy> Load(string assetPath, bool showError)
        {
            List<MeshCopy> results = new();

            UnityEngine.Object asset = AssetBridge.Instance.LoadAssetInternal<UnityEngine.Object>(assetPath + ".prefab", false);

            if (asset == null)
            {
                asset = AssetBridge.Instance.LoadAssetInternal<UnityEngine.Object>(assetPath + ".FBX", false);
            }

            if (asset == null && showError == true)
            {
                Debug.LogError("Failed to load asset at path: " + assetPath);
                return results;
            }

            if (asset is Mesh)
            {
                
                Mesh mesh = asset as Mesh;
                Material[] mats = new Material[0];
                MeshCopy meshCopy = new MeshCopy(mesh, mats, Matrix4x4.identity);
                results.Add(meshCopy);
                
                return results;
            }

            if (asset is GameObject)
            {
                // Instantiate the prefab
                GameObject instance = GameObject.Instantiate((GameObject)asset);

                //Recursively interate over all child gameObjects
                GetMeshes(instance, results);

                if (Application.isPlaying == true)
                {
                    GameObject.Destroy(instance);
                }
                else
                {
                    GameObject.DestroyImmediate(instance);
                }

                return results;
            }
            return results;
        }

        //Recursively get all filters and materials
        private static void GetMeshes(GameObject gameObject, List<MeshCopy> results)
        {
            //Get the mesh filter
            MeshFilter filter = gameObject.GetComponent<MeshFilter>();
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            
            if (filter != null && renderer != null)
            {
                //See if theres a MaterialColor on this gameObject
                MeshCopy meshCopy = new MeshCopy(filter.sharedMesh, renderer.sharedMaterials,  gameObject.transform.localToWorldMatrix);
                
                MaterialColor matColor = gameObject.GetComponent<MaterialColor>();
                if (matColor)
                {
                    meshCopy.ApplyMaterialColor(matColor);
                }
                
                results.Add(meshCopy);
            }

            SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer)
            {
                //See if theres a MaterialColor on this gameObject
                MeshCopy meshCopy = new MeshCopy(skinnedMeshRenderer.sharedMesh, skinnedMeshRenderer.sharedMaterials, gameObject.transform.localToWorldMatrix);

                MaterialColor matColor = gameObject.GetComponent<MaterialColor>();
                if (matColor)
                {
                    meshCopy.ApplyMaterialColor(matColor);
                }

                results.Add(meshCopy);
            }


            //Get the children
            foreach (Transform child in gameObject.transform)
            {
                GetMeshes(child.gameObject, results);
            }
        }

        private void ApplyMaterialColor(MaterialColor matColor)
        {
            //Apply the material color
            for (int i = 0; i < subMeshes.Count; i++)
            {
                var colorData = matColor.GetColor(i);
                if (colorData != null)
                {
                    SubMesh subMesh = subMeshes[i];
                
                    //Duplicate the materials
                    //Todo: Make it possible to set these properties in a vertex channel
                    //Also todo: Should this logic move back to MaterialColor class?
                    subMesh.material = new Material(subMesh.material);
                    subMesh.material.SetColor("_Color", matColor.ConvertColor(colorData.materialColor));
                    subMesh.material.SetColor("_EmissiveColor", matColor.ConvertColor(colorData.emissiveColor));
                    subMesh.material.SetFloat("_EmissiveMix", colorData.emissiveMix);
                }
            }
        }
    }


    //You build up a MeshCombiner by submitting assets to it
    //It then contains a list of MeshCopyReferences, which can be enabled/disabled or have other operations
    //performed on them
    [ExecuteInEditMode]
    [LuauAPI]
    public class MeshCombiner : MonoBehaviour
    {
        private static bool runThreaded = false;
        
        [SerializeField]
        public List<MeshCopyReference> sourceReferences = new List<MeshCopyReference>();


        [System.Serializable]
        public class MeshCopyReference
        {
            public MeshCombiner combiner;
            [SerializeField]
            public string assetPath;
            [SerializeField]
            public string name;
            [SerializeField]
            public bool enabled = true;
            [SerializeField]
            private Matrix4x4 _transform;
         
            //Do not serialize
            [NonSerialized]
            public MeshCopy[] meshCopy = null;
            
            //Add a setter on transform
            [SerializeField]
            private bool useTransform = false;
            public Matrix4x4 transform
            {
                get
                {
                    return _transform;
                }
                set  
                {
                    _transform = value;
                    if (value.isIdentity)
                    {
                        useTransform = false;
                    }
                    else
                    {
                        useTransform = true;
                    }
                }
            }

            public MeshCopyReference(MeshCombiner combiner, string assetPath, string name)
            {
                this.combiner = combiner;
                this.assetPath = assetPath;
                this.name = name;
                
            }

            public MeshCopyReference ManualClone()
            {
                MeshCopyReference output = new MeshCopyReference(this.combiner, this.assetPath, this.name);

                //Clone all the members
                output.enabled = this.enabled;
                output.meshCopy = new MeshCopy[this.meshCopy.Length];
                for (int i = 0; i < this.meshCopy.Length; i++)
                {
                    output.meshCopy[i] = this.meshCopy[i].ManualClone();
                }
                
                return output;
            } 
              
             
            public void Reload()
            {
                meshCopy = MeshCopy.Load(assetPath, false).ToArray();
            }

        }

        public MeshCopy finalMesh = new MeshCopy();
             
        private MeshCopyReference[] readOnlySourceReferences;
        private bool pendingUpdate = false;
        private bool runningUpdate = false;
        private bool newMeshReadyToUse = false;

 
        public MeshCopyReference AddMesh(string assetPath, string name, bool showError = false)
        {
            //Todo: Pull from a pool?
            //Todo: Allow for callback here to edit mesh before it's processed?
            MeshCopyReference meshCopyReference = new MeshCopyReference(this, assetPath, name);
            meshCopyReference.Reload();

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
            if (pendingUpdate == false && runningUpdate == false)
            {
                return;
            }
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
            finalMesh = new MeshCopy();

            foreach (MeshCopyReference meshCopyReference in readOnlySourceReferences)
            {
                if (meshCopyReference.enabled == false)
                {
                    continue;
                }

                //Loop through all the meshes
                foreach (MeshCopy meshCopy in meshCopyReference.meshCopy)
                {
                    //Add the mesh
                    finalMesh.MergeMeshCopy(meshCopy);
                }
            }

            //we're all done
            runningUpdate = false;
            
            newMeshReadyToUse = true;
        }

        public void UpdateMesh()
        {
            if (newMeshReadyToUse == false)
            {
                return;
            }
            newMeshReadyToUse = false;

            if (finalMesh.bones.Count == 0)
            {

                MeshFilter filter = gameObject.GetComponent<MeshFilter>();
                if (filter ==null)
                {
                    filter = gameObject.AddComponent<MeshFilter>();
                }

                MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    renderer = gameObject.AddComponent<MeshRenderer>();
                }

                //Apply meshes
                Mesh mesh = new Mesh();
                mesh.name = "MeshCombinerMesh";
                //Copy out of finalMesh
                mesh.SetVertices(finalMesh.vertices);
                //more
                mesh.SetUVs(0, finalMesh.uvs);
                mesh.SetUVs(1, finalMesh.uvs2);

                mesh.SetNormals(finalMesh.normals);
                mesh.SetTangents(finalMesh.tangents);
                mesh.SetColors(finalMesh.colors);

                //mesh.bones = finalMesh.bones.ToArray();
                //mesh.boneWeights = finalMesh.boneWeights.ToArray();
                //mesh.bindposes = finalMesh.bindPoses.ToArray();

                //Create subMeshes
                mesh.subMeshCount = finalMesh.subMeshes.Count;
                for (int i = 0; i < finalMesh.subMeshes.Count; i++)
                {
                    mesh.SetTriangles(finalMesh.subMeshes[i].triangles, i);
                }

                //Copy the materials to the renderer
                Material[] finalMaterials = new Material[finalMesh.subMeshes.Count];
                for (int i = 0; i < finalMesh.subMeshes.Count; i++)
                {
                    finalMaterials[i] = finalMesh.subMeshes[i].material;
                }
                renderer.sharedMaterials = finalMaterials;

                filter.sharedMesh = mesh;

                //Remove the skinned mesh renderer if there is one
                SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer != null)
                {
                    DestroyImmediate(skinnedMeshRenderer);
                }
            }
            else
            {
                //Same thing, but for skinned meshes
                SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer == null)
                {
                    skinnedMeshRenderer = gameObject.AddComponent<SkinnedMeshRenderer>();
                }

                //Apply meshes
                Mesh mesh = new Mesh();
                mesh.name = "MeshCombinerSkinnedMesh";
                //Copy out of finalMesh
                mesh.SetVertices(finalMesh.vertices);
                //more
                mesh.SetUVs(0, finalMesh.uvs);
                mesh.SetUVs(1, finalMesh.uvs2);


                mesh.SetNormals(finalMesh.normals);
                mesh.SetTangents(finalMesh.tangents);
                mesh.SetColors(finalMesh.colors);

                //mesh.bones = finalMesh.bones.ToArray();
                //mesh.boneWeights = finalMesh.boneWeights.ToArray();
                //mesh.bindposes = finalMesh.bindPoses.ToArray();

                //Create subMeshes
                mesh.subMeshCount = finalMesh.subMeshes.Count;
                for (int i = 0; i < finalMesh.subMeshes.Count; i++)
                {
                    mesh.SetTriangles(finalMesh.subMeshes[i].triangles, i);
                }

                //Copy the materials to the renderer
                Material[] finalMaterials = new Material[finalMesh.subMeshes.Count];
                for (int i = 0; i < finalMesh.subMeshes.Count; i++)
                {
                    finalMaterials[i] = finalMesh.subMeshes[i].material;
                }
                skinnedMeshRenderer.sharedMaterials = finalMaterials;
                skinnedMeshRenderer.sharedMesh = mesh;

                //Remove the meshFilter and renderer if they exist
                MeshFilter filter = gameObject.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    DestroyImmediate(filter);
                }
                MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    DestroyImmediate(renderer);
                }
            }


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

        private void ReloadMeshCopyReferences()
        {
            //reload  sourceMeshes
            foreach (MeshCopyReference reference in sourceReferences)
            {
                reference.Reload();
            }
            Dirty();

        }
    }

#if UNITY_EDITOR

    [CustomEditor(typeof(MeshCombiner))]
    public class MeshCombinerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            MeshCombiner myScript = (MeshCombiner)target;
       
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("<", GUILayout.Width(20));
            EditorGUILayout.LabelField("Name");
            EditorGUILayout.LabelField("Delete", GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();
             
            //Draw a bar
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            for (int i = 0; i < myScript.sourceReferences.Count; i++)
            {
                MeshCombiner.MeshCopyReference meshCopyReference = myScript.sourceReferences[i];
                EditorGUILayout.BeginHorizontal();
                //add checkbox
                meshCopyReference.enabled = EditorGUILayout.Toggle(meshCopyReference.enabled, GUILayout.Width(20));

                EditorGUILayout.LabelField(meshCopyReference.name );
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    myScript.sourceReferences.RemoveAt(i);
                    myScript.Dirty();
                    UnityEditor.EditorUtility.SetDirty(myScript);
                }
                 
                EditorGUILayout.EndHorizontal(); 
            }

            if (GUILayout.Button("Add"))
            {
                //Pick an asset
                string assetPath = EditorUtility.OpenFilePanel("Pick a prefab", "", "prefab");
                if (assetPath.Length != 0)
                {
                    //Get the name
                    string name = Path.GetFileNameWithoutExtension(assetPath);
                    string shortPath = StripAssetsFolder(assetPath);
                    //Add it
                    myScript.AddMesh(shortPath, name, true);

                    myScript.Dirty();
                    UnityEditor.EditorUtility.SetDirty(myScript);
                }
            }


            EditorGUILayout.LabelField("Verts: " + myScript.finalMesh.vertices.Count);
            EditorGUILayout.LabelField("Materials: " + myScript.finalMesh.subMeshes.Count);
        }

        private string StripAssetsFolder(string filePath)
        {
            //strip the extension
            string extension = Path.GetExtension(filePath);
            filePath = filePath.Substring(0, filePath.Length - extension.Length);
            
            int resourcesIndex = string.IsNullOrEmpty(filePath) ? -1 : filePath.IndexOf("/Resources/");
            if (resourcesIndex >= 0)
            {
                filePath = filePath.Substring(resourcesIndex + "/Resources/".Length);
            }
            return "Shared/Resources/"+filePath;
        }
    }
#endif
}
