using System;
using System.Collections.Generic;
using Luau;
using UnityEngine;

[LuauAPI]
public class RendererAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Renderer);
    }

    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters,
        int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes) {
        if (methodName is "SetMaterial") {
            if (numParameters == 3) {
                var skinnedMeshObj = LuauCore.GetParameterAsObject(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes, thread);
                var indx = LuauCore.GetParameterAsInt(1, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
                var materialObj = LuauCore.GetParameterAsObject(2, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes, thread);

                if (skinnedMeshObj is Renderer renderer && materialObj is Material material && indx >= 0) {
                    List<Material> materials = new();
                    renderer.GetMaterials(materials);
                    if (materials.Count <= indx) {
                        materials.Add(material);
                    } else {
                        materials[indx] = material;
                    }
                    renderer.SetMaterials(materials);
                    return 0;
                }
            }
        }
        return -1;
    }
}