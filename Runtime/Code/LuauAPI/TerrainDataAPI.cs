using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[LuauAPI]
public class TerrainDataAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(TerrainData);
    }

    public override int OverrideMemberMethod(LuauContext context, IntPtr thread, object targetObject, string methodName, int numParameters,
        int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes) {

        if (methodName == "RemoveTree" && numParameters == 1) {
            int index = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            TerrainData terrainData = (TerrainData)targetObject;
            var trees = new List<TreeInstance>(terrainData.treeInstances);
            trees.RemoveAt(index);
            ((TerrainData)targetObject).treeInstances = trees.ToArray();

            float[,] heights = terrainData.GetHeights(0, 0, 0, 0);
            terrainData.SetHeights(0, 0, heights);
            return 0;
        }

        return -1;
    }
}