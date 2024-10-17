using UnityEngine;

using VoxelData = System.UInt16;

namespace VoxelWorldStuff {

    public partial class MeshProcessor {
        private static bool NeedsCapSurface(VoxelData data, VoxelBlocks.BlockDefinition block) {
            if (VoxelWorld.VoxelIsSolid(data)) {
                return false;
            }
            return true;
        }

        //True = we generate surfaces between us and this block
        //false = we dont
        private static bool SurfaceFunction(VoxelBlocks.BlockDefinition block, VoxelData data) {
            int blockId = VoxelWorld.VoxelDataToBlockId(data);
            if (blockId == 0) return true;

            if (VoxelWorld.VoxelIsSolid(data)) {
                return false;
            }
            else {
                //Its not solid, so it has to match us 
                if (blockId != block.blockId) //glob onto ourselves
                {
                    return true;
                }
            }


            return false;
        }

        private static bool QuarterBlocksPlaceBlock(VoxelBlocks.BlockDefinition block, int localVoxelKey, VoxelData[] readOnlyVoxel, TemporaryMeshData temporaryMeshData, VoxelWorld world, Vector3 origin) {
            if (block.meshContexts.Count == 0) {
                return false;
            }

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
            bool airUp = SurfaceFunction(block, voxUp);
            bool airDown = SurfaceFunction(block, voxDown);
            bool airLeft = SurfaceFunction(block, voxLeft);
            bool airForward = SurfaceFunction(block, voxForward);
            bool airRight = SurfaceFunction(block, voxRight);
            bool airBack = SurfaceFunction(block, voxBack);

            //diagonals
            bool airForwardLeft = SurfaceFunction(block, voxForwardLeft);
            bool airForwardRight = SurfaceFunction(block, voxForwardRight);
            bool airBackLeft = SurfaceFunction(block, voxBackLeft);
            bool airBackRight = SurfaceFunction(block, voxBackRight);

            //downs
            bool airDownForward = SurfaceFunction(block, voxDownForward);
            bool airDownBack = SurfaceFunction(block, voxDownBack);
            bool airDownLeft = SurfaceFunction(block, voxDownLeft);
            bool airDownRight = SurfaceFunction(block, voxDownRight);

            bool airDownForwardRight = SurfaceFunction(block, voxDownForwardRight);
            bool airDownForwardLeft = SurfaceFunction(block, voxDownForwardLeft);
            bool airDownBackRight = SurfaceFunction(block, voxDownBackRight);
            bool airDownBackLeft = SurfaceFunction(block, voxDownBackLeft);

            //Ups
            bool airUpForward = SurfaceFunction(block, voxUpForward);
            bool airUpBack = SurfaceFunction(block, voxUpBack);
            bool airUpLeft = SurfaceFunction(block, voxUpLeft);
            bool airUpRight = SurfaceFunction(block, voxUpRight);

            bool airUpForwardRight = SurfaceFunction(block, voxUpForwardRight);
            bool airUpForwardLeft = SurfaceFunction(block, voxUpForwardLeft);
            bool airUpBackRight = SurfaceFunction(block, voxUpBackRight);
            bool airUpBackLeft = SurfaceFunction(block, voxUpBackLeft);

            var meshContextListU0 = VoxelBlocks.GetRandomMeshContext(block, origin,0);
            var meshContextListU1 = VoxelBlocks.GetRandomMeshContext(block, origin,1);
            var meshContextListU2 = VoxelBlocks.GetRandomMeshContext(block, origin,2);
            var meshContextListU3 = VoxelBlocks.GetRandomMeshContext(block, origin,3);
            var meshContextListD0 = VoxelBlocks.GetRandomMeshContext(block, origin,4);
            var meshContextListD1 = VoxelBlocks.GetRandomMeshContext(block, origin,5);
            var meshContextListD2 = VoxelBlocks.GetRandomMeshContext(block, origin,6);
            var meshContextListD3 = VoxelBlocks.GetRandomMeshContext(block, origin,7);


            // do the vertical flat faces ///////////////////////////////////////////////////////////////////////////////////////////////
            //Flat Left - lots of neighbors
            if (airLeft) {
                if (!airUp) {
                    if (!airBack) {
                        //Wait we might need to curl in here
                        if (airUpBack) {
                            //Is curled in
                            EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UI], temporaryMeshData, world, origin, 3, 0);
                        }
                        else {
                            EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UA], temporaryMeshData, world, origin, 3, 0);
                        }

                    }
                    if (!airForward) {
                        //Wait we might need to curl in here
                        if (airUpForward) {
                            //Is curled in
                            EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UJ], temporaryMeshData, world, origin, 3, 0);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UB], temporaryMeshData, world, origin, 3, 0);
                        }

                    }
                }
                if (!airDown) {
                    if (!airBack) {
                        if (airDownBack) {
                            //Is curled in
                            EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DI], temporaryMeshData, world, origin, 3, 0);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DA], temporaryMeshData, world, origin, 3, 0);
                        }
                    }
                    if (!airForward) {
                        if (airDownForward) {
                            //Is curled in
                            EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DJ], temporaryMeshData, world, origin, 3, 0);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DB], temporaryMeshData, world, origin, 3, 0);
                        }
                    }
                }
            }

            //Flat Right - lots of neighbors
            if (airRight) {
                if (!airUp) {
                    if (!airBack) {
                        //Wait we might need to curl in here
                        if (airUpBack) {
                            EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UJ], temporaryMeshData, world, origin, 1, 0);
                        }
                        else {
                            EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UB], temporaryMeshData, world, origin, 1, 0);
                        }
                    }
                    if (!airForward) {
                        //Wait we might need to curl in here
                        if (airUpForward) {
                            //Is curled in
                            EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UI], temporaryMeshData, world, origin, 1, 0);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UA], temporaryMeshData, world, origin, 1, 0);
                        }
                    }


                }
                if (!airDown) {
                    if (!airBack) {
                        if (airDownBack) {
                            //Is curled in
                            EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DJ], temporaryMeshData, world, origin, 1, 0);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DB], temporaryMeshData, world, origin, 1, 0);
                        }
                    }
                    if (!airForward) {
                        if (airDownForward) {
                            //Is curled in
                            EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DI], temporaryMeshData, world, origin, 1, 0);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DA], temporaryMeshData, world, origin, 1, 0);
                        }
                    }
                }
            }


            //Flat back
            if (airBack) {
                if (!airUp) {
                    if (!airLeft) {
                        if (airUpLeft) {
                            //Is curled in
                            EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UJ], temporaryMeshData, world, origin, 2, 0);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UB], temporaryMeshData, world, origin, 2, 0);
                        }
                    }
                    if (!airRight) {
                        if (airUpRight) {
                            //Is curled in
                            EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UI], temporaryMeshData, world, origin, 2, 0);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UA], temporaryMeshData, world, origin, 2, 0);
                        }
                    }
                }
                if (!airDown) {
                    if (!airLeft) {
                        if (airDownLeft) {
                            //Curled
                            EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DJ], temporaryMeshData, world, origin, 2, 0);
                        }
                        else {
                            EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DB], temporaryMeshData, world, origin, 2, 0);
                        }
                    }
                    if (!airRight) {
                        if (airDownRight) {
                            //Curled
                            EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DI], temporaryMeshData, world, origin, 2, 0);
                        }
                        else {
                            EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DA], temporaryMeshData, world, origin, 2, 0);
                        }

                    }
                }
            }

            //Flat forward
            if (airForward) //Surface is exposed
            {
                if (!airUp) //Above us is solid (top row)
                {
                    if (!airLeft) //Block to our left is Solid
                    {
                        //Fixing
                        if (airUpLeft) {
                            //Is curled in
                            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UI], temporaryMeshData, world, origin, 0, 0);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UA], temporaryMeshData, world, origin, 0, 0);
                        }

                    }
                    if (!airRight) {
                        if (airUpRight) {
                            //Is curled in
                            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UJ], temporaryMeshData, world, origin, 0, 0);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UB], temporaryMeshData, world, origin, 0, 0);
                        }
                    }
                }
                if (!airDown) {
                    if (!airLeft) {
                        if (airDownLeft) {
                            //Curled
                            EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DI], temporaryMeshData, world, origin, 0, 0);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DA], temporaryMeshData, world, origin, 0, 0);
                        }
                    }
                    if (!airRight) {
                        if (airDownRight) {
                            //Curled
                            EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DJ], temporaryMeshData, world, origin, 0, 0);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DB], temporaryMeshData, world, origin, 0, 0);
                        }

                    }
                }
            }


            //Do the corners /////////////////////////////////////////////////////////////////////////
            if (airLeft && airForward) {
                if (airUp) {
                    //Corner
                    EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UG], temporaryMeshData, world, origin, 0, 0);
                }
                else {
                    if (NeedsCapSurface(voxUp, block) && !airUpForwardLeft && !airUpForward && !airUpLeft) {
                        //solid corner
                        EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UL], temporaryMeshData, world, origin, 0, 0);
                    }
                    else {
                        //Vertical side
                        EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UD], temporaryMeshData, world, origin, 0, 0);
                    }
                }

                if (airDown) {
                    //Corner
                    EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DG], temporaryMeshData, world, origin, 0, 0);
                }
                else {
                    if (NeedsCapSurface(voxDown, block) && !airDownForwardLeft && !airDownForward && !airDownLeft) {
                        //solid corner
                        EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DL], temporaryMeshData, world, origin, 0, 0);
                    }
                    else {
                        //Vertical side
                        EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DD], temporaryMeshData, world, origin, 0, 0);
                    }
                }
            }


            if (airRight && airForward) {
                if (airUp) {
                    //Corner
                    EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UG], temporaryMeshData, world, origin, 1, 0);
                }
                else {
                    //Vertical side
                    if (NeedsCapSurface(voxUp, block) && !airUpForwardRight && !airUpForward && !airUpRight) {
                        //solid corner
                        EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UL], temporaryMeshData, world, origin, 1, 0);
                    }
                    else {
                        //Vertical side
                        EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UD], temporaryMeshData, world, origin, 1, 0);
                    }
                }

                if (airDown) {
                    //Corner
                    EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DG], temporaryMeshData, world, origin, 1, 0);
                }
                else {
                    if (NeedsCapSurface(voxDown, block) && !airDownForwardRight && !airDownForward && !airDownRight) {
                        //solid corner
                        EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DL], temporaryMeshData, world, origin, 1, 0);
                    }
                    else {
                        //Vertical side
                        EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DD], temporaryMeshData, world, origin, 1, 0);
                    }
                }
            }

            if (airLeft && airBack) {
                if (airUp) {
                    //Corner
                    EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UG], temporaryMeshData, world, origin, 3, 0);
                }
                else {
                    if (NeedsCapSurface(voxUp, block) && !airUpBackLeft && !airUpBack && !airUpLeft) {
                        //solid corner
                        EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UL], temporaryMeshData, world, origin, 3, 0);
                    }
                    else {
                        //Vertical side
                        EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UD], temporaryMeshData, world, origin, 3, 0);
                    }
                }

                if (airDown) {
                    //Corner
                    EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DG], temporaryMeshData, world, origin, 3, 0);
                }
                else {
                    if (NeedsCapSurface(voxDown, block) && !airDownBackLeft && !airDownBack && !airDownLeft) {
                        //solid corner
                        EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DL], temporaryMeshData, world, origin, 3, 0);
                    }
                    else {
                        //Vertical side
                        EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DD], temporaryMeshData, world, origin, 3, 0);
                    }
                }
            }

            if (airRight && airBack) {
                if (airUp) {
                    //Corner
                    EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UG], temporaryMeshData, world, origin, 2, 0);
                }
                else {
                    //Vertical side
                    if (NeedsCapSurface(voxUp, block) && !airUpBackRight && !airUpBack && !airUpRight) {
                        //solid corner
                        EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UL], temporaryMeshData, world, origin, 2, 0);
                    }
                    else {
                        //Vertical side
                        EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UD], temporaryMeshData, world, origin, 2, 0);
                    }
                }

                if (airDown) {
                    //Corner
                    EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DG], temporaryMeshData, world, origin, 2, 0);
                }
                else {
                    //Wait - we might be ploughing into an impossible connection, make a solid corner instead
                    if (NeedsCapSurface(voxDown, block) && !airDownBackRight && !airDownBack && !airDownRight) {
                        //solid corner
                        EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DL], temporaryMeshData, world, origin, 2, 0);
                    }
                    else {
                        //Vertical side
                        EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DD], temporaryMeshData, world, origin, 2, 0);
                    }
                }
            }


            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            //do the top/bot edges
            if (airLeft) {
                if (airUp) {
                    //Connection along a top edge for foward and backward
                    if (!airBack) {
                        //Top Edge
                        if (NeedsCapSurface(voxBack, block) && !airUpBackLeft && !airUpBack && !airBackLeft) {
                            //solid corner
                            EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UM], temporaryMeshData, world, origin, 3, 0);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UE], temporaryMeshData, world, origin, 3, 0);
                        }
                    }
                    if (!airForward) {
                        //Top Edge
                        if (NeedsCapSurface(voxForward, block) && !airUpForwardLeft && !airUpForward && !airForwardLeft) {
                            //Solid corner
                            EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UN], temporaryMeshData, world, origin, 3, 0);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UF], temporaryMeshData, world, origin, 3, 0);
                        }
                    }

                }

                if (airDown) {
                    if (!airBack) {
                        //Bot Edge
                        if (NeedsCapSurface(voxBack, block) && !airDownBackLeft && !airDownBack && !airBackLeft) {
                            //solid corner
                            EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DM], temporaryMeshData, world, origin, 3, 0);
                        }
                        else {
                            //Horizontal side
                            EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DE], temporaryMeshData, world, origin, 3, 0);
                        }
                    }
                    if (!airForward) {
                        //Bot Edge
                        if (NeedsCapSurface(voxForward, block) && !airDownForwardLeft && !airDownForward && !airForwardLeft) {
                            //solid corner
                            EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DN], temporaryMeshData, world, origin, 3, 0);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DF], temporaryMeshData, world, origin, 3, 0);
                        }
                    }

                }
            }
            if (airRight) {
                if (airUp) {
                    if (!airBack) {
                        //Top Edge
                        if (NeedsCapSurface(voxBack, block) && !airUpBackRight && !airUpBack && !airBackRight) {
                            //solid corner
                            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UN], temporaryMeshData, world, origin, 1, 0);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UF], temporaryMeshData, world, origin, 1, 0);
                        }
                    }
                    if (!airForward) {
                        //Top Edge
                        if (NeedsCapSurface(voxForward, block) && !airUpForwardRight && !airUpForward && !airForwardRight) {
                            //solid corner
                            EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UM], temporaryMeshData, world, origin, 1, 0);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UE], temporaryMeshData, world, origin, 1, 0);
                        }

                    }

                }

                if (airDown) {
                    if (!airBack) {
                        //Bot Edge
                        if (NeedsCapSurface(voxBack, block) && !airDownBackRight && !airDownBack && !airBackRight) {
                            //solid corner
                            EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DN], temporaryMeshData, world, origin, 1, 0);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DF], temporaryMeshData, world, origin, 1, 0);
                        }

                    }
                    if (!airForward) {
                        //Bot Edge
                        if (NeedsCapSurface(voxForward, block) && !airDownForwardRight && !airDownForward && !airForwardRight) {
                            //solid corner
                            EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DM], temporaryMeshData, world, origin, 1, 0);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DE], temporaryMeshData, world, origin, 1, 0);
                        }
                    }
                }
            }

            //do the top/bot edges
            if (airForward) {
                if (airUp) {
                    if (!airLeft) {
                        //Top Edge
                        if (NeedsCapSurface(voxLeft, block) && !airUpForwardLeft && !airUpLeft && !airForwardLeft) {
                            //Solid corner
                            EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UM], temporaryMeshData, world, origin, 0, 0);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UE], temporaryMeshData, world, origin, 0, 0);
                        }

                    }
                    if (!airRight) {
                        //Top Edge
                        if (NeedsCapSurface(voxRight, block) && !airUpForwardRight && !airUpRight && !airForwardRight) {
                            //solid corner
                            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UN], temporaryMeshData, world, origin, 0, 0);
                        }
                        else {
                            //Horizontal side
                            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UF], temporaryMeshData, world, origin, 0, 0);
                        }

                    }

                }

                if (airDown) {
                    if (!airLeft) {
                        //Bot Edge
                        if (NeedsCapSurface(voxLeft, block) && !airDownForwardLeft && !airDownLeft && !airForwardLeft) {
                            //solid corner
                            EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DM], temporaryMeshData, world, origin, 0, 0);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DE], temporaryMeshData, world, origin, 0, 0);
                        }

                    }
                    if (!airRight) {
                        //Bot Edge
                        if (NeedsCapSurface(voxRight, block) && !airDownForwardRight && !airDownRight && !airForwardRight) {
                            //solid corner
                            EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DN], temporaryMeshData, world, origin, 0, 0);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DF], temporaryMeshData, world, origin, 0, 0);
                        }

                    }

                }
            }
            if (airBack) {
                if (airUp) {
                    if (!airLeft) {
                        //Top Edge
                        if (NeedsCapSurface(voxLeft, block) && !airUpBackLeft && !airUpLeft && !airBackLeft) {
                            //solid corner
                            EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UN], temporaryMeshData, world, origin, 2, 0);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UF], temporaryMeshData, world, origin, 2, 0);
                        }

                    }
                    if (!airRight) {
                        //Top Edge
                        if (NeedsCapSurface(voxRight, block) && !airUpBackRight && !airUpRight && !airBackRight) {
                            //solid corner
                            EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UM], temporaryMeshData, world, origin, 2, 0);
                        }
                        else {
                            //side
                            EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UE], temporaryMeshData, world, origin, 2, 0);
                        }
                    }

                }

                if (airDown) {
                    if (!airLeft) {
                        //Bot Edge
                        if (NeedsCapSurface(voxLeft, block) && !airDownBackLeft && !airDownLeft && !airBackLeft) {
                            //Solid corner
                            EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DN], temporaryMeshData, world, origin, 2, 0);
                        }
                        else {
                            //side 
                            EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DF], temporaryMeshData, world, origin, 2, 0);
                        }

                    }
                    if (!airRight) {
                        //Bot Edge
                        if (NeedsCapSurface(voxRight, block) && !airDownBackRight && !airDownRight && !airBackRight) {
                            //solid corner
                            EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DM], temporaryMeshData, world, origin, 2, 0);
                        }
                        else {
                            //side
                            EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DE], temporaryMeshData, world, origin, 2, 0);
                        }

                    }

                }
            }

            //////////////////////////////////////////////////////////////////////////////////////////////////////
            //Do top+Bottom surfaces
            if (airUp) {
                if (!airForward && !airLeft) {
                    //Top Surface

                    //Wait, it might have air on the diagonal, so we should use the other type of top corner then
                    if (airForwardLeft) {
                        EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UH], temporaryMeshData, world, origin, 0, 0);
                    }
                    else {
                        EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UC], temporaryMeshData, world, origin, 0, 0);
                    }

                }
                if (!airForward && !airRight) {
                    //Top Surface
                    if (airForwardRight) {
                        EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UH], temporaryMeshData, world, origin, 1, 0);
                    }
                    else {
                        EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UC], temporaryMeshData, world, origin, 1, 0);
                    }
                }
                if (!airBack && !airLeft) {
                    //Top Surface
                    if (airBackLeft) {
                        EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UH], temporaryMeshData, world, origin, 3, 0);
                    }
                    else {
                        EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UC], temporaryMeshData, world, origin, 3, 0);
                    }
                }
                if (!airBack && !airRight) {
                    //Top Surface
                    if (airBackRight) {
                        EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UH], temporaryMeshData, world, origin, 2, 0);
                    }
                    else {
                        EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UC], temporaryMeshData, world, origin, 2, 0);
                    }

                }
            }
            if (airDown) {
                if (!airForward && !airLeft) {
                    //Bot Surface
                    if (airForwardLeft) {
                        EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DH], temporaryMeshData, world, origin, 0, 0);
                    }
                    else {
                        EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DC], temporaryMeshData, world, origin, 0, 0);
                    }
                }
                if (!airForward && !airRight) {
                    //Bot Surface
                    if (airForwardRight) {
                        EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DH], temporaryMeshData, world, origin, 1, 0);
                    }
                    else {
                        EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DC], temporaryMeshData, world, origin, 1, 0);
                    }
                }
                if (!airBack && !airLeft) {
                    //Bot Surface
                    if (airBackLeft) {
                        EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DH], temporaryMeshData, world, origin, 3, 0);
                    }
                    else {
                        EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DC], temporaryMeshData, world, origin, 3, 0);
                    }
                }
                if (!airBack && !airRight) {
                    //Bot Surface
                    if (airBackRight) {
                        EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DH], temporaryMeshData, world, origin, 2, 0);
                    }
                    else {
                        EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DC], temporaryMeshData, world, origin, 2, 0);
                    }
                }
            }
            /////////////////////////////////////////////////////////////////////////////////////////////

            //Tri Staaaaar

            if (airLeft && airForward && airDownForwardLeft) {
                VoxelData voxLeftDown = readOnlyVoxel[localVoxelKey - paddedChunkSize - 1];
                bool airLeftDown = (block.blockId != VoxelWorld.VoxelDataToBlockId(voxLeftDown));
                VoxelData voxForwardDown = readOnlyVoxel[localVoxelKey - paddedChunkSize + (paddedChunkSize * paddedChunkSize)];
                bool airForwardDown = (block.blockId != VoxelWorld.VoxelDataToBlockId(voxForwardDown));

                if (!airLeftDown && !airForwardDown) {
                    EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UK], temporaryMeshData, world, origin + new Vector3(0, -1, 0), 0, 0);
                }
            }

            //Do the other 3
            if (airRight && airForward && airDownForwardRight) {
                if (!airDownRight && !airDownForward) {
                    EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UK], temporaryMeshData, world, origin + new Vector3(0, -1, 0), 1, 0);
                }
            }
            if (airLeft && airBack && airDownBackLeft) {
                if (!airDownLeft && !airDownBack) {
                    EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UK], temporaryMeshData, world, origin + new Vector3(0, -1, 0), 3, 0);
                }
            }
            if (airRight && airBack && airDownBackRight) {
                if (!airDownRight && !airDownBack) {
                    EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UK], temporaryMeshData, world, origin + new Vector3(0, -1, 0), 2, 0);
                }
            }
            //Do the upwards ones now
            if (airLeft && airForward && airUpForwardLeft) {
                if (!airUpLeft && !airUpForward) {
                    EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DK], temporaryMeshData, world, origin + new Vector3(0, 1, 0), 0, 0);
                }
            }
            if (airRight && airForward && airUpForwardRight) {
                if (!airUpRight && !airUpForward) {
                    EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DK], temporaryMeshData, world, origin + new Vector3(0, 1, 0), 1, 0);
                }
            }
            if (airLeft && airBack && airUpBackLeft) {
                if (!airUpLeft && !airUpBack) {
                    EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DK], temporaryMeshData, world, origin + new Vector3(0, 1, 0), 3, 0);
                }
            }
            if (airRight && airBack && airUpBackRight) {
                if (!airUpRight && !airUpBack) {
                    EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DK], temporaryMeshData, world, origin + new Vector3(0, 1, 0), 2, 0);
                }
            }
            return true;
        }
    }
}