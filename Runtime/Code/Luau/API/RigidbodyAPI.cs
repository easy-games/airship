using System;
using Luau;
using UnityEngine;

[LuauAPI]
public class RigidbodyAPI : BaseLuaAPIClass
{
	public override Type GetAPIType()
	{
		return typeof(Rigidbody);
	}

    public override int OverrideMemberMethod(IntPtr thread, System.Object targetObject, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {
        if (methodName == "AddForce_ForceMode")
        {
            if (numParameters != 2)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: AddForce_ForceMode takes 2 parameters");
                return 0;
            }

            var force = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            var forceMode = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);

            var rb = (Rigidbody)targetObject;

            rb.AddForce(force, (ForceMode)forceMode);
            return 0;
        }

        return -1;
    }
}