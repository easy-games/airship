using System;
using System.Collections.Generic;
using Airship;
using Code.Player.Accessories;
using UnityEngine;

namespace Code.Airship.Resources.Scripts {

    /// <summary>
    /// This class exists because we cannot manipulate actual Meshes in a thread.
    /// So we copy and cache assets here first.
    /// </summary>
    public class MeshCopy {
        public class SubMesh {
            public List<int> triangles = new();
            public Material material = null;
            public BatchableMaterialData batchableMaterialData;

            public SubMesh ManualClone() {
                SubMesh output = ManualCloneNoTris();
                output.triangles = new List<int>(triangles);
                return output;
            }

            public SubMesh ManualCloneNoTris() {
                SubMesh output = new SubMesh();
                output.triangles = new List<int>();
                output.material = material;

                //Clone the batchableMaterialData
                output.batchableMaterialData = batchableMaterialData;

                return output;
            }
        }
        public class BatchableMaterialData {
            public Color color;

            public BatchableMaterialData() {
                this.color = Color.white;
            }
            public BatchableMaterialData(Color color) {
                this.color = color;
            }

            // Explicitly providing a cloning method which creates a new instance with modified properties
            public BatchableMaterialData WithColor(Color newColor) {
                return new BatchableMaterialData(newColor);
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

        //
        public Transform rootBone = null;
        public List<Transform> bones = new();
        public Dictionary<string, int> boneMappings = new();
        public List<string> boneNames = new();
        public List<Matrix4x4> bindPoses = new();


        public List<BoneWeight> boneWeights = new();
        public List<SubMesh> subMeshes = new();
        public List<BatchableMaterialData> subMaterials = new();

        //Body regions that need to be excluded to render this mesh
        public int bodyMask = 0;

        public bool unpacked = false;

        //These fields are just because we can't use Transforms inside a thread
        //so we make a copy of the localToWorld and worldToLocal for later use
        public Transform sourceTransform;
        public Matrix4x4 localToWorld;
        public Matrix4x4 worldToLocal;

        //Optional Extra matrix applied to the mesh
        public Matrix4x4 extraMeshTransform;
        public bool invertedMesh = false;
        public bool skinnedMesh = false;


        //Runs on the main thread. Probably a great source of caching and optimisations
        public MeshCopy(Mesh mesh, Material[] materials, Transform hostTransform, Transform[] skinnedBones = null, Transform skinnedRootBone = null, bool warn = true) {
            // if (mesh == null) {
            //     Debug.LogWarning("Null mesh on mesh copy");
            //     return;
            // }

            //See if we have tangent data, otherwise build it
            if (mesh.tangents.Length == 0) {
                if (warn) {
                    Debug.LogWarning("Mesh " + mesh.name + " has no tangents, generating them");
                }
                mesh.RecalculateTangents();
            }

            //grab transform data
            sourceTransform = hostTransform;
            localToWorld = hostTransform.localToWorldMatrix;
            worldToLocal = hostTransform.worldToLocalMatrix;

            //See if we're flipped (if the parity of all components is negative, we're flipped)
            if (hostTransform.lossyScale.x * hostTransform.lossyScale.y * hostTransform.lossyScale.z < 0) {
                invertedMesh = true;
            }

            //Copy the data to our local arrays
            mesh.GetVertices(vertices);
            mesh.GetNormals(normals);
            mesh.GetTangents(tangents);

            //transform the verts and normals
            if (skinnedBones == null) {
                skinnedMesh = false;
                Matrix4x4 worldMatrix = hostTransform.localToWorldMatrix;

                MeshCombinerBone meshCombinerBone = hostTransform.gameObject.GetComponentInParent<MeshCombinerBone>(true);
                if (meshCombinerBone) {
                 
                    //Find the named bone 
                    string name = meshCombinerBone.boneName;
                    boneMappings.Add(name, 0);
                    boneNames.Add(name);

                    //create a single fake bone and map it all onto this
                    boneWeights = new List<BoneWeight>(vertices.Count);
                    for (int i = 0; i < vertices.Count; i++) {
                        BoneWeight boneWeight = new BoneWeight();
                        boneWeight.boneIndex0 = 0;
                        boneWeight.weight0 = 1;
                        boneWeights.Add(boneWeight);
                    }
                    //Clear this out when we're doing bone attachments
                    localToWorld = Matrix4x4.identity;
                    worldToLocal = Matrix4x4.identity;

                    extraMeshTransform = meshCombinerBone.GetMeshTransform();

                    skinnedMesh = true;

                    //There is a situation where we need to care about the transform of an assembly
                    //eg: the duck backpack, so we have to put all of these meshes into the same space

                    //Walk back up the heirachy looking for another meshCombinerBone
                    //This is the root of the assembly
                    Transform parent = hostTransform.parent;
                    Transform localAssembly = null;
                    while (parent) {
                        MeshCombinerBone parentMeshCombinerBone = parent.gameObject.GetComponent<MeshCombinerBone>();
                        if (parentMeshCombinerBone) {
                            localAssembly = parent;
                            break;
                        }
                        parent = parent.parent;
                    }

                    if (localAssembly){
                        //Our parent might have transforms we care about (eg: duckHat -> duckBill)
                        //So push us out into worldspace and back into the transform of the parent
                        Matrix4x4 modelSpaceMatrix = localAssembly.transform.worldToLocalMatrix * worldMatrix;

                        for (int i = 0; i < vertices.Count; i++) {
                            vertices[i] = modelSpaceMatrix.MultiplyPoint3x4(vertices[i]);
                            normals[i] = modelSpaceMatrix.MultiplyVector(normals[i]).normalized;

                            Vector3 tangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                            tangent = modelSpaceMatrix.MultiplyVector(tangent).normalized;
                            tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
                        }
                    }
                }
                else {

                    if (worldMatrix.isIdentity == false) {
                        //This object is a static mesh, so it has to go in the static mesh setup
                        for (int i = 0; i < vertices.Count; i++) {
                            vertices[i] = worldMatrix.MultiplyPoint3x4(vertices[i]);
                            normals[i] = worldMatrix.MultiplyVector(normals[i]).normalized;

                            Vector3 tangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                            tangent = worldMatrix.MultiplyVector(tangent).normalized;
                            tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
                        }
                    }
                }
            }

            //Enforce all meshes having UVs, Uv2s and Colors
            //Todo: Escalate this based on an survey of whats actually needed
            mesh.GetUVs(0, uvs);
            if (uvs.Count == 0) {
                uvs = new List<Vector2>(new Vector2[vertices.Count]);
            }

            mesh.GetUVs(1, uvs2);
            if (uvs2.Count == 0) {
                uvs2 = new List<Vector2>(new Vector2[vertices.Count]);
            }
            
            mesh.GetColors(colors);
            if (colors.Count == 0) {
                colors = new List<Color>(vertices.Count);
                for (int i = 0; i < vertices.Count; i++) {
                    colors.Add(Color.white);
                }
            }


            if (skinnedBones != null) {
                skinnedMesh = true;

                mesh.GetBoneWeights(boneWeights);
                mesh.GetBindposes(bindPoses);

                bones = new List<Transform>(skinnedBones);

                //For merging later
                for (int i = 0; i < skinnedBones.Length; i++) {
                    if (!skinnedBones[i]) {
                        continue;
                    }
                    string name = skinnedBones[i].name;
                    boneMappings.Add(name, i);
                    boneNames.Add(name);
                }

                rootBone = skinnedRootBone;
            }

            // int instancePropertyID = Shader.PropertyToID("_BaseColor");

            bool isClient = RunCore.IsClient();
            for (int i = 0; i < mesh.subMeshCount; i++) {
                SubMesh subMesh = new();

                if (isClient) {
                    Material mat = null;
                    if (i < materials.Length) {
                        mat = materials[i];
                    }
                    else {
                        //default material
                        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    }
                    subMesh.material = mat;
                }
                
                //id if the material is batchable

                /*
                if (mat) {
                    //TODO: should we force this? 
                    //Must have a MaterialColor controlling properties to support Instancing FOR NOW
                    var matColor = hostTransform.gameObject.GetComponent<MaterialColorURP>();
                    if (matColor) {
                         
                        for (int index = 0; index < mat.shader.GetPropertyCount(); index++) {
                            int propertyName = mat.shader.GetPropertyNameId(index);
                            //Debug.Log(mat.shader.GetPropertyName(index));
                            if (propertyName == instancePropertyID) {
                                subMesh.batchableMaterialName = mat.shader.name;
                            }
                        }
                    }
                }*/

                mesh.GetTriangles(subMesh.triangles, i);
                subMeshes.Add(subMesh);
            }

            if (invertedMesh) {
                //Invert the mesh
                for (int i = 0; i < subMeshes.Count; i++) {
                    for (int j = 0; j < subMeshes[i].triangles.Count; j += 3) {
                        int temp = subMeshes[i].triangles[j];
                        subMeshes[i].triangles[j] = subMeshes[i].triangles[j + 2];
                        subMeshes[i].triangles[j + 2] = temp;
                    }
                }
                //Note I dont think the normals need to be flipped here as they're already transformed earlier
            }

        }

        public MeshCopy() {
        }

        public MeshCopy ManualClone() {
            //Don't forget to update this when you add new fields
            MeshCopy copy = new();
            copy.vertices = new List<Vector3>(vertices);
            copy.normals = new List<Vector3>(normals);
            copy.tangents = new List<Vector4>(tangents);
            copy.uvs = new List<Vector2>(uvs);
            copy.uvs2 = new List<Vector2>(uvs2);
            copy.colors = new List<Color>(colors);

            copy.boneWeights = new List<BoneWeight>(boneWeights);

            copy.rootBone = rootBone;
            copy.bones = new List<Transform>(bones);
            copy.boneNames = new List<string>(boneNames);
            copy.boneMappings = new Dictionary<string, int>(boneMappings);
            copy.bindPoses = new List<Matrix4x4>(bindPoses);

            copy.sourceTransform = sourceTransform;
            copy.localToWorld = localToWorld;
            copy.worldToLocal = worldToLocal;

            copy.extraMeshTransform = extraMeshTransform;

            copy.skinnedMesh = skinnedMesh;
            copy.invertedMesh = invertedMesh;

            copy.subMeshes = new List<SubMesh>(subMeshes.Count);

            for (int i = 0; i < subMeshes.Count; i++) {
                copy.subMeshes.Add(subMeshes[i].ManualClone());
            }

            copy.bodyMask = bodyMask;
            copy.unpacked = unpacked;

            return copy;
        }

        public MeshCopy ManualCloneUnpacked() {
            MeshCopy copy = new MeshCopy();
            Dictionary<int, int> vertexMap = new Dictionary<int, int>();
            Dictionary<int, int> vertexUsage = new Dictionary<int, int>();

            // Count vertex usage
            foreach (SubMesh subMesh in subMeshes) {
                foreach (int index in subMesh.triangles) {
                    if (vertexUsage.ContainsKey(index))
                        vertexUsage[index]++;
                    else
                        vertexUsage[index] = 1;
                }
            }

            // Copy original vertex data to the new mesh data
            copy.vertices.AddRange(vertices);
            copy.normals.AddRange(normals);
            copy.tangents.AddRange(tangents);
            copy.uvs.AddRange(uvs);
            copy.uvs2.AddRange(uvs2);
            copy.colors.AddRange(colors);
            copy.boneWeights.AddRange(boneWeights);


            // Iterate through subMeshes
            foreach (SubMesh subMesh in subMeshes) {
                SubMesh newSubMesh = subMesh.ManualCloneNoTris();
                copy.subMeshes.Add(newSubMesh);

                // Go through the triangles of the submesh
                for (int i = 0; i < subMesh.triangles.Count; i++) {
                    int oldIndex = subMesh.triangles[i];
                    int newIndex;

                    // If vertex is shared and has not been duplicated yet, duplicate it
                    if (vertexUsage[oldIndex] > 1 && !vertexMap.ContainsKey(oldIndex)) {
                        newIndex = copy.vertices.Count;

                        // Duplicate vertex data
                        copy.vertices.Add(vertices[oldIndex]);
                        copy.normals.Add(normals[oldIndex]);
                        copy.tangents.Add(tangents[oldIndex]);
                        copy.uvs.Add(uvs[oldIndex]);
                        copy.uvs2.Add(uvs2[oldIndex]);

                        copy.colors.Add(colors[oldIndex]);
                        if (boneWeights.Count > 0)
                            copy.boneWeights.Add(boneWeights[oldIndex]);


                        vertexMap[oldIndex] = newIndex;
                    }
                    else {
                        // Use original or previously duplicated vertex
                        newIndex = vertexMap.ContainsKey(oldIndex) ? vertexMap[oldIndex] : oldIndex;
                    }

                    // Add index to new submesh triangles
                    newSubMesh.triangles.Add(newIndex);
                }
            }

            // Copy additional mesh data as-is
            copy.rootBone = rootBone;
            copy.bones = new List<Transform>(bones);
            copy.boneNames = new List<string>(boneNames);
            copy.boneMappings = new Dictionary<string, int>(boneMappings);
            copy.bindPoses = new List<Matrix4x4>(bindPoses);

            copy.sourceTransform = sourceTransform;
            copy.localToWorld = localToWorld;
            copy.worldToLocal = worldToLocal;
            copy.extraMeshTransform = extraMeshTransform;
            copy.skinnedMesh = skinnedMesh;
            copy.invertedMesh = invertedMesh;

            copy.bodyMask = bodyMask;
            copy.unpacked = true;

            return copy;
        }

        public Bounds CalculateBoundsFromVertexData() {
            Bounds bounds = new Bounds();
            foreach (Vector3 vertex in vertices) {
                bounds.Encapsulate(vertex);
            }
            return bounds;
        }

        class VertexData {
            public VertexData(Vector3 pos, Vector3 normal, Vector4 tangent, Vector2 uv, Vector2 uv2, Color color, BoneWeight boneWeight) {
                this.pos = pos;
                this.normal = normal;
                this.color = color;
                this.uvs = uv;
                this.uvs2 = uv2;
                this.tangent = tangent;

                this.boneWeight = boneWeight;
            }
            public int MakeHash() {
                int hash = 0;
                hash ^= pos.GetHashCode();
                hash ^= normal.GetHashCode();
                hash ^= color.GetHashCode();
                hash ^= uvs.GetHashCode();
                hash ^= uvs2.GetHashCode();
                hash ^= tangent.GetHashCode();
                hash ^= instanceData.GetHashCode();
                hash ^= boneWeight.GetHashCode();
                return hash;
            }

            public Vector3 pos;
            public Vector3 normal;
            public Color color;
            public Vector2 uvs;
            public Vector2 uvs2;
            public Vector4 tangent;
            public Vector2 instanceData;
            public BoneWeight boneWeight;

        }

        private bool IsAllBoneId(BoneWeight weight, int boneId) {
            if (weight.boneIndex0 != boneId && weight.weight0 > 0)
                return false;
            if (weight.boneIndex1 != boneId && weight.weight1 > 0)
                return false;
            if (weight.boneIndex2 != boneId && weight.weight2 > 0)
                return false;
            if (weight.boneIndex3 != boneId && weight.weight3 > 0)
                return false;
            return true;
        }

        private bool IsAnyBoneId(BoneWeight weight, int boneId, float cutoff) {
            if (weight.boneIndex0 == boneId && weight.weight0 > cutoff)
                return true;
            if (weight.boneIndex1 == boneId && weight.weight1 > cutoff)
                return true;
            if (weight.boneIndex2 == boneId && weight.weight2 > cutoff)
                return true;
            if (weight.boneIndex3 == boneId && weight.weight3 > cutoff)
                return true;
            return false;
        }

        public void DeleteFacesBasedOnBone(string boneName) {
            bool found = boneMappings.TryGetValue(boneName, out int boneId);
            if (found) {
                DeleteFacesBasedOnBoneId(boneId);
            }
        }

        public void DeleteFacesBasedOnBoneId(int boneId) {

            const float cutoff = 0.5f;

            foreach (SubMesh subMesh in subMeshes) {
                List<int> newFaces = new List<int>();
                for (int i = 0; i < subMesh.triangles.Count; i += 3) {

                    //Skip this face?
                    if (IsAnyBoneId(boneWeights[subMesh.triangles[i + 0]], boneId, cutoff) == true ||
                        IsAnyBoneId(boneWeights[subMesh.triangles[i + 1]], boneId, cutoff) == true ||
                        IsAnyBoneId(boneWeights[subMesh.triangles[i + 2]], boneId, cutoff) == true) {

                        continue;
                    }

                    newFaces.Add(subMesh.triangles[i]);
                    newFaces.Add(subMesh.triangles[i + 1]);
                    newFaces.Add(subMesh.triangles[i + 2]);

                }
                subMesh.triangles = newFaces;
            }

        }

        public void DeleteFacesBasedOnBodyMask(int bodyMask) {
            if (uvs2.Count == 0 || uvs2.Count != vertices.Count) {
                Debug.LogError("No uv1 data found on the mesh for body masking.");
                return;
            }
 
            bool[] maskedVerts = new bool[vertices.Count];

            //Loop through all the vertices and classify if they pass the body mask
            for (int i = 0; i < uvs2.Count; i++) {

                //The bodymask is 32 bits, in an 8 by 4 grid, so we need to convert the uv to a 0-31 index
                Vector2 vec = uvs2[i];
                int x = (int)(vec.x * 8);       //8 wide
                int y = (int)((1-vec.y) * 4);   //4 tall - y axis is flipped (?)
                int index = x + y * 8;

                maskedVerts[i] = false;
                if (index > 0) {
                    int bit = 1 << (index-1);
                    if ((bodyMask & bit) != 0) {
                        maskedVerts[i] = true;
                    }
                }
            }            

            //Create the new faces
            foreach (SubMesh subMesh in subMeshes) {
                List<int> newFaces = new List<int>();
                for (int i = 0; i < subMesh.triangles.Count; i += 3) {
                    if (maskedVerts[subMesh.triangles[i]] == false && maskedVerts[subMesh.triangles[i + 1]] == false && maskedVerts[subMesh.triangles[i + 2]] == false) {
                          newFaces.Add(subMesh.triangles[i]);
                          newFaces.Add(subMesh.triangles[i + 1]);
                          newFaces.Add(subMesh.triangles[i + 2]);
                    }
                }
                subMesh.triangles = newFaces;
            }
        }

        public void RepackVertices() {
            if (normals.Count != vertices.Count || tangents.Count != vertices.Count || uvs.Count != vertices.Count || uvs2.Count != vertices.Count || boneWeights.Count != vertices.Count) {
                Debug.LogError("Vertex data is not all the same size!");
                return;
            }

            Dictionary<int, VertexData> uniqueVertices = new Dictionary<int, VertexData>();
            Dictionary<int, int> hashToIndex = new Dictionary<int, int>();
            List<VertexData> packedVertexData = new List<VertexData>();
            int nextIndex = 0; // This will keep track of the next index to assign.

            foreach (SubMesh subMesh in subMeshes) {
                List<int> newFaces = new List<int>();
                foreach (int index in subMesh.triangles) {

                    Color colorRecord = Color.white;
                    if (colors != null && colors.Count > 0) {
                        colorRecord = colors[index];
                    }

                    VertexData vertexData = new VertexData(vertices[index], normals[index], tangents[index], uvs[index], uvs2[index], colorRecord, boneWeights[index]);
                    int hash = vertexData.MakeHash();

                    if (!uniqueVertices.TryGetValue(hash, out VertexData existingVertex)) {
                        // Add the vertex if it's not already in the dictionary.
                        uniqueVertices.Add(hash, vertexData);
                        // Map this vertex's hash to the next available index.
                        packedVertexData.Add(vertexData);
                        hashToIndex.Add(hash, nextIndex);

                        // Use the current value of nextIndex for this vertex, then increment it.
                        newFaces.Add(nextIndex);
                        nextIndex++; // Increment the index for the next unique vertex.
                    }
                    else {
                        // This vertex is a duplicate, get its assigned index.
                        newFaces.Add(hashToIndex[hash]);
                    }
                }

                // Replace the subMesh's triangles with the new indices.
                subMesh.triangles = newFaces;
            }

            int count = packedVertexData.Count;
            vertices = new List<Vector3>(count);
            normals = new List<Vector3>(count);
            tangents = new List<Vector4>(count);
            uvs = new List<Vector2>(count);
            uvs2 = new List<Vector2>(count);
            colors = new List<Color>(count);
            boneWeights = new List<BoneWeight>(count);

            for (int i = 0; i < count; i++) {
                VertexData data = packedVertexData[i];
                //Add em
                vertices.Add(data.pos);
                normals.Add(data.normal);
                tangents.Add(data.tangent);
                uvs.Add(data.uvs);
                uvs2.Add(data.uvs2);
                colors.Add(data.color);
                boneWeights.Add(data.boneWeight);
            }

        }

        // public static Transform FindChildByName(Transform parent, string name) {
        //     foreach (Transform child in parent) {
        //         if (child.name == name) {
        //             return child;
        //         }
        //
        //         Transform result = FindChildByName(child, name);
        //         if (result != null) {
        //             return result;
        //         }
        //     }
        //
        //     return null;
        // }

        public void MergeMeshCopy(MeshCopy source) {
            //Take the contents of another meshCopy and absorb it
            if (source.skinnedMesh != skinnedMesh) {
                Debug.LogWarning("Merging a skinned mesh with a non skinned mesh" + source.sourceTransform.name + " " + source.skinnedMesh + " " + skinnedMesh);
                return;
            }

            if (source.skinnedMesh && source.vertices.Count != source.boneWeights.Count) {
                Debug.LogError("Incoming source does not have correct array sizes");
            }

            bool isFirstMesh = true;
            if (vertices.Count > 0) {
                isFirstMesh = false;
            }

            if (isFirstMesh == true) {
                this.rootBone = source.rootBone;
                this.sourceTransform = source.sourceTransform;
                this.localToWorld = source.localToWorld;
                this.worldToLocal = source.worldToLocal;
            }

            int currentVertexCount = vertices.Count;
            this.vertices.AddRange(source.vertices);
            this.normals.AddRange(source.normals);
            this.tangents.AddRange(source.tangents);
            this.uvs.AddRange(source.uvs);
            this.uvs2.AddRange(source.uvs2);
            this.colors.AddRange(source.colors);

            //this thing is parented to bones, but didn't provide any itself
            //Eg: a sword in righthand
            //So patch up the bone now if we can
            bool dontTransform = false;
            if (source.skinnedMesh && source.boneNames.Count == 1 && source.bones.Count == 0 && rootBone != null) {
                //Find the bone by name

                string boneName = source.boneNames[0];
                bool foundBone = boneMappings.TryGetValue(boneName, out int boneIndex);
                if (foundBone == false) {
                    Debug.LogWarning("Could not find bone " + boneName + " in " + source.sourceTransform.name);
                    boneIndex = 0;
                }

                source.bones.Add(bones[boneIndex]);
                source.bindPoses.Add(bindPoses[boneIndex]);

                Matrix4x4 poseMatrix = bindPoses[boneIndex].inverse * source.extraMeshTransform;

                for (int i = currentVertexCount; i < vertices.Count; i++) {
                    vertices[i] = poseMatrix.MultiplyPoint3x4(vertices[i]);
                    normals[i] = poseMatrix.MultiplyVector(normals[i]);
                    Vector3 tangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                    tangent = poseMatrix.MultiplyVector(tangent).normalized;
                    tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
                }

                dontTransform = true;
            }

            if (this.skinnedMesh && source.bones.Count > 0) {
                //when merging skinned meshes, all vertices are in the local space of their host SkinnedRenderer
                //This means we need to transform them into the space of the "Host" skinned renderer, the one where the bindPoses matrixes are coming from

                if (isFirstMesh == false && dontTransform == false) {
                    Matrix4x4 newMeshToHostMesh = worldToLocal * source.localToWorld;

                    //Transform the vertices
                    for (int i = currentVertexCount; i < vertices.Count; i++) {
                        vertices[i] = newMeshToHostMesh.MultiplyPoint3x4(vertices[i]);
                        normals[i] = newMeshToHostMesh.MultiplyVector(normals[i]);
                        Vector3 tangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                        tangent = newMeshToHostMesh.MultiplyVector(tangent).normalized;
                        tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
                    }
                }

                //Do correct bone mappings
                if (true) {
                    Dictionary<int, int> boneRemappings = new();

                    bool guessBones = false;
                    if (source.bones[0] == null) {
                        guessBones = true;
                        // Debug.LogWarning("Null bone in source mesh, making good guesses?");
                    }

                    //We're merging skinned meshes
                    for (int originalBoneIndex = 0; originalBoneIndex < source.bones.Count; originalBoneIndex++) {
                        if (guessBones == true) {
                            //So this happens when the armature has been deleted.
                            //You get handed an empty array of bones, but the bindposes are still there.
                            //So we make the assumption that the skeletons match and hope for the best
                            boneRemappings.Add(originalBoneIndex, originalBoneIndex);
                        }
                        else {
                            //Regular path
                            string boneName = source.boneNames[originalBoneIndex];
                            //Can't do this on the thread
                            //string boneName = source.bones[originalBoneIndex].name;

                            bool found = boneMappings.TryGetValue(boneName, out int boneIndexInTargetMesh);

                            if (found == false) {
                                //Dont have a mapping for this bone, insert it at the end
                                boneIndexInTargetMesh = bones.Count;
                                boneMappings.Add(boneName, boneIndexInTargetMesh);
                                bones.Add(source.bones[originalBoneIndex]);
                                boneNames.Add(boneName);
                                bindPoses.Add(source.bindPoses[originalBoneIndex]);
                            }
                            boneRemappings.Add(originalBoneIndex, boneIndexInTargetMesh);
                        }
                    }

                    // Debug.Log("doing bones: " + source.sourceTransform.name);
                    foreach (BoneWeight weight in source.boneWeights) {
                        BoneWeight newWeight = weight;
                        newWeight.boneIndex0 = boneRemappings[newWeight.boneIndex0];
                        newWeight.boneIndex1 = boneRemappings[newWeight.boneIndex1];
                        newWeight.boneIndex2 = boneRemappings[newWeight.boneIndex2];
                        newWeight.boneIndex3 = boneRemappings[newWeight.boneIndex3];
                        boneWeights.Add(newWeight);
                    }
                }
                else {
                    //Naieve version, each mesh gets their own bones
                    // int currentBonesCount = bones.Count;
                    // bones.AddRange(source.bones);
                    // bindPoses.AddRange(source.bindPoses);
                    //
                    // foreach(BoneWeight weight in source.boneWeights)
                    // {
                    //     BoneWeight newWeight = weight;
                    //     newWeight.boneIndex0 += currentBonesCount;
                    //     newWeight.boneIndex1 += currentBonesCount;
                    //     newWeight.boneIndex2 += currentBonesCount;
                    //     newWeight.boneIndex3 += currentBonesCount;
                    //     boneWeights.Add(newWeight);
                    // }
                }

            }

            var isClient = RunCore.IsClient();

            for (int i = 0; i < source.subMeshes.Count; i++) {
                SubMesh sourceMesh = source.subMeshes[i];
                if (isClient && sourceMesh.material == null) {
                    sourceMesh.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                }

                //find a submesh with a matching material
                int hash = sourceMesh.material.GetHashCode();

                SubMesh targetMesh = null;

                for (int j = 0; j < subMeshes.Count; j++) {
                    SubMesh candidateMesh = subMeshes[j];

                    //Standard merge for identical materials AND identical batchableMaterialData
                    if (candidateMesh.material.GetHashCode() == hash) {

                        if (candidateMesh.batchableMaterialData != null && sourceMesh.batchableMaterialData != null) {
                            if (candidateMesh.batchableMaterialData.color == sourceMesh.batchableMaterialData.color) {
                                targetMesh = candidateMesh;
                                break;
                            }
                        }

                        if (candidateMesh.batchableMaterialData == null && sourceMesh.batchableMaterialData == null) {
                            targetMesh = candidateMesh;
                            break;
                        }
                    }
                }

                if (targetMesh == null) {
                    //no submesh with this material, add it
                    targetMesh = sourceMesh.ManualCloneNoTris();

                    //clone material
                    targetMesh.material = sourceMesh.material;// = //new Material(sourceMesh.material);

                    subMeshes.Add(targetMesh);
                }

                //Add all the triangle indices to the target, but increment them by the current vertex count
                //
                //This is because we are merging two meshes, and the triangle indices in the source mesh
                //are relative to the source mesh, not the target mesh
                for (int j = 0; j < sourceMesh.triangles.Count; j++) {
                    targetMesh.triangles.Add(sourceMesh.triangles[j] + currentVertexCount);
                }


            }
        }


        /*
        //TODO can't access these in thread :(
        static int[] supportedShaderIds = new[] { Shader.PropertyToID("_Color") };
        private static bool MaterialIsBatchable(SubMesh candidateMesh, SubMesh sourceMesh) {
            if (candidateMesh.material.GetHashCode() != sourceMesh.material.GetHashCode() ||
                candidateMesh.batchableMaterialName == null || sourceMesh.batchableMaterialName == null ||
                candidateMesh.batchableMaterialName != sourceMesh.batchableMaterialName) {
                return false;
            }

            //Compare properties of each material
            var candidateShader = candidateMesh.material.shader;
            var sourceShader = sourceMesh.material.shader;

            for (int index = 0; index < candidateShader.GetPropertyCount(); index++) {
                int propertyId = candidateShader.GetPropertyNameId(index);

                //Check if this is an instancable property
                bool supportedProperty = false;
                for (int supportedI = 0; supportedI < supportedShaderIds.Length; supportedI++) {
                    if (propertyId == supportedShaderIds[supportedI]) {
                        supportedProperty = true;
                        break;
                    }
                }

                if (supportedProperty) {
                    continue;
                }

                //Compare Textures
                if (candidateShader.GetPropertyType(index) == ShaderPropertyType.Texture) {
                    //If this property isn't the same
                    if (candidateMesh.material.GetTexture(propertyId) != sourceMesh.material.GetTexture(propertyId)) {
                        return false;
                    }
                }

                //Compare Colors
                if (candidateShader.GetPropertyType(index) == ShaderPropertyType.Color) {
                    //If this property isn't the same
                    if (!candidateMesh.material.GetColor(propertyId).Equals(sourceMesh.material.GetColor(propertyId))) {
                        return false;
                    }
                }

                //Compare Floats and Ranges
                if (candidateShader.GetPropertyType(index) == ShaderPropertyType.Float || candidateShader.GetPropertyType(index) == ShaderPropertyType.Range) {
                    //If this property isn't the same
                    if (Math.Abs(candidateMesh.material.GetFloat(propertyId) - sourceMesh.material.GetFloat(propertyId)) > float.Epsilon) {
                        return false;
                    }
                }

                //Compare Vectors
                if (candidateShader.GetPropertyType(index) == ShaderPropertyType.Vector) {
                    //If this property isn't the same
                    if (!candidateMesh.material.GetVector(propertyId).Equals(sourceMesh.material.GetVector(propertyId))) {
                        return false;
                    }
                }
            }

            return true;
        }*/

        public static List<MeshCopy> LoadActiveAccessory(ActiveAccessory activeAccessory) {
            List<MeshCopy> results = new();
            if (activeAccessory.meshRenderers.Length > 0) {
                int i = 0;
                foreach (var meshRenderer in activeAccessory.meshRenderers) {
                    MeshCopy meshCopy = new MeshCopy(activeAccessory.meshFilters[i].sharedMesh, meshRenderer.sharedMaterials, meshRenderer.transform);

                    // if (meshRenderer.TryGetComponent<MaterialColorURP>(out var matColor)) {
                    //     meshCopy.ExtractMaterialColor(matColor);
                    // }

                    results.Add(meshCopy);
                    i++;
                }
            } else if (activeAccessory.skinnedMeshRenderers.Length > 0) {
                foreach (var skinnedMeshRenderer in activeAccessory.skinnedMeshRenderers) {
                    //See if theres a MaterialColor on this gameObject
                    MeshCopy meshCopy = new MeshCopy(skinnedMeshRenderer.sharedMesh, skinnedMeshRenderer.sharedMaterials, skinnedMeshRenderer.transform, skinnedMeshRenderer.bones, skinnedMeshRenderer.rootBone);

                    // if (skinnedMeshRenderer.TryGetComponent<MaterialColorURP>(out var matColor)) {
                    //     meshCopy.ExtractMaterialColor(matColor);
                    // }

                    //Grab their bone masks
                    meshCopy.bodyMask = activeAccessory.AccessoryComponent.bodyMask;

                    results.Add(meshCopy);
                }
            }

            return results;
        }

        // [Obsolete]
        // public static List<MeshCopy> LoadSlow(Transform transform) {
        //     List<MeshCopy> results = new List<MeshCopy>();
        //
        //     GameObject instance = transform.gameObject;
        //
        //     GetMeshesSlow(instance, results);
        //
        //     return results;
        // }
        //
        // //Recursively get all filters and materials
        // [Obsolete]
        // private static void GetMeshesSlow(GameObject gameObject, List<MeshCopy> results) {
        //     if (gameObject.name == MeshCombiner.MeshCombineSkinnedName) {
        //         return;
        //     }
        //     // if (gameObject.name == MeshCombiner.MeshCombineStaticName) {
        //     //     return;
        //     // }
        //     Debug.Log("Made it here: " + gameObject.name);
        //
        //     //Get the mesh filter
        //     MeshFilter filter = gameObject.GetComponent<MeshFilter>();
        //     MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
        //
        //     if (filter != null && renderer != null) {
        //         //See if theres a MaterialColor on this gameObject
        //         MeshCopy meshCopy = new MeshCopy(filter.sharedMesh, renderer.sharedMaterials, gameObject.transform);
        //
        //         MaterialColorURP matColor = gameObject.GetComponent<MaterialColorURP>();
        //         if (matColor) {
        //             meshCopy.ExtractMaterialColor(matColor);
        //         }
        //
        //         results.Add(meshCopy);
        //     }
        //
        //     SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
        //     if (skinnedMeshRenderer) {
        //         //See if theres a MaterialColor on this gameObject
        //         MeshCopy meshCopy = new MeshCopy(skinnedMeshRenderer.sharedMesh, skinnedMeshRenderer.sharedMaterials, gameObject.transform, skinnedMeshRenderer.bones, skinnedMeshRenderer.rootBone);
        //
        //         MaterialColorURP matColor = gameObject.GetComponent<MaterialColorURP>();
        //         if (matColor) {
        //             meshCopy.ExtractMaterialColor(matColor);
        //         }
        //
        //         //Grab their bone masks
        //         AccessoryComponent accessoryComponent = gameObject.GetComponent<AccessoryComponent>();
        //         if (accessoryComponent) {
        //             meshCopy.bodyMask = accessoryComponent.bodyMask;
        //         }
        //
        //         results.Add(meshCopy);
        //     }
        //
        //
        //     //Get the children
        //     foreach (Transform child in gameObject.transform) {
        //         GetMeshesSlow(child.gameObject, results);
        //     }
        // }

        public void ExtractMaterialColor(MaterialColorURP matColor) {
            //Apply the material color
            // for (int i = 0; i < subMeshes.Count; i++) {
            //     var colorData = matColor.colorSettings[i];
            //     if (colorData != null) {
            //         SubMesh subMesh = subMeshes[i];
            //         subMesh.batchableMaterialData = new BatchableMaterialData(colorData.baseColor);
            //         Debug.Log("Extracted color " + colorData.baseColor + ". name: " + this.sourceTransform.gameObject.name, this.sourceTransform.gameObject);
            //     }
            // }
        }


    }
}