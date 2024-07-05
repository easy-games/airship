using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Airship {

    //This class exists because we cannot manipulate actual UnityEngine.Meshs in a thread
    //So we copy and cache assets here first
    public class MeshCopy {
        public class SubMesh {
            public List<int> triangles = new();

            public Material material = null;
            public string batchableMaterialName = null;

            public List<BatchableMaterialData> batchableMaterialData = new();

            public SubMesh ManualClone() {
                SubMesh output = ManualCloneNoTris();
                output.triangles = new List<int>(triangles);
                return output;
            }

            public SubMesh ManualCloneNoTris() {
                SubMesh output = new SubMesh();
                output.triangles = new List<int>();
                output.material = material;

                output.batchableMaterialName = batchableMaterialName;
                //Clone the batchableMaterialData
                foreach (BatchableMaterialData data in batchableMaterialData) {
                    output.batchableMaterialData.Add(data);
                }

                return output;
            }
        }
        public struct BatchableMaterialData {
            public Color color { get; private set; }
            public Color emissiveColor { get; private set; }
            public float emissiveMix { get; private set; }

            public float emissiveLevel { get; private set; }

            public BatchableMaterialData(bool defaultValue) {
                this.color = Color.white;
                this.emissiveColor = Color.black;
                this.emissiveMix = 0;
                this.emissiveLevel = 0;
            }

            public BatchableMaterialData(Color color, Color emissiveColor, float emissiveMix, float emissiveLevel) {
                this.color = color;
                this.emissiveColor = emissiveColor;
                this.emissiveMix = emissiveMix;
                this.emissiveLevel = emissiveLevel;
            }

            // Explicitly providing a cloning method which creates a new instance with modified properties
            public BatchableMaterialData WithColor(Color newColor) {
                return new BatchableMaterialData(newColor, emissiveColor, emissiveMix, emissiveLevel);
            }

            public BatchableMaterialData WithEmissiveColor(Color newEmissiveColor) {
                return new BatchableMaterialData(color, newEmissiveColor, emissiveMix, emissiveLevel);
            }

            public BatchableMaterialData WithEmissiveValue(float newEmissiveValue) {
                return new BatchableMaterialData(color, emissiveColor, newEmissiveValue, emissiveLevel);
            }
        }

        //List of vertices uvs etc
        public List<Vector3> vertices = new();
        public List<Vector3> normals = new();
        public List<Vector4> tangents = new();
        public List<Vector3> binormals = new();
        public List<Vector2> uvs = new();
        public List<Vector2> uvs2 = new();
        public List<Vector2> instanceData = new(); //uv7 is for the batching index
        public List<Color> colors = new();
        public Transform rootBone = null;

        public List<Transform> bones = new();
        Dictionary<string, int> boneMappings = new();
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
        
        public bool skinnedMesh = false;

        
        //Runs on the main thread. Probably a great source of caching and optimisations
        public MeshCopy(Mesh mesh, Material[] materials, Transform hostTransform = null, Transform[] skinnedBones = null, Transform skinnedRootBone = null, bool warn = true) {
            if (mesh == null) {
                Debug.LogWarning("Null mesh on mesh copy");
                return;
            }

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
                    Debug.Log("Found a MeshCombinerBone for " + hostTransform.name + " to " + meshCombinerBone.boneName);
                    //This object is fake skinned into place

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


                    //This object is going to get parented to a bone, so we need to put this in "model space"
                    //model space is a worldspace, minus the parents transform

                    Matrix4x4 modelSpaceMatrix = hostTransform.parent.worldToLocalMatrix * worldMatrix;

                    for (int i = 0; i < vertices.Count; i++) {
                        vertices[i] = modelSpaceMatrix.MultiplyPoint3x4(vertices[i]);
                        normals[i] = modelSpaceMatrix.MultiplyVector(normals[i]).normalized;

                        Vector3 tangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                        tangent = modelSpaceMatrix.MultiplyVector(tangent).normalized;
                        tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
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

            //transform all the bindposes
            //if (transform != null)
            //{
            //  for (int i = 0; i < bindPoses.Count; i++)
            //  {
            //      bindPoses[i] = bindPoses[i] * transform.worldToLocalMatrix;//subMesh.skinnedMeshRenderer.transform.worldToLocalMatrix
            //  }
            //}

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

            int instancePropertyID = Shader.PropertyToID("INSTANCE_DATA");

            for (int i = 0; i < mesh.subMeshCount; i++) {
                SubMesh subMesh = new();

                Material mat = null;
                if (i < materials.Length) {
                    mat = materials[i];
                }
                else {
                    //default material
                    mat = new Material(Shader.Find("Hidden/AirshipErrorShader"));
                }
                subMesh.material = mat;

                //id if the material is batchable

                if (mat) {
                    //TODO: should we force this? 
                    //Must have a MaterialColor controlling properties to support Instancing FOR NOW
                    var matColor = hostTransform.gameObject.GetComponent<MaterialColor>();
                    if (matColor) {
                        //Debug.Log("Shader Name:" + mat.shader.name);
                        for (int index = 0; index < mat.shader.GetPropertyCount(); index++) {
                            int propertyName = mat.shader.GetPropertyNameId(index);
                            //Debug.Log(mat.shader.GetPropertyName(index));
                            if (propertyName == instancePropertyID) {
                                subMesh.batchableMaterialName = mat.shader.name;
                            }
                        }
                    }
                }

                mesh.GetTriangles(subMesh.triangles, i);
                subMeshes.Add(subMesh);
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
            copy.instanceData = new List<Vector2>(instanceData);
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
            if (instanceData.Count > 0)
                copy.instanceData.AddRange(instanceData);

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
                        if (instanceData.Count > 0)
                            copy.instanceData.Add(instanceData[oldIndex]);
                        copy.colors.Add(colors[oldIndex]);
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

            copy.bodyMask = bodyMask;
            copy.unpacked = true;

            return copy;
        }


        //Spin through the surface and build new vertex data
        public MeshCopy ManualCloneUnpackedFat() {
            //Todo: this could be greatly optimized - it only exists because the submesh triangles that are reaching this point
            //potentially share vertices that need unique vertex colors. Preprocess them out!

            MeshCopy copy = new MeshCopy();

            foreach (SubMesh subMesh in subMeshes) {
                SubMesh newSubMesh = subMesh.ManualCloneNoTris();
                copy.subMeshes.Add(newSubMesh);

                foreach (int index in subMesh.triangles) {
                    newSubMesh.triangles.Add(copy.vertices.Count);

                    //Unpack all the structs
                    copy.vertices.Add(vertices[index]);
                    copy.normals.Add(normals[index]);
                    copy.tangents.Add(tangents[index]);
                    copy.uvs.Add(uvs[index]);
                    copy.uvs2.Add(uvs2[index]);
                    if (instanceData.Count > 0) {
                        copy.instanceData.Add(instanceData[index]);
                    }
                    copy.colors.Add(colors[index]);
                    if (boneWeights.Count > 0) {
                        copy.boneWeights.Add(boneWeights[index]);
                    }
                }
            }
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
            public VertexData(Vector3 pos, Vector3 normal, Vector4 tangent, Vector2 uv, Vector2 uv2, Color color, BoneWeight boneWeight, Vector2 instanceData) {
                this.pos = pos;
                this.normal = normal;
                this.color = color;
                this.uvs = uv;
                this.uvs2 = uv2;
                this.tangent = tangent;
                this.instanceData = instanceData;
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
            foreach (Vector2 vec in uvs2) {
                //The bodymask is 32 bits, in an 8 by 4 grid, so we need to convert the uv to a 0-31 index
                int x = (int)(vec.x * 8);   //8 wide
                int y = (int)(vec.y * 4);   //4 tall
                int index = x + y * 8; 

                if (index > 0) {
                    int bit = 1 << index;
                    if ((bodyMask & bit) != 0) {
                        //This vertex is part of the body mask
                        maskedVerts[index] = true;
                    }
                    else {
                        //This vertex is not part of the body mask
                        maskedVerts[index] = false;
                    }
                }
            }
            
            //Create the new faces
            foreach (SubMesh subMesh in subMeshes) {
                List<int> newFaces = new List<int>();
                for (int i = 0; i < subMesh.triangles.Count; i += 3) {
                    if (maskedVerts[subMesh.triangles[i]] == false || maskedVerts[subMesh.triangles[i + 1]] == false || maskedVerts[subMesh.triangles[i + 2]] == false) {
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
                    Vector2 instanceRecord = Vector2.zero;
                    if (instanceData != null) {
                        instanceRecord = instanceData[index];
                    }
                    Color colorRecord = Color.white;
                    if (colors != null && colors.Count > 0) {
                        colorRecord = colors[index];
                    }

                    VertexData vertexData = new VertexData(vertices[index], normals[index], tangents[index], uvs[index], uvs2[index], colorRecord, boneWeights[index], instanceRecord);
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
            if (instanceData != null) {
                instanceData = new List<Vector2>(count);
                for (int i = 0; i < count; i++) {
                    VertexData data = packedVertexData[i];
                    instanceData.Add(data.instanceData);
                }
            }
        }


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
                rootBone = source.rootBone;
                sourceTransform = source.sourceTransform;
                localToWorld = source.localToWorld;
                worldToLocal = source.worldToLocal;
            }

            int currentVertexCount = vertices.Count;
            vertices.AddRange(source.vertices);
            normals.AddRange(source.normals);
            tangents.AddRange(source.tangents);
            uvs.AddRange(source.uvs);
            uvs2.AddRange(source.uvs2);

            //Initialize to zero on the instance data
            for (int i = 0; i < source.vertices.Count; i++) {
                instanceData.Add(Vector2.zero);
            }

            //this thing is parented to bones, but didn't provide any itself
            //Eg: a sword in righthand
            //So patch up the bone now if we can
            bool dontTransform = false;
            if (source.skinnedMesh && source.boneNames.Count == 1 && source.bones.Count == 0) {
                //Find the bone by name

                bool foundBone = boneMappings.TryGetValue(source.boneNames[0], out int boneIndex);
                if (foundBone == false) {
                    Debug.LogWarning("Could not find bone " + source.boneNames[0] + " in " + source.sourceTransform.name);
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

            if (skinnedMesh && source.bones.Count > 0) {
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

            for (int i = 0; i < source.subMeshes.Count; i++) {
                SubMesh sourceMesh = source.subMeshes[i];
                if (sourceMesh.material == null) {
                    sourceMesh.material = new Material(Shader.Find("Hidden/AirshipErrorShader"));
                }

                //find a submesh with a matching material
                int hash = sourceMesh.material.GetHashCode();
                int instanceIndex = -1;
                SubMesh targetMesh = null;

                for (int j = 0; j < subMeshes.Count; j++) {
                    SubMesh candidateMesh = subMeshes[j];

                    //Is this material instanceable?
                    if (candidateMesh.material.GetHashCode() == sourceMesh.material.GetHashCode() &&
                        candidateMesh.batchableMaterialName != null && sourceMesh.batchableMaterialName != null &&
                        candidateMesh.batchableMaterialName == sourceMesh.batchableMaterialName) {
                        targetMesh = candidateMesh;
                        instanceIndex = candidateMesh.batchableMaterialData.Count;

                        //Add new batchableMaterialData
                        if (sourceMesh.batchableMaterialData.Count == 0) {
                            //Add an empty one
                            targetMesh.batchableMaterialData.Add(new BatchableMaterialData(true));
                        }
                        else {
                            //Add the real one
                            targetMesh.batchableMaterialData.Add(sourceMesh.batchableMaterialData[0]);
                        }

                        break;
                    }

                    if (candidateMesh.material.GetHashCode() == hash) {
                        targetMesh = candidateMesh;
                        break;
                    }
                }

                if (targetMesh == null) {
                    //no submesh with this material, add it
                    targetMesh = sourceMesh.ManualCloneNoTris();

                    //clone material
                    targetMesh.material = sourceMesh.material;// = //new Material(sourceMesh.material);

                    subMeshes.Add(targetMesh);

                    if (targetMesh.batchableMaterialName != null) {
                        instanceIndex = 0;
                    }
                }

                //Add all the triangle indices to the target, but increment them by the current vertex count
                //
                //This is because we are merging two meshes, and the triangle indices in the source mesh
                //are relative to the source mesh, not the target mesh
                for (int j = 0; j < source.subMeshes[i].triangles.Count; j++) {
                    targetMesh.triangles.Add(source.subMeshes[i].triangles[j] + currentVertexCount);
                }

                //Write the instance information
                if (instanceIndex > -1) {
                    //Debug.Log("writing index " + instanceIndex + " to " + targetMesh.batchableMaterialName);

                    //write it to the instanceData (note this isn't optimal as it'll write the same vertex a few times if its shared, but it's not a big deal)
                    foreach (int vertexIndex in sourceMesh.triangles) {
                        instanceData[vertexIndex + currentVertexCount] = new Vector2(instanceIndex, instanceIndex);
                    }
                }
            }
        }


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
        }

        public static List<MeshCopy> Load(Transform transform, bool showError) {
            List<MeshCopy> results = new List<MeshCopy>();

            GameObject instance = transform.gameObject;

            GetMeshes(instance, results);

            return results;
        }

        public static List<MeshCopy> Load(string assetPath, bool showError) {
            int startTime = System.DateTime.Now.Millisecond;

            List<MeshCopy> results = new();

            UnityEngine.Object asset = AssetBridge.Instance.LoadAssetInternal<UnityEngine.Object>(assetPath + ".prefab", false);

            if (asset == null) {
                asset = AssetBridge.Instance.LoadAssetInternal<UnityEngine.Object>(assetPath + ".FBX", false);
            }

            if (asset == null && showError == true) {
                Debug.LogError("Failed to load asset at path: " + assetPath);
                return results;
            }

            if (asset is Mesh) {
                Mesh mesh = asset as Mesh;
                Material[] mats = new Material[0];
                MeshCopy meshCopy = new MeshCopy(mesh, mats, null, null);
                results.Add(meshCopy);
                Debug.Log("Load Time taken on mainthread: " + (System.DateTime.Now.Millisecond - startTime));
                return results;
            }

            if (asset is GameObject) {
                // Instantiate the prefab
                GameObject instance = GameObject.Instantiate((GameObject)asset);

                //Recursively interate over all child gameObjects
                GetMeshes(instance, results);

                if (Application.isPlaying == true) {
                    GameObject.Destroy(instance);
                }
                else {
                    GameObject.DestroyImmediate(instance);
                }
                Debug.Log("Load Time taken on mainthread: " + (System.DateTime.Now.Millisecond - startTime));
                return results;
            }
            Debug.Log("Load Time taken on mainthread: " + (System.DateTime.Now.Millisecond - startTime));
            return results;
        }

        //Recursively get all filters and materials
        private static void GetMeshes(GameObject gameObject, List<MeshCopy> results) {
            if (gameObject.name == MeshCombiner.MeshCombineSkinnedName) {
                return;
            }
            if (gameObject.name == MeshCombiner.MeshCombineStaticName) {
                return;
            }
            
            //Get the mesh filter
            MeshFilter filter = gameObject.GetComponent<MeshFilter>();
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();

            if (filter != null && renderer != null) {
                //See if theres a MaterialColor on this gameObject
                MeshCopy meshCopy = new MeshCopy(filter.sharedMesh, renderer.sharedMaterials, gameObject.transform);

                MaterialColor matColor = gameObject.GetComponent<MaterialColor>();
                if (matColor) {
                    meshCopy.ExtractMaterialColor(matColor);
                }

                results.Add(meshCopy);
            }

            SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer) {
                //See if theres a MaterialColor on this gameObject
                MeshCopy meshCopy = new MeshCopy(skinnedMeshRenderer.sharedMesh, skinnedMeshRenderer.sharedMaterials, gameObject.transform, skinnedMeshRenderer.bones, skinnedMeshRenderer.rootBone);

                MaterialColor matColor = gameObject.GetComponent<MaterialColor>();
                if (matColor) {
                    meshCopy.ExtractMaterialColor(matColor);
                }

                //Grab their bone masks
                AccessoryComponent accessoryComponent = gameObject.GetComponent<AccessoryComponent>();
                if (accessoryComponent) {
                    meshCopy.bodyMask = accessoryComponent.bodyMask;
                }
                
                results.Add(meshCopy);
            }


            //Get the children
            foreach (Transform child in gameObject.transform) {
                GetMeshes(child.gameObject, results);
            }
        }
              


        public void ExtractMaterialColor(MaterialColor matColor) {
            //Apply the material color
            for (int i = 0; i < subMeshes.Count; i++) {
                var colorData = matColor.GetColorSettings(i);
                if (colorData != null) {
                    SubMesh subMesh = subMeshes[i];
                    subMesh.batchableMaterialData.Add(new BatchableMaterialData(colorData.materialColor, colorData.emissiveColor, colorData.emissiveMix, colorData.emissiveLevel));
                }
            }
        }


    }
}