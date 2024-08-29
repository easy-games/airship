using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using UnityEngine;
using UnityEngine.Assertions.Must;

namespace Assets.Airship.VoxelRenderer
{
    
    public class VoxelMeshCopy
        
    {
        public class PrecalculatedRotation
        {
            public Vector3[] vertices;
            public Vector3[] normals;
            Rotations rotation;
            public PrecalculatedRotation(List<Vector3> srcVertices, List<Vector3> srcNormals, Rotations rot, Quaternion quat)
            {
                rotation = rot;

                this.vertices = new Vector3[srcVertices.Count];
                this.normals = new Vector3[srcNormals.Count];

                for (int i = 0; i < srcVertices.Count; i++)
                {
                    this.vertices[i] = quat * srcVertices[i];
                    this.normals[i] = quat * srcNormals[i];
                }
            }

            public PrecalculatedRotation(Vector3[] srcVertices, Vector3[] srcNormals, Rotations rot, Quaternion quat)
            {
                rotation = rot;
                
                this.vertices = new Vector3[srcVertices.Length];
                this.normals = new Vector3[srcNormals.Length];

                for (int i = 0; i < srcVertices.Length; i++)
                {
                    this.vertices[i] = quat * srcVertices[i];
                    this.normals[i] = quat * srcNormals[i];
                }
            }
        }

        public enum Rotations
        {
            None,
            Y90,
            Y180,
            Y270,
        }

        public KeyValuePair<Rotations, Quaternion>[] quaternions = new KeyValuePair<Rotations, Quaternion>[]
        {
            new KeyValuePair<Rotations, Quaternion>(Rotations.None, Quaternion.Euler(0, 0, 0)),
            new KeyValuePair<Rotations, Quaternion>(Rotations.Y90, Quaternion.Euler(0, 90, 0)),
            new KeyValuePair<Rotations, Quaternion>(Rotations.Y180, Quaternion.Euler(0, 180, 0)),
            new KeyValuePair<Rotations, Quaternion>(Rotations.Y270, Quaternion.Euler(0, 270, 0)),
        };
           
        //List of vertices uvs etc
        public Dictionary<int, PrecalculatedRotation> rotation = new();
        public Vector2[] srcUvs;
        
        public Color32[] srcColors;
        public Vector3[] srcVertices;
        public Vector3[] srcNormals;
        public Surface[] surfaces;
        
        public class Surface
        {
            public int[] triangles;
            public Material meshMaterial;
            public string meshMaterialName = "";

            public Surface(int[] triangles, Material material, string materialName)
            {

                this.triangles = new int[triangles.Length];
                System.Array.Copy(triangles, this.triangles, triangles.Length);

                this.meshMaterial = material;
                this.meshMaterialName = materialName;
            }
            public Surface()
            {
                
            }
        }

        public VoxelMeshCopy(GameObject obj) {
            ParseInstance(obj);
        }

        public VoxelMeshCopy (VoxelMeshCopy src)
        {
            //Copy the data to our local arrays
            srcVertices = new Vector3[src.srcVertices.Length];
            srcNormals = new Vector3[src.srcNormals.Length];
            srcUvs = new Vector2[src.srcUvs.Length];
            
            srcColors = new Color32[src.srcColors.Length];

            System.Array.Copy(src.srcVertices, srcVertices, src.srcVertices.Length);
            System.Array.Copy(src.srcNormals, srcNormals, src.srcNormals.Length);
            System.Array.Copy(src.srcUvs, srcUvs, src.srcUvs.Length);
            System.Array.Copy(src.srcColors, srcColors, src.srcColors.Length);

            //copy the surfaces
            surfaces = new Surface[src.surfaces.Length];
            for (int i = 0; i < src.surfaces.Length; i++)
            {
                surfaces[i] = new Surface(src.surfaces[i].triangles, src.surfaces[i].meshMaterial, src.surfaces[i].meshMaterialName);
            }
            
            //Calculate the rotations
            foreach (var rot in quaternions)
            {
                rotation.Add((int)rot.Key, new PrecalculatedRotation(src.srcVertices, src.srcNormals, rot.Key, rot.Value));
            }
        }

        //Recursively get all filters and materials
        private void GetMeshes(GameObject gameObject, List<MeshFilter> filters)
        {
            //Get the mesh filter
            MeshFilter filter = gameObject.GetComponent<MeshFilter>();
            if (filter != null)
            {
                filters.Add(filter);
            }

            //Get the children
            foreach (Transform child in gameObject.transform)
            {
                GetMeshes(child.gameObject, filters);
            }
        }

        public VoxelMeshCopy(string assetPath, bool showError = false)
        {
            
            if (assetPath == "") {
                return;
            }
                
            Object asset = AssetBridge.Instance.LoadAssetInternal<Object>(assetPath + ".prefab", false);

            if (asset == null)
            {
                asset = AssetBridge.Instance.LoadAssetInternal<Object>(assetPath + ".FBX", false);
            }

            if (asset == null && showError == true)
            {
                Debug.LogError("Failed to load asset at path: " + assetPath);
                return;
            }

            if (asset is Mesh)
            {
                Mesh mesh = asset as Mesh;
                List<Vector3> srcVerticesList = new List<Vector3>();
                mesh.GetVertices(srcVerticesList);
                srcVertices = srcVerticesList.ToArray();

                List<Vector3> srcNormalsList = new List<Vector3>();
                mesh.GetNormals(srcNormalsList);
                srcNormals = srcNormalsList.ToArray();

                List<Vector2> uvsList = new List<Vector2>();
                mesh.GetUVs(0, uvsList);
                srcUvs = uvsList.ToArray();

                Surface surf = new Surface();
                surf.triangles = mesh.GetTriangles(0);
                surfaces = new Surface[] { surf };

                List<Color> colorsList = new List<Color>();
                List<Color32> colors32List = new List<Color32>();
                
                mesh.GetColors(colorsList);
                foreach (Color c in colorsList)
                {
                    colors32List.Add(c);
                }
                srcColors = colors32List.ToArray();
                
                //Calculate the rotations
                foreach (var rot in quaternions)
                {
                    rotation.Add((int)rot.Key, new PrecalculatedRotation(srcVerticesList, srcNormalsList, rot.Key, rot.Value));
                }
                return;
            }

            if (asset is GameObject)
            {
                // Instantiate the prefab
                GameObject instance = GameObject.Instantiate((GameObject)asset);

                ParseInstance(instance);
                
                if (Application.isPlaying == true)
                {
                    GameObject.Destroy(instance);
                }
                else
                {
                    GameObject.DestroyImmediate(instance);
                }
               
                return;
            }
        
        }
 
        private void ParseInstance(GameObject instance) {
            // Loop through all child objects of the instance
            List<MeshFilter> filters = new List<MeshFilter>();

            //Recursively interate over all child gameObjects
            GetMeshes(instance, filters);
            // Debug.Log("Name" + assetPath);

            //Do the mesh combine manually
            List<Vector3> srcVerticesList = new List<Vector3>();
            List<Vector3> srcNormalsList = new List<Vector3>();
            List<Vector2> srcUvsList = new List<Vector2>();
            List<Color32> srcColorsList = new List<Color32>();
            List<Surface> surfaceList = new List<Surface>();

            foreach (var filter in filters) {
                //Get the mesh
                Mesh mesh = filter.sharedMesh;

                //Add the vertices
                int vertexOffset = srcVerticesList.Count;

                List<Vector3> tempVerticesList = new List<Vector3>();
                List<Vector3> temmpNormalsList = new List<Vector3>();
                mesh.GetVertices(tempVerticesList);
                mesh.GetNormals(temmpNormalsList);

                Matrix4x4 mat = filter.transform.localToWorldMatrix;
                for (int i = 0; i < tempVerticesList.Count; i++) {
                    srcVerticesList.Add(mat * tempVerticesList[i]);
                    srcNormalsList.Add(mat * temmpNormalsList[i]);
                }

                //Add the uvs
                srcUvsList.AddRange(mesh.uv);

                //Add the colors
                if (mesh.colors.Length > 0) {
                    List<Color> srcColors = new List<Color>();
                    mesh.GetColors(srcColors);
                    for (int i = 0; i < srcColors.Count; i++) {
                        srcColorsList.Add(srcColors[i]);
                    }
                }
                else {
                    for (int i = 0; i < mesh.vertices.Length; i++) {
                        srcColorsList.Add(new Color32(255, 255, 255, 255));
                    }
                }

                for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++) {
                    //Add a new surface
                    Surface surf = new Surface();

                    UnityEngine.Rendering.SubMeshDescriptor subMeshDescriptor = mesh.GetSubMesh(subMeshIndex);
                    surf.triangles = new int[subMeshDescriptor.indexCount];
                    int[] tris = mesh.GetTriangles(subMeshIndex);
                    for (int i = 0; i < subMeshDescriptor.indexCount; i++) {
                        surf.triangles[i] = tris[i] + vertexOffset;
                    }
                    Material srcMat = filter.gameObject.GetComponent<MeshRenderer>().sharedMaterials[subMeshIndex];
                    surf.meshMaterial = srcMat;
                    surf.meshMaterialName = srcMat.name;

                    //Add the surface
                    surfaceList.Add(surf);
                }
            }

            //write it
            srcVertices = srcVerticesList.ToArray();
            srcNormals = srcNormalsList.ToArray();
            srcUvs = srcUvsList.ToArray();
            srcColors = srcColorsList.ToArray();
            surfaces = surfaceList.ToArray();

            foreach (var rot in quaternions) {
                rotation.Add((int)rot.Key, new PrecalculatedRotation(srcVerticesList, srcNormalsList, rot.Key, rot.Value));
            }
        }

        public void AdjustUVs(Rect uvs)
        {
            if (this.srcUvs == null) {
                return;
            }
            //Adjust the uvs to the atlased texture
            for (int i = 0; i < this.srcUvs.Length; i++)
            {
                this.srcUvs[i] = new Vector2(this.srcUvs[i].x * uvs.width + uvs.x, this.srcUvs[i].y * uvs.height + uvs.y);
            }
        }

        internal void FlipVertically()
        {
            for (int i = 0; i < srcVertices.Length; i++)
            {
                srcVertices[i] = new Vector3(srcVertices[i].x, -srcVertices[i].y , srcVertices[i].z);
            }
            //flip the faces
            foreach (Surface surf in surfaces)
            {
                for (int i = 0; i < surf.triangles.Length; i += 3)
                {
                  int temp = surf.triangles[i + 1];
                  surf.triangles[i + 1] = surf.triangles[i + 2];
                  surf.triangles[i + 2] = temp;
                }
            }
            
            //Flip the normals
            for (int i = 0; i < srcNormals.Length; i++)
            {
                srcNormals[i] = new Vector3(srcNormals[i].x, -srcNormals[i].y, srcNormals[i].z);
            }

            rotation = new();
            //Calculate the rotations
            foreach (var rot in quaternions)
            {
                rotation.Add((int)rot.Key, new PrecalculatedRotation(srcVertices, srcNormals, rot.Key, rot.Value));
            }
        }

        internal void FlipHorizontally()
        {
            for (int i = 0; i < srcVertices.Length; i++)
            {
                srcVertices[i] = new Vector3(-srcVertices[i].x, srcVertices[i].y, srcVertices[i].z);
            }
            //flip the faces
            foreach (Surface surf in surfaces)
            {
                for (int i = 0; i < surf.triangles.Length; i += 3)
                {
                    int temp = surf.triangles[i + 1];
                    surf.triangles[i + 1] = surf.triangles[i + 2];
                    surf.triangles[i + 2] = temp;
                }
            }
            
            //Flip the normals
            for (int i = 0; i < srcNormals.Length; i++)
            {
                srcNormals[i] = new Vector3(-srcNormals[i].x, srcNormals[i].y, srcNormals[i].z);
            }

            rotation = new();
            //Calculate the rotations
            foreach (var rot in quaternions)
            {
                rotation.Add((int)rot.Key, new PrecalculatedRotation(srcVertices, srcNormals, rot.Key, rot.Value));
            }
        }

        public void ApplyMaterial(Material meshMaterial) {
            if (surfaces == null) {
                return;
            }

            foreach (Surface surf in surfaces) {
                surf.meshMaterial = meshMaterial;
                surf.meshMaterialName = meshMaterial.name;
            }
        }
    }
}