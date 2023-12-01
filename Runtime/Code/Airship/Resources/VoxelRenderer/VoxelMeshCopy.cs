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
        public Vector2[] uvs;
        public int[] triangles;
        public Color[] colors;
        public Vector3[] srcVertices;
        public Vector3[] srcNormals;
        public Material meshMaterial;
        public string meshMaterialName;

        public VoxelMeshCopy(Mesh mesh)
        {
            //Copy the data to our local arrays

            List<Vector3> srcVerticesList = new List<Vector3>();
            mesh.GetVertices(srcVerticesList);
            srcVertices = srcVerticesList.ToArray();

            List<Vector3> srcNormalsList = new List<Vector3>();
            mesh.GetNormals(srcNormalsList);
            srcNormals = srcNormalsList.ToArray();

            List<Vector2> uvsList = new List<Vector2>();
            mesh.GetUVs(0, uvsList);
            uvs = uvsList.ToArray();

            List<int> trianglesList = new List<int>();
            mesh.GetTriangles(trianglesList, 0);
            triangles = trianglesList.ToArray();
            
            List<Color> colorsList = new List<Color>();
            mesh.GetColors(colorsList);
            colors = colorsList.ToArray();

            //Calculate the rotations
            foreach (var rot in quaternions)
            {
                rotation.Add((int)rot.Key, new PrecalculatedRotation(srcVerticesList, srcNormalsList, rot.Key, rot.Value));
            }
        }

        //Recursively get all filters and materials
        private void GetMeshes(GameObject gameObject, List<MeshFilter> filters, List<Material> materials)
        {
            //Get the mesh filter
            MeshFilter filter = gameObject.GetComponent<MeshFilter>();
            if (filter != null)
            {
                filters.Add(filter);
                materials.Add(gameObject.GetComponent<MeshRenderer>().sharedMaterial);
            }

            //Get the children
            foreach (Transform child in gameObject.transform)
            {
                GetMeshes(child.gameObject, filters, materials);
            }
        }

        public VoxelMeshCopy(string assetPath, bool showError = false)
        {

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
                uvs = uvsList.ToArray();

                List<int> trianglesList = new List<int>();
                mesh.GetTriangles(trianglesList, 0);
                triangles = trianglesList.ToArray();

                List<Color> colorsList = new List<Color>();
                mesh.GetColors(colorsList);
                colors = colorsList.ToArray();


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

                // Loop through all child objects of the instance
                List<MeshFilter> filters = new List<MeshFilter>();
                List<Material> materials = new List<Material>();

                //Recursively interate over all child gameObjects
                GetMeshes(instance, filters, materials);
                               
                
                CombineInstance[] combine = new CombineInstance[filters.Count];
                
                int i = 0;
                while (i < filters.Count)
                {
                    combine[i].mesh = filters[i].sharedMesh;
                    combine[i].transform = filters[i].transform.localToWorldMatrix;

                  
                    i++;
                }
                //Create a new mesh to merge these meshes into
                Mesh mesh = new Mesh();
                mesh.CombineMeshes(combine, true, true, false);

                //write it
                List<Vector3> srcVerticesList = new List<Vector3>();
                mesh.GetVertices(srcVerticesList);
                srcVertices = srcVerticesList.ToArray();

                List<Vector3> srcNormalsList = new List<Vector3>();
                mesh.GetNormals(srcNormalsList);
                srcNormals = srcNormalsList.ToArray();

                List<Vector2> uvsList = new List<Vector2>();
                mesh.GetUVs(0, uvsList);
                uvs = uvsList.ToArray();

                List<int> trianglesList = new List<int>();
                mesh.GetTriangles(trianglesList, 0);
                triangles = trianglesList.ToArray();

                List<Color> colorsList = new List<Color>();
                mesh.GetColors(colorsList);
                colors = colorsList.ToArray();
                
                //Hackery, first material only
                if (materials.Count > 0)
                {
                    meshMaterial = materials[0];
                    meshMaterialName = materials[0].name;
                }
                
                if (Application.isPlaying == true)
                {
                    GameObject.Destroy(instance);
                }
                else
                {
                    GameObject.DestroyImmediate(instance);
                }
                foreach (var rot in quaternions)
                {
                    rotation.Add((int)rot.Key, new PrecalculatedRotation(srcVerticesList, srcNormalsList, rot.Key, rot.Value));
                }
                return;
            }

        }

        public void AdjustUVs(Rect uvs)
        {
            //Adjust the uvs to the atlased texture
            for (int i = 0; i < this.uvs.Length; i++)
            {
                this.uvs[i] = new Vector2(this.uvs[i].x * uvs.width + uvs.x, this.uvs[i].y * uvs.height + uvs.y);
            }
        }

        internal void FlipVertically()
        {
            for (int i = 0; i < srcVertices.Length; i++)
            {
                srcVertices[i] = new Vector3(srcVertices[i].x, -srcVertices[i].y , srcVertices[i].z);
            }
            //flip the faces
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int temp = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = temp;
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
    }
}