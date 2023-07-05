using System;

using System.Runtime.InteropServices;
using UnityEngine;

public static class LuauPlugin
{
	public delegate void PrintCallback(IntPtr thread, int style, IntPtr buffer, int length);
	public delegate int GetPropertyCallback(IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameSize);
	public delegate int SetPropertyCallback(IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameSize, LuauCore.PODTYPE type, IntPtr propertyData, int propertySize);
	public delegate int CallMethodCallback(IntPtr thread, int instanceId, IntPtr className, int classNameSize, IntPtr methodName, int methodNameSize, int numParameters, IntPtr firstParameterType, IntPtr firstParameterData, IntPtr firstParameterSize);
	public delegate int ObjectGCCallback(int instanceId);
	public delegate IntPtr RequireCallback(IntPtr thread, IntPtr fileName, int fileNameSize);
	public delegate int RequirePathCallback(IntPtr thread, IntPtr fileName, int fileNameSize);
	public delegate int YieldCallback(IntPtr thread, IntPtr host);

#if UNITY_EDITOR_WIN && USE_NATIVE_PLUGIN_LOADER
    [PluginFunctionAttr("Startup")]
    public static Startup LuauStartup = null;
    public delegate bool Startup(PrintCallback printCallback, GetPropertyCallback getPropertyCallback, SetPropertyCallback setPropertyCallback, CallMethodCallback callMethodCallback, ObjectGCCallback gcCallback, RequireCallback requireCallback, IntPtr stringArray, int stringCount, RequirePathCallback requirePathCallback, YieldCallback yieldCallback);

	[PluginFunctionAttr("Reset")]
	public static Reset LuauReset = null;
	public delegate void Reset();

    [PluginFunctionAttr("CreateThread")]
    public static CreateThread LuauCreateThread = null;
    public delegate IntPtr CreateThread(IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int gameObjectId);

    [PluginFunctionAttr("CompileCode")]
    public static CompileCode LuauCompileCode = null;
    public delegate IntPtr CompileCode(IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int optimizationLevel);

    [PluginFunctionAttr("RunThread")]
    public static RunThread LuauRunThread = null;
    public delegate int RunThread(IntPtr thread);

    [PluginFunctionAttr("CallMethodOnThread")]
    public static CallMethodOnThread LuauCallMethodOnThread = null;
    public delegate int CallMethodOnThread(IntPtr thread, IntPtr methodName, int methodNameSize, int numParameters);

    [PluginFunctionAttr("DestroyThread")]
    public static DestroyThread LuauDestroyThread = null;
    public delegate void DestroyThread(IntPtr thread);

    [PluginFunctionAttr("PushValueToThread")]
    public static PushValueToThread LuauPushValueToThread = null;
    public delegate void PushValueToThread(IntPtr thread, int type, IntPtr data, int dataSize);

    [PluginFunctionAttr("PushVector3ToThread")]
    public static PushVector3ToThread LuauPushVector3ToThread = null;
    public delegate void PushVector3ToThread(IntPtr thread, float x, float y, float z);
#else

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	private static extern bool Startup(PrintCallback printCallback, GetPropertyCallback getPropertyCallback, SetPropertyCallback setPropertyCallback, CallMethodCallback callMethodCallback, ObjectGCCallback gcCallback, RequireCallback requireCallback, IntPtr stringArray, int stringCount, RequirePathCallback requirePathCallback, YieldCallback yieldCallback);
	public static bool LuauStartup(PrintCallback printCallback, GetPropertyCallback getPropertyCallback, SetPropertyCallback setPropertyCallback, CallMethodCallback callMethodCallback, ObjectGCCallback gcCallback, RequireCallback requireCallback, IntPtr stringArray, int stringCount, RequirePathCallback requirePathCallback, YieldCallback yieldCallback)
	{
		return Startup(printCallback, getPropertyCallback, setPropertyCallback, callMethodCallback, gcCallback, requireCallback, stringArray, stringCount, requirePathCallback, yieldCallback);
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	private static extern bool Reset();
	public static bool LuauReset()
	{
		return Reset();
	}


#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	private static extern bool Shutdown();
	public static bool LuauShutdown()
	{
		return Shutdown();
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr CreateThread(IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int gameObjectId, bool binary);
	public static IntPtr LuauCreateThread(IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int gameObjectId, bool binary)
	{
		return CreateThread(script, scriptLength, filename, filenameLength, gameObjectId, binary);
	}


#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr CompileCode(IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int optimizationLevel);
	public static IntPtr LuauCompileCode(IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int optimizationLevel)
	{
		return CompileCode(script, scriptLength, filename, filenameLength, optimizationLevel);
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern int RunThread(IntPtr thread);
	public static int LuauRunThread(IntPtr thread)
	{
		return RunThread(thread);
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern int CallMethodOnThread(IntPtr thread, IntPtr methodName, int methodNameSize, int numParameters);
	public static int LuauCallMethodOnThread(IntPtr thread, IntPtr methodName, int methodNameSize, int numParameters)
	{
		return CallMethodOnThread(thread, methodName, methodNameSize, numParameters);
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void DestroyThread(IntPtr thread);
	public static void LuauDestroyThread(IntPtr thread)
	{
		Debug.Log("Destroying thread " + thread);
		DestroyThread(thread);

	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void UnpinThread(IntPtr thread);
	public static void LuauUnpinThread(IntPtr thread)
	{
		// Debug.Log("Unpinning thread " + thread);
		UnpinThread(thread);

	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void PushValueToThread(IntPtr thread, int type, IntPtr data, int dataSize);
	public static void LuauPushValueToThread(IntPtr thread, int type, IntPtr data, int dataSize)
	{
		PushValueToThread(thread, type, data, dataSize);
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void PushVector3ToThread(IntPtr thread, float x, float y, float z);
	public static void LuauPushVector3ToThread(IntPtr thread, float x, float y, float z)
	{
		PushVector3ToThread(thread, x, y, z);
	}
#endif

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void GetDebugTrace(IntPtr thread);
	public static void LuauGetDebugTrace(IntPtr thread)
	{
		GetDebugTrace(thread);
	}

}