
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

using VoxelData = System.UInt16;
using BlockId = System.UInt16;
using Assets.Airship.VoxelRenderer;
using UnityEngine.Profiling;

namespace VoxelWorldStuff {

    [LuauAPI]
    public partial class MeshProcessor {
        const bool doComplexMeshes = true;

        const int chunkSize = VoxelWorld.chunkSize;
        Chunk chunk;

        private bool geometryReady = false;
        private bool hasDetailMeshes = false;
        public bool GetGeometryReady() { return geometryReady; }

        static Vector3[] srcVertices;
        static Vector3[] srcRegularSamplePoints;
        static Vector3[] srcCornerSamplePoints;

        static Vector2[] srcUvs;
        static Vector3[] srcNormals;
        static int[][] srcFaces;
        static int[][] altSrcFaces;
        static Vector3Int[] faceChecks;
        static Vector3Int[][] occlusionSamples;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnStartup() {
            srcVertices = null;
            srcRegularSamplePoints = null;
            srcCornerSamplePoints = null;
            srcUvs = null;
            srcNormals = null;
            srcFaces = null;
            altSrcFaces = null;
            faceChecks = null;
            occlusionSamples = null;
        }

        static int[] faceAxisForFace = { 2, 2, 0, 0, 1, 1 };
        static Vector3[] normalForFace =
        {
                new Vector3(0,0, 1),
                new Vector3(0,0, -1),
                new Vector3(-1,0, 0),
                new Vector3(1,0, 0),
                new Vector3(0,1, 0),
                new Vector3(0, -1, 0)
        };
        DateTime startMeshProcessingTime;

        public int lastMeshUpdateDuration;

        const int paddedChunkSize = chunkSize + 2;

        VoxelData[] readOnlyVoxel = new VoxelData[paddedChunkSize * paddedChunkSize * paddedChunkSize];
        VoxelData[] processedVoxelMask = new VoxelData[paddedChunkSize * paddedChunkSize * paddedChunkSize];
        private const int capacity = 20000;

        class TemporaryMeshData {
            public Dictionary<string, SubMesh> subMeshes = new();

            public Vector3[] vertices = new Vector3[capacity];
            public int verticesCount = 0;

            public Color32[] colors = new Color32[capacity];
            public int colorsCount = 0;

            public Vector3[] normals = new Vector3[capacity];
            public int normalsCount = 0;

            public Vector2[] uvs = new Vector2[capacity];
            public int uvsCount = 0;


        }



        TemporaryMeshData temporaryMeshData;


        TemporaryMeshData[] detailMeshData;




        bool[] shade = new bool[4];

        Vector3Int key;

        bool finishedProcessing = false;

        public bool GetHasDetailMeshes() {
            return hasDetailMeshes;
        }

        class SubMesh {
            //Todo: Less garbage?
            public List<int> triangles = new(16000);
            public Material srcMaterial;

            public SubMesh(Material originalMaterial) {
                //material = new Material(originalMaterial);
                srcMaterial = originalMaterial;
                triangles = new List<int>();
            }
        };


        public struct SamplePoint {
            public Vector3 position;
            public Vector3 normal;
            public SamplePoint(Vector3 pos, Vector3 norm) {
                position = pos;
                normal = norm;
            }
            public override int GetHashCode() {
                // XOR the hash codes of the position and normal vectors
                return position.GetHashCode() ^ normal.GetHashCode();
            }
        }


        public class Face {
            public Vector3 samplePoint;
            public Color color;
            public Vector3 normal;

            public Face(Vector3 samplePoint, Color color, Vector3 normal) {
                this.samplePoint = samplePoint;
                this.color = color;
                this.normal = normal;
            }
        }

        private static void InitVertexData() {

            //Init static data
            if (srcVertices == null) {
                //triangles = new int[numFaces * 6];
                srcVertices = new Vector3[]
                {
                        //Front face
                        new Vector3(-0.5f, 0.5f, 0.5f), // Top left
                        new Vector3(0.5f, 0.5f, 0.5f), // Top right
                        new Vector3(-0.5f, -0.5f, 0.5f), // Bottom left
                        new Vector3(0.5f, -0.5f, 0.5f), // Bottom right
                    
                        // Back face
                        new Vector3(-0.5f, 0.5f, -0.5f), // Top left
                        new Vector3(0.5f, 0.5f, -0.5f), // Top right
                        new Vector3(-0.5f, -0.5f, -0.5f), // Bottom left
                        new Vector3(0.5f, -0.5f, -0.5f), // Bottom right

                        // Left face
                        new Vector3(-0.5f, 0.5f, 0.5f), // Top left
                        new Vector3(-0.5f, 0.5f, -0.5f), // Top right
                        new Vector3(-0.5f, -0.5f, 0.5f), // Bottom left
                        new Vector3(-0.5f, -0.5f, -0.5f), // Bottom right

                        // Right face
                        new Vector3(0.5f, 0.5f, 0.5f), // Top left
                        new Vector3(0.5f, 0.5f, -0.5f), // Top right
                        new Vector3(0.5f, -0.5f, 0.5f), // Bottom left
                        new Vector3(0.5f, -0.5f, -0.5f), // Bottom right

                        // Top face
                        new Vector3(-0.5f, 0.5f, 0.5f), // Top left
                        new Vector3(-0.5f, 0.5f, -0.5f), // Top right
                        new Vector3(0.5f, 0.5f, 0.5f), // Bottom left
                        new Vector3(0.5f, 0.5f, -0.5f), // Bottom right

                        // Bottom face
                        new Vector3(-0.5f, -0.5f, 0.5f), // Top left
                        new Vector3(-0.5f, -0.5f, -0.5f), // Top right
                        new Vector3(0.5f, -0.5f, 0.5f), // Bottom left
                        new Vector3(0.5f, -0.5f, -0.5f), // Bottom right
                };

                for (int j = 0; j < srcVertices.Length; j++) {
                    srcVertices[j] += new Vector3(0.5f, 0.5f, 0.5f);
                }

                float insetValue = 0.05f;

                float inset = 0.5f - insetValue;
                float offset = 0.5f + insetValue;
                srcCornerSamplePoints = new Vector3[]
                {
                        //Front face
                        new Vector3(-inset, inset, offset), // Top left
                        new Vector3(inset, inset, offset), // Top right
                        new Vector3(-inset, -inset, offset), // Bottom left
                        new Vector3(inset, -inset, offset), // Bottom right
                    
                        // Back face
                        new Vector3(-inset, inset, -offset), // Top left
                        new Vector3(inset, inset, -offset), // Top right
                        new Vector3(-inset, -inset, -offset), // Bottom left
                        new Vector3(inset, -inset, -offset), // Bottom right

                        // Left face
                        new Vector3(-offset, inset, inset), // Top left
                        new Vector3(-offset, inset, -inset), // Top right
                        new Vector3(-offset, -inset, inset), // Bottom left
                        new Vector3(-offset, -inset, -inset), // Bottom right

                        // Right face
                        new Vector3(offset, inset, inset), // Top left
                        new Vector3(offset, inset, -inset), // Top right
                        new Vector3(offset, -inset, inset), // Bottom left
                        new Vector3(offset, -inset, -inset), // Bottom right

                        // Top face
                        new Vector3(-inset, offset, inset), // Top left
                        new Vector3(-inset, offset, -inset), // Top right
                        new Vector3(inset, offset, inset), // Bottom left
                        new Vector3(inset, offset, -inset), // Bottom right

                        // Bottom face
                        new Vector3(-inset, -offset, inset), // Top left
                        new Vector3(-inset, -offset, -inset), // Top right
                        new Vector3(inset, -offset, inset), // Bottom left
                        new Vector3(inset, -offset, -inset), // Bottom right
                };

                for (int j = 0; j < srcCornerSamplePoints.Length; j++) {
                    srcCornerSamplePoints[j] += new Vector3(0.5f, 0.5f, 0.5f);
                }

                //Keep the offset, but no inset
                inset = 0.5f;
                srcRegularSamplePoints = new Vector3[]
                {
                        //Front face
                        new Vector3(-inset, inset, offset), // Top left
                        new Vector3(inset, inset, offset), // Top right
                        new Vector3(-inset, -inset, offset), // Bottom left
                        new Vector3(inset, -inset, offset), // Bottom right
                    
                        // Back face
                        new Vector3(-inset, inset, -offset), // Top left
                        new Vector3(inset, inset, -offset), // Top right
                        new Vector3(-inset, -inset, -offset), // Bottom left
                        new Vector3(inset, -inset, -offset), // Bottom right

                        // Left face
                        new Vector3(-offset, inset, inset), // Top left
                        new Vector3(-offset, inset, -inset), // Top right
                        new Vector3(-offset, -inset, inset), // Bottom left
                        new Vector3(-offset, -inset, -inset), // Bottom right

                        // Right face
                        new Vector3(offset, inset, inset), // Top left
                        new Vector3(offset, inset, -inset), // Top right
                        new Vector3(offset, -inset, inset), // Bottom left
                        new Vector3(offset, -inset, -inset), // Bottom right

                        // Top face
                        new Vector3(-inset, offset, inset), // Top left
                        new Vector3(-inset, offset, -inset), // Top right
                        new Vector3(inset, offset, inset), // Bottom left
                        new Vector3(inset, offset, -inset), // Bottom right

                        // Bottom face
                        new Vector3(-inset, -offset, inset), // Top left
                        new Vector3(-inset, -offset, -inset), // Top right
                        new Vector3(inset, -offset, inset), // Bottom left
                        new Vector3(inset, -offset, -inset), // Bottom right
                };

                for (int j = 0; j < srcRegularSamplePoints.Length; j++) {
                    srcRegularSamplePoints[j] += new Vector3(0.5f, 0.5f, 0.5f);
                }


                srcUvs = new Vector2[]
                {
                        // Front face
                        new Vector2(1, 1), // Top left
                        new Vector2(0, 1), // Top right
                        new Vector2(1, 0), // Bottom left
                        new Vector2(0, 0), // Bottom right

                        // Back face
                        new Vector2(0, 1), // Top left
                        new Vector2(1, 1), // Top right
                        new Vector2(0, 0), // Bottom left
                        new Vector2(1, 0), // Bottom right

                        // Left face
                        new Vector2(0, 1), // Top left
                        new Vector2(1, 1), // Top right
                        new Vector2(0, 0), // Bottom left
                        new Vector2(1, 0), // Bottom right

                        // Right face
                        new Vector2(1, 1), // Top left
                        new Vector2(0, 1), // Top right
                        new Vector2(1, 0), // Bottom left
                        new Vector2(0, 0), // Bottom right

                        // Top face
                        new Vector2(1, 1), // Top left
                        new Vector2(0, 1), // Top right
                        new Vector2(1, 0), // Bottom left
                        new Vector2(0, 0), // Bottom right

                        // Bottom face
                        new Vector2(0, 1), // Top left
                        new Vector2(1, 1), // Top right
                        new Vector2(0, 0), // Bottom left
                        new Vector2(1, 0), // Bottom right
                };

                srcFaces = new int[][]
                {
                        new int[]
                        {
                            //Front
                            0, 2, 1, // First triangle
                            1, 2, 3, // Second triangle
                        },
                        new int[]
                        {
                            // Back face
                            0, 1, 2, // First triangle
                            1, 3, 2, // Second triangle
                        },
                        new int[]
                        {
                            // Left face
                             0, 1, 2, // First triangle
                            1, 3, 2, // Second triangle
                        },
                        new int[]
                        {
                            // Right face
                            0, 2, 1, // First triangle
                            1, 2, 3, // Second triangle
                        },
                        new int[]
                        {
                            // Top face
                             0, 2, 1, // First triangle
                            1, 2, 3, // Second triangle
                        },
                        new int[]
                        {
                            // Bottom face
                            0, 1, 2, // First triangle
                            1, 3, 2, // Second triangle
                    
                        }
                };
                altSrcFaces = new int[][]
                {
                        new int[]
                        {
                            //Front
                            0, 3, 1, // First triangle
                            0, 2, 3, // Second triangle
                        },
                        new int[]
                        {
                            // Back face
                            0, 3, 2, // First triangle
                            0, 1, 3, // Second triangle
                        },
                        new int[]
                        {
                            // Left face
                            0, 3, 2, // First triangle
                            0, 1, 3, // Second triangle
                        },
                        new int[]
                        {
                            // Right face
                            0, 3, 1, // First triangle
                            0, 2, 3, // Second triangle
                        },
                        new int[]
                        {
                            // Top face
                           0, 3, 1, // First triangle
                            0, 2, 3, // Second triangle
                        },
                        new int[]
                        {
                            // Bottom face
                            0, 3, 2, // First triangle
                            0, 1, 3, // Second triangle
                    
                        }
                };


                faceChecks = new Vector3Int[]
                {
                        new Vector3Int(0, 0, 1), // Front
                        new Vector3Int(0, 0, -1), // Back
                        new Vector3Int(-1, 0, 0), // Left
                        new Vector3Int(1, 0, 0), // Right
                        new Vector3Int(0, 1, 0), // Up
                        new Vector3Int(0, -1, 0), // Down
                };


                srcNormals = new Vector3[]
                {
                        new Vector3(0, 0, 1), // Front
                        new Vector3(0, 0, -1), // Back
                        new Vector3(-1, 0, 0), // Left
                        new Vector3(1, 0, 0), // Right
                        new Vector3(0, 1, 0), // Up
                        new Vector3(0, -1, 0), // Down
                };


                occlusionSamples = new Vector3Int[][]
                {
                        //Front
                        new Vector3Int[]
                        {
                            //top left
                            new Vector3Int( 0, 1, 1),
                            new Vector3Int(-1, 0, 1),
                            new Vector3Int(-1, 1, 1), //diag
                            //top right
                            new Vector3Int( 0, 1, 1),
                            new Vector3Int( 1, 0, 1),
                            new Vector3Int( 1, 1, 1), //diag
                            //bottom left
                            new Vector3Int( 0,-1, 1),
                            new Vector3Int(-1, 0, 1),
                            new Vector3Int(-1, -1, 1), //diag
                            //bottom right
                            new Vector3Int( 0,-1, 1),
                            new Vector3Int( 1, 0, 1),
                            new Vector3Int( 1,-1, 1), //diag
                        },
                        //back
                        new Vector3Int[]
                        {
                            //top left
                            new Vector3Int( 0, 1, -1),
                            new Vector3Int(-1, 0, -1),
                            new Vector3Int(-1, 1, -1), //diag
                            //top right
                            new Vector3Int( 0, 1, -1),
                            new Vector3Int( 1, 0, -1),
                            new Vector3Int( 1, 1, -1), //diag
                            //bottom left
                            new Vector3Int( 0,-1, -1),
                            new Vector3Int(-1, 0, -1),
                            new Vector3Int(-1,-1, -1), //diag
                            //bottom right
                            new Vector3Int( 0,-1, -1),
                            new Vector3Int( 1, 0, -1),
                            new Vector3Int( 1,-1, -1), //diag
                        },
                        //left
                        new Vector3Int[]
                        {
                            //top left
                            new Vector3Int(-1, 1, 0),
                            new Vector3Int(-1, 0, 1),
                            new Vector3Int(-1, 1, 1),//diag
                            //top right
                            new Vector3Int(-1, 1, 0),
                            new Vector3Int(-1, 0, -1),
                            new Vector3Int(-1, 1, -1),//diag
                            //bottom left
                            new Vector3Int(-1,-1, 0),
                            new Vector3Int(-1, 0, 1),
                            new Vector3Int(-1, -1, 1),//diag
                            //bottom right
                            new Vector3Int(-1,-1, 0),
                            new Vector3Int(-1, 0, -1),
                            new Vector3Int(-1, -1, -1),//diag
                        },

                          //right
                        new Vector3Int[]
                        {
                            //top left
                            new Vector3Int(1, 1, 0),
                            new Vector3Int(1, 0, 1),
                            new Vector3Int(1, 1, 1),//diag
                            //top right
                            new Vector3Int(1, 1, 0),
                            new Vector3Int(1, 0, -1),
                            new Vector3Int(1, 1, -1),//diag
                            //bottom left
                            new Vector3Int(1,-1, 0),
                            new Vector3Int(1, 0, 1),
                            new Vector3Int(1, -1, 1),//diag
                            //bottom right
                            new Vector3Int(1,-1, 0),
                            new Vector3Int(1, 0, -1),
                            new Vector3Int(1, -1, -1),//diag
                        },
                    
                        //Top
                        new Vector3Int[]
                        {
                            //top left
                            new Vector3Int(0, 1, 1),
                            new Vector3Int(-1, 1, 0),
                            new Vector3Int(-1, 1, 1),//diag
                            //top right
                            new Vector3Int(0, 1, -1),
                            new Vector3Int(-1, 1, 0),
                            new Vector3Int(-1, 1, -1),//diag
                            //bot left
                            new Vector3Int(0, 1, 1),
                            new Vector3Int(1, 1, 0),
                            new Vector3Int(1, 1, 1),//diag
                            //bot right
                            new Vector3Int(0, 1, -1),
                            new Vector3Int(1, 1, 0),
                            new Vector3Int(1, 1, -1),//diag
                      
                          },                  
                        //bot
                        new Vector3Int[]
                        {
                            //top left
                            new Vector3Int(0, -1, 1),
                            new Vector3Int(-1, -1, 0),
                            new Vector3Int(-1, -1, 1),//diag
                            //top right
                            new Vector3Int(0, -1, -1),
                            new Vector3Int(-1, -1, 0),
                            new Vector3Int(-1, -1, -1),//diag
                            //bot left
                            new Vector3Int(0, -1, 1),
                            new Vector3Int(1, -1, 0),
                            new Vector3Int(1, -1, 1),//diag
                            //bot right
                            new Vector3Int(0, -1, -1),
                            new Vector3Int(1, -1, 0),
                            new Vector3Int(1, -1, -1),//diag

                          },
                };
            }
        }

        public MeshProcessor(Chunk chunk) {
            Profiler.BeginSample("MeshProcessor Constructor");
            MeshProcessor.InitVertexData();
            startMeshProcessingTime = DateTime.Now;

            this.chunk = chunk;
             
            //copy the voxels over
            Profiler.BeginSample("CopyVoxels");

            //The voxels we care about are inset by 1 here, and expanded out by +3 on the positive axies
            //This is because we need to know the voxels around the edges of the chunk and expanded a bit for tiles
            //and neighbours

            //Debug copy
#pragma warning disable CS0162
            if (false) {
                for (int x = 0; x < paddedChunkSize; x++) {
                    for (int y = 0; y < paddedChunkSize; y++) {
                        for (int z = 0; z < paddedChunkSize; z++) {
                            int index = x + y * paddedChunkSize + z * paddedChunkSize * paddedChunkSize;
                            readOnlyVoxel[index] = chunk.world.ReadVoxelAtInternal(new Vector3Int(chunk.bottomLeftInt.x + x - 1, chunk.bottomLeftInt.y + y - 1, chunk.bottomLeftInt.z + z - 1));
                        }
                    }
                }
            }
            else {

                //Main block
                for (int x = 0; x < chunkSize; x++) {
                    for (int y = 0; y < chunkSize; y++) {
                        for (int z = 0; z < chunkSize; z++) {
                            int index = (x + 1) + (y + 1) * paddedChunkSize + (z + 1) * paddedChunkSize * paddedChunkSize;
                            readOnlyVoxel[index] = chunk.GetLocalVoxelAt(x, y, z);
                        }
                    }
                }

                //xPlane
                for (int y = 0; y < paddedChunkSize; y++) {
                    for (int z = 0; z < paddedChunkSize; z++) {
                        int x0 = 0;
                        int x1 = paddedChunkSize - 1;
                        readOnlyVoxel[x0 + y * paddedChunkSize + z * paddedChunkSize * paddedChunkSize] = chunk.world.ReadVoxelAtInternal(new Vector3Int(chunk.bottomLeftInt.x + x0 - 1, chunk.bottomLeftInt.y + y - 1, chunk.bottomLeftInt.z + z - 1));
                        readOnlyVoxel[x1 + y * paddedChunkSize + z * paddedChunkSize * paddedChunkSize] = chunk.world.ReadVoxelAtInternal(new Vector3Int(chunk.bottomLeftInt.x + x1 - 1, chunk.bottomLeftInt.y + y - 1, chunk.bottomLeftInt.z + z - 1));


                    }
                }

                //yPlane, skipping the voxels already filled by x plane
                for (int x = 1; x < paddedChunkSize - 1; x++) {
                    for (int z = 0; z < paddedChunkSize; z++) {
                        int y0 = 0;
                        int y1 = paddedChunkSize - 1;
                        readOnlyVoxel[x + y0 * paddedChunkSize + z * paddedChunkSize * paddedChunkSize] = chunk.world.ReadVoxelAtInternal(new Vector3Int(chunk.bottomLeftInt.x + x - 1, chunk.bottomLeftInt.y + y0 - 1, chunk.bottomLeftInt.z + z - 1));
                        readOnlyVoxel[x + y1 * paddedChunkSize + z * paddedChunkSize * paddedChunkSize] = chunk.world.ReadVoxelAtInternal(new Vector3Int(chunk.bottomLeftInt.x + x - 1, chunk.bottomLeftInt.y + y1 - 1, chunk.bottomLeftInt.z + z - 1));
                    }
                }

                //Zplane, skipping the voxels already filled by x plane and y plane
                for (int x = 1; x < paddedChunkSize - 1; x++) {
                    for (int y = 1; y < paddedChunkSize - 1; y++) {
                        int z0 = 0;
                        int z1 = paddedChunkSize - 1;
                        readOnlyVoxel[x + y * paddedChunkSize + z0 * paddedChunkSize * paddedChunkSize] = chunk.world.ReadVoxelAtInternal(new Vector3Int(chunk.bottomLeftInt.x + x - 1, chunk.bottomLeftInt.y + y - 1, chunk.bottomLeftInt.z + z0 - 1));
                        readOnlyVoxel[x + y * paddedChunkSize + z1 * paddedChunkSize * paddedChunkSize] = chunk.world.ReadVoxelAtInternal(new Vector3Int(chunk.bottomLeftInt.x + x - 1, chunk.bottomLeftInt.y + y - 1, chunk.bottomLeftInt.z + z1 - 1));
                    }
                }

#pragma warning restore CS0162

                //Copy readOnlyVoxel to processedVoxelMask
                for (int i = 0; i < processedVoxelMask.Length; i++) {
                    processedVoxelMask[i] = readOnlyVoxel[i];
                }


                key = chunk.GetKey();
                Profiler.EndSample();

#pragma warning disable CS0162
                Profiler.BeginSample("LaunchThread");
                //Run
                if (VoxelWorld.runThreaded) {
                    ThreadPool.QueueUserWorkItem(ThreadedUpdateFullMeshWrapper, chunk.world);
                }
                else {
                    ThreadedUpdateFullMesh(chunk.world);
                }
                Profiler.EndSample();
#pragma warning restore CS0162
            }
            Profiler.EndSample();
        }

        private static void EnsureCapacity(TemporaryMeshData target, int requiredSize) {
            if (requiredSize > target.vertices.Length) {
                // Double the size
                Array.Resize(ref target.vertices, requiredSize * 2);
                Array.Resize(ref target.colors, requiredSize * 2);
                Array.Resize(ref target.normals, requiredSize * 2);
                Array.Resize(ref target.uvs, requiredSize * 2);
                //Debug.Log("Resize! " + (requiredSize * 2));
            }
        }

        private static void EmitMesh(VoxelBlocks.BlockDefinition block, VoxelMeshCopy mesh, TemporaryMeshData target, VoxelWorld world, Vector3 origin, bool light, int rot = 0) {
            if (mesh == null) {
                return;
            }
            if (mesh.srcVertices == null) {
                return;
            }

            string matName = block.meshMaterialName;

            foreach (VoxelMeshCopy.Surface surface in mesh.surfaces) {
                SubMesh targetSubMesh;
                if (matName == "atlas") {
                    target.subMeshes.TryGetValue(matName, out SubMesh subMesh);
                    if (subMesh == null) {
                        subMesh = new SubMesh(world.blocks.materials[matName]);
                        target.subMeshes[matName] = subMesh;
                    }
                    targetSubMesh = subMesh;
                }
                else {
                    matName = surface.meshMaterialName;
                    target.subMeshes.TryGetValue(matName, out SubMesh subMesh);
                    if (subMesh == null) {
                        subMesh = new SubMesh(surface.meshMaterial);
                        target.subMeshes[matName] = subMesh;
                    }
                    targetSubMesh = subMesh;
                }
                // Add triangles
                for (int i = 0; i < surface.triangles.Length; i++) {
                    targetSubMesh.triangles.Add(surface.triangles[i] + target.verticesCount);
                }
            }

            //Add mesh data
            mesh.rotation.TryGetValue(rot, out VoxelMeshCopy.PrecalculatedRotation sourceRotation);

            int count = sourceRotation.vertices.Length;
            Vector3 offset = origin + new Vector3(0.5f, 0.5f, 0.5f);

            // Ensure capacity
            EnsureCapacity(target, target.verticesCount + count);

            // Prepare transformed vertices
            for (int i = 0; i < count; i++) {
                Vector3 transformedPosition = sourceRotation.vertices[i] + offset;
                //write the transformed position (well, translated)
                target.vertices[target.verticesCount++] = transformedPosition;

            }


            // Copy other arrays directly
            Array.Copy(mesh.srcUvs, 0, target.uvs, target.uvsCount, count);
            target.uvsCount += count;

            Array.Copy(sourceRotation.normals, 0, target.normals, target.normalsCount, count);
            target.normalsCount += count;

            if (mesh.srcColors != null && mesh.srcColors.Length > 0) {
                Array.Copy(mesh.srcColors, 0, target.colors, target.colorsCount, count);
                target.colorsCount += count;
            }
            else {
                //fill with white
                for (int i = 0; i < count; i++) {
                    target.colors[target.colorsCount++] = Color.white;

                }
            }
        }

        public enum FitResult {
            NO_FIT,
            FIT
        }

        private FitResult FitBigTile(int lx, int ly, int lz, int sizeX, int sizeY, int sizeZ, int match) {
            if (lx + sizeX > chunkSize + 1 || ly + sizeY > chunkSize + 1 || lz + sizeZ > chunkSize + 1) {
                return FitResult.NO_FIT;
            }

            //Because we overlap into other chunks, we can assume the other chunk will fill our lowest numbers
            for (int dx = lx; dx < lx + sizeX; dx++) {
                for (int dy = ly; dy < ly + sizeY; dy++) {
                    for (int dz = lz; dz < lz + sizeZ; dz++) {
                        int localVoxelKey = (dx + dy * paddedChunkSize + dz * paddedChunkSize * paddedChunkSize);
                        VoxelData vox = processedVoxelMask[localVoxelKey];

                        BlockId blockIndex = VoxelWorld.VoxelDataToBlockId(vox);
                        if (blockIndex != match) {
                            return FitResult.NO_FIT;
                        }
                    }
                }
            }
            //Clear all the tiles
            for (int dx = lx; dx < lx + sizeX; dx++) {
                for (int dy = ly; dy < ly + sizeY; dy++) {
                    for (int dz = lz; dz < lz + sizeZ; dz++) {
                        int localVoxelKey = (dx + dy * paddedChunkSize + dz * paddedChunkSize * paddedChunkSize);
                        processedVoxelMask[localVoxelKey] = 0;
                    }
                }
            }

            return FitResult.FIT;
        }

        private void InitDetailMeshes() {
            if (hasDetailMeshes == false) {
                hasDetailMeshes = true;

                if (detailMeshData == null) {
                    //create the detail meshes if needed
                    detailMeshData = new TemporaryMeshData[3];
                    detailMeshData[0] = new TemporaryMeshData();
                    detailMeshData[1] = new TemporaryMeshData();
                    detailMeshData[2] = new TemporaryMeshData();
                }

                for (int i = 0; i < 3; i++) {
                    detailMeshData[i].verticesCount = 0;
                    detailMeshData[i].colorsCount = 0;
                    detailMeshData[i].normalsCount = 0;
                    detailMeshData[i].uvsCount = 0;

                }

            }
        }

        private bool SeeIfVoxelVisible(int voxelKey) {
            if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[voxelKey + 1]) == false) return true;
            if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[voxelKey - 1]) == false) return true;
            if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[voxelKey + paddedChunkSize]) == false) return true;
            if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[voxelKey - paddedChunkSize]) == false) return true;
            if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[voxelKey + (paddedChunkSize * paddedChunkSize)]) == false) return true;
            if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[voxelKey - (paddedChunkSize * paddedChunkSize)]) == false) return true;

            return false;
        }

        private bool SeeIfLargeBlockVisible(int voxelKey, int sizeX, int sizeY, int sizeZ) {
            //Check bot surface
            for (int xPlane = 0; xPlane < sizeX; xPlane++) {
                for (int zPlane = 0; zPlane < sizeZ; zPlane++) {
                    int voxelKey2 = voxelKey + (xPlane) + (-1 * paddedChunkSize) + (zPlane * paddedChunkSize * paddedChunkSize);
                    if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[voxelKey2]) == false) return true;
                }
            }
            //check top Surface
            for (int xPlane = 0; xPlane < sizeX; xPlane++) {
                for (int zPlane = 0; zPlane < sizeZ; zPlane++) {
                    int voxelKey2 = voxelKey + (xPlane) + (sizeY * paddedChunkSize) + (zPlane * paddedChunkSize * paddedChunkSize);
                    if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[voxelKey2]) == false) return true;
                }
            }
            //check left Surface
            for (int yPlane = 0; yPlane < sizeY; yPlane++) {
                for (int zPlane = 0; zPlane < sizeZ; zPlane++) {
                    int voxelKey2 = voxelKey + (-1) + (yPlane * paddedChunkSize) + (zPlane * paddedChunkSize * paddedChunkSize);
                    if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[voxelKey2]) == false) return true;
                }
            }
            //Check right surface
            for (int yPlane = 0; yPlane < sizeY; yPlane++) {
                for (int zPlane = 0; zPlane < sizeZ; zPlane++) {
                    int voxelKey2 = voxelKey + (sizeX) + (yPlane * paddedChunkSize) + (zPlane * paddedChunkSize * paddedChunkSize);
                    if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[voxelKey2]) == false) return true;
                }
            }
            //Check front surface
            for (int xPlane = 0; xPlane < sizeX; xPlane++) {
                for (int yPlane = 0; yPlane < sizeY; yPlane++) {
                    int voxelKey2 = voxelKey + (xPlane) + (yPlane * paddedChunkSize) + (-1 * paddedChunkSize * paddedChunkSize);
                    if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[voxelKey2]) == false) return true;
                }
            }
            //Check back surface
            for (int xPlane = 0; xPlane < sizeX; xPlane++) {
                for (int yPlane = 0; yPlane < sizeY; yPlane++) {
                    int voxelKey2 = voxelKey + (xPlane) + (yPlane * paddedChunkSize) + (sizeZ * paddedChunkSize * paddedChunkSize);
                    if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[voxelKey2]) == false) return true;
                }
            }


            return false;
        }

        private void ThreadedUpdateFullMeshWrapper(System.Object worldObj) {
            try {
                ThreadedUpdateFullMesh(worldObj);
            }
            catch (System.Exception e) {
                Debug.LogError("Error in threaded update full mesh: " + e.Message);
            }
        }

        private void ThreadedUpdateFullMesh(System.Object worldObj) {
            VoxelWorld world = (VoxelWorld)worldObj;

            temporaryMeshData = new TemporaryMeshData();
            temporaryMeshData.verticesCount = 0;
            temporaryMeshData.colorsCount = 0;
            temporaryMeshData.normalsCount = 0;
            temporaryMeshData.uvsCount = 0;

            bool found = world.blocks.materials.TryGetValue("atlas", out Material mat);
            if (found == false) {
                return;
            }
            temporaryMeshData.subMeshes["atlas"] = new SubMesh(mat);

            Vector3Int worldKey = (key * chunkSize);
            int skipCount = 0;
            const int inset = 1;

            for (int x = 0; x < VoxelWorld.chunkSize; x++) {
                for (int y = 0; y < VoxelWorld.chunkSize; y++) {
                    for (int z = 0; z < VoxelWorld.chunkSize; z++) {

                        Vector3Int localVector = new Vector3Int(x, y, z);
                        Vector3Int localVoxel = new Vector3Int(x + inset, y + inset, z + inset); //Account for padding
                        Vector3Int origin = localVector + worldKey;
                        int localVoxelKey = ((localVoxel.x) + (localVoxel.y) * paddedChunkSize + (localVoxel.z) * paddedChunkSize * paddedChunkSize);
                        VoxelData vox = readOnlyVoxel[localVoxelKey];

                        BlockId blockIndex = VoxelWorld.VoxelDataToBlockId(vox);
                        if (blockIndex == 0) //Air!
                        {
                            //no visual
                            continue;
                        }

                        VoxelBlocks.BlockDefinition block = world.blocks.GetBlock(blockIndex);

                        if (block == null) {
                            continue;
                        }

                        // Prefab blocks use "fake" blocks that are just invisible (like air!)
                        if (block.prefab) {
                            // no visual
                            continue;
                        }

                        //Is this block contextual?
                        if (block.contextStyle == VoxelBlocks.ContextStyle.ContextBlocks) {
                            if (ContextPlaceBlock(block, localVoxelKey, readOnlyVoxel, temporaryMeshData, world, origin) == true) {
                                continue;
                            }
                        }

                        if (block.contextStyle == VoxelBlocks.ContextStyle.QuarterTiles) {
                            if (QuarterBlocksPlaceBlock(block, localVoxelKey, readOnlyVoxel, temporaryMeshData, world, origin) == true) {
                                continue;
                            }
                        }


                        //Is this block a tile 
                        if (block.contextStyle == VoxelBlocks.ContextStyle.GreedyMeshingTiles && doComplexMeshes == true) {
                            InitDetailMeshes();


                            foreach (int index in block.meshTileProcessingOrder) {
                                Vector3Int size = VoxelBlocks.meshTileSizes[index];

                                if (FitBigTile(x + inset, y + inset, z + inset, size.x, size.y, size.z, blockIndex) == FitResult.FIT) {
                                    //See if the edges of all these tiles are visible
                                    bool visible = SeeIfLargeBlockVisible(localVoxelKey, size.x, size.y, size.z);
                                    if (visible) {
                                        int rotation = Math.Abs(VoxelWorld.HashCoordinates((int)origin.x, (int)origin.y, (int)origin.z) % 4);

                                        VoxelBlocks.LodSet set = block.meshTiles[index];

                                        EmitMesh(block, set.lod0, detailMeshData[0], world, origin + VoxelBlocks.meshTileOffsets[index], true, rotation);

                                        EmitMesh(block, set.lod1, detailMeshData[1], world, origin + VoxelBlocks.meshTileOffsets[index], true, rotation);

                                        EmitMesh(block, set.lod2, detailMeshData[2], world, origin + VoxelBlocks.meshTileOffsets[index], true, rotation);
                                    }
                                    break;
                                }

                            }
                            //If its still filled, write a 1x1
                            if (processedVoxelMask[localVoxelKey] > 0) {
                                processedVoxelMask[localVoxelKey] = 0;

                                if (SeeIfVoxelVisible(localVoxelKey) == true) {
                                    //Check around this block for at least one transparent bit
                                    int rotation = Math.Abs(VoxelWorld.HashCoordinates((int)origin.x, (int)origin.y, (int)origin.z) % 4);
                                    VoxelBlocks.LodSet set = block.meshTiles[0];

                                    EmitMesh(block, set.lod0, detailMeshData[0], world, origin, true, rotation);
                                    EmitMesh(block, set.lod1, detailMeshData[1], world, origin, true, rotation);
                                    EmitMesh(block, set.lod2, detailMeshData[2], world, origin, true, rotation);
                                }
                                else {
                                    skipCount++;
                                }

                            }

                            continue;
                        }


                        //where we put this mesh is variable!
                        if (block.mesh != null) {
                            //Grass etc                           
                            if (block.detail == true) {
                                //Init the detail meshes now
                                InitDetailMeshes();

                                if (block.mesh != null) {
                                    EmitMesh(block, block.mesh, detailMeshData[0], world, origin, true);
                                }
                                if (block.meshLod != null) {
                                    EmitMesh(block, block.mesh, detailMeshData[1], world, origin, true);
                                }
                            }
                            else {
                                //same mesh that the voxels use (think stairs etc)
                                EmitMesh(block, block.mesh, temporaryMeshData, world, origin, true);
                            }
                            //No code past here
                            continue;
                        }

                        if (temporaryMeshData.verticesCount + (4 * 6) >= temporaryMeshData.vertices.Length) {
                            EnsureCapacity(temporaryMeshData, temporaryMeshData.verticesCount + (4 * 6));
                        }


                        //Add regular cube Faces
                        for (int faceIndex = 0; faceIndex < 6; faceIndex++) {
                            //Vector3Int check = origin + faceChecks[faceIndex];
                            //VoxelData other = world.ReadVoxelAtInternal(check);

                            Vector3Int check = faceChecks[faceIndex];

                            //Todo: faceChecks could just be offsets and save some math here
                            int voxelKeyCheck = (localVoxel.x + check.x) + (localVoxel.y + check.y) * paddedChunkSize + (localVoxel.z + check.z) * paddedChunkSize * paddedChunkSize;
                            VoxelData other = readOnlyVoxel[voxelKeyCheck];

                            BlockId otherBlockIndex = VoxelWorld.VoxelDataToBlockId(other);

                            bool solid = VoxelWorld.VoxelIsSolid(other);
                            if (otherBlockIndex == 59) {
                                solid = false;
                            }

                            //Figure out if we're meant to generate a face. 
                            //If we're facing something nonsolid that isn't the same as us (eg: glass faces dont build internally)
                            if (solid == false && otherBlockIndex != blockIndex) {
                                Rect uvRect = block.GetUvsForFace(faceIndex);

                                string matName = block.materials[faceIndex];

                                temporaryMeshData.subMeshes.TryGetValue(matName, out SubMesh subMesh);
                                if (subMesh == null) {
                                    subMesh = new SubMesh(world.blocks.materials[matName]);
                                    temporaryMeshData.subMeshes[matName] = subMesh;
                                }

                                int faceAxis = faceAxisForFace[faceIndex];
                                Vector3 normal = normalForFace[faceIndex];

                                int vertexCount = temporaryMeshData.verticesCount;
                                for (int j = 0; j < 4; j++) {
                                    temporaryMeshData.vertices[temporaryMeshData.verticesCount++] = srcVertices[(faceIndex * 4) + j] + origin;
                                    temporaryMeshData.normals[temporaryMeshData.normalsCount++] = srcNormals[faceIndex];
                                }

                                //UV gen
                                for (int j = 0; j < 4; j++) {
                                    Vector2 uv = srcUvs[(faceIndex * 4) + j];

                                    uv.x = uv.x * uvRect.width + uvRect.xMin;
                                    uv.y = uv.y * uvRect.height + uvRect.yMin;

                                    temporaryMeshData.uvs[temporaryMeshData.uvsCount++] = uv;
                                }


                                //Do occlusions
                                if (block.doOcclusion == true) //If this mesh wants occlusions, calculate the occlusions for this face
                                {
                                    for (int j = 0; j < 4; j++) {

                                        Vector3 samplePoint;
                                        byte g = 0;

                                        if (IsAnyVoxelOccluded(faceIndex, j, localVoxel) == false) {
                                            g = 255; //No ambient occlusion
                                            //we're not in a corner
                                            samplePoint = srcRegularSamplePoints[(faceIndex * 4) + j] + origin;
                                        }
                                        else {
                                            //we're in a corner!
                                            shade[j] = true;
                                            g = 0; //ambient occluded
                                            samplePoint = srcCornerSamplePoints[(faceIndex * 4) + j] + origin;
                                        }

                                        Color32 col = Color.white;// DoDirectLighting(world, samplePoint, faceAxis, normal, lightingCache);

                                        col.g = g;
                                        //shade[j] = col.r < 255;

                                        temporaryMeshData.colors[temporaryMeshData.colorsCount++] = col;
                                    }



                                    //See if opposite corners are shaded      0--1        0--1
                                    //see if single 1 corner is shaded     alt|\ |    norm| /|
                                    //see if single 2 corner is shaded        2--3        2--3
                                    if ((shade[0] && shade[3]) || (shade[1] && !shade[0] && !shade[2] && !shade[3]) || (shade[2] && !shade[0] && !shade[1] && !shade[3])) {
                                        //Turn the triangulation
                                        for (int j = 0; j < altSrcFaces[faceIndex].Length; j++) {
                                            subMesh.triangles.Add(altSrcFaces[faceIndex][j] + vertexCount);
                                        }
                                    }
                                    else {
                                        for (int j = 0; j < srcFaces[faceIndex].Length; j++) {
                                            subMesh.triangles.Add(srcFaces[faceIndex][j] + vertexCount);
                                        }
                                    }
                                }
                                else {
                                    //Unused
                                    /*
                                    for (int j = 0; j < 4; j++)
                                    {
                                        Vector3 worldPoint = srcVertices[(faceIndex * 4) + j] + origin;
                                        float occlusion = world.CalculateSunShadowAtPoint(worldPoint + (normal * 0.01f), faceAxis, normal);
                                        shade[j] = occlusion > 0;

                                        colors.Add(new Color32((byte)(occlusion * 255.0f), 0, 0, 0));
                                    }

                                    //See if opposite corners are shaded      0--1        0--1
                                    //see if single 1 corner is shaded     alt|\ |    norm| /|
                                    //see if single 2 corner is shaded        2--3        2--3
                                    if ((shade[0] && shade[3]) || (shade[1] && !shade[0] && !shade[2] && !shade[3]) || (shade[2] && !shade[0] && !shade[1] && !shade[3]))
                                    {
                                        //Turn the triangulation

                                        for (int j = 0; j < altSrcFaces[faceIndex].Length; j++)
                                        {
                                            //triangles[trianglesWritePos++] = altSrcFaces[i][j] + vertexCount;
                                            subMesh.triangles.Add(altSrcFaces[faceIndex][j] + vertexCount);
                                        }
                                    }
                                    else
                                    {

                                        for (int j = 0; j < srcFaces[faceIndex].Length; j++)
                                        {
                                            //triangles[trianglesWritePos++] = srcFaces[i][j] + vertexCount;
                                            subMesh.triangles.Add(srcFaces[faceIndex][j] + vertexCount);
                                        }
                                    }
                                    */
                                }

                            }
                        }
                    }
                }
            }

            if (skipCount > 0) {
                //Debug.Log("Skipped " + skipCount + " blocks");
            }

            lastMeshUpdateDuration = (int)((DateTime.Now - startMeshProcessingTime).TotalMilliseconds);

            //All done
            finishedProcessing = true;

            //Flag this meshprocessor as having finished geometry 
            geometryReady = true;
        }


        private bool IsAnyVoxelOccluded(int faceIndex, int j, Vector3Int localVoxel) {
            Vector3Int i0 = occlusionSamples[faceIndex][j * 3 + 0];
            int index0 = (localVoxel.x + i0.x) + (localVoxel.y + i0.y) * paddedChunkSize + (localVoxel.z + i0.z) * paddedChunkSize * paddedChunkSize;
            if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[index0])) {
                return true;
            }

            Vector3Int i1 = occlusionSamples[faceIndex][j * 3 + 1];
            int index1 = (localVoxel.x + i1.x) + (localVoxel.y + i1.y) * paddedChunkSize + (localVoxel.z + i1.z) * paddedChunkSize * paddedChunkSize;
            if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[index1])) {
                return true;
            }

            Vector3Int i2 = occlusionSamples[faceIndex][j * 3 + 2];
            int index2 = (localVoxel.x + i2.x) + (localVoxel.y + i2.y) * paddedChunkSize + (localVoxel.z + i2.z) * paddedChunkSize * paddedChunkSize;
            if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[index2])) {
                return true;
            }

            return false;
        }
        public bool GetFinishedProcessing() {
            return finishedProcessing;
        }

        private static void CreateUnityMeshFromTemporayMeshData(Mesh mesh, Renderer renderer, TemporaryMeshData tempMesh, VoxelWorld world, bool cloneMaterials) {
            Profiler.BeginSample("ConstructMesh");
            mesh.subMeshCount = tempMesh.subMeshes.Count;
            mesh.SetVertices(tempMesh.vertices, 0, tempMesh.verticesCount);
            mesh.SetUVs(0, tempMesh.uvs, 0, tempMesh.uvsCount);
            mesh.SetColors(tempMesh.colors, 0, tempMesh.colorsCount);
            mesh.SetNormals(tempMesh.normals, 0, tempMesh.normalsCount);

            int meshWrite = 0;
            foreach (SubMesh subMeshRec in tempMesh.subMeshes.Values) {
                mesh.SetTriangles(subMeshRec.triangles, meshWrite);
                meshWrite++;
            }

            Profiler.EndSample();
            //Profiler.BeginSample("RecalcTangents");
            //mesh.RecalculateTangents();
            //Profiler.EndSample();

            Profiler.BeginSample("MakeMaterials");
            Material[] mats = new Material[tempMesh.subMeshes.Count];
            int matWrite = 0;
            foreach (SubMesh subMeshRec in tempMesh.subMeshes.Values) {
                if (cloneMaterials == true) {
                    Material clonedMaterial = new Material(subMeshRec.srcMaterial);


                    mats[matWrite] = clonedMaterial;
                }
                else {
                    mats[matWrite] = subMeshRec.srcMaterial;
                }
                matWrite++;
            }
            Profiler.EndSample();
            Profiler.BeginSample("AssignMaterials");
            renderer.sharedMaterials = mats;
            Profiler.EndSample();
        }

        public void FinalizeMesh(GameObject obj, Mesh mesh, Renderer renderer, Mesh[] detailMeshes, Renderer[] detailRenderers, VoxelWorld world) {
            if (GetGeometryReady() == true) {

                //Updates both the geometry and baked lighting
                Profiler.BeginSample("FinalizeMeshMain");
                CreateUnityMeshFromTemporayMeshData(mesh, renderer, temporaryMeshData, world, false);
                Profiler.EndSample();

                if (detailMeshes != null) {
                    for (int i = 0; i < 3; i++) {
                        Profiler.BeginSample("FinalizeMeshDetail");
                        CreateUnityMeshFromTemporayMeshData(detailMeshes[i], detailRenderers[i], detailMeshData[i], world, false);
                        Profiler.EndSample();
                    }
                }

                obj.transform.localPosition = Vector3.zero;
                obj.transform.localScale = Vector3.one;
                obj.transform.localRotation = Quaternion.identity;

                geometryReady = false;
            }

        }

        /// <summary>
        /// Generate game object with a block mesh
        /// </summary>
        /// <param name="blockIndex"></param>
        /// <param name="world"></param>
        /// <param name="triplanerMode">0 = none, 1 = world, 2 = local</param>
        /// <param name="triplanarScale"></param>
        /// <returns></returns>
        public static GameObject ProduceSingleBlock(int blockIndex, VoxelWorld world, float triplanerMode = 2, float triplanarScale = 1) {
            MeshProcessor.InitVertexData();

            VoxelBlocks.BlockDefinition block = world.blocks.GetBlock((ushort)blockIndex);

            if (block == null || block.prefab == true || blockIndex == 0) //air
            {
                Debug.Log($"VoxelMeshProcessor could not get block at index {blockIndex}");
                return null;
            }

            GameObject obj = new GameObject();
            MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();

            //Allocate some buffers to work with
            TemporaryMeshData meshData = new TemporaryMeshData();
            meshRenderer.sharedMaterial = world.blocks.materials["atlas"];

            Mesh theMesh = new Mesh();

            //Center around 0,0,0
            Vector3 origin = new Vector3(-0.5f, -0.5f, -0.5f);

            if (block.mesh != null) {
                EmitMesh(block, block.mesh, meshData, world, origin, false);
            }
            else {
                //Add regular cube Faces
                for (int faceIndex = 0; faceIndex < 6; faceIndex++) {
                    Rect uvRect = block.GetUvsForFace(faceIndex);
                    string matName = block.materials[faceIndex];

                    meshData.subMeshes.TryGetValue(matName, out SubMesh subMesh);
                    if (subMesh == null) {
                        subMesh = new SubMesh(world.blocks.materials[matName]);
                        meshData.subMeshes[matName] = subMesh;
                    }

                    int vertexCount = meshData.verticesCount;
                    for (int j = 0; j < 4; j++) {
                        //vertices.Add(srcVertices[(i * 4) + j] + origin);
                        meshData.vertices[meshData.verticesCount++] = srcVertices[(faceIndex * 4) + j] + origin;
                        meshData.normals[meshData.normalsCount++] = srcNormals[faceIndex];
                        //Vertex color
                        meshData.colors[meshData.colorsCount++] = Color.white;
                    }

                    //UV gen
                    for (int j = 0; j < 4; j++) {
                        Vector2 uv = srcUvs[(faceIndex * 4) + j];

                        uv.x = uv.x * uvRect.width + uvRect.xMin;
                        uv.y = uv.y * uvRect.height + uvRect.yMin;

                        meshData.uvs[meshData.uvsCount++] = uv;
                    }


                    //Faces
                    for (int j = 0; j < srcFaces[faceIndex].Length; j++) {
                        subMesh.triangles.Add(srcFaces[faceIndex][j] + vertexCount);
                    }
                }
            }
            CreateUnityMeshFromTemporayMeshData(theMesh, meshRenderer, meshData, world, true);

            foreach (Material mat in meshRenderer.sharedMaterials) {
                var existing = mat.GetFloat("_TriplanarScale");
                mat.SetFloat("_TriplanarScale", existing * triplanarScale);
            }

            meshFilter.sharedMesh = theMesh;
            return obj;
        }
        /*
                public Material GetBlockMaterial(int blockIndex, VoxelWorld world) {
                    var block = world.blocks.GetBlock((ushort)blockIndex);
                    Rect uvRect = block.GetUvsForFace(1);
                    var newMat = new Material(world.blocks.materials["atlas"]);
                    newMat.SetTextureScale("_MainTex", UVRect.size);
                    newMat.SetTextureOffset("_MainTex", UVRect.min);
                }*/

        private static bool ContextPlaceBlock(VoxelBlocks.BlockDefinition block, int localVoxelKey, VoxelData[] readOnlyVoxel, TemporaryMeshData temporaryMeshData, VoxelWorld world, Vector3 origin) {
            //get surrounding data
            VoxelData voxUp = readOnlyVoxel[localVoxelKey + paddedChunkSize];
            VoxelData voxDown = readOnlyVoxel[localVoxelKey - paddedChunkSize];
            VoxelData voxLeft = readOnlyVoxel[localVoxelKey - 1];
            VoxelData voxRight = readOnlyVoxel[localVoxelKey + 1];
            VoxelData voxForward = readOnlyVoxel[localVoxelKey + (paddedChunkSize * paddedChunkSize)];
            VoxelData voxBack = readOnlyVoxel[localVoxelKey - (paddedChunkSize * paddedChunkSize)];


            //Check for top is air
            if (VoxelWorld.VoxelIsSolid(voxUp) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxUp) &&
                VoxelWorld.VoxelIsSolid(voxDown) == true) {
                bool airLeft = (VoxelWorld.VoxelIsSolid(voxLeft) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxLeft));
                bool airRight = (VoxelWorld.VoxelIsSolid(voxRight) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxRight));
                bool airForward = (VoxelWorld.VoxelIsSolid(voxForward) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxForward));
                bool airBack = (VoxelWorld.VoxelIsSolid(voxBack) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxBack));

                //Are we a block with 4 surrounding air spaces? That is block C!
                if (airLeft && airRight && airForward && airBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.C], temporaryMeshData, world, origin, true);
                    return true;
                }

                //are we a block with 3 surrounding air spaces? That is block D!
                //Four combos
                if (airLeft && airRight && airForward && !airBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.D], temporaryMeshData, world, origin, true, 1);
                    return true;
                }
                if (airLeft && airRight && !airForward && airBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.D], temporaryMeshData, world, origin, true, 3);
                    return true;
                }
                if (airLeft && !airRight && airForward && airBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.D], temporaryMeshData, world, origin, true, 0);
                    return true;
                }
                if (!airLeft && airRight && airForward && airBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.D], temporaryMeshData, world, origin, true, 2);
                    return true;
                }

                //2 edge visible (a corner)
                if (airLeft && !airRight && airForward && !airBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.E], temporaryMeshData, world, origin, true, 0);
                    return true;
                }
                //2 edge visible (a corner)
                if (airLeft && !airRight && !airForward && airBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.E], temporaryMeshData, world, origin, true, 3);
                    return true;
                }

                //2 edge visible (a corner)
                if (!airLeft && airRight && airForward && !airBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.E], temporaryMeshData, world, origin, true, 1);
                    return true;
                }
                //2 edge visible (a corner)
                if (!airLeft && airRight && !airForward && airBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.E], temporaryMeshData, world, origin, true, 2);
                    return true;
                }

                //2 edger visible (a bridge)
                if (airLeft && airRight && !airForward && !airBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.F], temporaryMeshData, world, origin, true, 0);
                    return true;
                }
                //2 edger visible (a bridge)
                if (!airLeft && !airRight && airForward && airBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.F], temporaryMeshData, world, origin, true, 1);
                    return true;
                }

                //1 edge visible (t section)
                if (airLeft && !airRight && !airForward && !airBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.G], temporaryMeshData, world, origin, true, 0);
                    return true;
                }

                //1 edge visible (t section)
                if (!airLeft && airRight && !airForward && !airBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.G], temporaryMeshData, world, origin, true, 2);
                    return true;
                }

                // 1 edge visible(t section)
                if (!airLeft && !airRight && airForward && !airBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.G], temporaryMeshData, world, origin, true, 1);
                    return true;
                }

                // 1 edge visible(t section)
                if (!airLeft && !airRight && !airForward && airBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.G], temporaryMeshData, world, origin, true, 3);
                    return true;
                }


                //Flat tops (B)

                VoxelData voxLeftForward = readOnlyVoxel[localVoxelKey - 1 + (paddedChunkSize * paddedChunkSize)];
                VoxelData voxRightForward = readOnlyVoxel[localVoxelKey + 1 + (paddedChunkSize * paddedChunkSize)];
                VoxelData voxLeftBack = readOnlyVoxel[localVoxelKey - 1 - (paddedChunkSize * paddedChunkSize)];
                VoxelData voxRightBack = readOnlyVoxel[localVoxelKey + 1 - (paddedChunkSize * paddedChunkSize)];


                bool airLeftForward = (VoxelWorld.VoxelIsSolid(voxLeftForward) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxLeftForward));
                bool airRightForward = (VoxelWorld.VoxelIsSolid(voxRightForward) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxRightForward));
                bool airLeftBack = (VoxelWorld.VoxelIsSolid(voxLeftBack) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxLeftBack));
                bool airRightBack = (VoxelWorld.VoxelIsSolid(voxRightBack) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxRightBack));

                //Check for 1 air space
                if (airLeftForward && !airRightForward && !airLeftBack && !airRightBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.B1], temporaryMeshData, world, origin, true, 1);
                    return true;
                }
                if (!airLeftForward && airRightForward && !airLeftBack && !airRightBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.B1], temporaryMeshData, world, origin, true, 2);
                    return true;
                }
                if (!airLeftForward && !airRightForward && airLeftBack && !airRightBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.B1], temporaryMeshData, world, origin, true, 0);
                    return true;
                }
                if (!airLeftForward && !airRightForward && !airLeftBack && airRightBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.B1], temporaryMeshData, world, origin, true, 3);
                    return true;
                }

                //Check for 2 air space on the same side
                if (airLeftForward && airRightForward && !airLeftBack && !airRightBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.B2A], temporaryMeshData, world, origin, true, 1);
                    return true;
                }
                if (!airLeftForward && !airRightForward && airLeftBack && airRightBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.B2A], temporaryMeshData, world, origin, true, 3);
                    return true;
                }
                if (airLeftForward && !airRightForward && airLeftBack && !airRightBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.B2A], temporaryMeshData, world, origin, true, 0);
                    return true;
                }
                if (!airLeftForward && airRightForward && !airLeftBack && airRightBack) {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.B2A], temporaryMeshData, world, origin, true, 2);
                    return true;
                }


                //Assume we a flat top with no surrounding air spaces
                EmitMesh(block, block.meshContexts[(int)VoxelBlocks.ContextBlockTypes.B], temporaryMeshData, world, origin, true, 0);

                //Todo, this needs to check diagonals
                return true;
            }

            //fallback, if any face is visible, emit the whole mesh
            if ((VoxelWorld.VoxelIsSolid(voxDown) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxDown)) ||
                 (VoxelWorld.VoxelIsSolid(voxLeft) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxLeft)) ||
                 (VoxelWorld.VoxelIsSolid(voxRight) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxRight)) ||
                 (VoxelWorld.VoxelIsSolid(voxForward) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxForward)) ||
                 (VoxelWorld.VoxelIsSolid(voxBack) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxBack))) {
                EmitMesh(block, block.meshContexts[0], temporaryMeshData, world, origin, true, 0);
            }
            else {
                //Just empty air as this isnt visible
            }
            return true;
        }


    }
}