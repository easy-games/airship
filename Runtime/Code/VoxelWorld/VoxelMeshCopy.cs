using System.Collections.Generic;
using UnityEngine;
using VoxelWorldStuff;

namespace Assets.Airship.VoxelRenderer {
    public class VoxelMeshCopy {
        public class PrecalculatedRotation {
            public Vector3[] vertices;
            public Vector3[] normals;
            private Rotations rotation;

            public PrecalculatedRotation(Vector3[] srcVertices, Vector3[] srcNormals, Rotations rot, Quaternion quat) {
                rotation = rot;

                vertices = new Vector3[srcVertices.Length];
                normals = new Vector3[srcNormals.Length];

                for (var i = 0; i < srcVertices.Length; i++) {
                    vertices[i] = quat * srcVertices[i];
                    normals[i] = quat * srcNormals[i];
                }
            }
        }

        public class PrecalculatedFlip {
            public Vector3[] vertices;
            public Vector3[] normals;
            public Surface[] surfaces;

            public PrecalculatedFlip(
                Vector3[] srcVertices,
                Vector3[] srcNormals,
                Surface[] srcSurfaces,
                VoxelWorld.Flips flip) {
                vertices = new Vector3[srcVertices.Length];
                normals = new Vector3[srcNormals.Length];

                var bits = (VoxelWorld.Flips)flip;

                //Copy the surfaces
                surfaces = new Surface[srcSurfaces.Length];
                for (var i = 0; i < srcSurfaces.Length; i++) {
                    surfaces[i] = srcSurfaces[i].Clone();
                }


                //copy all the verts
                for (var i = 0; i < srcVertices.Length; i++) {
                    vertices[i] = srcVertices[i];
                    normals[i] = srcNormals[i];
                }

                var parity = 0;

                //FlipBits

                //0 No rotation
                //1 rot left 
                //2 rot right
                //3 rot 180
                //4 flip y 
                //5 flip y, rot left
                //6 flip y, rot right
                //7 flip y, rot 180


                //check for 1
                if (bits == VoxelWorld.Flips.Flip_90Deg) {
                    //Rot left 90
                    for (var i = 0; i < srcVertices.Length; i++) {
                        vertices[i] = new Vector3(srcVertices[i].z, srcVertices[i].y, -srcVertices[i].x);
                        normals[i] = new Vector3(srcNormals[i].z, srcNormals[i].y, -srcNormals[i].x);
                    }
                }

                //check for 2
                if (bits == VoxelWorld.Flips.Flip_180Deg) {
                    //Rot 180
                    for (var i = 0; i < srcVertices.Length; i++) {
                        vertices[i] = new Vector3(-srcVertices[i].x, srcVertices[i].y, -srcVertices[i].z);
                        normals[i] = new Vector3(-srcNormals[i].x, srcNormals[i].y, -srcNormals[i].z);
                    }
                }

                //check for 3
                if (bits == VoxelWorld.Flips.Flip_270Deg) {
                    //Rot right 270
                    for (var i = 0; i < srcVertices.Length; i++) {
                        vertices[i] = new Vector3(-srcVertices[i].z, srcVertices[i].y, srcVertices[i].x);
                        normals[i] = new Vector3(-srcNormals[i].z, srcNormals[i].y, srcNormals[i].x);
                    }
                }

                //Do the vertical flipped ones
                //check for 4
                if (bits == VoxelWorld.Flips.Flip_0DegVertical) {
                    //Flip y
                    for (var i = 0; i < srcVertices.Length; i++) {
                        vertices[i] = new Vector3(srcVertices[i].x, -srcVertices[i].y, srcVertices[i].z);
                        normals[i] = new Vector3(srcNormals[i].x, -srcNormals[i].y, srcNormals[i].z);
                    }

                    parity++;
                }

                //check for 5
                if (bits == VoxelWorld.Flips.Flip_90DegVertical) {
                    //Flip y, rot left 90
                    for (var i = 0; i < srcVertices.Length; i++) {
                        vertices[i] = new Vector3(srcVertices[i].z, -srcVertices[i].y, -srcVertices[i].x);
                        normals[i] = new Vector3(srcNormals[i].z, -srcNormals[i].y, -srcNormals[i].x);
                    }

                    parity++;
                }

                //check for 6
                if (bits == VoxelWorld.Flips.Flip_180DegVertical) {
                    //Flip y, rot 180
                    for (var i = 0; i < srcVertices.Length; i++) {
                        vertices[i] = new Vector3(-srcVertices[i].x, -srcVertices[i].y, -srcVertices[i].z);
                        normals[i] = new Vector3(-srcNormals[i].x, -srcNormals[i].y, -srcNormals[i].z);
                    }

                    parity++;
                }

                //check for 7
                if (bits == VoxelWorld.Flips.Flip_270DegVertical) {
                    //Flip y, rot right 90
                    for (var i = 0; i < srcVertices.Length; i++) {
                        vertices[i] = new Vector3(-srcVertices[i].z, -srcVertices[i].y, srcVertices[i].x);
                        normals[i] = new Vector3(-srcNormals[i].z, -srcNormals[i].y, srcNormals[i].x);
                    }

                    parity++;
                }

                //Check for parity, if so we have to flip the winding orders
                if (parity % 2 > 0) {
                    //flip faces
                    for (var i = 0; i < surfaces.Length; i++) {
                        surfaces[i].Invert();
                    }
                }
            }
        }

        public enum Rotations {
            None,
            Y90,
            Y180,
            Y270
        }

        private Rotations[] allRotations = (Rotations[])System.Enum.GetValues(typeof(Rotations));

        public KeyValuePair<Rotations, Quaternion>[] quaternions = new KeyValuePair<Rotations, Quaternion>[] {
            new(Rotations.None, Quaternion.Euler(0, 0, 0)),
            new(Rotations.Y90, Quaternion.Euler(0, 90, 0)),
            new(Rotations.Y180, Quaternion.Euler(0, 180, 0)),
            new(Rotations.Y270, Quaternion.Euler(0, 270, 0))
        };

        //List of vertices uvs etc
        public PrecalculatedFlip[] flip = new PrecalculatedFlip[8];

        public PrecalculatedRotation[] rotation = new PrecalculatedRotation[4];

        public Vector2[] srcUvs;

        public Color32[] srcColors;
        public Vector3[] srcVertices;
        public Vector3[] srcNormals;
        public Surface[] surfaces;

        public class Surface {
            public int[] triangles;
            public Material meshMaterial;
            public string meshMaterialName = "";
            public int meshMaterialId;

            public Surface(int[] triangles, Material material, string materialName, int materialId) {
                this.triangles = new int[triangles.Length];
                System.Array.Copy(triangles, this.triangles, triangles.Length);

                meshMaterial
                    = material; // Assuming Material is a reference type; you might need to clone it depending on its implementation.
                meshMaterialName = materialName;
                meshMaterialId = materialId;
                MeshProcessor.materialIdToMaterial[materialId] = material;
            }

            public Surface() { }

            public Surface Clone() {
                // Clone the triangles array
                var clonedTriangles = new int[triangles.Length];
                System.Array.Copy(triangles, clonedTriangles, triangles.Length);

                // Clone the material (assuming a reference copy is okay, or implement deep copy if necessary)
                var clonedMaterial = meshMaterial;

                // Return a new Surface instance with the cloned data
                return new Surface(clonedTriangles, clonedMaterial, meshMaterialName, meshMaterialId);
            }

            public void Invert() {
                for (var i = 0; i < triangles.Length; i += 3) {
                    var temp = triangles[i + 1];
                    triangles[i + 1] = triangles[i + 2];
                    triangles[i + 2] = temp;
                }
            }
        }

        public VoxelMeshCopy(GameObject obj) {
            ParseInstance(obj);
        }

        public VoxelMeshCopy(VoxelMeshCopy src) {
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
            for (var i = 0; i < src.surfaces.Length; i++) {
                surfaces[i] = new Surface(src.surfaces[i].triangles, src.surfaces[i].meshMaterial,
                    src.surfaces[i].meshMaterialName, src.surfaces[i].meshMaterialId);
            }

            //Calculate the rotations
            var cc = 0;
            foreach (var rot in quaternions) {
                rotation[cc++] = new PrecalculatedRotation(srcVertices, srcNormals, rot.Key, rot.Value);
            }

            //Calculate the flips
            for (var i = 0; i < 8; i++) {
                flip[i] = new PrecalculatedFlip(srcVertices, srcNormals, surfaces, VoxelWorld.allFlips[i]);
            }
        }

        //Recursively get all filters and materials
        private void GetMeshes(GameObject gameObject, List<MeshFilter> filters) {
            //Get the mesh filter
            var filter = gameObject.GetComponent<MeshFilter>();
            if (filter != null) {
                filters.Add(filter);
            }

            //Get the children
            foreach (Transform child in gameObject.transform) {
                GetMeshes(child.gameObject, filters);
            }
        }

        public VoxelMeshCopy(string assetPath, bool showError = false) {
            if (assetPath == "") {
                return;
            }

            var asset = AssetBridge.Instance.LoadAssetInternal<Object>(assetPath + ".prefab", false);

            if (asset == null) {
                asset = AssetBridge.Instance.LoadAssetInternal<Object>(assetPath + ".FBX", false);
            }

            if (asset == null && showError == true) {
                Debug.LogError("Failed to load asset at path: " + assetPath);
                return;
            }

            if (asset is Mesh) {
                var mesh = asset as Mesh;
                var srcVerticesList = new List<Vector3>();
                mesh.GetVertices(srcVerticesList);
                srcVertices = srcVerticesList.ToArray();

                var srcNormalsList = new List<Vector3>();
                mesh.GetNormals(srcNormalsList);
                srcNormals = srcNormalsList.ToArray();

                var uvsList = new List<Vector2>();
                mesh.GetUVs(0, uvsList);
                srcUvs = uvsList.ToArray();

                var surf = new Surface();
                surf.triangles = mesh.GetTriangles(0);
                surfaces = new Surface[] { surf };

                var colorsList = new List<Color>();
                var colors32List = new List<Color32>();

                mesh.GetColors(colorsList);
                foreach (var c in colorsList) {
                    colors32List.Add(c);
                }

                srcColors = colors32List.ToArray();

                //Calculate the rotations
                var cc = 0;
                foreach (var rot in quaternions) {
                    rotation[cc++] = new PrecalculatedRotation(srcVerticesList.ToArray(), srcNormalsList.ToArray(),
                        rot.Key, rot.Value);
                }

                for (var i = 0; i < 8; i++) {
                    flip[i] = new PrecalculatedFlip(srcVertices, srcNormals, surfaces, VoxelWorld.allFlips[i]);
                }

                return;
            }

            if (asset is GameObject) {
                // Instantiate the prefab
                var instance = Object.Instantiate((GameObject)asset);

                ParseInstance(instance);

                if (Application.isPlaying == true) {
                    Object.Destroy(instance);
                } else {
                    Object.DestroyImmediate(instance);
                }

                return;
            }
        }

        private void ParseInstance(GameObject instance) {
            // Loop through all child objects of the instance
            var filters = new List<MeshFilter>();

            //Recursively interate over all child gameObjects
            GetMeshes(instance, filters);
            // Debug.Log("Name" + assetPath);

            //Do the mesh combine manually
            var srcVerticesList = new List<Vector3>();
            var srcNormalsList = new List<Vector3>();
            var srcUvsList = new List<Vector2>();
            var srcColorsList = new List<Color32>();
            var surfaceList = new List<Surface>();

            foreach (var filter in filters) {
                //Get the mesh
                var mesh = filter.sharedMesh;

                //Add the vertices
                var vertexOffset = srcVerticesList.Count;

                var tempVerticesList = new List<Vector3>();
                var temmpNormalsList = new List<Vector3>();
                mesh.GetVertices(tempVerticesList);
                mesh.GetNormals(temmpNormalsList);

                var mat = filter.transform.localToWorldMatrix;
                for (var i = 0; i < tempVerticesList.Count; i++) {
                    srcVerticesList.Add(mat * tempVerticesList[i]);
                    srcNormalsList.Add(mat * temmpNormalsList[i]);
                }

                //Add the uvs
                srcUvsList.AddRange(mesh.uv);

                //Add the colors
                if (mesh.colors.Length > 0) {
                    var srcColors = new List<Color>();
                    mesh.GetColors(srcColors);
                    for (var i = 0; i < srcColors.Count; i++) {
                        srcColorsList.Add(srcColors[i]);
                    }
                } else {
                    for (var i = 0; i < mesh.vertices.Length; i++) {
                        srcColorsList.Add(new Color32(255, 255, 255, 255));
                    }
                }

                for (var subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++) {
                    //Add a new surface
                    var surf = new Surface();

                    var subMeshDescriptor = mesh.GetSubMesh(subMeshIndex);
                    surf.triangles = new int[subMeshDescriptor.indexCount];
                    var tris = mesh.GetTriangles(subMeshIndex);
                    for (var i = 0; i < subMeshDescriptor.indexCount; i++) {
                        surf.triangles[i] = tris[i] + vertexOffset;
                    }

                    var srcMat = filter.gameObject.GetComponent<MeshRenderer>().sharedMaterials[subMeshIndex];
                    if (!srcMat) {
                        Debug.LogError("Unable to find material index: " + subMeshIndex + " on submesh: " +
                                       subMeshIndex + " on object: " + filter.gameObject.name);
                        continue;
                    }

                    surf.meshMaterial = srcMat;
                    surf.meshMaterialName = srcMat.name;
                    surf.meshMaterialId = srcMat.GetInstanceID();

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

            var cc = 0;
            foreach (var rot in quaternions) {
                rotation[cc++] = new PrecalculatedRotation(srcVerticesList.ToArray(), srcNormalsList.ToArray(), rot.Key,
                    rot.Value);
            }

            for (var i = 0; i < 8; i++) {
                flip[i] = new PrecalculatedFlip(srcVertices, srcNormals, surfaces, VoxelWorld.allFlips[i]);
            }
        }

        public void AdjustUVs(Rect uvs) {
            if (srcUvs == null) {
                return;
            }

            //Adjust the uvs to the atlased texture
            for (var i = 0; i < srcUvs.Length; i++) {
                srcUvs[i] = new Vector2(srcUvs[i].x * uvs.width + uvs.x, srcUvs[i].y * uvs.height + uvs.y);
            }
        }

        internal void FlipVertically() {
            for (var i = 0; i < srcVertices.Length; i++) {
                srcVertices[i] = new Vector3(srcVertices[i].x, -srcVertices[i].y, srcVertices[i].z);
            }

            //flip the faces
            foreach (var surf in surfaces) {
                surf.Invert();
            }

            //Flip the normals
            for (var i = 0; i < srcNormals.Length; i++) {
                srcNormals[i] = new Vector3(srcNormals[i].x, -srcNormals[i].y, srcNormals[i].z);
            }


            //Calculate the rotations
            var cc = 0;
            foreach (var rot in quaternions) {
                rotation[cc++] = new PrecalculatedRotation(srcVertices, srcNormals, rot.Key, rot.Value);
            }

            for (var i = 0; i < 8; i++) {
                flip[i] = new PrecalculatedFlip(srcVertices, srcNormals, surfaces, VoxelWorld.allFlips[i]);
            }
        }

        internal void FlipHorizontally() {
            for (var i = 0; i < srcVertices.Length; i++) {
                srcVertices[i] = new Vector3(-srcVertices[i].x, srcVertices[i].y, srcVertices[i].z);
            }

            //flip the faces
            foreach (var surf in surfaces) {
                surf.Invert();
            }

            //Flip the normals
            for (var i = 0; i < srcNormals.Length; i++) {
                srcNormals[i] = new Vector3(-srcNormals[i].x, srcNormals[i].y, srcNormals[i].z);
            }

            //Calculate the rotations
            var cc = 0;
            foreach (var rot in quaternions) {
                rotation[cc++] = new PrecalculatedRotation(srcVertices, srcNormals, rot.Key, rot.Value);
            }

            for (var i = 0; i < 8; i++) {
                flip[i] = new PrecalculatedFlip(srcVertices, srcNormals, surfaces, VoxelWorld.allFlips[i]);
            }
        }

        public void ApplyMaterial(Material meshMaterial) {
            if (surfaces == null) {
                return;
            }

            foreach (var surf in surfaces) {
                surf.meshMaterial = meshMaterial;
                surf.meshMaterialName = meshMaterial.name;
                surf.meshMaterialId = meshMaterial.GetInstanceID();
            }
        }
    }
}