#define DO_THREAD_SAFTEYCHECK
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Threading;

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

	public static Thread s_unityMainThread = null;
	public static void SafteyCheck()
	{
#if DO_THREAD_SAFTEYCHECK
		if (s_unityMainThread == null)
        {
			//Make the assumption that the first thread to call in here is the main thread
            s_unityMainThread = Thread.CurrentThread;
        }
        else
        {
            if (s_unityMainThread != Thread.CurrentThread)
            {
                Debug.LogError("LuauPlugin called from a thread other than the main thread!");
            }
        }
#endif       
    }


#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	private static extern bool Startup(PrintCallback printCallback, GetPropertyCallback getPropertyCallback, SetPropertyCallback setPropertyCallback, CallMethodCallback callMethodCallback, ObjectGCCallback gcCallback, RequireCallback requireCallback, IntPtr stringArray, int stringCount, RequirePathCallback requirePathCallback, YieldCallback yieldCallback);
	public static bool LuauStartup(PrintCallback printCallback, GetPropertyCallback getPropertyCallback, SetPropertyCallback setPropertyCallback, CallMethodCallback callMethodCallback, ObjectGCCallback gcCallback, RequireCallback requireCallback, IntPtr stringArray, int stringCount, RequirePathCallback requirePathCallback, YieldCallback yieldCallback)
	{
        SafteyCheck();
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
        SafteyCheck();
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
		SafteyCheck();
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
		SafteyCheck();
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
        SafteyCheck();
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
        SafteyCheck();
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
        SafteyCheck();
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
        SafteyCheck();
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
        SafteyCheck();
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
        SafteyCheck();
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
        SafteyCheck();
        PushVector3ToThread(thread, x, y, z);
	}
 

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void GetDebugTrace(IntPtr thread);
	public static void LuauGetDebugTrace(IntPtr thread)
	{
        SafteyCheck();
        GetDebugTrace(thread);
	}

}