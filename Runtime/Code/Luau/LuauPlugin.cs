#define DO_THREAD_SAFTEYCHECK
// #define DO_CALL_SAFTEYCHECK
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Threading;
using System.Runtime.InteropServices.WindowsRuntime;
using Luau;

public static class LuauPlugin
{
	public delegate void PrintCallback(IntPtr thread, int style, IntPtr buffer, int length);
	public delegate int GetPropertyCallback(IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameSize);
	public delegate int SetPropertyCallback(IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameSize, LuauCore.PODTYPE type, IntPtr propertyData, int propertySize);
	public delegate int CallMethodCallback(IntPtr thread, int instanceId, IntPtr className, int classNameSize, IntPtr methodName, int methodNameSize, int numParameters, IntPtr firstParameterType, IntPtr firstParameterData, IntPtr firstParameterSize, IntPtr shouldYield);
	public delegate int ObjectGCCallback(int instanceId, IntPtr objectDebugPointer);
	public delegate IntPtr RequireCallback(IntPtr thread, IntPtr fileName, int fileNameSize);
	public delegate int RequirePathCallback(IntPtr thread, IntPtr fileName, int fileNameSize);
	public delegate int YieldCallback(IntPtr thread, IntPtr host, IntPtr trace, int traceSize);

	public static int unityMainThreadId = -1;
	public static bool s_currentlyExecuting = false;
	public enum CurrentCaller
	{
		None,
		RunThread,
		CallMethodOnThread,
		CreateThread
	}
	
    public static CurrentCaller s_currentCaller = CurrentCaller.None;


    public static void ThreadSafteyCheck() {
#if DO_THREAD_SAFTEYCHECK
		if (unityMainThreadId == -1) {
			//Make the assumption that the first thread to call in here is the main thread
            // unityMainThreadId = Thread.CurrentThread.ManagedThreadId;
            // Debug.Log($"Setting main thread id to {unityMainThreadId}");
            // Debug.LogWarning($"[Thread Safety] Unexpected call made while UnityMainThreadId was not being tracked. CurrentThreadId={Thread.CurrentThread.ManagedThreadId}");
        } else {
            if (unityMainThreadId != Thread.CurrentThread.ManagedThreadId) {
                // Debug.LogError($"LuauPlugin called from a thread other than the main thread! CurrentThreadId={Thread.CurrentThread.ManagedThreadId}, MainThreadId={unityMainThreadId}");
            }
        }
#endif       
    }

	public static void BeginExecutionCheck(CurrentCaller caller)
	{
#if DO_CALL_SAFTEYCHECK
		if (s_currentlyExecuting == true)
		{
            Debug.LogError("LuauPlugin called " + caller + " while a lua thread was still executing " + s_currentCaller);
        }
        s_currentCaller = caller;
		s_currentlyExecuting = true;
#endif
	}
    public static void EndExecutionCheck()
    {
#if DO_CALL_SAFTEYCHECK
        s_currentlyExecuting = false;
		s_currentCaller = CurrentCaller.None;
#endif
    }

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
    [DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
    private static extern bool InitializePrintCallback(PrintCallback printCallback);
    public static bool LuauInitializePrintCallback(PrintCallback printCallback)
    {
	    ThreadSafteyCheck();

	    bool returnValue = InitializePrintCallback(printCallback);
	    return returnValue;
    }

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
    [DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	private static extern bool Startup(GetPropertyCallback getPropertyCallback, SetPropertyCallback setPropertyCallback, CallMethodCallback callMethodCallback, ObjectGCCallback gcCallback, RequireCallback requireCallback, IntPtr stringArray, int stringCount, RequirePathCallback requirePathCallback, YieldCallback yieldCallback);
	public static bool LuauStartup(GetPropertyCallback getPropertyCallback, SetPropertyCallback setPropertyCallback, CallMethodCallback callMethodCallback, ObjectGCCallback gcCallback, RequireCallback requireCallback, IntPtr stringArray, int stringCount, RequirePathCallback requirePathCallback, YieldCallback yieldCallback)
	{
        ThreadSafteyCheck();
        
        bool returnValue = Startup(getPropertyCallback, setPropertyCallback, callMethodCallback, gcCallback, requireCallback, stringArray, stringCount, requirePathCallback, yieldCallback);
        return returnValue;
    }

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	private static extern bool Reset();
	public static bool LuauReset()
	{
        ThreadSafteyCheck();

        bool returnValue = Reset();
        return returnValue;
	}



#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
    [DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
    private static extern void RunEndFrameLogic(IntPtr listOfGameObjectIds, int numGameObjectIds, IntPtr listOfDestroyedGameObjectIds, int numDestoyedGameObjectIds);
    public static void LuauRunEndFrameLogic(IntPtr listOfGameObjectIds, int numGameObjectIds, IntPtr listOfDestroyedGameObjectIds, int numDestoyedGameObjectIds)
    {
        ThreadSafteyCheck();
        RunEndFrameLogic(listOfGameObjectIds, numGameObjectIds, listOfDestroyedGameObjectIds, numDestoyedGameObjectIds);
    }


#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
    [DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	private static extern bool Shutdown();
	public static bool LuauShutdown()
	{
		ThreadSafteyCheck();
 
        bool returnValue = Shutdown();
		return returnValue;
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void CreateAirshipComponent(IntPtr thread, int unityInstanceId, int componentId, LuauMetadataPropertyMarshalDto[] props, int nProps);
	public static void LuauCreateAirshipComponent(IntPtr thread, int unityInstanceId, int componentId, LuauMetadataPropertyMarshalDto[] props)
	{
		ThreadSafteyCheck();
		CreateAirshipComponent(thread, unityInstanceId, componentId, props, props.Length);
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void RemoveAirshipComponent(IntPtr thread, int unityInstanceId, int componentId);
	public static void LuauRemoveAirshipComponent(IntPtr thread, int unityInstanceId, int componentId)
	{
		ThreadSafteyCheck();
		RemoveAirshipComponent(thread, unityInstanceId, componentId);
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void WriteToAirshipComponent(IntPtr thread, int unityInstanceId, int componentId, string name, IntPtr value, int valueSize, int valueType);
	public static void LuauWriteToAirshipComponent(IntPtr thread, int unityInstanceId, int componentId, string name, IntPtr value, int valueSize, int valueType)
	{
		ThreadSafteyCheck();
		WriteToAirshipComponent(thread, unityInstanceId, componentId, name, value, valueSize, valueType);
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void PushAirshipComponent(IntPtr thread, int unityInstanceId, int componentId);
	public static void LuauPushAirshipComponent(IntPtr thread, int unityInstanceId, int componentId)
	{
		ThreadSafteyCheck();
		PushAirshipComponent(thread, unityInstanceId, componentId);
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void PushAirshipComponents(IntPtr thread, int unityInstanceId, [In, Out] int[] componentIds, int nComponents, bool appendToTable);
	public static void LuauPushAirshipComponents(IntPtr thread, int unityInstanceId, int[] componentIds, bool appendToTable = false)
	{
		ThreadSafteyCheck();
		PushAirshipComponents(thread, unityInstanceId, componentIds, componentIds.Length, appendToTable);
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void UpdateIndividualAirshipComponent(IntPtr thread, int unityInstanceId, int componentId, int updateType, float dt, bool safe);
	public static void LuauUpdateIndividualAirshipComponent(IntPtr thread, int unityInstanceId, int componentId, AirshipComponentUpdateType updateType, float dt, bool safe)
	{
		ThreadSafteyCheck();
		UpdateIndividualAirshipComponent(thread, unityInstanceId, componentId, (int)updateType, dt, true);
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void UpdateAllAirshipComponents(int updateType, float dt);
	public static void LuauUpdateAllAirshipComponents(AirshipComponentUpdateType updateType, float dt)
	{
		ThreadSafteyCheck();
		UpdateAllAirshipComponents((int)updateType, dt);
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr CreateThread(IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int gameObjectId, bool binary);
	public static IntPtr LuauCreateThread(IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int gameObjectId, bool binary)
	{
		ThreadSafteyCheck();
		BeginExecutionCheck(CurrentCaller.CreateThread);
		IntPtr returnValue = CreateThread(script, scriptLength, filename, filenameLength, gameObjectId, binary);
        EndExecutionCheck();
        return returnValue;
    }

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void SetThreadDestroyed(IntPtr thread);
	public static void LuauSetThreadDestroyed(IntPtr thread)
	{
		ThreadSafteyCheck();
		SetThreadDestroyed(thread);
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr CompileCode(IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int optimizationLevel);
	public static IntPtr LuauCompileCode(IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int optimizationLevel)
	{
        // ThreadSafteyCheck();
        IntPtr returnValue = CompileCode(script, scriptLength, filename, filenameLength, optimizationLevel);
		return returnValue;
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern int RunThread(IntPtr thread, int nArgs);
	public static int LuauRunThread(IntPtr thread, int nArgs = 0)
	{
        ThreadSafteyCheck();
		//BeginExecutionCheck(CurrentCaller.CreateThread);
        int returnValue = RunThread(thread, nArgs);
        //EndExecutionCheck();
        return returnValue;
    }

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern int CallMethodOnThread(IntPtr thread, IntPtr methodName, int methodNameSize, int numParameters);
	public static int LuauCallMethodOnThread(IntPtr thread, IntPtr methodName, int methodNameSize, int numParameters)
	{
        ThreadSafteyCheck();
		BeginExecutionCheck(CurrentCaller.CallMethodOnThread);
        int returnValue = CallMethodOnThread(thread, methodName, methodNameSize, numParameters);
        EndExecutionCheck();
        return returnValue;
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
        ThreadSafteyCheck();
        DestroyThread(thread);
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void PinThread(IntPtr thread);
	public static void LuauPinThread(IntPtr thread)
	{
		// Debug.Log("Unpinning thread " + thread);
		ThreadSafteyCheck();
		PinThread(thread);
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
        ThreadSafteyCheck();
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
        ThreadSafteyCheck();
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
        ThreadSafteyCheck();
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
        ThreadSafteyCheck();
        GetDebugTrace(thread);
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void RunTaskScheduler(float now);
	public static void LuauRunTaskScheduler() {
		ThreadSafteyCheck();
		RunTaskScheduler(Time.time);
	}
}
