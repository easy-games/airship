
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

using VoxelData = System.UInt16;
using BlockId = System.UInt16;
using Assets.Airship.VoxelRenderer;
using UnityEngine.Profiling;
using System.Diagnostics;

namespace VoxelWorldStuff
{

    public partial class MeshProcessor
    {
        private static bool NeedsCapSurface(VoxelData data, VoxelBlocks.BlockDefinition block)
        {
            return true;
        }
        
        private static bool QuarterBlocksPlaceBlock(VoxelBlocks.BlockDefinition block, int localVoxelKey, VoxelData[] readOnlyVoxel, TemporaryMeshData temporaryMeshData, VoxelWorld world, Vector3 origin)
        {
            //get surrounding data
            VoxelData voxUp = readOnlyVoxel[localVoxelKey + paddedChunkSize];
            VoxelData voxDown = readOnlyVoxel[localVoxelKey - paddedChunkSize];
            VoxelData voxLeft = readOnlyVoxel[localVoxelKey - 1];
            VoxelData voxRight = readOnlyVoxel[localVoxelKey + 1];
            VoxelData voxForward = readOnlyVoxel[localVoxelKey + (paddedChunkSize * paddedChunkSize)];
            VoxelData voxBack = readOnlyVoxel[localVoxelKey - (paddedChunkSize * paddedChunkSize)];

            VoxelData voxForwardRight = readOnlyVoxel[localVoxelKey + 1 + (paddedChunkSize * paddedChunkSize)];
            VoxelData voxForwardLeft = readOnlyVoxel[localVoxelKey - 1 + (paddedChunkSize * paddedChunkSize)];
            VoxelData voxBackRight = readOnlyVoxel[localVoxelKey + 1 - (paddedChunkSize * paddedChunkSize)];
            VoxelData voxBackLeft = readOnlyVoxel[localVoxelKey - 1 - (paddedChunkSize * paddedChunkSize)];

            VoxelData voxDownForwardRight = readOnlyVoxel[localVoxelKey + 1 + (paddedChunkSize * paddedChunkSize) - paddedChunkSize];
            VoxelData voxDownForwardLeft = readOnlyVoxel[localVoxelKey - 1 + (paddedChunkSize * paddedChunkSize) - paddedChunkSize];
            VoxelData voxDownBackRight = readOnlyVoxel[localVoxelKey + 1 - (paddedChunkSize * paddedChunkSize) - paddedChunkSize];
            VoxelData voxDownBackLeft = readOnlyVoxel[localVoxelKey - 1 - (paddedChunkSize * paddedChunkSize) - paddedChunkSize];
          
            
            VoxelData voxUpForwardRight = readOnlyVoxel[localVoxelKey + 1 + (paddedChunkSize * paddedChunkSize) + paddedChunkSize];
            VoxelData voxUpForwardLeft = readOnlyVoxel[localVoxelKey - 1 + (paddedChunkSize * paddedChunkSize) + paddedChunkSize];
            VoxelData voxUpBackRight = readOnlyVoxel[localVoxelKey + 1 - (paddedChunkSize * paddedChunkSize) + paddedChunkSize];
            VoxelData voxUpBackLeft = readOnlyVoxel[localVoxelKey - 1 - (paddedChunkSize * paddedChunkSize) + paddedChunkSize];
                    
            VoxelData voxDownForward = readOnlyVoxel[localVoxelKey - paddedChunkSize + (paddedChunkSize * paddedChunkSize)];
            VoxelData voxDownBack = readOnlyVoxel[localVoxelKey - paddedChunkSize - (paddedChunkSize * paddedChunkSize)];
            VoxelData voxDownLeft = readOnlyVoxel[localVoxelKey - paddedChunkSize - 1];
            VoxelData voxDownRight = readOnlyVoxel[localVoxelKey - paddedChunkSize + 1];

            VoxelData voxUpForward = readOnlyVoxel[localVoxelKey + paddedChunkSize + (paddedChunkSize * paddedChunkSize)];
            VoxelData voxUpBack = readOnlyVoxel[localVoxelKey + paddedChunkSize - (paddedChunkSize * paddedChunkSize)];
            VoxelData voxUpLeft = readOnlyVoxel[localVoxelKey + paddedChunkSize - 1];
            VoxelData voxUpRight = readOnlyVoxel[localVoxelKey + paddedChunkSize + 1];

            //Cardinals
            bool airUp = (block.blockId != VoxelWorld.VoxelDataToBlockId(voxUp));
            bool airDown = (block.blockId != VoxelWorld.VoxelDataToBlockId(voxDown));
            bool airLeft = (block.blockId != VoxelWorld.VoxelDataToBlockId(voxLeft));
            bool airForward = (block.blockId != VoxelWorld.VoxelDataToBlockId(voxForward));
            bool airRight = (block.blockId != VoxelWorld.VoxelDataToBlockId(voxRight));
            bool airBack = (block.blockId != VoxelWorld.VoxelDataToBlockId(voxBack));

            //diagonals
            bool airForwardLeft = block.blockId != VoxelWorld.VoxelDataToBlockId(voxForwardLeft);
            bool airForwardRight = block.blockId != VoxelWorld.VoxelDataToBlockId(voxForwardRight);
            bool airBackLeft = block.blockId != VoxelWorld.VoxelDataToBlockId(voxBackLeft);
            bool airBackRight = block.blockId != VoxelWorld.VoxelDataToBlockId(voxBackRight);
            
            //downs
            bool airDownForward = block.blockId != VoxelWorld.VoxelDataToBlockId(voxDownForward);
            bool airDownBack =  block.blockId != VoxelWorld.VoxelDataToBlockId(voxDownBack);
            bool airDownLeft =  block.blockId != VoxelWorld.VoxelDataToBlockId(voxDownLeft);
            bool airDownRight =  block.blockId != VoxelWorld.VoxelDataToBlockId(voxDownRight);

            bool airDownForwardRight = block.blockId != VoxelWorld.VoxelDataToBlockId(voxDownForwardRight);
            bool airDownForwardLeft = block.blockId != VoxelWorld.VoxelDataToBlockId(voxDownForwardLeft);
            bool airDownBackRight =  block.blockId != VoxelWorld.VoxelDataToBlockId(voxDownBackRight);
            bool airDownBackLeft = block.blockId != VoxelWorld.VoxelDataToBlockId(voxDownBackLeft);

            //Ups
            bool airUpForward = block.blockId != VoxelWorld.VoxelDataToBlockId(voxUpForward);
            bool airUpBack = block.blockId != VoxelWorld.VoxelDataToBlockId(voxUpBack);
            bool airUpLeft = block.blockId != VoxelWorld.VoxelDataToBlockId(voxUpLeft);
            bool airUpRight = block.blockId != VoxelWorld.VoxelDataToBlockId(voxUpRight);

            bool airUpForwardRight = block.blockId != VoxelWorld.VoxelDataToBlockId(voxUpForwardRight);
            bool airUpForwardLeft = block.blockId != VoxelWorld.VoxelDataToBlockId(voxUpForwardLeft);
            bool airUpBackRight = block.blockId != VoxelWorld.VoxelDataToBlockId(voxUpBackRight);
            bool airUpBackLeft = block.blockId != VoxelWorld.VoxelDataToBlockId(voxUpBackLeft);

            // do the vertical flat faces ///////////////////////////////////////////////////////////////////////////////////////////////
            //Flat Left - lots of neighbors
            if (airLeft)
            {
                if (!airUp)
                {
                    if (!airBack)
                    {
                        //Wait we might need to curl in here
                        if (airUpBack)
                        {
                            //Is curled in
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UI], temporaryMeshData, world, origin, true, 3);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UA], temporaryMeshData, world, origin, true, 3);
                        }

                    }
                    if (!airForward)
                    {
                        //Wait we might need to curl in here
                        if (airUpForward)
                        {
                            //Is curled in
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UJ], temporaryMeshData, world, origin, true, 3);
                        }
                        else
                        {
                            //Is flat
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UB], temporaryMeshData, world, origin, true, 3);
                        }

                    }
                }
                if (!airDown)
                {
                    if (!airBack)
                    {
                        if (airDownBack)
                        {
                            //Is curled in
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DI], temporaryMeshData, world, origin, true, 3);
                        }
                        else
                        {
                            //Is flat
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DA], temporaryMeshData, world, origin, true, 3);
                        }
                    }
                    if (!airForward)
                    {
                        if (airDownForward)
                        {
                            //Is curled in
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DJ], temporaryMeshData, world, origin, true, 3);
                        }
                        else
                        {
                            //Is flat
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DB], temporaryMeshData, world, origin, true, 3);
                        }
                    }
                }
            }

            //Flat Right - lots of neighbors
            if (airRight)
            {
                if (!airUp)
                {
                    if (!airBack)
                    {
                        //Wait we might need to curl in here
                        if (airUpBack)
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UJ], temporaryMeshData, world, origin, true, 1);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UB], temporaryMeshData, world, origin, true, 1);
                        }
                    }
                    if (!airForward)
                    {
                        //Wait we might need to curl in here
                        if (airUpForward)
                        {
                            //Is curled in
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UI], temporaryMeshData, world, origin, true, 1);
                        }
                        else
                        {
                            //Is flat
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UA], temporaryMeshData, world, origin, true, 1);
                        }
                    }


                }
                if (!airDown)
                {
                    if (!airBack)
                    {
                        if (airDownBack)
                        {
                            //Is curled in
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DJ], temporaryMeshData, world, origin, true, 1);
                        }
                        else
                        {
                            //Is flat
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DB], temporaryMeshData, world, origin, true, 1);
                        }
                    }
                    if (!airForward)
                    {
                        if (airDownForward)
                        {
                            //Is curled in
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DI], temporaryMeshData, world, origin, true, 1);
                        }
                        else
                        {
                            //Is flat
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DA], temporaryMeshData, world, origin, true, 1);
                        }
                    }
                }
            }


            //Flat back
            if (airBack)
            {
                if (!airUp)
                {
                    if (!airLeft)
                    {
                        if (airUpLeft)
                        {
                            //Is curled in
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UJ], temporaryMeshData, world, origin, true, 2);
                        }
                        else
                        {
                            //Is flat
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UB], temporaryMeshData, world, origin, true, 2);
                        }
                    }
                    if (!airRight)
                    {
                        if (airUpRight)
                        {
                            //Is curled in
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UI], temporaryMeshData, world, origin, true, 2);
                        }
                        else
                        {
                            //Is flat
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UA], temporaryMeshData, world, origin, true, 2);
                        }
                    }
                }
                if (!airDown)
                {
                    if (!airLeft)
                    {
                        if (airDownLeft)
                        {
                            //Curled
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DJ], temporaryMeshData, world, origin, true, 2);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DB], temporaryMeshData, world, origin, true, 2);
                        }
                    }
                    if (!airRight)
                    {
                        if (airDownRight)
                        {
                            //Curled
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DI], temporaryMeshData, world, origin, true, 2);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DA], temporaryMeshData, world, origin, true, 2);
                        }

                    }
                }
            }

            //Flat forward
            if (airForward)
            {
                if (!airUp)
                {
                    if (!airLeft)
                    {
                        if (airUpLeft)
                        {
                            //Is curled in
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UI], temporaryMeshData, world, origin, true, 0);
                        }
                        else
                        {
                            //Is flat
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UA], temporaryMeshData, world, origin, true, 0);
                        }

                    }
                    if (!airRight)
                    {
                        if (airUpRight)
                        {
                            //Is curled in
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UJ], temporaryMeshData, world, origin, true, 0);
                        }
                        else
                        {
                            //Is flat
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UB], temporaryMeshData, world, origin, true, 0);
                        }
                    }
                }
                if (!airDown)
                {
                    if (!airLeft)
                    {
                        if (airDownLeft)
                        {
                            //Curled
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DI], temporaryMeshData, world, origin, true, 0);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DA], temporaryMeshData, world, origin, true, 0);
                        }
                    }
                    if (!airRight)
                    {
                        if (airDownRight)
                        {
                            //Curled
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DJ], temporaryMeshData, world, origin, true, 0);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DB], temporaryMeshData, world, origin, true, 0);
                        }

                    }
                }
            }


            //Do the corners /////////////////////////////////////////////////////////////////////////
            if (airLeft && airForward)
            {
                if (airUp)
                {
                    //Corner
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UG], temporaryMeshData, world, origin, true, 0);
                }
                else
                {
                    if (!airUpForwardLeft && !airUpForward && !airUpLeft)
                    {
                        //solid corner
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UL], temporaryMeshData, world, origin, true, 0);
                    }
                    else
                    {
                        //Vertical side
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UD], temporaryMeshData, world, origin, true, 0);
                    }
                }

                if (airDown)
                {
                    //Corner
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DG], temporaryMeshData, world, origin, true, 0);
                }
                else
                {

                    //Wait - we might be ploughing into an impossible connection, make a solid corner instead
                    if (!airDownForwardLeft && !airDownForward && !airDownLeft)
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DL], temporaryMeshData, world, origin, true, 0);
                    }
                    else
                    {
                        //Vertical side
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DD], temporaryMeshData, world, origin, true, 0);

                    }

                }
            }


            if (airRight && airForward)
            {
                if (airUp)
                {
                    //Corner
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UG], temporaryMeshData, world, origin, true, 1);
                }
                else
                {
                    //Vertical side
                    if (!airUpForwardRight && !airUpForward && !airUpRight)
                    {
                        //solid corner
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UL], temporaryMeshData, world, origin, true, 1);
                    }
                    else
                    {
                        //Vertical side
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UD], temporaryMeshData, world, origin, true, 1);
                    }

                }

                if (airDown)
                {
                    //Corner
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DG], temporaryMeshData, world, origin, true, 1);
                }
                else
                {
                    //Wait - we might be ploughing into an impossible connection, make a solid corner instead
                    if (!airDownForwardRight && !airDownForward && !airDownRight)
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DL], temporaryMeshData, world, origin, true, 1);
                    }
                    else
                    {
                        //Vertical side
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DD], temporaryMeshData, world, origin, true, 1);
                    }
                }
            }

            if (airLeft && airBack)
            {
                if (airUp)
                {
                    //Corner
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UG], temporaryMeshData, world, origin, true, 3);
                }
                else
                {
                    if (!airUpBackLeft && !airUpBack && !airUpLeft)
                    {
                        //solid corner
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UL], temporaryMeshData, world, origin, true, 3);
                    }
                    else
                    {
                        //Vertical side
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UD], temporaryMeshData, world, origin, true, 3);
                    }
                }

                if (airDown)
                {
                    //Corner
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DG], temporaryMeshData, world, origin, true, 3);
                }
                else
                {
                    //Wait - we might be ploughing into an impossible connection, make a solid corner instead
                    if (!airDownBackLeft && !airDownBack && !airDownLeft)
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DL], temporaryMeshData, world, origin, true, 3);
                    }
                    else
                    {
                        //Vertical side
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DD], temporaryMeshData, world, origin, true, 3);
                    }
                }
            }

            if (airRight && airBack)
            {
                if (airUp)
                {
                    //Corner
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UG], temporaryMeshData, world, origin, true, 2);
                }
                else
                {
                    //Vertical side
                    if (!airUpBackRight && !airUpBack && !airUpRight)
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UL], temporaryMeshData, world, origin, true, 2);
                    }
                    else
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UD], temporaryMeshData, world, origin, true, 2);
                    }
                }

                if (airDown)
                {
                    //Corner
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DG], temporaryMeshData, world, origin, true, 2);
                }
                else
                {
                    //Wait - we might be ploughing into an impossible connection, make a solid corner instead
                    if (!airDownBackRight && !airDownBack && !airDownRight)
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DL], temporaryMeshData, world, origin, true, 2);
                    }
                    else
                    {
                        //Vertical side
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DD], temporaryMeshData, world, origin, true, 2);
                    }
                }
            }


            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            //do the top/bot edges
            if (airLeft)
            {
                if (airUp)
                {
                    //Connection along a top edge for foward and backward
                    if (!airBack)
                    {
                        //Top Edge
                        if (!airUpBackLeft && !airUpBack && !airBackLeft) 
                        {
                            //Connected onto solid
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UM], temporaryMeshData, world, origin, true, 3);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UE], temporaryMeshData, world, origin, true, 3);
                        }
                    }
                    if (!airForward)
                    {
                        //Top Edge
                        if (!airUpForwardLeft && !airUpForward && !airForwardLeft)
                        {
                            //Connected onto solid
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UN], temporaryMeshData, world, origin, true, 3);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UF], temporaryMeshData, world, origin, true, 3);
                        }
                    }

                }

                if (airDown)
                {
                    if (!airBack)
                    {
                        //Bot Edge
                        if (!airDownBackLeft && !airDownBack && !airBackLeft)
                        {
                            //Connected onto solid
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DM], temporaryMeshData, world, origin, true, 3);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DE], temporaryMeshData, world, origin, true, 3);
                        }
                    }
                    if (!airForward)
                    {
                        //Bot Edge
                        if (!airDownForwardLeft && !airDownForward && !airForwardLeft)
                        {
                            //Connected onto solid
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DN], temporaryMeshData, world, origin, true, 3);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DF], temporaryMeshData, world, origin, true, 3);
                        }
                    }

                }
            }
            if (airRight)
            {
                if (airUp)
                {
                    if (!airBack)
                    {
                        //Top Edge
                        if (!airUpBackRight && !airUpBack && !airBackRight)
                        {
                            //Connected onto solid
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UN], temporaryMeshData, world, origin, true, 1);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UF], temporaryMeshData, world, origin, true, 1);
                        }
                    }
                    if (!airForward)
                    {
                        //Top Edge
                        if (!airUpForwardRight && !airUpForward && !airForwardRight)
                        {
                            //Connected onto solid
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UM], temporaryMeshData, world, origin, true, 1);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UE], temporaryMeshData, world, origin, true, 1);
                        }
                        
                    }

                }

                if (airDown)
                {
                    if (!airBack)
                    {
                        //Bot Edge
                        if (!airDownBackRight && !airDownBack && !airBackRight)
                        {
                            //Connected onto solid
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DN], temporaryMeshData, world, origin, true, 1);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DF], temporaryMeshData, world, origin, true, 1);
                        }
                        
                    }
                    if (!airForward)
                    {
                        //Bot Edge
                        if (!airDownForwardRight && !airDownForward && !airForwardRight)
                        {
                            //Connected onto solid
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DM], temporaryMeshData, world, origin, true, 1);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DE], temporaryMeshData, world, origin, true, 1);
                        }
                        
                    }

                }
            }

            //do the top/bot edges
            if (airForward)
            {
                if (airUp)
                {
                    if (!airLeft)
                    {
                        //Top Edge
                        if (!airUpForwardLeft && !airUpLeft && !airForwardLeft)
                        {
                            //Connected onto solid
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UM], temporaryMeshData, world, origin, true, 0);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UE], temporaryMeshData, world, origin, true, 0);
                        }
                        
                    }
                    if (!airRight)
                    {
                        //Top Edge
                        if (!airUpForwardRight && !airUpRight && !airForwardRight)
                        {
                            //Connected onto solid
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UN], temporaryMeshData, world, origin, true, 0);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UF], temporaryMeshData, world, origin, true, 0);
                        }
                        
                    }

                }

                if (airDown)
                {
                    if (!airLeft)
                    {
                        //Bot Edge
                        if (!airDownForwardLeft && !airDownLeft && !airForwardLeft)
                        {
                            //Connected onto solid
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DM], temporaryMeshData, world, origin, true, 0);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DE], temporaryMeshData, world, origin, true, 0);
                        }
                        
                    }
                    if (!airRight)
                    {
                        //Bot Edge
                        if (!airDownForwardRight && !airDownRight && !airForwardRight)
                        {
                            //Connected onto solid
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DN], temporaryMeshData, world, origin, true, 0);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DF], temporaryMeshData, world, origin, true, 0);
                        }
                        
                    }

                }
            }
            if (airBack)
            {
                if (airUp)
                {
                    if (!airLeft)
                    {
                        //Top Edge
                        if (!airUpBackLeft && !airUpLeft && !airBackLeft)
                        {
                            //Connected onto solid
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UN], temporaryMeshData, world, origin, true, 2);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UF], temporaryMeshData, world, origin, true, 2);
                        }
                        
                    }
                    if (!airRight)
                    {
                        //Top Edge
                        if (!airUpBackRight && !airUpRight && !airBackRight)
                        {
                            //Connected onto solid
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UM], temporaryMeshData, world, origin, true, 2);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UE], temporaryMeshData, world, origin, true, 2);
                        }
                    }

                }

                if (airDown)
                {
                    if (!airLeft)
                    {
                        //Bot Edge
                        if (!airDownBackLeft && !airDownLeft && !airBackLeft)
                        {
                            //Connected onto solid
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DN], temporaryMeshData, world, origin, true, 2);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DF], temporaryMeshData, world, origin, true, 2);
                        }
                        
                    }
                    if (!airRight)
                    {
                        //Bot Edge
                        if (!airDownBackRight && !airDownRight && !airBackRight)
                        {
                            //Connected onto solid
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DM], temporaryMeshData, world, origin, true, 2);
                        }
                        else
                        {
                            EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DE], temporaryMeshData, world, origin, true, 2);
                        }
                        
                    }

                }
            }

            //////////////////////////////////////////////////////////////////////////////////////////////////////
            //Do top+Bottom surfaces
            if (airUp)
            {
                if (!airForward && !airLeft)
                {
                    //Top Surface

                    //Wait, it might have air on the diagonal, so we should use the other type of top corner then
                    if (airForwardLeft)
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UH], temporaryMeshData, world, origin, true, 0);
                    }
                    else
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UC], temporaryMeshData, world, origin, true, 0);
                    }

                }
                if (!airForward && !airRight)
                {
                    //Top Surface
                    if (airForwardRight)
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UH], temporaryMeshData, world, origin, true, 1);
                    }
                    else
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UC], temporaryMeshData, world, origin, true, 1);
                    }
                }
                if (!airBack && !airLeft)
                {
                    //Top Surface
                    if (airBackLeft)
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UH], temporaryMeshData, world, origin, true, 3);
                    }
                    else
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UC], temporaryMeshData, world, origin, true, 3);
                    }
                }
                if (!airBack && !airRight)
                {
                    //Top Surface
                    if (airBackRight)
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UH], temporaryMeshData, world, origin, true, 2);
                    }
                    else
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UC], temporaryMeshData, world, origin, true, 2);
                    }

                }
            }
            if (airDown)
            {
                if (!airForward && !airLeft)
                {
                    //Bot Surface
                    if (airForwardLeft)
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DH], temporaryMeshData, world, origin, true, 0);
                    }
                    else
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DC], temporaryMeshData, world, origin, true, 0);
                    }
                }
                if (!airForward && !airRight)
                {
                    //Bot Surface
                    if (airForwardRight)
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DH], temporaryMeshData, world, origin, true, 1);
                    }
                    else
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DC], temporaryMeshData, world, origin, true, 1);
                    }
                }
                if (!airBack && !airLeft)
                {
                    //Bot Surface
                    if (airBackLeft)
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DH], temporaryMeshData, world, origin, true, 3);
                    }
                    else
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DC], temporaryMeshData, world, origin, true, 3);
                    }
                }
                if (!airBack && !airRight)
                {
                    //Bot Surface
                    if (airBackRight)
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DH], temporaryMeshData, world, origin, true, 2);
                    }
                    else
                    {
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DC], temporaryMeshData, world, origin, true, 2);
                    }
                }
            }
            /////////////////////////////////////////////////////////////////////////////////////////////

            //Tri Staaaaar

            if (airLeft && airForward && airDownForwardLeft)
            {
                VoxelData voxLeftDown = readOnlyVoxel[localVoxelKey - paddedChunkSize - 1];
                bool airLeftDown = ( block.blockId != VoxelWorld.VoxelDataToBlockId(voxLeftDown));
                VoxelData voxForwardDown = readOnlyVoxel[localVoxelKey - paddedChunkSize + (paddedChunkSize * paddedChunkSize)];
                bool airForwardDown = (  block.blockId != VoxelWorld.VoxelDataToBlockId(voxForwardDown));

                if (!airLeftDown && !airForwardDown)
                {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UK], temporaryMeshData, world, origin + new Vector3(0, -1, 0), true, 0);
                }
            }

            //Do the other 3
            if (airRight && airForward && airDownForwardRight)
            {
                if (!airDownRight && !airDownForward)
                {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UK], temporaryMeshData, world, origin + new Vector3(0, -1, 0), true, 1);
                }
            }
            if (airLeft && airBack && airDownBackLeft)
            {
                if (!airDownLeft && !airDownBack)
                {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UK], temporaryMeshData, world, origin + new Vector3(0, -1, 0), true, 3);
                }
            }
            if (airRight && airBack && airDownBackRight)
            {
                if (!airDownRight && !airDownBack)
                {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UK], temporaryMeshData, world, origin + new Vector3(0, -1, 0), true, 2);
                }
            }
            //Do the upwards ones now
            if (airLeft && airForward && airUpForwardLeft)
            {
                if (!airUpLeft && !airUpForward)
                {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DK], temporaryMeshData, world, origin + new Vector3(0, 1, 0), true, 0);
                }
            }
            if (airRight && airForward && airUpForwardRight)
            {
                if (!airUpRight && !airUpForward)
                {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DK], temporaryMeshData, world, origin + new Vector3(0, 1, 0), true, 1);
                }
            }
            if (airLeft && airBack && airUpBackLeft)
            {
                if (!airUpLeft && !airUpBack)
                {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DK], temporaryMeshData, world, origin + new Vector3(0, 1, 0), true, 3);
                }
            }
            if (airRight && airBack && airUpBackRight)
            {
                if (!airUpRight && !airUpBack)
                {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DK], temporaryMeshData, world, origin + new Vector3(0, 1, 0), true, 2);
                }
            }
            return true;
        }
    }
}