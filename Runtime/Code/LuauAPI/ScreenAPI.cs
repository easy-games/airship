using System;
using Luau;

[LuauAPI]
public class ScreenAPI : BaseLuaAPIClass
{

	public override Type GetAPIType()
	{
		return typeof(UnityEngine.Screen); 
	}
	public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
	{
		return -1;
	}

	public override int OverrideMemberMethod(LuauContext context, IntPtr thread, System.Object targetObject, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
	{
		return -1;
	}

}