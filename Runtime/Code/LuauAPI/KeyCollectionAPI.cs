using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using Luau;
using UnityEngine;

[LuauAPI]
public class KeyCollectionAPI : BaseLuaAPIClass
{
    
    public override Type GetAPIType()
    {
        return typeof(Dictionary<System.Int32,NetworkObject>.KeyCollection);
    }

    public override int OverrideMemberMethod(LuauContext context, IntPtr thread, object targetObject, string methodName, int numParameters,
        int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {
        if (methodName == "ElementAt")
        {
            if (numParameters == 1)
            {
                int index = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
                var keyCollection = (Dictionary<System.Int32, NetworkObject>.KeyCollection)targetObject;
                object value = keyCollection.ElementAt(index);
                LuauCore.WritePropertyToThread(thread, value, value.GetType());
                return 1;
            }
        }

        return -1;
    }

}