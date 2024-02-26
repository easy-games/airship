using System;
using Luau;
using UnityEngine;

[LuauAPI]
public class LayerMaskAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(LayerMask);
    }

    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters, int[] parameterDataPODTypes,
        IntPtr[] parameterDataPtrs, int[] paramaterDataSizes) {
        if (methodName == "GetMask") {
            string[] layerNames = new string[numParameters];
            for (int i = 0; i < numParameters; i++) {
                string name = LuauCore.GetParameterAsString(i, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    paramaterDataSizes);
                layerNames[i] = name;
            }
            var val = LayerMask.GetMask(layerNames);
            LuauCore.WritePropertyToThread(thread, val, val.GetType());
            return 1;
        }
        if (methodName == "InvertMask")
        {
            if (numParameters == 1)
            {
                int layerMask = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);

                LuauCore.WritePropertyToThread(thread, ~layerMask, typeof(int));
                return 1;
            }
        }

        return -1;
    }
}