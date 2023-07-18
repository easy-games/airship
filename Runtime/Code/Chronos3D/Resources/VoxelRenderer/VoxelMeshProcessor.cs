
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

using VoxelData = System.UInt16;
using BlockId = System.UInt16;
using Assets.Chronos.VoxelRenderer;
using UnityEngine.Profiling;
using System.Net.Security;
using Codice.CM.Client.Differences;
using Codice.Client.Common;

namespace VoxelWorldStuff
{
    [LuauAPI]
    public class MeshProcessor
    {
        const int chunkSize = VoxelWorld.chunkSize;
        Chunk chunk;

        private bool bakedLightingDirty = false;
        private bool geometryDirty = false;
        private bool hasDetailMeshes = false;
        public bool GetGeometryDirty() { return geometryDirty; }

        static Vector3[] srcVertices;
        static Vector3[] srcRegularSamplePoints;
        static Vector3[] srcCornerSamplePoints;
        static Vector3[] srcMeshLightingSamplePoints;
        static Vector2[] srcUvs;
        static Vector3[] srcNormals;
        static int[][] srcFaces;
        static int[][] altSrcFaces;
        static Vector3Int[] faceChecks;
        static Vector3Int[][] occlusionSamples;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnStartup()
        {
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
        DateTime startLightProcessingTime;
        public int lastMeshUpdateDuration;
        public int lastLightUpdateDuration;

//            <Tile1x1>Tileset/Leafs/LeafSet1x1</Tile1x1>
    //<Tile2x2>Tileset/Leafs/LeafSet2x2</Tile2x2>
    //<Tile3x3>Tileset/Leafs/LeafSet3x3</Tile3x3>
    //<Tile4x4>Tileset/Leafs/LeafSet4x4</Tile4x4>

        const int paddedChunkSize = chunkSize + 2;
        //const int paddedChunkSize = chunkSize + 6;  //a+b

       

        
        VoxelData[] readOnlyVoxel = new VoxelData[paddedChunkSize * paddedChunkSize * paddedChunkSize];
        private const int capacity = chunkSize * chunkSize * chunkSize * 4;

        class TemporaryMeshData
        {
            public Dictionary<string, SubMesh> subMeshes = new();

            public Vector3[] vertices = new Vector3[capacity];
            public int verticesCount = 0;

            public Color32[] colors = new Color32[capacity];
            public int colorsCount = 0;

            public Vector3[] normals = new Vector3[capacity];
            public int normalsCount = 0;

            public Vector2[] uvs = new Vector2[capacity];
            public int uvsCount = 0;

            public SamplePoint[] samplePoints = new SamplePoint[capacity];
            public int samplePointCount = 0;
        }
        
        class TemporaryLightingData
        {
            public Vector2[] bakedLightA = new Vector2[capacity];
            public int bakedLightACount = 0;

            public Vector2[] bakedLightB = new Vector2[capacity];
            public int bakedLightBCount = 0;
        }

        TemporaryMeshData temporaryMeshData;
        TemporaryLightingData temporaryLightingData;

        TemporaryMeshData[] detailMeshData;
        TemporaryLightingData[] detailLightingData;



        bool[] shade = new bool[4];
        
        VoxelWorld.LightReference[] highQualityLightArray;

        Vector3Int key;
        
        bool finishedProcessing = false;

        public bool GetHasDetailMeshes()
        {
            return hasDetailMeshes;
        }


        class SubMesh
        {
            //Todo: Less garbage?
            public List<int> triangles = new(16000);

            public Material srcMaterial;

            public SubMesh(Material originalMaterial)
            {
                //material = new Material(originalMaterial);
                srcMaterial = originalMaterial;
                triangles = new List<int>();
            }

        };

        public class PersistantData
        {
            
            public VoxelWorld.LightReference[] detailLightArray;
            public VoxelWorld.LightReference[] highQualityLightArray;

        }

        public struct SamplePoint
        {
            public Vector3 position;
            public Vector3 normal;
            public SamplePoint(Vector3 pos, Vector3 norm)
            {
                position = pos;
                normal = norm;
            }

            public override int GetHashCode()
            {
                // XOR the hash codes of the position and normal vectors
                return position.GetHashCode() ^ normal.GetHashCode();
            }
        }


        public class Face
        {
            public Vector3 samplePoint;
            public Color color;
            public Vector3 normal;

            public Face(Vector3 samplePoint, Color color, Vector3 normal)
            {
                this.samplePoint = samplePoint;
                this.color = color;
                this.normal = normal;
            }
        }

        private static void InitVertexData()
        {

            //Init static data
            if (srcVertices == null)
            {
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

                for (int j = 0; j < srcVertices.Length; j++)
                {
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

                for (int j = 0; j < srcCornerSamplePoints.Length; j++)
                {
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

                for (int j = 0; j < srcRegularSamplePoints.Length; j++)
                {
                    srcRegularSamplePoints[j] += new Vector3(0.5f, 0.5f, 0.5f);
                }

                srcMeshLightingSamplePoints = new Vector3[]
                {
                    new Vector3(-offset,-offset, -offset) + new Vector3(0.5f,0.5f,0.5f),
                    new Vector3(offset,-offset, -offset) + new Vector3(0.5f,0.5f,0.5f),
                    new Vector3(-offset,offset, -offset) + new Vector3(0.5f,0.5f,0.5f),
                    new Vector3(offset,offset, -offset) + new Vector3(0.5f,0.5f,0.5f),
                    new Vector3(-offset,-offset, offset) + new Vector3(0.5f,0.5f,0.5f),
                    new Vector3(offset,-offset, offset) + new Vector3(0.5f,0.5f,0.5f),
                    new Vector3(-offset,offset, offset) + new Vector3(0.5f,0.5f,0.5f),
                    new Vector3(offset,offset, offset) + new Vector3(0.5f,0.5f,0.5f)
                };

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
        
        public MeshProcessor(Chunk chunk, bool onlyUpdateLighting)
        {
            Profiler.BeginSample("MeshProcessor Constructor");
            MeshProcessor.InitVertexData();
            startMeshProcessingTime = DateTime.Now;

            this.chunk = chunk;
            if (onlyUpdateLighting == true)
            {
                //copy the lights over
                chunk.meshPersistantData.detailLightArray = (VoxelWorld.LightReference[])chunk.GetDetailLightArray().Clone();
                chunk.meshPersistantData.highQualityLightArray = (VoxelWorld.LightReference[])chunk.GetHighQualityLightArray().Clone();

                //Run
#pragma warning disable CS0162
                if (VoxelWorld.runThreaded)
                {
                    ThreadPool.QueueUserWorkItem(ThreadedUpdateLighting, chunk.world);
                }
                else
                {
                    ThreadedUpdateLighting(chunk.world);
                }
#pragma warning restore CS0162                

            }
            else
            {
                //Full update
                //copy the lights over
                Profiler.BeginSample("LightClone");
                highQualityLightArray = (VoxelWorld.LightReference[])chunk.GetHighQualityLightArray().Clone();
                chunk.meshPersistantData.detailLightArray = (VoxelWorld.LightReference[])chunk.GetDetailLightArray().Clone();
                chunk.meshPersistantData.highQualityLightArray = (VoxelWorld.LightReference[])chunk.GetHighQualityLightArray().Clone();
                Profiler.EndSample();

                //copy the voxels over
                Profiler.BeginSample("CopyVoxels");

                //The voxels we care about are inset by 1 here, and expanded out by +3 on the positive axies
                //This is because we need to know the voxels around the edges of the chunk and expanded a bit for tiles
                //and neighbours

                //Debug copy
                if (false)
                {
                    for (int x = 0; x < paddedChunkSize; x++)
                    {
                        for (int y = 0; y < paddedChunkSize; y++)
                        {
                            for (int z = 0; z < paddedChunkSize; z++)
                            {
                                int index = x  + y * paddedChunkSize + z * paddedChunkSize * paddedChunkSize;
                                readOnlyVoxel[index] = chunk.world.ReadVoxelAtInternal(new Vector3Int(chunk.bottomLeftInt.x + x - 1, chunk.bottomLeftInt.y + y - 1, chunk.bottomLeftInt.z + z - 1));
                            }
                        }
                    }
                }
                else
                {
                
                    //Main block
                    for (int x = 0; x < chunkSize; x++)
                    {
                        for (int y = 0; y < chunkSize; y++)
                        {
                            for (int z = 0; z < chunkSize; z++)
                            {
                                int index = (x + 1) + (y + 1) * paddedChunkSize + (z + 1) * paddedChunkSize * paddedChunkSize;
                                readOnlyVoxel[index] = chunk.GetLocalVoxelAt(x, y, z);
                            }
                        }
                    }
 
                    //xPlane
                    for (int y = 0; y < paddedChunkSize; y++)
                    {
                        for (int z = 0; z < paddedChunkSize; z++)
                        {
                            int x0 = 0;
                            int x1 = paddedChunkSize - 1;
                            readOnlyVoxel[x0 + y * paddedChunkSize + z * paddedChunkSize * paddedChunkSize] = chunk.world.ReadVoxelAtInternal(new Vector3Int(chunk.bottomLeftInt.x + x0 - 1, chunk.bottomLeftInt.y + y - 1, chunk.bottomLeftInt.z + z - 1));
                            readOnlyVoxel[x1 + y * paddedChunkSize + z * paddedChunkSize * paddedChunkSize] = chunk.world.ReadVoxelAtInternal(new Vector3Int(chunk.bottomLeftInt.x + x1 - 1, chunk.bottomLeftInt.y + y - 1, chunk.bottomLeftInt.z + z - 1));
                        

                        }
                    }

                    //yPlane, skipping the voxels already filled by x plane
                    for (int x = 1; x < paddedChunkSize - 1; x++)
                    {
                        for (int z = 0; z < paddedChunkSize; z++)
                        {
                            int y0 = 0;
                            int y1 = paddedChunkSize - 1;
                            readOnlyVoxel[x + y0 * paddedChunkSize + z * paddedChunkSize * paddedChunkSize] = chunk.world.ReadVoxelAtInternal(new Vector3Int(chunk.bottomLeftInt.x + x - 1, chunk.bottomLeftInt.y + y0 - 1, chunk.bottomLeftInt.z + z - 1));
                            readOnlyVoxel[x + y1 * paddedChunkSize + z * paddedChunkSize * paddedChunkSize] = chunk.world.ReadVoxelAtInternal(new Vector3Int(chunk.bottomLeftInt.x + x - 1, chunk.bottomLeftInt.y + y1 - 1, chunk.bottomLeftInt.z + z - 1));
                        }
                    }

                    //Zplane, skipping the voxels already filled by x plane and y plane
                    for (int x = 1; x < paddedChunkSize - 1; x++)
                    {
                        for (int y = 1; y < paddedChunkSize - 1; y++)
                        {
                            int z0 = 0;
                            int z1 = paddedChunkSize - 1;
                            readOnlyVoxel[x + y * paddedChunkSize + z0 * paddedChunkSize * paddedChunkSize] = chunk.world.ReadVoxelAtInternal(new Vector3Int(chunk.bottomLeftInt.x + x - 1, chunk.bottomLeftInt.y + y - 1, chunk.bottomLeftInt.z + z0 - 1));
                            readOnlyVoxel[x + y * paddedChunkSize + z1 * paddedChunkSize * paddedChunkSize] = chunk.world.ReadVoxelAtInternal(new Vector3Int(chunk.bottomLeftInt.x + x - 1, chunk.bottomLeftInt.y + y - 1, chunk.bottomLeftInt.z + z1 - 1));
                        }
                    }
                }


                key = chunk.GetKey();
                Profiler.EndSample();

#pragma warning disable CS0162
                //Run
                if (VoxelWorld.runThreaded)
                {
                    ThreadPool.QueueUserWorkItem(ThreadedUpdateFullMesh, chunk.world);
                }
                else
                {
                    ThreadedUpdateFullMesh(chunk.world);
                }
#pragma warning restore CS0162
            }
            Profiler.EndSample();
        }

        private static void EmitMesh(VoxelBlocks.BlockDefinition block, MeshCopy mesh, TemporaryMeshData target, VoxelWorld world, Vector3 origin, bool light)
        {
            string matName = block.meshMaterialName;

            SubMesh targetSubMesh;
            if (matName == "atlas")
            {
                target.subMeshes.TryGetValue(matName, out SubMesh subMesh);
                if (subMesh == null)
                {
                    subMesh = new SubMesh(world.blocks.materials[matName]);
                    target.subMeshes[matName] = subMesh;
                }
                targetSubMesh = subMesh;
            }
            else
            {
                matName = mesh.meshMaterialName;
                target.subMeshes.TryGetValue(matName, out SubMesh subMesh);
                if (subMesh == null)
                {
                    subMesh = new SubMesh(mesh.meshMaterial);
                    target.subMeshes[matName] = subMesh;
                }
                targetSubMesh = subMesh;
            }

            //Add mesh data
            Vector3 offset = origin + new Vector3(0.5f, 0.5f, 0.5f);
            int vertexCount = target.verticesCount;

            //If over size of array, resize arrays
            int requiredSize = target.verticesCount + mesh.uvs.Count;
            if (requiredSize > target.vertices.Length)
            { 
                //Double the size
                Array.Resize(ref target.vertices, requiredSize * 2);
                Array.Resize(ref target.colors, requiredSize * 2);
                Array.Resize(ref target.normals, requiredSize * 2);
                Array.Resize(ref target.uvs, requiredSize * 2);
                Array.Resize(ref target.samplePoints, requiredSize * 2);

            }

            //Calculate the direct lighting at the 8 corners
            //Color[] corners = new Color[8];
            /*
            for (int i = 0; i < 8; i++)
            {
                Vector3 cornerPos = srcMeshLightingSamplePoints[i];
                Vector3 worldPos = cornerPos + offset;
                corners[i] = Color.white;// DoDirectLighting(world, worldPos, 1, Vector3.up, lightingCache);
            }*/


            //Todo: @@@
            int rot = 0;// VoxelWorld.HashCoordinates((int)origin.x, (int)origin.y, (int)origin.z) % 4;

            mesh.rotation.TryGetValue(rot, out MeshCopy.PrecalculatedRotation sourceRotation);

            for (int i = 0; i < sourceRotation.vertices.Count; i++)
            {
                Vector3 vertex = sourceRotation.vertices[i];
                Vector3 normal = sourceRotation.normals[i];
                
                Vector2 uv = mesh.uvs[i];
                Vector3 transformedPosition = vertex + offset;

                target.vertices[target.verticesCount++] = transformedPosition;
                target.uvs[target.uvsCount++] = uv;
                target.normals[target.normalsCount++] = normal;

                Vector3 samplePoint = transformedPosition; //transformed positio
                //Color32 col = DoInterpolatedLighting(vertex, corners);
                target.colors[target.colorsCount++] = Color.white;

                if (light)
                {
                    target.samplePoints[target.samplePointCount++] = new SamplePoint(samplePoint, normal);
                }
            }


            //Add triangles
            for (int i = 0; i < mesh.triangles.Count; i++)
            {
                targetSubMesh.triangles.Add(mesh.triangles[i] + vertexCount);
            }
        }

        private static Color DoInterpolatedLighting(Vector3 localPosition, Color[] colors)
        {
            /*
              0  new Vector3(-offset, -offset, -offset) + new Vector3(0.5f, 0.5f, 0.5f),
              1  new Vector3(offset, -offset, -offset) + new Vector3(0.5f, 0.5f, 0.5f),
              2  new Vector3(-offset, offset, -offset) + new Vector3(0.5f, 0.5f, 0.5f),
              3  new Vector3(offset, offset, -offset) + new Vector3(0.5f, 0.5f, 0.5f),
              4  new Vector3(-offset, -offset, offset) + new Vector3(0.5f, 0.5f, 0.5f),
              5  new Vector3(offset, -offset, offset) + new Vector3(0.5f, 0.5f, 0.5f),
              6  new Vector3(-offset, offset, offset) + new Vector3(0.5f, 0.5f, 0.5f),
              7  new Vector3(offset, offset, offset) + new Vector3(0.5f, 0.5f, 0.5f)
            */
            float topX = localPosition.x + 0.5f;
            float topY = localPosition.y + 0.5f;
            float topZ = localPosition.z + 0.5f;

            // Interpolate along the x-axis
            Color c00 = Color.Lerp(colors[0], colors[1], topX);
            Color c01 = Color.Lerp(colors[2], colors[3], topX);
            Color c10 = Color.Lerp(colors[4], colors[5], topX);
            Color c11 = Color.Lerp(colors[5], colors[7], topX);

            // Interpolate along the y-axis
            Color c0 = Color.Lerp(c00, c01, topY);
            Color c1 = Color.Lerp(c10, c11, topY);

            // Interpolate along the z-axis
            Color finalColor = Color.Lerp(c0, c1, topZ);

            return finalColor;

        }
        
        public enum FitResult
        {
            NO_FIT,
            FIT
        }
           

        private FitResult FitBigTile(int lx, int ly, int lz, int sizeX, int sizeY, int sizeZ, int match)
        {
            if (lx + sizeX > chunkSize+1   || ly + sizeY > chunkSize + 1 || lz + sizeZ > chunkSize + 1)
            {
                return FitResult.NO_FIT;
            }
            
            //Because we overlap into other chunks, we can assume the other chunk will fill our lowest numbers
            for (int dx = lx; dx < lx + sizeX; dx++)
            {
                for (int dy = ly; dy < ly + sizeY; dy++)
                {
                    for (int dz = lz; dz < lz + sizeZ; dz++)
                    {
                        int localVoxelKey = (dx + dy * paddedChunkSize + dz * paddedChunkSize * paddedChunkSize);
                        VoxelData vox = readOnlyVoxel[localVoxelKey];

                        BlockId blockIndex = VoxelWorld.VoxelDataToBlockId(vox);
                        if (blockIndex != match)
                        {
                            return FitResult.NO_FIT;
                        }
                    }
                }
            }
            //Clear all the tiles
            for (int dx = lx; dx < lx + sizeX; dx++)
            {
                for (int dy = ly; dy < ly + sizeY; dy++)
                {
                    for (int dz = lz; dz < lz + sizeZ; dz++)
                    {
                        int localVoxelKey = (dx + dy * paddedChunkSize + dz * paddedChunkSize * paddedChunkSize);
                        readOnlyVoxel[localVoxelKey] = 0;
                    }
                }
            }
            
            return FitResult.FIT;
        }

        private void ThreadedUpdateFullMesh(System.Object worldObj)
        {
            VoxelWorld world = (VoxelWorld)worldObj;

            temporaryMeshData = new TemporaryMeshData();
            temporaryMeshData.verticesCount = 0;
            temporaryMeshData.colorsCount = 0;
            temporaryMeshData.normalsCount = 0;
            temporaryMeshData.uvsCount = 0;
            temporaryMeshData.samplePointCount = 0;

            bool found = world.blocks.materials.TryGetValue("atlas", out Material mat);
            if (found == false)
            {
                return;
            }
            temporaryMeshData.subMeshes["atlas"] = new SubMesh(mat);
            

            Color32 sun = Color.white;
            Vector3Int worldKey = (key * chunkSize);

            Dictionary<SamplePoint, Color> lightingCache = new();
            
            const int inset = 1;

            for (int x = 0; x < VoxelWorld.chunkSize; x++)
            {
                for (int y = 0; y < VoxelWorld.chunkSize; y++)
                {
                    for (int z = 0; z < VoxelWorld.chunkSize; z++)
                    {
                                                
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

                        if (block == null)
                        {
                            continue;
                        }
                        
                        // Prefab blocks use "fake" blocks that are just invisible (like air!)
                        if (block.fake)
                        {
                            // no visual
                            continue;
                        }
                        
                        if (block.usesTiles == true)
                        {
                            foreach (int index in block.meshTileProcessingOrder)
                            {
                                
                                Vector3Int size = VoxelBlocks.meshTileSizes[index];
                                
                                if (FitBigTile(x + inset, y + inset, z + inset, size.x, size.y, size.z, blockIndex) == FitResult.FIT)
                                {
                                    EmitMesh(block, block.meshTiles[index], temporaryMeshData, world, origin + VoxelBlocks.meshTileOffsets[index], true);
                                    break; 
                                }
                                
                            }
                            //If its still filled, write a 1x1
                            if (readOnlyVoxel[localVoxelKey] > 0)
                            {
                                readOnlyVoxel[localVoxelKey] = 0;
                                EmitMesh(block, block.meshTiles[0], temporaryMeshData, world, origin, true);
                            }
                            continue;
                        }
              
                        
                        //where we put this mesh is variable!
                        if (block.mesh != null)
                        {
                            //Grass etc                           
                            if (block.detail == true)
                            {
                                //Init the detail meshes now
                                if (hasDetailMeshes == false)
                                {
                                    hasDetailMeshes = true;

                                    if (detailMeshData == null)
                                    {
                                        //create the detail meshes if needed
                                        detailMeshData = new TemporaryMeshData[2];
                                        detailMeshData[0] = new TemporaryMeshData();
                                        detailMeshData[1] = new TemporaryMeshData();
                                    }

                                    for (int i = 0; i < 2; i++)
                                    {
                                        detailMeshData[i].verticesCount = 0;
                                        detailMeshData[i].colorsCount = 0;
                                        detailMeshData[i].normalsCount = 0;
                                        detailMeshData[i].uvsCount = 0;
                                        detailMeshData[i].samplePointCount = 0;
                                    }
                                
                                }
                                
                                if (block.mesh != null)
                                {
                                    EmitMesh(block, block.mesh, detailMeshData[0], world, origin, true);
                                }
                                if (block.meshLod != null)
                                {
                                    EmitMesh(block, block.mesh, detailMeshData[1], world, origin, true);
                                }
                            }
                            else
                            {
                                //same mesh that the voxels use (think stairs etc)
                                EmitMesh(block, block.mesh, temporaryMeshData, world, origin, true);
                            }
                            //No code past here
                            continue;
                        }


                        //Add regular cube Faces
                        for (int faceIndex = 0; faceIndex < 6; faceIndex++)
                        {
                            //Vector3Int check = origin + faceChecks[faceIndex];
                            //VoxelData other = world.ReadVoxelAtInternal(check);
                            
                            Vector3Int check = faceChecks[faceIndex];

                            int voxelKeyCheck = (localVoxel.x+check.x) + (localVoxel.y + check.y) * paddedChunkSize + (localVoxel.z + check.z) * paddedChunkSize * paddedChunkSize;
                            VoxelData other = readOnlyVoxel[voxelKeyCheck];


                            BlockId otherBlockIndex = VoxelWorld.VoxelDataToBlockId(other);

                            //Figure out if we're meant to generate a face. 
                            //If we're facing something nonsolid that isn't the same as us (eg: glass faces dont build internally)
                            if (VoxelWorld.VoxelIsSolid(other) == false && otherBlockIndex != blockIndex)
                            {
                                Rect uvRect = block.GetUvsForFace(faceIndex);

                                string matName = block.materials[faceIndex];

                                temporaryMeshData.subMeshes.TryGetValue(matName, out SubMesh subMesh);
                                if (subMesh == null)
                                {
                                    subMesh = new SubMesh(world.blocks.materials[matName]);
                                    temporaryMeshData.subMeshes[matName] = subMesh;
                                }

                                int faceAxis = faceAxisForFace[faceIndex];
                                Vector3 normal = normalForFace[faceIndex];

                                int vertexCount = temporaryMeshData.verticesCount;
                                for (int j = 0; j < 4; j++)
                                {
                                    //vertices.Add(srcVertices[(i * 4) + j] + origin);
                                    temporaryMeshData.vertices[temporaryMeshData.verticesCount++] = srcVertices[(faceIndex * 4) + j] + origin;
                                    temporaryMeshData.normals[temporaryMeshData.normalsCount++] = srcNormals[faceIndex];
                                }

                                //UV gen
                                for (int j = 0; j < 4; j++)
                                {
                                    Vector2 uv = srcUvs[(faceIndex * 4) + j];

                                    uv.x = uv.x * uvRect.width + uvRect.xMin;
                                    uv.y = uv.y * uvRect.height + uvRect.yMin;

                                    temporaryMeshData.uvs[temporaryMeshData.uvsCount++] = uv;
                                }
 
                               
                                //Do occlusions
                                if (block.doOcclusion == true) //If this mesh wants occlusions, calculate the occlusions for this face
                                {
                                    for (int j = 0; j < 4; j++)
                                    {

                                        Vector3 samplePoint;
                                        byte g = 0;
 
                                        if (IsAnyVoxelOccluded(faceIndex, j, localVoxel) == false)
                                        {
                                            g = 255; //No ambient occlusion
                                            //we're not in a corner
                                            samplePoint = srcRegularSamplePoints[(faceIndex * 4) + j] + origin;
                                        }
                                        else
                                        {
                                            //we're in a corner!
                                            shade[j] = true;
                                            g = 0; //ambient occluded
                                            samplePoint = srcCornerSamplePoints[(faceIndex * 4) + j] + origin;
                                        }

                                        Color32 col = Color.white;// DoDirectLighting(world, samplePoint, faceAxis, normal, lightingCache);

                                        col.g = g;
                                        //shade[j] = col.r < 255;

                                        temporaryMeshData.colors[temporaryMeshData.colorsCount++] = col;

                                        //save the sample data for later
                                        temporaryMeshData.samplePoints[temporaryMeshData.samplePointCount++] = new SamplePoint(samplePoint, normal);
                                    }


                                    
                                    //See if opposite corners are shaded      0--1        0--1
                                    //see if single 1 corner is shaded     alt|\ |    norm| /|
                                    //see if single 2 corner is shaded        2--3        2--3
                                    if ((shade[0] && shade[3]) || (shade[1] && !shade[0] && !shade[2] && !shade[3]) || (shade[2] && !shade[0] && !shade[1] && !shade[3]))
                                    {
                                        //Turn the triangulation
                                        for (int j = 0; j < altSrcFaces[faceIndex].Length; j++)
                                        {
                                            subMesh.triangles.Add(altSrcFaces[faceIndex][j] + vertexCount);
                                        }
                                    }
                                    else
                                    {
                                        for (int j = 0; j < srcFaces[faceIndex].Length; j++)
                                        {
                                            subMesh.triangles.Add(srcFaces[faceIndex][j] + vertexCount);
                                        }
                                    }
                                }
                                else
                                {
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

            //Mark the mesh as needing a geometry refresh
            geometryDirty = true;

            lastMeshUpdateDuration = (int)((DateTime.Now - startMeshProcessingTime).TotalMilliseconds);
            
            //Process off the baked lighting now too
            ProcessBakedLighting(world);

            //All done
            finishedProcessing = true;
        }

        
        private bool IsAnyVoxelOccluded(int faceIndex, int j, Vector3Int localVoxel)
        {
            Vector3Int i0 = occlusionSamples[faceIndex][j * 3 + 0];
            int index0 = (localVoxel.x + i0.x) + (localVoxel.y + i0.y) * paddedChunkSize + (localVoxel.z + i0.z) * paddedChunkSize * paddedChunkSize;
            if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[index0]))
            {
                return true;
            }

            Vector3Int i1 = occlusionSamples[faceIndex][j * 3 + 1];
            int index1 = (localVoxel.x + i1.x) + (localVoxel.y + i1.y) * paddedChunkSize + (localVoxel.z + i1.z) * paddedChunkSize * paddedChunkSize;
            if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[index1]))
            {
                return true;
            }

            Vector3Int i2 = occlusionSamples[faceIndex][j * 3 + 2];
            int index2 = (localVoxel.x + i2.x) + (localVoxel.y + i2.y) * paddedChunkSize + (localVoxel.z + i2.z) * paddedChunkSize * paddedChunkSize;
            if (VoxelWorld.VoxelIsSolid(readOnlyVoxel[index2]))
            {
                return true;
            }

            return false;
        }
      
        public void ThreadedUpdateLighting(System.Object worldObj)
        {
            //Process off the baked lighting now too
            ProcessBakedLighting((VoxelWorld)worldObj);

            //All done
            finishedProcessing = true;
        }

        private Color32 DoDirectLighting(VoxelWorld world, Vector3 samplePoint, int faceAxis, Vector3 normal, Dictionary<SamplePoint, Color> cache)
        {

            SamplePoint sample = new SamplePoint(samplePoint, normal);
            bool found = cache.TryGetValue(sample, out Color retValue);
            if (found == true)
            {
                return retValue;
            }
            Color32 col = new Color32(0, 0, 0, 0);


            //Sun
            float occlusionLevel = world.CalculateSunShadowAtPoint(samplePoint, faceAxis, normal);

            col.r = (byte)(255.0f - (occlusionLevel * 255.0f));

            int counter = 0;
            foreach (VoxelWorld.LightReference lightRef in highQualityLightArray)
            {
                if (lightRef == null)
                {
                    continue;
                }
                if (lightRef.shadow == true)
                {
                    //pack the two pointlight shadows into B and A
                    int occluded = world.CalculatePointLightShadowAtPoint(samplePoint, normal, lightRef);


                    if (occluded == 1)
                    {
                        //shade[j] = true;
                    }

                    if (counter == 0)
                    {
                        if (occluded > 0)
                        {
                            col.b = 0;
                        }
                        else
                        {
                            col.b = 255;
                        }
                    }
                    else //if (counter == 1)
                    {
                        if (occluded == 1)
                        {
                            col.a = 0;
                        }
                        else
                        {
                            col.a = 255;
                        }
                    }
                }
                else
                {
                    //no shadow
                    if (counter == 0)
                    {
                        col.b = 255;
                    }
                    else
                    {
                        col.a = 255;
                    }
                }

                counter += 1;
            }
            cache[sample] = col;

            return col;
        }

        public void ProcessBakedLighting(VoxelWorld world)
        {
            startLightProcessingTime = DateTime.Now;
            
            temporaryLightingData = new TemporaryLightingData();
            temporaryLightingData.bakedLightACount = 0;
            temporaryLightingData.bakedLightBCount = 0;

            ProcessLightingForSubmesh(temporaryMeshData, temporaryLightingData, world);

            if (hasDetailMeshes == true)
            {
                detailLightingData = new TemporaryLightingData[2];
                for (int i = 0; i < 2; i++)
                {
                    detailLightingData[i] = new TemporaryLightingData();
                    detailLightingData[i].bakedLightACount = 0;
                    detailLightingData[i].bakedLightBCount = 0;
                    ProcessLightingForSubmesh(detailMeshData[i], detailLightingData[i], world);
                }
            }

            //Mark the lighting data as needing updating
            bakedLightingDirty = true;

            lastLightUpdateDuration = (int)((DateTime.Now - startLightProcessingTime).Milliseconds);
        }


        private void ProcessLightingForSubmesh(TemporaryMeshData target, TemporaryLightingData output, VoxelWorld world)
        {
            //Make sure it'll fit
            if (target.samplePointCount + output.bakedLightACount > output.bakedLightA.Length)
            {
                //resize the array to be double
                int newSize = output.bakedLightA.Length * 2;
                while (newSize < target.samplePointCount + output.bakedLightA.Length)
                {
                    newSize *= 2;
                }
                Array.Resize(ref output.bakedLightA, newSize);
                Array.Resize(ref output.bakedLightB, newSize);
                
            }

            for (int i = 0; i < target.samplePointCount; i++)
            {
                SamplePoint sample = target.samplePoints[i];
                Vector3 normal = sample.normal;
                Vector3 position = sample.position;

                Color detailLight = new Color();
                foreach (VoxelWorld.LightReference lightRef in chunk.meshPersistantData.detailLightArray)
                {
                    if (lightRef == null)
                    {
                        continue;
                    }
                    if (lightRef.shadow == true)
                    {
                        detailLight += world.CalculatePointLightColorAtPointShadow(position, normal, lightRef);
                    }
                    else
                    {
                        float falloff = world.CalculatePointLightColorAtPoint(position, normal, lightRef);
                        detailLight += lightRef.color * falloff;
                    }
                }

                //Radiosity
                if (world.radiosityEnabled)
                {
                    Color currentProbeColor = world.GetRadiosityProbeColorForWorldPoint(position, normal);// * world.globalRadiosityScale;
                    detailLight += currentProbeColor * world.globalRadiosityScale; //visiblity, not the energy transfer
                }
                //store the baked light
                output.bakedLightA[output.bakedLightACount++] = new Vector2(detailLight.r, detailLight.g);
                output.bakedLightB[output.bakedLightBCount++] = new Vector2(detailLight.b, 0);
            }
        }

        public bool GetFinishedProcessing()
        {
            return finishedProcessing;
        }

        private static void CreateUnityMeshFromTemporayMeshData(Mesh mesh, Renderer renderer, TemporaryMeshData tempMesh, TemporaryLightingData lightingData)
        {
            mesh.subMeshCount = tempMesh.subMeshes.Count;
            mesh.SetVertices(tempMesh.vertices, 0, tempMesh.verticesCount);
            mesh.SetUVs(0, tempMesh.uvs, 0, tempMesh.uvsCount);
            mesh.SetColors(tempMesh.colors, 0, tempMesh.colorsCount);
            mesh.SetNormals(tempMesh.normals, 0, tempMesh.normalsCount);
            
            if (lightingData != null)
            {
                mesh.SetUVs(1, lightingData.bakedLightA, 0, lightingData.bakedLightACount);
                mesh.SetUVs(2, lightingData.bakedLightB, 0, lightingData.bakedLightBCount);
           

                if (lightingData.bakedLightACount != tempMesh.uvsCount)
                {
                    Debug.Log("bad baked light count" + lightingData.bakedLightACount + " vs " + tempMesh.uvsCount);
                }
            }

            Material[] mats = new Material[tempMesh.subMeshes.Count];
            int matWrite = 0;
            foreach (SubMesh subMeshRec in tempMesh.subMeshes.Values)
            {
                mats[matWrite] = new Material(subMeshRec.srcMaterial);
                mats[matWrite].enabledKeywords = subMeshRec.srcMaterial.enabledKeywords;

                mats[matWrite].EnableKeyword("VERTEX_LIGHT_ON");
                mats[matWrite].SetFloat("VERTEX_LIGHT", 1);

                mesh.SetTriangles(subMeshRec.triangles, matWrite);
                matWrite++;
            }

            renderer.sharedMaterials = mats;

            //mesh.RecalculateNormals();
            mesh.RecalculateTangents();
        }

        public void FinalizeMesh(Mesh mesh, Renderer renderer, Mesh[] detailMeshes, Renderer[] detailRenderers)
        {
            if (geometryDirty == true)
            {
                //Updates both the geometry and baked lighting
                CreateUnityMeshFromTemporayMeshData(mesh, renderer, temporaryMeshData, temporaryLightingData);

                if (detailMeshes != null)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        CreateUnityMeshFromTemporayMeshData(detailMeshes[i], detailRenderers[i], detailMeshData[i], detailLightingData[i]);
                    }
                }

                geometryDirty = false;
                bakedLightingDirty = false;
            }

            if (bakedLightingDirty == true)
            {

                mesh.SetUVs(1, temporaryLightingData.bakedLightA, 0, temporaryLightingData.bakedLightACount);
                mesh.SetUVs(2, temporaryLightingData.bakedLightB, 0, temporaryLightingData.bakedLightBCount);

                
                if (detailMeshes != null)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        detailMeshes[i].SetUVs(1, detailLightingData[i].bakedLightA, 0, detailLightingData[i].bakedLightACount);
                        detailMeshes[i].SetUVs(2, detailLightingData[i].bakedLightB, 0, detailLightingData[i].bakedLightBCount);
                    }
                }
                bakedLightingDirty = false;
            }
        }

        public static GameObject ProduceSingleBlock(int blockIndex, VoxelWorld world)
        {
            MeshProcessor.InitVertexData();
                        
            
            VoxelBlocks.BlockDefinition block = world.blocks.GetBlock((ushort)blockIndex);
            
            if (block == null || block.fake == true || blockIndex == 0) //air
            {
                Debug.Log("Block not available");
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

            if (block.mesh != null)
            {
                EmitMesh(block, block.mesh, meshData, world, origin, false);
            }
            else
            {
                //Add regular cube Faces
                for (int faceIndex = 0; faceIndex < 6; faceIndex++)
                {
                    Rect uvRect = block.GetUvsForFace(faceIndex);
                    string matName = block.materials[faceIndex];

                    meshData.subMeshes.TryGetValue(matName, out SubMesh subMesh);
                    if (subMesh == null)
                    {
                        subMesh = new SubMesh(world.blocks.materials[matName]);
                        meshData.subMeshes[matName] = subMesh;
                    }
           
                    int vertexCount = meshData.verticesCount;
                    for (int j = 0; j < 4; j++)
                    {
                        //vertices.Add(srcVertices[(i * 4) + j] + origin);
                        meshData.vertices[meshData.verticesCount++] = srcVertices[(faceIndex * 4) + j] + origin;
                        meshData.normals[meshData.normalsCount++] = srcNormals[faceIndex];
                        //Vertex color
                        meshData.colors[meshData.colorsCount++] = Color.white;
                    }

                    //UV gen
                    for (int j = 0; j < 4; j++)
                    {
                        Vector2 uv = srcUvs[(faceIndex * 4) + j];

                        uv.x = uv.x * uvRect.width + uvRect.xMin;
                        uv.y = uv.y * uvRect.height + uvRect.yMin;

                        meshData.uvs[meshData.uvsCount++] = uv;
                    }

           
                    //Faces
                    for (int j = 0; j < srcFaces[faceIndex].Length; j++)
                    {
                        subMesh.triangles.Add(srcFaces[faceIndex][j] + vertexCount);
                    }
                }
            }
            CreateUnityMeshFromTemporayMeshData(theMesh, meshRenderer, meshData, null);

            //If a material is using TRIPLANAR_STYLE_WORLD, switch it to local
            //because this object is going to move around!
            foreach (Material mat in meshRenderer.sharedMaterials)
            {
                if (mat.IsKeywordEnabled("TRIPLANAR_STYLE_WORLD"))
                {
                    mat.DisableKeyword("TRIPLANAR_STYLE_WORLD");
                    mat.EnableKeyword("TRIPLANAR_STYLE_LOCAL");
                }
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
    }
}