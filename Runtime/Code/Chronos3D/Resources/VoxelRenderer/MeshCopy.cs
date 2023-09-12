using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using UnityEngine;
using UnityEngine.Assertions.Must;

namespace Assets.Chronos.VoxelRenderer
{
    
    public class MeshCopy
    {
        public class PrecalculatedRotation
        {
            public List<Vector3> vertices = new List<Vector3>();
            public List<Vector3> normals = new List<Vector3>();
            Rotations rotation;
            public PrecalculatedRotation(List<Vector3> vertices, List<Vector3> normals, Rotations rot, Quaternion quat)
            {
                rotation = rot;

                for (int i = 0; i < vertices.Count; i++)
                {
                    this.vertices.Add(quat * vertices[i]);
                    this.normals.Add(quat * normals[i]);
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
        public List<Vector2> uvs = new List<Vector2>();
        public List<int> triangles = new List<int>();
        public List<Color> colors = new List<Color>();
        public List<Vector3> srcVertices = new List<Vector3>();
        public List<Vector3> srcNormals = new List<Vector3>();
        public Material meshMaterial;
        public string meshMaterialName;

        public MeshCopy(Mesh mesh)
        {
            //Copy the data to our local arrays
            
            mesh.GetVertices(srcVertices);
            mesh.GetNormals(srcNormals);
            mesh.GetUVs(0, uvs);
            mesh.GetTriangles(triangles, 0);
            mesh.GetColors(colors);

            //Calculate the rotations
            foreach (var rot in quaternions)
            {
                rotation.Add((int)rot.Key, new PrecalculatedRotation(srcVertices, srcNormals, rot.Key, rot.Value));
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

        public MeshCopy(string assetPath, bool showError = false)
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
                mesh.GetVertices(srcVertices);
                mesh.GetUVs(0, uvs);
                mesh.GetNormals(srcNormals);
                mesh.GetTriangles(triangles, 0);
                mesh.GetColors(colors);


                //Calculate the rotations
                foreach (var rot in quaternions)
                {
                    rotation.Add((int)rot.Key, new PrecalculatedRotation(srcVertices, srcNormals, rot.Key, rot.Value));
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
                mesh.CombineMeshes(combine, true);

                //write it
                mesh.GetVertices(srcVertices);
                mesh.GetUVs(0, uvs);
                mesh.GetNormals(srcNormals);
                mesh.GetTriangles(triangles, 0);
                mesh.GetColors(colors);
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
                    rotation.Add((int)rot.Key, new PrecalculatedRotation(srcVertices, srcNormals, rot.Key, rot.Value));
                }
                return;
            }

        }

        public void AdjustUVs(Rect uvs)
        {
            //Adjust the uvs to the atlased texture
            for (int i = 0; i < this.uvs.Count; i++)
            {
                this.uvs[i] = new Vector2(this.uvs[i].x * uvs.width + uvs.x, this.uvs[i].y * uvs.height + uvs.y);
            }
        }
    }
}