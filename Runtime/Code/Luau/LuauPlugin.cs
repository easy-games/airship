#define DO_THREAD_SAFTEYCHECK
// #define DO_CALL_SAFTEYCHECK
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Threading;
using Luau;
using Debug = UnityEngine.Debug;

public static class LuauPlugin
{
	public delegate void PrintCallback(LuauContext context, IntPtr thread, int style, int gameObjectId, IntPtr buffer, int length, IntPtr ptr);
	public delegate int GetPropertyCallback(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameSize);
	public delegate int SetPropertyCallback(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameSize, LuauCore.PODTYPE type, IntPtr propertyData, int propertySize);
	public delegate int CallMethodCallback(LuauContext context, IntPtr thread, int instanceId, IntPtr className, int classNameSize, IntPtr methodName, int methodNameSize, int numParameters, IntPtr firstParameterType, IntPtr firstParameterData, IntPtr firstParameterSize, IntPtr shouldYield);
	public delegate int ConstructorCallback(LuauContext context, IntPtr thread, IntPtr className, int classNameSize, int numParameters, IntPtr firstParameterType, IntPtr firstParameterData, IntPtr firstParameterSize);
	public delegate int ObjectGCCallback(int instanceId, IntPtr objectDebugPointer);
	public delegate IntPtr RequireCallback(LuauContext context, IntPtr thread, IntPtr fileName, int fileNameSize);
	public delegate int RequirePathCallback(LuauContext context, IntPtr thread, IntPtr fileName, int fileNameSize);
	public delegate int YieldCallback(LuauContext context, IntPtr thread, IntPtr host, IntPtr trace, int traceSize);

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


    private static void ThreadSafetyCheck() {
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowIfNotNull(IntPtr luauExceptionPtr) {
	    if (luauExceptionPtr != IntPtr.Zero) {
		    throw new LuauException(luauExceptionPtr);
	    }
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
    public static bool LuauInitializePrintCallback(PrintCallback printCallback) {
	    ThreadSafetyCheck();

	    bool returnValue = InitializePrintCallback(printCallback);
	    return returnValue;
    }

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
    [DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	private static extern bool Startup(GetPropertyCallback getPropertyCallback, SetPropertyCallback setPropertyCallback, CallMethodCallback callMethodCallback, ObjectGCCallback gcCallback, RequireCallback requireCallback, ConstructorCallback constructorCallback, IntPtr stringArray, int stringCount, RequirePathCallback requirePathCallback, YieldCallback yieldCallback);
	public static bool LuauStartup(GetPropertyCallback getPropertyCallback, SetPropertyCallback setPropertyCallback, CallMethodCallback callMethodCallback, ObjectGCCallback gcCallback, RequireCallback requireCallback, ConstructorCallback constructorCallback, IntPtr stringArray, int stringCount, RequirePathCallback requirePathCallback, YieldCallback yieldCallback)
	{
        ThreadSafetyCheck();
        
        bool returnValue = Startup(getPropertyCallback, setPropertyCallback, callMethodCallback, gcCallback, requireCallback, constructorCallback, stringArray, stringCount, requirePathCallback, yieldCallback);
        return returnValue;
    }
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	private static extern void SubsystemRegistration();
	public static void LuauSubsystemRegistration() {
		ThreadSafetyCheck();
		SubsystemRegistration();
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	private static extern bool OpenState(LuauContext context);
	public static bool LuauOpenState(LuauContext context) {
		ThreadSafetyCheck();
		return OpenState(context);
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	private static extern bool CloseState(LuauContext context);
	public static bool LuauCloseState(LuauContext context) {
		ThreadSafetyCheck();
		return CloseState(context);
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	private static extern bool Reset(LuauContext context);
	public static bool LuauReset(LuauContext context) {
        ThreadSafetyCheck();

        bool returnValue = Reset(context);
        return returnValue;
	}



#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
    [DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
    private static extern void RunEndFrameLogic(IntPtr listOfGameObjectIds, int numGameObjectIds, IntPtr listOfDestroyedGameObjectIds, int numDestroyedGameObjectIds);
    public static void LuauRunEndFrameLogic(IntPtr listOfGameObjectIds, int numGameObjectIds, IntPtr listOfDestroyedGameObjectIds, int numDestroyedGameObjectIds) {
        ThreadSafetyCheck();
        RunEndFrameLogic(listOfGameObjectIds, numGameObjectIds, listOfDestroyedGameObjectIds, numDestroyedGameObjectIds);
    }


#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
    [DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	private static extern bool Shutdown();
	public static bool LuauShutdown()
	{
		ThreadSafetyCheck();
 
        bool returnValue = Shutdown();
		return returnValue;
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr CreateAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, LuauMetadataPropertyMarshalDto[] props, int nProps, int transformInstanceId);
	public static void LuauCreateAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, LuauMetadataPropertyMarshalDto[] props, int transformInstanceId)
	{
		ThreadSafetyCheck();
		ThrowIfNotNull(CreateAirshipComponent(context, thread, unityInstanceId, componentId, props, props.Length, transformInstanceId));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr RemoveAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId);
	public static void LuauRemoveAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId)
	{
		ThreadSafetyCheck();
		ThrowIfNotNull(RemoveAirshipComponent(context, thread, unityInstanceId, componentId));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr WriteToAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, LuauMetadataPropertyMarshalDto prop);
	public static void LuauWriteToAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, LuauMetadataPropertyMarshalDto prop)
	{
		ThreadSafetyCheck();
		ThrowIfNotNull(WriteToAirshipComponent(context, thread, unityInstanceId, componentId, prop));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr PushAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId);
	public static void LuauPushAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId)
	{
		ThreadSafetyCheck();
		ThrowIfNotNull(PushAirshipComponent(context, thread, unityInstanceId, componentId));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr PushAirshipComponents(LuauContext context, IntPtr thread, int unityInstanceId, [In, Out] int[] componentIds, int nComponents, bool appendToTable);
	public static void LuauPushAirshipComponents(LuauContext context, IntPtr thread, int unityInstanceId, int[] componentIds, bool appendToTable = false)
	{
		ThreadSafetyCheck();
		ThrowIfNotNull(PushAirshipComponents(context, thread, unityInstanceId, componentIds, componentIds.Length, appendToTable));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr UpdateIndividualAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int updateType, float dt, bool safe);
	public static void LuauUpdateIndividualAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, AirshipComponentUpdateType updateType, float dt, bool safe) {
		ThreadSafetyCheck();
		ThrowIfNotNull(UpdateIndividualAirshipComponent(context, thread, unityInstanceId, componentId, (int)updateType, dt, true));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr UpdateCollisionAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int updateType, int collisionObjId);
	public static void LuauUpdateCollisionAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, AirshipComponentUpdateType updateType, int collisionObjId)
	{
		ThreadSafetyCheck();
		ThrowIfNotNull(UpdateCollisionAirshipComponent(context, thread, unityInstanceId, componentId, (int)updateType, collisionObjId));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr UpdateAllAirshipComponents(LuauContext context, int updateType, float dt);
	public static void LuauUpdateAllAirshipComponents(LuauContext context, AirshipComponentUpdateType updateType, float dt)
	{
		ThreadSafetyCheck();
		ThrowIfNotNull(UpdateAllAirshipComponents(context, (int)updateType, dt));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern bool HasAirshipMethod(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int updateType);
	public static bool LuauHasAirshipMethod(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, AirshipComponentUpdateType updateType)
	{
		ThreadSafetyCheck();
		return HasAirshipMethod(context, thread, unityInstanceId, componentId, (int)updateType);
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr CreateThread(LuauContext context, IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int gameObjectId, bool binary);
	public static IntPtr LuauCreateThread(LuauContext context, IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int gameObjectId, bool binary)
	{
		ThreadSafetyCheck();
		BeginExecutionCheck(CurrentCaller.CreateThread);
		IntPtr returnValue = CreateThread(context, script, scriptLength, filename, filenameLength, gameObjectId, binary);
        EndExecutionCheck();
        return returnValue;
    }

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr CreateThreadWithCachedModule(LuauContext context, string filename, int gameObjectId);
	public static IntPtr LuauCreateThreadWithCachedModule(LuauContext context, string filename, int gameObjectId)
	{
		ThreadSafetyCheck();
		IntPtr returnValue = CreateThreadWithCachedModule(context, filename, gameObjectId);
		EndExecutionCheck();
		return returnValue;
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr CacheModuleOnThread(IntPtr thread, string filename);
	public static void LuauCacheModuleOnThread(IntPtr thread, string filename)
	{
		ThreadSafetyCheck();
		ThrowIfNotNull(CacheModuleOnThread(thread, filename));
		EndExecutionCheck();
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr SetThreadDestroyed(IntPtr thread);
	public static void LuauSetThreadDestroyed(IntPtr thread)
	{
		ThreadSafetyCheck();
		ThrowIfNotNull(SetThreadDestroyed(thread));
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
        ThreadSafetyCheck();
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
        ThreadSafetyCheck();
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
        ThreadSafetyCheck();
        DestroyThread(thread);
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr PinThread(IntPtr thread);
	public static void LuauPinThread(IntPtr thread)
	{
		// Debug.Log("Unpinning thread " + thread);
		ThreadSafetyCheck();
		ThrowIfNotNull(PinThread(thread));
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr UnpinThread(IntPtr thread);
	public static void LuauUnpinThread(IntPtr thread)
	{
        // Debug.Log("Unpinning thread " + thread);
        ThreadSafetyCheck();
        ThrowIfNotNull(UnpinThread(thread));
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void PushValueToThread(IntPtr thread, int type, IntPtr data, int dataSize);
	public static void LuauPushValueToThread(IntPtr thread, int type, IntPtr data, int dataSize)
	{
        ThreadSafetyCheck();
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
        ThreadSafetyCheck();
        PushVector3ToThread(thread, x, y, z);
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void PushTableToThread(IntPtr thread, int initialSize);
	public static void LuauPushTableToThread(IntPtr thread, int initialSize = 0)
	{
		ThreadSafetyCheck();
		PushTableToThread(thread, initialSize);
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void ErrorThread(IntPtr thread, IntPtr msg, int msgSize);
	public static void LuauErrorThread(IntPtr thread, IntPtr msg, int msgSize)
	{
		ThreadSafetyCheck();
		ErrorThread(thread, msg, msgSize);
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void GetDebugTrace(IntPtr thread);
	public static void LuauGetDebugTrace(IntPtr thread)
	{
        ThreadSafetyCheck();
        GetDebugTrace(thread);
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr RunTaskScheduler(LuauContext context, float now);
	public static void LuauRunTaskScheduler(LuauContext context) {
		ThreadSafetyCheck();
		ThrowIfNotNull(RunTaskScheduler(context, Time.time));
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern LuauContext GetContextFromThread(IntPtr thread);
	public static LuauContext LuauGetContextFromThread(IntPtr thread) {
		ThreadSafetyCheck();
		return GetContextFromThread(thread);
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void Free(IntPtr thread);
	public static void LuauFree(IntPtr thread) {
		ThreadSafetyCheck();
		Free(thread);
	}
}
