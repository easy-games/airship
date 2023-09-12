using System;
using UnityEngine;

[LuauAPI]
public class LayerMaskAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(LayerMask);
    }

    public override int OverrideStaticMethod(IntPtr thread, string methodName, int numParameters, int[] parameterDataPODTypes,
        IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {
        if (methodName == "GetMask")
        {
            if (numParameters == 1)
            {
                string name = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    paramaterDataSizes);

                var val = LayerMask.GetMask(name);
                LuauCore.WritePropertyToThread(thread, val, val.GetType());
                return 1;
            }
        }

        return -1;
    }
}