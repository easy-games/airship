using Unity;
using UnityEngine;
using VoxelWorldStuff;

using VoxelData = System.UInt16;
using BlockId = System.UInt16;

public struct VoxelRaycastResult {
    public bool Hit;
    public float Distance;
    public Vector3 HitPosition;
    public Vector3 HitNormal;
}

public partial class VoxelWorld : MonoBehaviour {

    public static Vector3 Sign(Vector3 input) {
        return new Vector3(Mathf.Sign(input.x), Mathf.Sign(input.y), Mathf.Sign(input.z));
    }
    public static Vector3 Abs(Vector3 input) {
        return new Vector3(Mathf.Abs(input.x), Mathf.Abs(input.y), Mathf.Abs(input.z));
    }
    public static Vector3 Floor(Vector3 input) {
        return new Vector3(Mathf.Floor(input.x), Mathf.Floor(input.y), Mathf.Floor(input.z));
    }

    public static Vector3Int FloorInt(Vector3 input) {
        return new Vector3Int(Mathf.FloorToInt(input.x), Mathf.FloorToInt(input.y), Mathf.FloorToInt(input.z));
    }

    public bool CanSeePoint(Vector3 pos, Vector3 dest, Vector3 destNormal) {

        Vector3 dirVec = (dest - pos);
        Vector3 direction = dirVec.normalized;
        if (Vector3.Dot(direction, destNormal) > 0) {
            return false;
        }

        //use RaycastVoxelForLighting
        int res = RaycastVoxelForLighting(pos, direction, dirVec.magnitude - 2f); //Because we're often tracing within < 1 block in size

        if (res == 0) {
            return true;
        }
        return false;
    }


    //voxel raycast routine using Amanatides method, returns a bool, gives a hitnormal
    public (bool hit, float distance, Vector3 hitPosition, Vector3 hitNormal) RaycastVoxel_Internal(Vector3 pos, Vector3 direction, float maxDistance, bool debug = false) {

        //integer voxel position (world)
        Vector3Int snappedPosInt = FloorInt(pos);
        Vector3 snappedPosFloat = Floor(pos);
        
        //Position inside a chunk
        Vector3Int localPos = Chunk.WorldPosToLocalPos(snappedPosInt);

        //current chunk
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out Chunk currentChunk);

        int localx = localPos.x;
        int localy = localPos.y;
        int localz = localPos.z;

        int stepSignx = direction.x < 0 ? -1 : 1;
        int stepSigny = direction.y < 0 ? -1 : 1;
        int stepSignz = direction.z < 0 ? -1 : 1;


        float tDeltax = Mathf.Abs(1.0f / direction.x);
        float tDeltay = Mathf.Abs(1.0f / direction.y);
        float tDeltaz = Mathf.Abs(1.0f / direction.z);

        float tMaxx = stepSignx > 0.0f ? snappedPosFloat.x + 1.0f - pos.x : pos.x - snappedPosFloat.x;
        float tMaxy = stepSigny > 0.0f ? snappedPosFloat.y + 1.0f - pos.y : pos.y - snappedPosFloat.y;
        float tMaxz = stepSignz > 0.0f ? snappedPosFloat.z + 1.0f - pos.z : pos.z - snappedPosFloat.z;

        tMaxx *= tDeltax;
        tMaxy *= tDeltay;
        tMaxz *= tDeltaz;

        float dist = 0;
        int lastFace;

        while (dist < maxDistance) {
            if (tMaxx < tMaxy) {
                if (tMaxx < tMaxz) {
                    localx += stepSignx;
                    dist = tMaxx;
                    tMaxx += tDeltax;

                    lastFace = 0;

                    if (localx > chunkSize - 1) {
                        localx -= chunkSize;
                        chunkKey.x += 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                    else
                     if (localx < 0) {
                        localx += chunkSize;
                        chunkKey.x -= 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                }
                else {
                    localz += stepSignz;
                    dist = tMaxz;
                    tMaxz += tDeltaz;
                    lastFace = 2;

                    if (localz > chunkSize - 1) {
                        localz -= chunkSize;
                        chunkKey.z += 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                    else
                     if (localz < 0) {
                        localz += chunkSize;
                        chunkKey.z -= 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                }
            }
            else {
                if (tMaxy < tMaxz) {
                    localy += stepSigny;
                    dist = tMaxy;
                    tMaxy += tDeltay;
                    lastFace = 1;

                    if (localy > chunkSize - 1) {
                        localy -= chunkSize;
                        chunkKey.y += 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                    else
                    if (localy < 0) {
                        localy += chunkSize;
                        chunkKey.y -= 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }

                }
                else {

                    localz += stepSignz;
                    dist = tMaxz;
                    tMaxz += tDeltaz;

                    lastFace = 2;

                    if (localz > chunkSize - 1) {
                        localz -= chunkSize;
                        chunkKey.z += 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                    else
                    if (localz < 0) {
                        localz += chunkSize;
                        chunkKey.z -= 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                }
            }

            if (currentChunk != null) {
                //if (GetRawBlock(currentChunk,localx,localy,localz) == true)
                if (currentChunk.GetLocalVoxelAt(localx, localy, localz) > 0) {
                    //Vector3 hitPos = pos + direction * dist;
                    Vector3 hitNormal = Vector3.zero;
                    switch (lastFace) {
                        case 0:
                        hitNormal = new Vector3(-stepSignx, 0, 0);
                        break;
                        case 1:
                        hitNormal = new Vector3(0, -stepSigny, 0);
                        break;
                        case 2:
                        hitNormal = new Vector3(0, 0, -stepSignz);
                        break;
                    }
                    return (true, dist, pos + direction * dist, hitNormal);
                }
                /*
                {
              
                    //calculate the normal
                    switch (lastFace)
                    {
                        case 0:
                            return (true, dist, (stepSignx < 0) ? Vector3.right : Vector3.left);
                        case 1:
                            return (true, dist, (stepSigny < 0) ? Vector3.up : Vector3.down);
                        default:
                            return (true, dist, (stepSignz < 0) ? Vector3.forward : Vector3.back);
                    } 
                }*/
            }
            else {
                //Jump to next chunk! In theory.


            }


        }
        return (false, maxDistance, Vector3.zero, Vector3.zero);
    }




    public int RaycastVoxelForLighting(Vector3 pos, Vector3 direction, float maxDistance, bool debug = false) {

        //integer voxel position (world)
        Vector3Int snappedPosInt = FloorInt(pos);
        Vector3 snappedPosFloat = Floor(pos);

        //Position inside a chunk
        Vector3Int localPos = Chunk.WorldPosToLocalPos(snappedPosInt);

        //current chunk
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out Chunk currentChunk);

        int localx = localPos.x;
        int localy = localPos.y;
        int localz = localPos.z;


        //startSolid check
        if (currentChunk != null) {
            VoxelData vox = currentChunk.GetLocalVoxelAt(localx, localy, localz);

            if (VoxelIsSolid(vox)) {
                //Continue!
                return 2;
            }
        }


        int stepSignx = direction.x < 0 ? -1 : 1;
        int stepSigny = direction.y < 0 ? -1 : 1;
        int stepSignz = direction.z < 0 ? -1 : 1;


        float tDeltax = Mathf.Abs(1.0f / direction.x);
        float tDeltay = Mathf.Abs(1.0f / direction.y);
        float tDeltaz = Mathf.Abs(1.0f / direction.z);

        float tMaxx = stepSignx > 0.0f ? snappedPosFloat.x + 1.0f - pos.x : pos.x - snappedPosFloat.x;
        float tMaxy = stepSigny > 0.0f ? snappedPosFloat.y + 1.0f - pos.y : pos.y - snappedPosFloat.y;
        float tMaxz = stepSignz > 0.0f ? snappedPosFloat.z + 1.0f - pos.z : pos.z - snappedPosFloat.z;

        tMaxx *= tDeltax;
        tMaxy *= tDeltay;
        tMaxz *= tDeltaz;

        float dist = 0;


        while (dist < maxDistance) {
            if (tMaxx < tMaxy) {
                if (tMaxx < tMaxz) {
                    localx += stepSignx;
                    dist = tMaxx;
                    tMaxx += tDeltax;


                    if (localx > chunkSize - 1) {
                        localx -= chunkSize;
                        chunkKey.x += 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                    else
                     if (localx < 0) {
                        localx += chunkSize;
                        chunkKey.x -= 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                }
                else {
                    localz += stepSignz;
                    dist = tMaxz;
                    tMaxz += tDeltaz;


                    if (localz > chunkSize - 1) {
                        localz -= chunkSize;
                        chunkKey.z += 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                    else
                     if (localz < 0) {
                        localz += chunkSize;
                        chunkKey.z -= 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                }
            }
            else {
                if (tMaxy < tMaxz) {
                    localy += stepSigny;
                    dist = tMaxy;
                    tMaxy += tDeltay;


                    if (localy > chunkSize - 1) {
                        localy -= chunkSize;
                        chunkKey.y += 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                    else
                    if (localy < 0) {
                        localy += chunkSize;
                        chunkKey.y -= 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }

                }
                else {

                    localz += stepSignz;
                    dist = tMaxz;
                    tMaxz += tDeltaz;

                    if (localz > chunkSize - 1) {
                        localz -= chunkSize;
                        chunkKey.z += 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                    else
                    if (localz < 0) {
                        localz += chunkSize;
                        chunkKey.z -= 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                }
            }

            if (currentChunk != null) {
                VoxelData vox = currentChunk.GetLocalVoxelAt(localx, localy, localz);


                if (VoxelIsSolid(vox)) {
                    //calculate the impact point
                    //Vector3 finalPos = (chunkKey * chunkSize) + new Vector3(localx, localy, localz) + posInsideVoxel;

                    return 1;
                }
            }
            else {
                //Jump to next chunk! In theory.


            }


        }
        return 0;
    }


    public (bool, Vector3Int, float, Vector3, Color, Chunk) RaycastVoxelForRadiosity(Vector3 pos, Vector3 direction, float maxDistance, bool debug = false) {

        //integer voxel position (world)
        Vector3Int snappedPosInt = FloorInt(pos);
        Vector3 snappedPosFloat = Floor(pos);


        //Position inside a chunk
        Vector3Int localPos = Chunk.WorldPosToLocalPos(snappedPosInt);

        //current chunk
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out Chunk currentChunk);

        int localx = localPos.x;
        int localy = localPos.y;
        int localz = localPos.z;

        int stepSignx = direction.x < 0 ? -1 : 1;
        int stepSigny = direction.y < 0 ? -1 : 1;
        int stepSignz = direction.z < 0 ? -1 : 1;

        float tDeltax = Mathf.Abs(1.0f / direction.x);
        float tDeltay = Mathf.Abs(1.0f / direction.y);
        float tDeltaz = Mathf.Abs(1.0f / direction.z);

        float tMaxx = stepSignx > 0.0f ? snappedPosFloat.x + 1.0f - pos.x : pos.x - snappedPosFloat.x;
        float tMaxy = stepSigny > 0.0f ? snappedPosFloat.y + 1.0f - pos.y : pos.y - snappedPosFloat.y;
        float tMaxz = stepSignz > 0.0f ? snappedPosFloat.z + 1.0f - pos.z : pos.z - snappedPosFloat.z;

        tMaxx *= tDeltax;
        tMaxy *= tDeltay;
        tMaxz *= tDeltaz;

        float dist = 0;
        int lastFace;

        int lastx = localx;
        int lasty = localy;
        int lastz = localz;

        while (dist < maxDistance) {
            if (tMaxx < tMaxy) {
                if (tMaxx < tMaxz) {
                    localx += stepSignx;
                    dist = tMaxx;
                    tMaxx += tDeltax;

                    lastFace = 0;

                    if (localx > chunkSize - 1) {
                        localx -= chunkSize;
                        chunkKey.x += 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                    else
                     if (localx < 0) {
                        localx += chunkSize;
                        chunkKey.x -= 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                }
                else {
                    localz += stepSignz;
                    dist = tMaxz;
                    tMaxz += tDeltaz;
                    lastFace = 2;

                    if (localz > chunkSize - 1) {
                        localz -= chunkSize;
                        chunkKey.z += 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                    else
                     if (localz < 0) {
                        localz += chunkSize;
                        chunkKey.z -= 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                }
            }
            else {
                if (tMaxy < tMaxz) {
                    localy += stepSigny;
                    dist = tMaxy;
                    tMaxy += tDeltay;
                    lastFace = 1;

                    if (localy > chunkSize - 1) {
                        localy -= chunkSize;
                        chunkKey.y += 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                    else
                    if (localy < 0) {
                        localy += chunkSize;
                        chunkKey.y -= 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }

                }
                else {

                    localz += stepSignz;
                    dist = tMaxz;
                    tMaxz += tDeltaz;

                    lastFace = 2;

                    if (localz > chunkSize - 1) {
                        localz -= chunkSize;
                        chunkKey.z += 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                    else
                    if (localz < 0) {
                        localz += chunkSize;
                        chunkKey.z -= 1;
                        chunks.TryGetValue(chunkKey, out currentChunk);
                    }
                }
            }

            if (currentChunk != null) {

                VoxelData vox = currentChunk.GetLocalVoxelAt(localx, localy, localz);
                if (VoxelIsSolid(vox)) {
                    //Calculate the center of the voxel before the one we hit
                    Vector3Int finalVoxel = (chunkKey * chunkSize) + new Vector3Int(lastx, lasty, lastz);

                    VoxelBlocks.BlockDefinition block = blocks.GetBlock(VoxelDataToBlockId(vox));
                    Color col = block.averageColor[lastFace];

                    //calculate the normal
                    switch (lastFace) {
                        case 0:
                        return (true, finalVoxel, dist, (stepSignx < 0) ? Vector3.right : Vector3.left, col, currentChunk);
                        case 1:
                        return (true, finalVoxel, dist, (stepSigny < 0) ? Vector3.up : Vector3.down, col, currentChunk);
                        default:
                        return (true, finalVoxel, dist, (stepSignz < 0) ? Vector3.forward : Vector3.back, col, currentChunk);
                    }
                }
            }
            else {
                //Jump to next chunk! In theory.
            }

            lastx = localx;
            lasty = localy;
            lastz = localz;
        }
        return (false, Vector3Int.zero, maxDistance, Vector3.zero, Color.black, null); //no light from the sky?
    }


}