using System;
using System.Collections.Generic;
using Luau;
using UnityEngine;

[LuauAPI]
public class RendererAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Renderer);
    }

    public override int OverrideMemberMethod(LuauContext context, IntPtr thread, object targetObject, string methodName, int numParameters,
        ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        if (methodName is "SetMaterial") {
            if (numParameters == 2) {
                var indx = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
                var materialObj = LuauCore.GetParameterAsObject(1, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes, thread);

                if (targetObject is Renderer renderer && materialObj is Material material && indx >= 0) {
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

    public override Type[] GetDescendantTypes() {
        return new[] {
            typeof(MeshRenderer), typeof(SkinnedMeshRenderer), typeof(SpriteRenderer), typeof(BillboardRenderer),
            typeof(LineRenderer), typeof(TrailRenderer), typeof(ParticleSystemRenderer)
        };
    }
}