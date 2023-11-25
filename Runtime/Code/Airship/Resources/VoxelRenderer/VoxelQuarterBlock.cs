
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


            bool airUp = (!VoxelWorld.VoxelIsSolid(voxUp) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxUp));
            bool airDown = (!VoxelWorld.VoxelIsSolid(voxDown) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxDown));
            bool airLeft = (!VoxelWorld.VoxelIsSolid(voxLeft) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxLeft));
            bool airForward = (!VoxelWorld.VoxelIsSolid(voxForward) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxForward));
            bool airRight = (!VoxelWorld.VoxelIsSolid(voxRight) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxRight));
            bool airBack = (!VoxelWorld.VoxelIsSolid(voxBack) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxBack));

            //diagonals
            bool airForwardLeft = !VoxelWorld.VoxelIsSolid(voxForwardLeft) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxForwardLeft);
            bool airForwardRight = !VoxelWorld.VoxelIsSolid(voxForwardRight) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxForwardRight);
            bool airBackLeft = !VoxelWorld.VoxelIsSolid(voxBackLeft) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxBackLeft);
            bool airBackRight = !VoxelWorld.VoxelIsSolid(voxBackRight) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxBackRight);
            
            //downs
            bool airDownForward = !VoxelWorld.VoxelIsSolid(voxDownForward) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxDownForward);
            bool airDownBack = !VoxelWorld.VoxelIsSolid(voxDownBack) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxDownBack);
            bool airDownLeft = !VoxelWorld.VoxelIsSolid(voxDownLeft) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxDownLeft);
            bool airDownRight = !VoxelWorld.VoxelIsSolid(voxDownRight) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxDownRight);

            bool airDownForwardRight = !VoxelWorld.VoxelIsSolid(voxDownForwardRight) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxDownForwardRight);
            bool airDownForwardLeft = !VoxelWorld.VoxelIsSolid(voxDownForwardLeft) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxDownForwardLeft);
            bool airDownBackRight = !VoxelWorld.VoxelIsSolid(voxDownBackRight) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxDownBackRight);
            bool airDownBackLeft = !VoxelWorld.VoxelIsSolid(voxDownBackLeft) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxDownBackLeft);

            //Ups
            bool airUpForward = !VoxelWorld.VoxelIsSolid(voxUpForward) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxUpForward);
            bool airUpBack = !VoxelWorld.VoxelIsSolid(voxUpBack) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxUpBack);
            bool airUpLeft = !VoxelWorld.VoxelIsSolid(voxUpLeft) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxUpLeft);
            bool airUpRight = !VoxelWorld.VoxelIsSolid(voxUpRight) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxUpRight);

            bool airUpForwardRight = !VoxelWorld.VoxelIsSolid(voxUpForwardRight) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxUpForwardRight);
            bool airUpForwardLeft = !VoxelWorld.VoxelIsSolid(voxUpForwardLeft) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxUpForwardLeft);
            bool airUpBackRight = !VoxelWorld.VoxelIsSolid(voxUpBackRight) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxUpBackRight);
            bool airUpBackLeft = !VoxelWorld.VoxelIsSolid(voxUpBackLeft) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxUpBackLeft);

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
                    //Vertical side
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UD], temporaryMeshData, world, origin, true, 0);
                }

                if (airDown)
                {
                    //Corner
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DG], temporaryMeshData, world, origin, true, 0);
                }
                else
                {

                    //Wait - we might be ploughing into an impossible connection, make a solid corner instead
                    if (!VoxelWorld.VoxelIsSolid(voxDown) && !airDownForwardLeft && !airDownForward && !airDownLeft)
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
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UD], temporaryMeshData, world, origin, true, 1);
                }

                if (airDown)
                {
                    //Corner
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DG], temporaryMeshData, world, origin, true, 1);
                }
                else
                {
                    //Wait - we might be ploughing into an impossible connection, make a solid corner instead
                    if (!VoxelWorld.VoxelIsSolid(voxDown) && !airDownForwardRight && !airDownForward && !airDownRight)
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

                    //Vertical side
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UD], temporaryMeshData, world, origin, true, 3);
                }

                if (airDown)
                {
                    //Corner
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DG], temporaryMeshData, world, origin, true, 3);
                }
                else
                {
                    //Wait - we might be ploughing into an impossible connection, make a solid corner instead
                    if (!VoxelWorld.VoxelIsSolid(voxDown) && !airDownBackLeft && !airDownBack && !airDownLeft)
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
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UD], temporaryMeshData, world, origin, true, 2);
                }

                if (airDown)
                {
                    //Corner
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DG], temporaryMeshData, world, origin, true, 2);
                }
                else
                {
                    //Wait - we might be ploughing into an impossible connection, make a solid corner instead
                    if (!VoxelWorld.VoxelIsSolid(voxDown) && !airDownBackRight && !airDownBack && !airDownRight)
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
                        if (!VoxelWorld.VoxelIsSolid(voxBack) && !airUpBackLeft && !airUpBack && !airBackLeft) 
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
                        if (!VoxelWorld.VoxelIsSolid(voxForward) && !airUpForwardLeft && !airUpForward && !airForwardLeft)
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
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DE], temporaryMeshData, world, origin, true, 3);
                    }
                    if (!airForward)
                    {
                        //Bot Edge
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DF], temporaryMeshData, world, origin, true, 3);
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
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UF], temporaryMeshData, world, origin, true, 1);
                    }
                    if (!airForward)
                    {
                        //Top Edge
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UE], temporaryMeshData, world, origin, true, 1);
                    }

                }

                if (airDown)
                {
                    if (!airBack)
                    {
                        //Bot Edge
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DF], temporaryMeshData, world, origin, true, 1);
                    }
                    if (!airForward)
                    {
                        //Bot Edge
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DE], temporaryMeshData, world, origin, true, 1);
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
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UE], temporaryMeshData, world, origin, true, 0);
                    }
                    if (!airRight)
                    {
                        //Top Edge
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UF], temporaryMeshData, world, origin, true, 0);
                    }

                }

                if (airDown)
                {
                    if (!airLeft)
                    {
                        //Bot Edge
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DE], temporaryMeshData, world, origin, true, 0);
                    }
                    if (!airRight)
                    {
                        //Bot Edge
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DF], temporaryMeshData, world, origin, true, 0);
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
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UF], temporaryMeshData, world, origin, true, 2);
                    }
                    if (!airRight)
                    {
                        //Top Edge
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UE], temporaryMeshData, world, origin, true, 2);
                    }

                }

                if (airDown)
                {
                    if (!airLeft)
                    {
                        //Bot Edge
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DF], temporaryMeshData, world, origin, true, 2);
                    }
                    if (!airRight)
                    {
                        //Bot Edge
                        EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DE], temporaryMeshData, world, origin, true, 2);
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
                bool airLeftDown = (!VoxelWorld.VoxelIsSolid(voxLeftDown) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxLeftDown));
                VoxelData voxForwardDown = readOnlyVoxel[localVoxelKey - paddedChunkSize + (paddedChunkSize * paddedChunkSize)];
                bool airForwardDown = (!VoxelWorld.VoxelIsSolid(voxForwardDown) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxForwardDown));

                if (!airLeftDown && !airForwardDown)
                {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UK], temporaryMeshData, world, origin + new Vector3(0, -1, 0), true, 0);
                }
            }

            //Do the other 3
            if (airRight && airForward && airDownForwardRight)
            {
                VoxelData voxRightDown = readOnlyVoxel[localVoxelKey - paddedChunkSize + 1];
                bool airRightDown = (!VoxelWorld.VoxelIsSolid(voxRightDown) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxRightDown));
                VoxelData voxForwardDown = readOnlyVoxel[localVoxelKey - paddedChunkSize + (paddedChunkSize * paddedChunkSize)];
                bool airForwardDown = (!VoxelWorld.VoxelIsSolid(voxForwardDown) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxForwardDown));
                if (!airRightDown && !airForwardDown)
                {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UK], temporaryMeshData, world, origin + new Vector3(0, -1, 0), true, 1);
                }
            }
            if (airLeft && airBack && airDownBackLeft)
            {
                VoxelData voxLeftDown = readOnlyVoxel[localVoxelKey - paddedChunkSize - 1];
                bool airLeftDown = (!VoxelWorld.VoxelIsSolid(voxLeftDown) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxLeftDown));
                VoxelData voxBackDown = readOnlyVoxel[localVoxelKey - paddedChunkSize - (paddedChunkSize * paddedChunkSize)];
                bool airBackDown = (!VoxelWorld.VoxelIsSolid(voxBackDown) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxBackDown));
                if (!airLeftDown && !airBackDown)
                {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UK], temporaryMeshData, world, origin + new Vector3(0, -1, 0), true, 3);
                }
            }
            if (airRight && airBack && airDownBackRight)
            {
                VoxelData voxRightDown = readOnlyVoxel[localVoxelKey - paddedChunkSize + 1];
                bool airRightDown = (!VoxelWorld.VoxelIsSolid(voxRightDown) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxRightDown));
                VoxelData voxBackDown = readOnlyVoxel[localVoxelKey - paddedChunkSize - (paddedChunkSize * paddedChunkSize)];
                bool airBackDown = (!VoxelWorld.VoxelIsSolid(voxBackDown) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxBackDown));
                if (!airRightDown && !airBackDown)
                {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.UK], temporaryMeshData, world, origin + new Vector3(0, -1, 0), true, 2);
                }
            }
            //Do the upwards ones now
            if (airLeft && airForward && airUpForwardLeft)
            {
                VoxelData voxLeftUp = readOnlyVoxel[localVoxelKey + paddedChunkSize - 1];
                bool airLeftUp = (!VoxelWorld.VoxelIsSolid(voxLeftUp) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxLeftUp));
                VoxelData voxForwardUp = readOnlyVoxel[localVoxelKey + paddedChunkSize + (paddedChunkSize * paddedChunkSize)];
                bool airForwardUp = (!VoxelWorld.VoxelIsSolid(voxForwardUp) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxForwardUp));
                if (!airLeftUp && !airForwardUp)
                {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DK], temporaryMeshData, world, origin + new Vector3(0, 1, 0), true, 0);
                }
            }
            if (airRight && airForward && airUpForwardRight)
            {
                VoxelData voxRightUp = readOnlyVoxel[localVoxelKey + paddedChunkSize + 1];
                bool airRightUp = (!VoxelWorld.VoxelIsSolid(voxRightUp) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxRightUp));
                VoxelData voxForwardUp = readOnlyVoxel[localVoxelKey + paddedChunkSize + (paddedChunkSize * paddedChunkSize)];
                bool airForwardUp = (!VoxelWorld.VoxelIsSolid(voxForwardUp) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxForwardUp));
                if (!airRightUp && !airForwardUp)
                {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DK], temporaryMeshData, world, origin + new Vector3(0, 1, 0), true, 1);
                }
            }
            if (airLeft && airBack && airUpBackLeft)
            {
                VoxelData voxLeftUp = readOnlyVoxel[localVoxelKey + paddedChunkSize - 1];
                bool airLeftUp = (!VoxelWorld.VoxelIsSolid(voxLeftUp) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxLeftUp));
                VoxelData voxBackUp = readOnlyVoxel[localVoxelKey + paddedChunkSize - (paddedChunkSize * paddedChunkSize)];
                bool airBackUp = (!VoxelWorld.VoxelIsSolid(voxBackUp) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxBackUp));
                if (!airLeftUp && !airBackUp)
                {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DK], temporaryMeshData, world, origin + new Vector3(0, 1, 0), true, 3);
                }
            }
            if (airRight && airBack && airUpBackRight)
            {
                VoxelData voxRightUp = readOnlyVoxel[localVoxelKey + paddedChunkSize + 1];
                bool airRightUp = (!VoxelWorld.VoxelIsSolid(voxRightUp) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxRightUp));
                VoxelData voxBackUp = readOnlyVoxel[localVoxelKey + paddedChunkSize - (paddedChunkSize * paddedChunkSize)];
                bool airBackUp = (!VoxelWorld.VoxelIsSolid(voxBackUp) && block.blockId != VoxelWorld.VoxelDataToBlockId(voxBackUp));
                if (!airRightUp && !airBackUp)
                {
                    EmitMesh(block, block.meshContexts[(int)VoxelBlocks.QuarterBlockTypes.DK], temporaryMeshData, world, origin + new Vector3(0, 1, 0), true, 2);
                }
            }


            return true;
        }
    }
}