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
        
        /// <param name="block">Block being placed</param>
        /// <param name="data">Data of neighboring block</param>
        /// <returns>
        /// True = we generate surfaces between us and this block
        /// False = we dont
        /// </returns>
        private static bool ShouldGenerateSurface(VoxelBlocks.BlockDefinition block, VoxelData data, int flip, Vector3 offset = default) {
            int blockId = VoxelWorld.VoxelDataToBlockId(data);
            if (blockId == 0) return true;

            if (VoxelWorld.VoxelIsSolid(data)) {
                return false;
            }
            else {
                // Its not solid, so it has to match us 
                if (blockId != block.blockId) return true;
                // Half blocks much have same flip to connect
                if (block.definition.halfBlock) {
                    if (flip != VoxelWorld.GetVoxelFlippedBits(data)) return true;
                    
                    var scaleAxis = VoxelWorld.GetScaleFromFlipBits(flip) - Vector3.one;
                    scaleAxis.Scale(offset);
                    if (scaleAxis.magnitude > 0) return true;
                }
            }


            return false;
        }

        /// <summary>
        /// Helper function for emitting quarter block 
        /// </summary>
        /// <returns>True if the voxel data provided is a quarter block but not the same block type as the one passed in.</returns>
        private static bool IsDifferentQuarterBlock(VoxelWorld world, ushort blockId, ushort voxelData) {
            // If the voxelData doesn't represent a quarter block return false
            if (world.voxelBlocks.GetBlock(voxelData).definition.contextStyle !=
                VoxelBlocks.ContextStyle.QuarterBlocks) return false;
            // If they are the same block id return false
            if (blockId == VoxelWorld.VoxelDataToBlockId(voxelData)) return false;
            return true;
        }
        

        private static bool QuarterBlocskEmitSingleBlock(VoxelBlocks.BlockDefinition block, TemporaryMeshData meshData, VoxelWorld world, Vector2 damageUv, Color32 col) {

            Vector3 origin = new Vector3(-0.5f,-0.5f,-0.5f);
            int flip = 0;
        
            var meshContextListU0 = VoxelBlocks.GetRandomMeshContext(block, origin, 0);

            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UG], meshData, world, origin, 0, flip, damageUv, col, null);
            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UG], meshData, world, origin, 1, flip, damageUv, col, null);
            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UG], meshData, world, origin, 2, flip, damageUv, col, null);
            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UG], meshData, world, origin, 3, flip, damageUv, col, null);
            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.DG], meshData, world, origin, 0, flip, damageUv, col, null);
            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.DG], meshData, world, origin, 1, flip, damageUv, col, null);
            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.DG], meshData, world, origin, 2, flip, damageUv, col, null);
            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.DG], meshData, world, origin, 3, flip, damageUv, col, null);

            //EmitMesh(block, block.mesh.lod0, meshData, world, origin, rotation, flip, damageUv);
            return true;
        }

        private static bool QuarterBlocksPlaceBlock(VoxelBlocks.BlockDefinition block, int localVoxelKey, VoxelData[] readOnlyVoxel, TemporaryMeshData temporaryMeshData, VoxelWorld world, Vector3 origin, Vector2 damageUv, Color32 col, Vector3 scale, int flip) {
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
            bool airUp = ShouldGenerateSurface(block, voxUp, flip, Vector3.up);
            bool airDown = ShouldGenerateSurface(block, voxDown, flip, Vector3.down);
            bool airLeft = ShouldGenerateSurface(block, voxLeft, flip, Vector3.left);
            bool airForward = ShouldGenerateSurface(block, voxForward, flip, Vector3.forward);
            bool airRight = ShouldGenerateSurface(block, voxRight, flip, Vector3.right);
            bool airBack = ShouldGenerateSurface(block, voxBack, flip, Vector3.back);

            //diagonals
            bool airForwardLeft = ShouldGenerateSurface(block, voxForwardLeft, flip);
            bool airForwardRight = ShouldGenerateSurface(block, voxForwardRight, flip);
            bool airBackLeft = ShouldGenerateSurface(block, voxBackLeft, flip);
            bool airBackRight = ShouldGenerateSurface(block, voxBackRight, flip);

            //downs
            bool airDownForward = ShouldGenerateSurface(block, voxDownForward, flip);
            bool airDownBack = ShouldGenerateSurface(block, voxDownBack, flip);
            bool airDownLeft = ShouldGenerateSurface(block, voxDownLeft, flip);
            bool airDownRight = ShouldGenerateSurface(block, voxDownRight, flip);

            bool airDownForwardRight = ShouldGenerateSurface(block, voxDownForwardRight, flip);
            bool airDownForwardLeft = ShouldGenerateSurface(block, voxDownForwardLeft, flip);
            bool airDownBackRight = ShouldGenerateSurface(block, voxDownBackRight, flip);
            bool airDownBackLeft = ShouldGenerateSurface(block, voxDownBackLeft, flip);

            //Ups
            bool airUpForward = ShouldGenerateSurface(block, voxUpForward, flip);
            bool airUpBack = ShouldGenerateSurface(block, voxUpBack, flip);
            bool airUpLeft = ShouldGenerateSurface(block, voxUpLeft, flip);
            bool airUpRight = ShouldGenerateSurface(block, voxUpRight, flip);

            bool airUpForwardRight = ShouldGenerateSurface(block, voxUpForwardRight, flip);
            bool airUpForwardLeft = ShouldGenerateSurface(block, voxUpForwardLeft, flip);
            bool airUpBackRight = ShouldGenerateSurface(block, voxUpBackRight, flip);
            bool airUpBackLeft = ShouldGenerateSurface(block, voxUpBackLeft, flip);
            
            // If block up is a quarter block and has any air neighbors then it can be seen through
            var entirelyCovered = true;
            for (var x = 0; x < 3; x++) {
                for (var y = 0; y < 3; y++) {
                    for (var z = 0; z < 3; z++) {
                        if (x == 1 && y == 1 && z == 1) continue;

                        var key = localVoxelKey + x - 1 + (y - 1) * paddedChunkSize +
                                  (paddedChunkSize * paddedChunkSize) * (z - 1);
                        var vox = readOnlyVoxel[key];
                        if (ShouldGenerateSurface(block, vox, flip)) {
                            entirelyCovered = false;
                            if (y == 2 && IsDifferentQuarterBlock(world, block.blockId, voxUp)) airUp = true;
                            if (y == 0 && IsDifferentQuarterBlock(world, block.blockId, voxDown)) airDown = true;
                            if (x == 0 && IsDifferentQuarterBlock(world, block.blockId, voxLeft)) airLeft = true;
                            if (x == 2 && IsDifferentQuarterBlock(world, block.blockId, voxRight)) airRight = true;
                            if (z == 2 && IsDifferentQuarterBlock(world, block.blockId, voxForward)) airForward = true;
                            if (z == 0 && IsDifferentQuarterBlock(world, block.blockId, voxBack)) airBack = true;

                            // if (x >= 1) {
                            //     if (z >= 1 && IsDifferentQuarterBlock(world, block.blockId, voxForwardRight)) airForwardRight = true;
                            // }
                            if (x >= 1 && y >= 1 && z >= 1 &&
                                IsDifferentQuarterBlock(world, block.blockId, voxUpForwardRight))
                                airUpForwardRight = true;
                            if (x <= 1 && y >= 1 && z >= 1 &&
                                IsDifferentQuarterBlock(world, block.blockId, voxUpForwardLeft))
                                airUpForwardLeft = true;
                            if (x >= 1 && y <= 1 && z >= 1 &&
                                IsDifferentQuarterBlock(world, block.blockId, voxDownForwardRight))
                                airDownForwardRight = true;
                            if (x <= 1 && y <= 1 && z >= 1 &&
                                IsDifferentQuarterBlock(world, block.blockId, voxDownForwardLeft))
                                airDownForwardLeft = true;
                            if (x >= 1 && y >= 1 && z <= 1 &&
                                IsDifferentQuarterBlock(world, block.blockId, voxUpBackRight))
                                airUpBackRight = true;
                            if (x <= 1 && y >= 1 && z <= 1 &&
                                IsDifferentQuarterBlock(world, block.blockId, voxUpBackLeft))
                                airUpBackLeft = true;
                            if (x >= 1 && y <= 1 && z <= 1 &&
                                IsDifferentQuarterBlock(world, block.blockId, voxDownBackRight))
                                airDownBackRight = true;
                            if (x <= 1 && y <= 1 && z <= 1 &&
                                IsDifferentQuarterBlock(world, block.blockId, voxDownBackLeft))
                                airDownBackLeft = true;
                            
                            // Not super corners nor cardinals
                            if (x >= 1 && z >= 1) {
                                if ((y == 0 || (x == 2 && z == 2)) && IsDifferentQuarterBlock(world, block.blockId, voxForwardRight)) {
                                    airForwardRight = true;
                                }
                            }
                            if (x <= 1 && z >= 1) {
                                if ((y == 0 || (x == 0 && z == 2)) && IsDifferentQuarterBlock(world, block.blockId, voxForwardLeft)) {
                                    airForwardLeft = true;
                                }
                            }
                            if (x >= 1 && z <= 1) {
                                if ((y == 0 || (x == 2 && z == 0)) && IsDifferentQuarterBlock(world, block.blockId, voxBackRight)) {
                                    airBackRight = true;
                                }
                            }
                            if (x <= 1 && z <= 1) {
                                if ((y == 0 || (x == 0 && z == 0)) && IsDifferentQuarterBlock(world, block.blockId, voxBackLeft)) {
                                    airBackLeft = true;
                                }
                            }
                                
                        }
                    }
                }
            }

            if (entirelyCovered) return true;

            var meshContextListU0 = VoxelBlocks.GetRandomMeshContext(block, origin,0);
            var meshContextListU1 = VoxelBlocks.GetRandomMeshContext(block, origin,1);
            var meshContextListU2 = VoxelBlocks.GetRandomMeshContext(block, origin,2);
            var meshContextListU3 = VoxelBlocks.GetRandomMeshContext(block, origin,3);
            var meshContextListD0 = VoxelBlocks.GetRandomMeshContext(block, origin,4);
            var meshContextListD1 = VoxelBlocks.GetRandomMeshContext(block, origin,5);
            var meshContextListD2 = VoxelBlocks.GetRandomMeshContext(block, origin,6);
            var meshContextListD3 = VoxelBlocks.GetRandomMeshContext(block, origin,7);


            float[] lerps = null;// new float[6];
            /*
            if (!airLeft) {
                lerps[0] = 1.0f;
            }
            if (!airRight) {
                lerps[1] = 1.0f;
            }
            
            if (airUp) {
                lerps[2] = 1.0f;  
            }
            //if (airDown) {
               // lerps[3] = 1.0f;
            //}

            if (!airForward) {
                lerps[4] = 1.0f;
            }
            if (!airBack) {
                lerps[5] = 1.0f;
            }*/
            
            // do the vertical flat faces ///////////////////////////////////////////////////////////////////////////////////////////////
            //Flat Left - lots of neighbors
            if (airLeft) {
                if (!airUp) {
                    if (!airBack) {
                        //Wait we might need to curl in here
                        if (VoxelWorld.VoxelDataToBlockId(voxUpBack) != block.blockId) {
                            //Is curled in
                            EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UI], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UA], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                        }

                    }
                    if (!airForward) {
                        //Wait we might need to curl in here
                        if (VoxelWorld.VoxelDataToBlockId(voxUpForward) != block.blockId) {
                            //Is curled in
                            EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UJ], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UB], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                        }

                    }
                }
                if (!airDown) {
                    if (!airBack) {
                        if (VoxelWorld.VoxelDataToBlockId(voxDownBack) != block.blockId) {
                            //Is curled in
                            EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DI], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DA], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                        }
                    }
                    if (!airForward) {
                        if (VoxelWorld.VoxelDataToBlockId(voxDownForward) != block.blockId) {
                            //Is curled in
                            EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DJ], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DB], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                        }
                    }
                }
            }

            //Flat Right - lots of neighbors
            if (airRight) {
                if (!airUp) {
                    if (!airBack) {
                        //Wait we might need to curl in here
                        if (VoxelWorld.VoxelDataToBlockId(voxUpBack) != block.blockId) {
                            EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UJ], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UB], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                        }
                    }
                    if (!airForward) {
                        //Wait we might need to curl in here
                        if (VoxelWorld.VoxelDataToBlockId(voxUpForward) != block.blockId) {
                            //Is curled in
                            EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UI], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UA], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                        }
                    }


                }
                if (!airDown) {
                    if (!airBack) {
                        if (VoxelWorld.VoxelDataToBlockId(voxDownBack) != block.blockId) {
                            //Is curled in
                            EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DJ], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DB], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                        }
                    }
                    if (!airForward) {
                        if (VoxelWorld.VoxelDataToBlockId(voxDownForward) != block.blockId) {
                            //Is curled in
                            EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DI], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DA], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                        }
                    }
                }
            }


            //Flat back
            if (airBack) {
                if (!airUp) {
                    if (!airLeft) {
                        if (VoxelWorld.VoxelDataToBlockId(voxUpLeft) != block.blockId) {
                            //Is curled in
                            EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UJ], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UB], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                        }
                    }
                    if (!airRight) {
                        if (VoxelWorld.VoxelDataToBlockId(voxUpRight) != block.blockId) {
                            //Is curled in
                            EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UI], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UA], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                        }
                    }
                }
                if (!airDown) {
                    if (!airLeft) {
                        if (VoxelWorld.VoxelDataToBlockId(voxDownLeft) != block.blockId) {
                            //Curled
                            EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DJ], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DB], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                        }
                    }
                    if (!airRight) {
                        if (VoxelWorld.VoxelDataToBlockId(voxDownRight) != block.blockId) {
                            //Curled
                            EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DI], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DA], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
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
                        if (VoxelWorld.VoxelDataToBlockId(voxUpLeft) != block.blockId) {
                            //Is curled in
                            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UI], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UA], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                        }

                    }
                    if (!airRight) {
                        if (VoxelWorld.VoxelDataToBlockId(voxUpRight) != block.blockId) {
                            //Is curled in
                            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UJ], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UB], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                        }
                    }
                }
                if (!airDown) {
                    if (!airLeft) {
                        if (VoxelWorld.VoxelDataToBlockId(voxDownLeft) != block.blockId) {
                            //Curled
                            EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DI], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DA], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                        }
                    }
                    if (!airRight) {
                        if (VoxelWorld.VoxelDataToBlockId(voxDownRight) != block.blockId) {
                            //Curled
                            EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DJ], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //Is flat
                            EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DB], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                        }

                    }
                }
            }


            //Do the corners /////////////////////////////////////////////////////////////////////////
            if (airLeft && airForward) {
                if (airUp) {
                    //Corner
                    EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UG], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                }
                else {
                    if (NeedsCapSurface(voxUp, block) && !airUpForwardLeft && !airUpForward && !airUpLeft) {
                        //solid corner
                        EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UL], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                    }
                    else {
                        //Vertical side
                        EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UD], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                    }
                }

                if (airDown) {
                    //Corner
                    EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DG], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                }
                else {
                    if (NeedsCapSurface(voxDown, block) && !airDownForwardLeft && !airDownForward && !airDownLeft) {
                        //solid corner
                        EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DL], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                    }
                    else {
                        //Vertical side
                        EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DD], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                    }
                }
            }


            if (airRight && airForward) {
                if (airUp) {
                    //Corner
                    EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UG], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                }
                else {
                    //Vertical side
                    if (NeedsCapSurface(voxUp, block) && !airUpForwardRight && !airUpForward && !airUpRight) {
                        //solid corner
                        EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UL], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                    }
                    else {
                        //Vertical side
                        EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UD], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                    }
                }

                if (airDown) {
                    //Corner
                    EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DG], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                }
                else {
                    if (NeedsCapSurface(voxDown, block) && !airDownForwardRight && !airDownForward && !airDownRight) {
                        //solid corner
                        EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DL], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                    }
                    else {
                        //Vertical side
                        EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DD], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                    }
                }
            }

            if (airLeft && airBack) {
                if (airUp) {
                    //Corner
                    EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UG], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                }
                else {
                    if (NeedsCapSurface(voxUp, block) && !airUpBackLeft && !airUpBack && !airUpLeft) {
                        //solid corner
                        EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UL], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                    }
                    else {
                        //Vertical side
                        EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UD], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                    }
                }

                if (airDown) {
                    //Corner
                    EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DG], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                }
                else {
                    if (NeedsCapSurface(voxDown, block) && !airDownBackLeft && !airDownBack && !airDownLeft) {
                        //solid corner
                        EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DL], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                    }
                    else {
                        //Vertical side
                        EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DD], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                    }
                }
            }

            if (airRight && airBack) {
                if (airUp) {
                    //Corner
                    EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UG], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                }
                else {
                    //Vertical side
                    if (NeedsCapSurface(voxUp, block) && !airUpBackRight && !airUpBack && !airUpRight) {
                        //solid corner
                        EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UL], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                    }
                    else {
                        //Vertical side
                        EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UD], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                    }
                }

                if (airDown) {
                    //Corner
                    EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DG], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                }
                else {
                    //Wait - we might be ploughing into an impossible connection, make a solid corner instead
                    if (NeedsCapSurface(voxDown, block) && !airDownBackRight && !airDownBack && !airDownRight) {
                        //solid corner
                        EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DL], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                    }
                    else {
                        //Vertical side
                        EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DD], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
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
                        if (!airUpBackLeft && !airUpBack && !airBackLeft) {
                            //solid corner
                            EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UM], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UE], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                        }
                    }
                    if (!airForward) {
                        //Top Edge
                        if (!airUpForwardLeft && !airUpForward && !airForwardLeft) {
                            //Solid corner
                            EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UN], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UF], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                        }
                    }

                }

                if (airDown) {
                    if (!airBack) {
                        //Bot Edge
                        if (!airDownBackLeft && !airDownBack && !airBackLeft) {
                            //solid corner
                            EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DM], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //Horizontal side
                            EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DE], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                        }
                    }
                    if (!airForward) {
                        //Bot Edge
                        if (!airDownForwardLeft && !airDownForward && !airForwardLeft) {
                            //solid corner
                            EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DN], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DF], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                        }
                    }

                }
            }
            if (airRight) {
                if (airUp) {
                    if (!airBack) {
                        //Top Edge
                        if (!airUpBackRight && !airUpBack && !airBackRight) {
                            //solid corner
                            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UN], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UF], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                        }
                    }
                    if (!airForward) {
                        //Top Edge
                        if (!airUpForwardRight && !airUpForward && !airForwardRight) {
                            //solid corner
                            EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UM], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UE], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                        }

                    }

                }

                if (airDown) {
                    if (!airBack) {
                        //Bot Edge
                        if (!airDownBackRight && !airDownBack && !airBackRight) {
                            //solid corner
                            EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DN], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DF], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                        }

                    }
                    if (!airForward) {
                        //Bot Edge
                        if (!airDownForwardRight && !airDownForward && !airForwardRight) {
                            //solid corner
                            EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DM], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DE], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                        }
                    }
                }
            }

            //do the top/bot edges
            if (airForward) {
                if (airUp) {
                    if (!airLeft) {
                        //Top Edge
                        if (!airUpForwardLeft && !airUpLeft && !airForwardLeft) {
                            //Solid corner
                            EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UM], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UE], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                        }

                    }
                    if (!airRight) {
                        //Top Edge
                        if (!airUpForwardRight && !airUpRight && !airForwardRight) {
                            //solid corner
                            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UN], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //Horizontal side
                            EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UF], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                        }

                    }

                }

                if (airDown) {
                    if (!airLeft) {
                        //Bot Edge
                        if (!airDownForwardLeft && !airDownLeft && !airForwardLeft) {
                            //solid corner
                            EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DM], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DE], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                        }

                    }
                    if (!airRight) {
                        //Bot Edge
                        if (!airDownForwardRight && !airDownRight && !airForwardRight) {
                            //solid corner
                            EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DN], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DF], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                        }

                    }

                }
            }
            if (airBack) {
                if (airUp) {
                    if (!airLeft) {
                        //Top Edge
                        if (!airUpBackLeft && !airUpLeft && !airBackLeft) {
                            //solid corner
                            EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UN], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //horizontal side
                            EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UF], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                        }

                    }
                    if (!airRight) {
                        //Top Edge
                        if (!airUpBackRight && !airUpRight && !airBackRight) {
                            //solid corner
                            EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UM], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //side
                            EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UE], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                        }
                    }

                }

                if (airDown) {
                    if (!airLeft) {
                        //Bot Edge
                        if (!airDownBackLeft && !airDownLeft && !airBackLeft) {
                            //Solid corner
                            EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DN], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //side 
                            EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DF], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                        }

                    }
                    if (!airRight) {
                        //Bot Edge
                        if (!airDownBackRight && !airDownRight && !airBackRight) {
                            //solid corner
                            EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DM], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                        }
                        else {
                            //side
                            EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DE], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
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
                        EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UH], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                    }
                    else {
                        EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UC], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                    }

                }
                if (!airForward && !airRight) {
                    //Top Surface
                    if (airForwardRight) {
                        EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UH], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                    }
                    else {
                        EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UC], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                    }
                }
                if (!airBack && !airLeft) {
                    //Top Surface
                    if (airBackLeft) {
                        EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UH], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                    }
                    else {
                        EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UC], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                    }
                }
                if (!airBack && !airRight) {
                    //Top Surface
                    if (airBackRight) {
                        EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UH], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                    }
                    else {
                        EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UC], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                    }

                }
            }
            if (airDown) {
                if (!airForward && !airLeft) {
                    //Bot Surface
                    if (airForwardLeft) {
                        EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DH], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                    }
                    else {
                        EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DC], temporaryMeshData, world, origin, 0, flip, damageUv, col, lerps, scale);
                    }
                }
                if (!airForward && !airRight) {
                    //Bot Surface
                    if (airForwardRight) {
                        EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DH], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                    }
                    else {
                        EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DC], temporaryMeshData, world, origin, 1, flip, damageUv, col, lerps, scale);
                    }
                }
                if (!airBack && !airLeft) {
                    //Bot Surface
                    if (airBackLeft) {
                        EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DH], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                    }
                    else {
                        EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DC], temporaryMeshData, world, origin, 3, flip, damageUv, col, lerps, scale);
                    }
                }
                if (!airBack && !airRight) {
                    //Bot Surface
                    if (airBackRight) {
                        EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DH], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
                    }
                    else {
                        EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DC], temporaryMeshData, world, origin, 2, flip, damageUv, col, lerps, scale);
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

                if (!airLeftDown && !airForwardDown && VoxelWorld.VoxelDataToBlockId(voxDown) == block.blockId) {
                    EmitMesh(block, meshContextListU0[(int)VoxelBlocks.QuarterBlockTypes.UK], temporaryMeshData, world, origin + new Vector3(0, -1, 0), 0, flip, damageUv, col, lerps, scale);
                }
            }

            //Do the other 3
            if (airRight && airForward && airDownForwardRight) {
                if (!airDownRight && !airDownForward && VoxelWorld.VoxelDataToBlockId(voxDown) == block.blockId) {
                    EmitMesh(block, meshContextListU1[(int)VoxelBlocks.QuarterBlockTypes.UK], temporaryMeshData, world, origin + new Vector3(0, -1, 0), 1, flip, damageUv, col, lerps, scale);
                }
            }
            if (airLeft && airBack && airDownBackLeft) {
                if (!airDownLeft && !airDownBack && VoxelWorld.VoxelDataToBlockId(voxDown) == block.blockId) {
                    EmitMesh(block, meshContextListU3[(int)VoxelBlocks.QuarterBlockTypes.UK], temporaryMeshData, world, origin + new Vector3(0, -1, 0), 3, flip, damageUv, col, lerps, scale);
                }
            }
            if (airRight && airBack && airDownBackRight) {
                if (!airDownRight && !airDownBack && VoxelWorld.VoxelDataToBlockId(voxDown) == block.blockId) {
                    EmitMesh(block, meshContextListU2[(int)VoxelBlocks.QuarterBlockTypes.UK], temporaryMeshData, world, origin + new Vector3(0, -1, 0), 2, flip, damageUv, col, lerps, scale);
                }
            }
            //Do the upwards ones now
            if (airLeft && airForward && airUpForwardLeft) {
                if (!airUpLeft && !airUpForward && VoxelWorld.VoxelDataToBlockId(voxUp) == block.blockId) {
                    EmitMesh(block, meshContextListD0[(int)VoxelBlocks.QuarterBlockTypes.DK], temporaryMeshData, world, origin + new Vector3(0, 1, 0), 0, flip, damageUv, col, lerps, scale);
                }
            }
            if (airRight && airForward && airUpForwardRight) {
                if (!airUpRight && !airUpForward && VoxelWorld.VoxelDataToBlockId(voxUp) == block.blockId) {
                    EmitMesh(block, meshContextListD1[(int)VoxelBlocks.QuarterBlockTypes.DK], temporaryMeshData, world, origin + new Vector3(0, 1, 0), 1, flip, damageUv, col, lerps, scale);
                }
            }
            if (airLeft && airBack && airUpBackLeft) {
                if (!airUpLeft && !airUpBack && VoxelWorld.VoxelDataToBlockId(voxUp) == block.blockId) {
                    EmitMesh(block, meshContextListD3[(int)VoxelBlocks.QuarterBlockTypes.DK], temporaryMeshData, world, origin + new Vector3(0, 1, 0), 3, flip, damageUv, col, lerps, scale);
                }
            }
            if (airRight && airBack && airUpBackRight) {
                if (!airUpRight && !airUpBack && VoxelWorld.VoxelDataToBlockId(voxUp) == block.blockId) {
                    EmitMesh(block, meshContextListD2[(int)VoxelBlocks.QuarterBlockTypes.DK], temporaryMeshData, world, origin + new Vector3(0, 1, 0), 2, flip, damageUv, col, lerps, scale);
                }
            }
            return true;
        }
    }
}