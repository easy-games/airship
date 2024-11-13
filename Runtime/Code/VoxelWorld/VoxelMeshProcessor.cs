
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using VoxelData = System.UInt16;
using BlockId = System.UInt16;
using Assets.Airship.VoxelRenderer;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using Random = System.Random;
using System.Runtime.CompilerServices;

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
        [HideFromTS] public static Dictionary<int, Material> materialIdToMaterial = new();

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
            materialIdToMaterial = new();
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
        Color32[] readOnlyColor = new Color32[paddedChunkSize * paddedChunkSize * paddedChunkSize];
        VoxelData[] processedVoxelMask = new VoxelData[paddedChunkSize * paddedChunkSize * paddedChunkSize];
        public Dictionary<ushort, float> readOnlyDamageMap = new();
        
        private const int capacity = 40000;
                
        class TemporaryMeshData {
            /// <summary>
            /// Map from GetInstanceId() of Material to submeshes
            /// </summary>
            public Dictionary<int, SubMesh> subMeshes = new();

            public Vector3[] vertices = new Vector3[capacity];
            public int verticesCount = 0;

            public Color32[] colors = new Color32[capacity];
            public int colorsCount = 0;

            public Vector3[] normals = new Vector3[capacity];
            public int normalsCount = 0;

            public Vector2[] uvs = new Vector2[capacity];
            public int uvsCount = 0;

            public Vector2[] damageUvs = new Vector2[capacity];
            public int damageUvsCount = 0;
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
            public int srcMaterialId;

            public SubMesh(int originalMaterialId) {
                //material = new Material(originalMaterial);
                srcMaterialId = originalMaterialId;
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

                for (int x = 0; x < paddedChunkSize; x++) {
                    for (int y = 0; y < paddedChunkSize; y++) {
                        for (int z = 0; z < paddedChunkSize; z++) {
                            int index = x + y * paddedChunkSize + z * paddedChunkSize * paddedChunkSize;
            
                            if (x == 0 || x == paddedChunkSize - 1 || 
                                y == 0 || y == paddedChunkSize - 1 || 
                                z == 0 || z == paddedChunkSize - 1) {
                                // Border voxel
                                var worldPos = new Vector3Int(chunk.bottomLeftInt.x + x - 1,
                                    chunk.bottomLeftInt.y + y - 1,
                                    chunk.bottomLeftInt.z + z - 1);
                                var vw = chunk.world;
                                readOnlyVoxel[index] = vw.ReadVoxelAtInternal(worldPos);
                                readOnlyColor[index] = vw.GetVoxelColorAt(worldPos);
                            } else {
                                // Interior voxel (subtract one because x=0 represents a voxel outside of this chunk)
                                readOnlyVoxel[index] = chunk.GetLocalVoxelAt(x - 1, y - 1, z - 1);
                                readOnlyColor[index] = chunk.GetLocalColorAt(x - 1, y - 1, z - 1);
                            }
                        }
                    }
                }

#pragma warning restore CS0162

                //Copy readOnlyVoxel to processedVoxelMask
                for (int i = 0; i < processedVoxelMask.Length; i++) {
                    processedVoxelMask[i] = readOnlyVoxel[i];
                }

                //Copy the damage values
                readOnlyDamageMap = new(chunk.damageMap);


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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ByteRemap(float a, float left, float right) {
            a = Mathf.Clamp(a, -0.5f, 0.5f) + 0.5f;  //0..1
            return (byte)(Mathf.Lerp(left, right, a) * 255.0f);
        }

        private static int EmitMesh(VoxelBlocks.BlockDefinition block, VoxelMeshCopy mesh, TemporaryMeshData target, VoxelWorld world, Vector3 origin, int rot, int flip, Vector2 damageUv, float[] lerps = null) {
            if (mesh == null) {
                return 0;
            }
            if (mesh.srcVertices == null) {
                return 0;
            }
            
            //Grab the flipped surface
            VoxelMeshCopy.PrecalculatedFlip flipSurface = mesh.flip[flip];
            if (flipSurface == null) {
                return 0;
            }

            //Material meshMaterial = block.meshMaterial;
            if (block.meshMaterialInstanceId != 0) {
                SubMesh targetSubMesh;
                target.subMeshes.TryGetValue(block.meshMaterialInstanceId, out SubMesh subMesh);
                
                if (subMesh == null) {
                    subMesh = new SubMesh(block.meshMaterialInstanceId);
                    target.subMeshes[block.meshMaterialInstanceId] = subMesh;
                }
                targetSubMesh = subMesh;
                // Add triangles
                foreach (VoxelMeshCopy.Surface surface in flipSurface.surfaces) {
                    for (int i = 0; i < surface.triangles.Length; i++) {
                        targetSubMesh.triangles.Add(surface.triangles[i] + target.verticesCount);
                    }
                }
            }
            else {
                foreach (VoxelMeshCopy.Surface surface in flipSurface.surfaces) {
                    SubMesh targetSubMesh;
                    if (surface.meshMaterial == null) {
                        continue;
                    }               
                    target.subMeshes.TryGetValue(surface.meshMaterialId, out SubMesh subMesh);
                    if (subMesh == null) {
                        subMesh = new SubMesh(surface.meshMaterialId);
                        target.subMeshes[surface.meshMaterialId] = subMesh;
                    }
                    targetSubMesh = subMesh;
                    // Add triangles
                    for (int i = 0; i < surface.triangles.Length; i++) {
                        targetSubMesh.triangles.Add(surface.triangles[i] + target.verticesCount);
                    }
                }
            }

            int count = 0;

            Vector3[] sourceVertices = null;
            Vector3[] sourceNormals = null;

            //Use the rot over using the flip bits (? is this correct? should we define it per block?)
            if (rot != 0) {
                VoxelMeshCopy.PrecalculatedRotation sourceRotation = mesh.rotation[rot];
                if (sourceRotation == null) {
                    return 0;
                }
                sourceVertices = sourceRotation.vertices;
                sourceNormals = sourceRotation.normals;
            }
            else {
                sourceVertices = flipSurface.vertices;
                sourceNormals = flipSurface.normals;
            }
                 
            count = sourceVertices.Length;
            Vector3 offset = origin + new Vector3(0.5f, 0.5f, 0.5f);

            // Ensure capacity
            EnsureCapacity(target, target.verticesCount + count);

            // Prepare transformed vertices
     
            for (int i = 0; i < count; i++) {
                Vector3 transformedPosition = sourceVertices[i] + offset;
                //write the transformed position (well, translated)
                target.vertices[target.verticesCount++] = transformedPosition;
            }

            // Copy other arrays directly
            Array.Copy(mesh.srcUvs, 0, target.uvs, target.uvsCount, count);
            target.uvsCount += count;
                
            //damage UVs
            Array.Fill(target.damageUvs, damageUv, target.damageUvsCount, count);
            target.damageUvsCount += count;

            //Normals
            Array.Copy(sourceNormals, 0, target.normals, target.normalsCount, count);
            target.normalsCount += count;
            
            
            if (lerps == null) {
                if (mesh.srcColors != null && mesh.srcColors.Length > 0) {
                    Array.Copy(mesh.srcColors, 0, target.colors, target.colorsCount, count);
                    target.colorsCount += count;
                }
            }
            else {
                //Interpolate the lerps and use that for vertex colors
                for (int j = 0; j < sourceVertices.Length; j++) {
                    Vector3 pos = sourceVertices[j];
                    byte dx = ByteRemap(pos.x, lerps[0], lerps[1]);
                    byte dy = ByteRemap(pos.y, lerps[3], lerps[2]);
                    byte dz = ByteRemap(pos.z, lerps[5], lerps[4]);
                    target.colors[target.colorsCount++] = new Color32(dx, dy, dz, 255);
                }
            }
            
            return count;
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
                    detailMeshData[i].damageUvsCount = 0;
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
                Debug.LogError("Error in threaded update full mesh: " + e);
            }
        }

        private void ThreadedUpdateFullMesh(System.Object worldObj) {
            VoxelWorld world = (VoxelWorld)worldObj;

            temporaryMeshData = new TemporaryMeshData();
            temporaryMeshData.verticesCount = 0;
            temporaryMeshData.colorsCount = 0;
            temporaryMeshData.normalsCount = 0;
            temporaryMeshData.uvsCount = 0;
            temporaryMeshData.damageUvsCount = 0;

            Material mat = world.voxelBlocks.atlasMaterial;
            
            //temporaryMeshData.subMeshes["atlas"] = new SubMesh(mat);

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

                        //Read the damage number
                        ushort internalVoxelKey = (ushort)(x + y * chunkSize + z * chunkSize * chunkSize);
                        readOnlyDamageMap.TryGetValue(internalVoxelKey, out float damage);
                        Vector2 damageUv = new Vector2(damage, 0);

                        BlockId blockIndex = VoxelWorld.VoxelDataToBlockId(vox);
                        if (blockIndex == 0) //Air!
                        {
                            //no visual
                            continue;
                        }

                        VoxelBlocks.BlockDefinition block = world.voxelBlocks.GetBlock(blockIndex);

                        if (block == null) {
                            continue;
                        }

                        switch (block.definition.contextStyle) {
                            case VoxelBlocks.ContextStyle.Prefab:
                                continue;
                            case VoxelBlocks.ContextStyle.PipeBlocks:
                                if (ContextPlacePipeBlock(block, localVoxelKey, readOnlyVoxel, temporaryMeshData, world, origin, damageUv) == true) {
                                    continue;
                                }
                            break;
                            case VoxelBlocks.ContextStyle.QuarterBlocks:
                                if (QuarterBlocksPlaceBlock(block, localVoxelKey, readOnlyVoxel, temporaryMeshData, world, origin, damageUv) == true) {
                                    continue;
                                }
                            break;
                            case VoxelBlocks.ContextStyle.GreedyMeshingTiles:
                                InitDetailMeshes();
                            
                                foreach (int index in block.meshTileProcessingOrder) {
                                    Vector3Int size = VoxelBlocks.meshTileSizes[index];

                                    if (FitBigTile(x + inset, y + inset, z + inset, size.x, size.y, size.z, blockIndex) == FitResult.FIT) {
                                        //See if the edges of all these tiles are visible
                                        bool visible = SeeIfLargeBlockVisible(localVoxelKey, size.x, size.y, size.z);
                                        if (visible) {
                                            int rotation = Math.Abs(VoxelWorld.HashCoordinates((int)origin.x, (int)origin.y, (int)origin.z) % 4);

                                            VoxelBlocks.LodSet set = block.meshTiles[index];

                                            int flip = 0;
                                           
                                            if (set.lod1 != null) {
                                                EmitMesh(block, set.lod0, detailMeshData[0], world, origin + VoxelBlocks.meshTileOffsets[index], rotation, flip, damageUv);
                                                EmitMesh(block, set.lod1, detailMeshData[1], world, origin + VoxelBlocks.meshTileOffsets[index], rotation, flip, damageUv);
                                                EmitMesh(block, set.lod2, detailMeshData[2], world, origin + VoxelBlocks.meshTileOffsets[index], rotation, flip, damageUv);
                                            }
                                            else {
                                                EmitMesh(block, set.lod0, temporaryMeshData, world, origin + VoxelBlocks.meshTileOffsets[index], rotation, flip, damageUv);
                                            }
                                           
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

                                        int flip = 0;
                                        if (set.lod1 != null) {
                                            EmitMesh(block, set.lod0, detailMeshData[0], world, origin, rotation, flip, damageUv);
                                            EmitMesh(block, set.lod1, detailMeshData[1], world, origin, rotation, flip, damageUv);
                                            EmitMesh(block, set.lod2, detailMeshData[2], world, origin, rotation, flip, damageUv);
                                        } else {
                                            EmitMesh(block, set.lod0, temporaryMeshData, world, origin, rotation, flip, damageUv);
                                        
                                        }
                                    }
                                    else {
                                        skipCount++;
                                    }

                                }
                                continue;
                        }
                        
                        //where we put this mesh is variable!
                        if (block.mesh != null) {
                            int flip = VoxelWorld.GetVoxelFlippedBits(vox);
                            int rotation = 0;
                            if (block.definition.randomRotation) {
                                rotation = Math.Abs(VoxelWorld.HashCoordinates((int)origin.x, (int)origin.y, (int)origin.z) % 4);
                            }
                            
                            //Grass etc                           
                            if (block.definition.staticMeshLOD1 != null) {
                                //Init the detail meshes now
                                InitDetailMeshes();
                                
                                if (block.mesh != null && block.mesh.lod0 != null) {
                                    EmitMesh(block, block.mesh.lod0, detailMeshData[0], world, origin, rotation, flip, damageUv);

                                    if (block.mesh.lod1 != null) {
                                        EmitMesh(block, block.mesh.lod1, detailMeshData[1], world, origin, rotation, flip, damageUv);
                                    }
                                    if (block.mesh.lod2 != null) {
                                        EmitMesh(block, block.mesh.lod2, detailMeshData[2], world, origin, rotation, flip, damageUv);
                                    }
                                }
                                
                            }
                            else {
                                //same mesh that the voxels use (think stairs etc)
                                EmitMesh(block, block.mesh.lod0, temporaryMeshData, world, origin, rotation, flip, damageUv);
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

                                int faceMatId = block.materialInstanceIds[faceIndex];
                                temporaryMeshData.subMeshes.TryGetValue(faceMatId, out SubMesh subMesh);
                                if (subMesh == null) {
                                    subMesh = new SubMesh(faceMatId);
                                    temporaryMeshData.subMeshes[faceMatId] = subMesh;
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

                                //Damage gen
                                for (int j = 0; j < 4; j++) {
                                    temporaryMeshData.damageUvs[temporaryMeshData.damageUvsCount++] = damageUv;
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
                            }
                        }
                    }
                }
            }

            if (skipCount > 0) {
                // Debug.Log("Skipped " + skipCount + " blocks");
            }

            if (true) {   
                var s = Stopwatch.StartNew();
                for (int i = 0; i < temporaryMeshData.verticesCount; i++) {
                    var vertPos = temporaryMeshData.vertices[i];
                    var vertWorldPos = vertPos;
                    var vertWorldPosRounded = new Vector3((float) Math.Round(vertWorldPos.x), (float) Math.Round(vertWorldPos.y),
                        (float) Math.Round(vertWorldPos.z));
                    
                    var neighborColors = new Color32[8];
                    var neighborWeights = new double[8];
                    var neighborCount = 0;
                    var weightTotal = 0.0;
                    // Grab neighbor colors and pick a weighted average color for this position
                    for (var x = -1; x <= 1; x += 2) {
                        for (var y = -1; y <= 1; y += 2) {
                            for (var z = -1; z <= 1; z += 2) {
                                var pos = vertWorldPosRounded + new Vector3(x, y, z) * 0.5f;

                                var readonlyVoxelPos = Vector3Int.FloorToInt(vertWorldPosRounded - chunk.chunkKey * chunkSize + new Vector3(x, y, z) * 0.5f + Vector3.one);
                                var readonlyVoxelKey = ((readonlyVoxelPos.x) + (readonlyVoxelPos.y) * paddedChunkSize + (readonlyVoxelPos.z) * paddedChunkSize * paddedChunkSize);
                                if (readonlyVoxelKey < 0 || readonlyVoxelKey >= readOnlyColor.Length) continue; // Out of bounds

                                var voxelPos = VoxelWorld.FloorInt(pos) + Vector3.one * 0.5f;
                                var dist = (vertWorldPos - voxelPos).magnitude;
                                if (dist > 1.5) continue;
                                var weight = 1.5 - dist;
                                weightTotal += weight;

                                neighborColors[neighborCount] = readOnlyColor[readonlyVoxelKey];
                                neighborWeights[neighborCount] = weight;
                                neighborCount++;
                            }
                        }   
                    }

                    var finalColor = new Color32();
                    for (var n = 0; n < neighborCount; n++) {
                        var weightedColor = Color32.Lerp(new Color32(), neighborColors[n], (float)(neighborWeights[n] / weightTotal));
                        finalColor.r += weightedColor.r;
                        finalColor.g += weightedColor.g;
                        finalColor.b += weightedColor.b;
                        finalColor.a += weightedColor.a;
                    }

                    temporaryMeshData.colors[i] = finalColor;
                }
                temporaryMeshData.colorsCount = temporaryMeshData.verticesCount;
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

            if (mesh == null || renderer == null || tempMesh == null) {
                return;
            }

            Profiler.BeginSample("ConstructMesh");
            mesh.subMeshCount = tempMesh.subMeshes.Count;
            mesh.SetVertices(tempMesh.vertices, 0, tempMesh.verticesCount);
            mesh.SetUVs(0, tempMesh.uvs, 0, tempMesh.uvsCount);
            mesh.SetUVs(1, tempMesh.damageUvs, 0, tempMesh.damageUvsCount);
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
                var srcMaterial = materialIdToMaterial[subMeshRec.srcMaterialId];
                if (cloneMaterials == true) {
                    Material clonedMaterial = new Material(srcMaterial);
                    mats[matWrite] = clonedMaterial;
                } else {
                    mats[matWrite] = srcMaterial;
                }
                matWrite++;
            }
            Profiler.EndSample();
           
            renderer.sharedMaterials = mats;
           
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


        //Swap shaders to get around the need to 
        //Local on the left, world on the right
        static List<Tuple<string, string>> shaderPairs = new List<Tuple<string, string>>
        {
            Tuple.Create("Shader Graphs/TriplanarSmoothstepLocalURP", "Shader Graphs/TriplanarSmoothstepWorldURP")
        };

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

            VoxelBlocks.BlockDefinition block = world.voxelBlocks.GetBlock((ushort)blockIndex);

            if (block == null || blockIndex == 0) //air
            {
                Debug.Log($"VoxelMeshProcessor could not get block at index {blockIndex}");
                return null;
            }

            GameObject obj = new GameObject();
            obj.name = block.definition.name;
            MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();

            //Allocate some buffers to work with
            TemporaryMeshData meshData = new TemporaryMeshData();
            meshRenderer.sharedMaterial = world.voxelBlocks.atlasMaterial;

            Mesh theMesh = new Mesh();

            //Center around 0,0,0
            Vector3 origin = new Vector3(-0.5f, -0.5f, -0.5f);
            int flip = 0;
            int rotation = 0;
            
            float damage = 0;
            var damageUv = new Vector2(damage, 0);
            
            if (block.definition.contextStyle == VoxelBlocks.ContextStyle.QuarterBlocks) {
                QuarterBlocskEmitSingleBlock(block, meshData, world, damageUv);
            }
            if (block.definition.contextStyle == VoxelBlocks.ContextStyle.StaticMesh) {
                if (block.mesh != null && block.mesh.lod0 != null) {
                    EmitMesh(block, block.mesh.lod0, meshData, world, origin, rotation, flip, damageUv);
                }
            }
            if (block.definition.contextStyle == VoxelBlocks.ContextStyle.Block) {

                //Add regular cube Faces
                for (int faceIndex = 0; faceIndex < 6; faceIndex++) {
                    Rect uvRect = block.GetUvsForFace(faceIndex);
                    var matInstanceId = block.materialInstanceIds[faceIndex];
                    meshData.subMeshes.TryGetValue(matInstanceId, out SubMesh subMesh);
                    if (subMesh == null) {
                        subMesh = new SubMesh(matInstanceId);
                        meshData.subMeshes[matInstanceId] = subMesh;
                    }

                    int vertexCount = meshData.verticesCount;
                    for (int j = 0; j < 4; j++) {
                        //vertices.Add(srcVertices[(i * 4) + j] + origin);
                        meshData.vertices[meshData.verticesCount++] = srcVertices[(faceIndex * 4) + j] + origin;
                        meshData.normals[meshData.normalsCount++] = srcNormals[faceIndex];
                        //Vertex color
                        meshData.colors[meshData.colorsCount++] = Color.black;
                    }

                    //UV gen
                    for (int j = 0; j < 4; j++) {
                        Vector2 uv = srcUvs[(faceIndex * 4) + j];

                        uv.x = uv.x * uvRect.width + uvRect.xMin;
                        uv.y = uv.y * uvRect.height + uvRect.yMin;

                        meshData.uvs[meshData.uvsCount++] = uv;
                    }

                    //Damage
                    for (int j = 0; j < 4; j++) {
                        meshData.damageUvs[meshData.damageUvsCount++] = damageUv;
                    }

                    //Faces
                    for (int j = 0; j < srcFaces[faceIndex].Length; j++) {
                        subMesh.triangles.Add(srcFaces[faceIndex][j] + vertexCount);
                    }
                }
            }
            CreateUnityMeshFromTemporayMeshData(theMesh, meshRenderer, meshData, world, true);

            //Tamper with the shaders/materials if they're known
            foreach (Material mat in meshRenderer.sharedMaterials) {

                if (mat.HasProperty("_Triplanar_Scale")) {
                    var existing = mat.GetFloat("_Triplanar_Scale");
                    mat.SetFloat("_Triplanar_Scale", existing * triplanarScale);
                }

                //Swap the shader if its known
                if (triplanerMode == 2) { //Local
                    
                    foreach (var shaderSwap in shaderPairs) {
                        if (mat.shader.name == shaderSwap.Item2) {
                            mat.shader = Shader.Find(shaderSwap.Item1);
                        }
                    }
                }
                if (triplanerMode == 1) { //World

                    foreach (var shaderSwap in shaderPairs) {
                        if (mat.shader.name == shaderSwap.Item1) {
                            mat.shader = Shader.Find(shaderSwap.Item2);
                        }
                    }
                }
            }

            meshFilter.sharedMesh = theMesh;
            return obj;
        }
 
        private static bool ContextPlacePipeBlock(VoxelBlocks.BlockDefinition block, int localVoxelKey, VoxelData[] readOnlyVoxel, TemporaryMeshData temporaryMeshData, VoxelWorld world, Vector3 origin, Vector2 damageUv) {
            //get surrounding data
            VoxelData voxUp = readOnlyVoxel[localVoxelKey + paddedChunkSize];
            VoxelData voxDown = readOnlyVoxel[localVoxelKey - paddedChunkSize];
            VoxelData voxLeft = readOnlyVoxel[localVoxelKey - 1];
            VoxelData voxRight = readOnlyVoxel[localVoxelKey + 1];
            VoxelData voxForward = readOnlyVoxel[localVoxelKey + (paddedChunkSize * paddedChunkSize)];
            VoxelData voxBack = readOnlyVoxel[localVoxelKey - (paddedChunkSize * paddedChunkSize)];
            int flip = 0;

            var meshContextArray = block.meshContexts[0];
                        
            //Check for top is air
            if (VoxelWorld.VoxelIsSolid(voxUp) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxUp) &&
                VoxelWorld.VoxelIsSolid(voxDown) == true) {
                bool airLeft = (VoxelWorld.VoxelIsSolid(voxLeft) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxLeft));
                bool airRight = (VoxelWorld.VoxelIsSolid(voxRight) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxRight));
                bool airForward = (VoxelWorld.VoxelIsSolid(voxForward) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxForward));
                bool airBack = (VoxelWorld.VoxelIsSolid(voxBack) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxBack));
                
                //Are we a block with 4 surrounding air spaces? That is block C!
                if (airLeft && airRight && airForward && airBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.C], temporaryMeshData, world, origin, 0, flip, damageUv);
                    return true;
                }

                //are we a block with 3 surrounding air spaces? That is block D!
                //Four combos
                if (airLeft && airRight && airForward && !airBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.D], temporaryMeshData, world, origin, 1, flip, damageUv);
                    return true;
                }
                if (airLeft && airRight && !airForward && airBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.D], temporaryMeshData, world, origin, 3, flip, damageUv);
                    return true;
                }
                if (airLeft && !airRight && airForward && airBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.D], temporaryMeshData, world, origin, 0, flip, damageUv);
                    return true;
                }
                if (!airLeft && airRight && airForward && airBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.D], temporaryMeshData, world, origin, 2, flip, damageUv);
                    return true;
                }

                //2 edge visible (a corner)
                if (airLeft && !airRight && airForward && !airBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.E], temporaryMeshData, world, origin, 0, flip, damageUv);
                    return true;
                }
                //2 edge visible (a corner)
                if (airLeft && !airRight && !airForward && airBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.E], temporaryMeshData, world, origin, 3, flip, damageUv);
                    return true;
                }

                //2 edge visible (a corner)
                if (!airLeft && airRight && airForward && !airBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.E], temporaryMeshData, world, origin, 1, flip, damageUv);
                    return true;
                }
                //2 edge visible (a corner)
                if (!airLeft && airRight && !airForward && airBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.E], temporaryMeshData, world, origin, 2, flip, damageUv);
                    return true;
                }

                //2 edger visible (a bridge)
                if (airLeft && airRight && !airForward && !airBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.F], temporaryMeshData, world, origin, 0, flip, damageUv);
                    return true;
                }
                //2 edger visible (a bridge)
                if (!airLeft && !airRight && airForward && airBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.F], temporaryMeshData, world, origin, 1, flip, damageUv);
                    return true;
                }

                //1 edge visible (t section)
                if (airLeft && !airRight && !airForward && !airBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.G], temporaryMeshData, world, origin, 0, flip, damageUv);
                    return true;
                }

                //1 edge visible (t section)
                if (!airLeft && airRight && !airForward && !airBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.G], temporaryMeshData, world, origin, 2, flip, damageUv);
                    return true;
                }

                // 1 edge visible(t section)
                if (!airLeft && !airRight && airForward && !airBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.G], temporaryMeshData, world, origin, 1, flip, damageUv);
                    return true;
                }

                // 1 edge visible(t section)
                if (!airLeft && !airRight && !airForward && airBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.G], temporaryMeshData, world, origin, 3, flip, damageUv);
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
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.B1], temporaryMeshData, world, origin, 1, flip, damageUv);
                    return true;
                }
                if (!airLeftForward && airRightForward && !airLeftBack && !airRightBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.B1], temporaryMeshData, world, origin, 2, flip, damageUv);
                    return true;
                }
                if (!airLeftForward && !airRightForward && airLeftBack && !airRightBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.B1], temporaryMeshData, world, origin, 0, flip, damageUv);
                    return true;
                }
                if (!airLeftForward && !airRightForward && !airLeftBack && airRightBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.B1], temporaryMeshData, world, origin, 3, flip, damageUv);
                    return true;
                }

                //Check for 2 air space on the same side
                if (airLeftForward && airRightForward && !airLeftBack && !airRightBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.B2A], temporaryMeshData, world, origin, 1, flip, damageUv);
                    return true;
                }
                if (!airLeftForward && !airRightForward && airLeftBack && airRightBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.B2A], temporaryMeshData, world, origin, 3, flip, damageUv);
                    return true;
                }
                if (airLeftForward && !airRightForward && airLeftBack && !airRightBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.B2A], temporaryMeshData, world, origin, 0, flip, damageUv);
                    return true;
                }
                if (!airLeftForward && airRightForward && !airLeftBack && airRightBack) {
                    EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.B2A], temporaryMeshData, world, origin, 2, flip, damageUv);
                    return true;
                }
                
                //Assume we a flat top with no surrounding air spaces
                EmitMesh(block, meshContextArray[(int)VoxelBlocks.PipeBlockTypes.B], temporaryMeshData, world, origin, 0, flip, damageUv);

                //Todo, this needs to check diagonals
                return true;
            }

            //fallback, if any face is visible, emit the whole mesh
            if ((VoxelWorld.VoxelIsSolid(voxDown) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxDown)) ||
                 (VoxelWorld.VoxelIsSolid(voxLeft) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxLeft)) ||
                 (VoxelWorld.VoxelIsSolid(voxRight) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxRight)) ||
                 (VoxelWorld.VoxelIsSolid(voxForward) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxForward)) ||
                 (VoxelWorld.VoxelIsSolid(voxBack) == false && block.blockId != VoxelWorld.VoxelDataToBlockId(voxBack))) {
                EmitMesh(block, meshContextArray[0], temporaryMeshData, world, origin, 0, flip, damageUv);
            }
            else {
                //Just empty air as this isnt visible
            }
            return true;
        }


    }
}