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
	public delegate void ToStringCallback(IntPtr thread, int instanceId, IntPtr str, int maxLen, out int len);

	public static int unityMainThreadId = -1;
	public static bool s_currentlyExecuting = false;
	public enum CurrentCaller
	{
		None,
		RunThread,
		CallMethodOnThread,
		CreateThread
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct LuauBytecodeVersion {
		public int Min;
		public int Max;
		public int Target;
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
    private static void ThrowIfNotNullPtr(IntPtr luauExceptionPtr) {
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
	private static extern bool Startup(GetPropertyCallback getPropertyCallback, SetPropertyCallback setPropertyCallback, CallMethodCallback callMethodCallback, ObjectGCCallback gcCallback, RequireCallback requireCallback, ConstructorCallback constructorCallback, IntPtr stringArray, int stringCount, RequirePathCallback requirePathCallback, YieldCallback yieldCallback, ToStringCallback toStringCallback);
	public static bool LuauStartup(GetPropertyCallback getPropertyCallback, SetPropertyCallback setPropertyCallback, CallMethodCallback callMethodCallback, ObjectGCCallback gcCallback, RequireCallback requireCallback, ConstructorCallback constructorCallback, IntPtr stringArray, int stringCount, RequirePathCallback requirePathCallback, YieldCallback yieldCallback, ToStringCallback toStringCallback) {
        ThreadSafetyCheck();
        
        bool returnValue = Startup(getPropertyCallback, setPropertyCallback, callMethodCallback, gcCallback, requireCallback, constructorCallback, stringArray, stringCount, requirePathCallback, yieldCallback, toStringCallback);
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
	private static extern void RunBeginFrameLogic();
	public static void LuauRunBeginFrameLogic() {
		ThreadSafetyCheck();
		RunBeginFrameLogic();
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
    [DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
    private static extern IntPtr RunEndFrameLogic(IntPtr listOfGameObjectIds, int numGameObjectIds, IntPtr listOfDestroyedGameObjectIds, int numDestroyedGameObjectIds);
    public static void LuauRunEndFrameLogic(IntPtr listOfGameObjectIds, int numGameObjectIds, IntPtr listOfDestroyedGameObjectIds, int numDestroyedGameObjectIds) {
        ThreadSafetyCheck();
        ThrowIfNotNullPtr(RunEndFrameLogic(listOfGameObjectIds, numGameObjectIds, listOfDestroyedGameObjectIds, numDestroyedGameObjectIds));
    }


#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
    [DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	private static extern void Shutdown();
	public static void LuauShutdown() {
		ThreadSafetyCheck();
        Shutdown();
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr InitializeAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, LuauMetadataPropertyMarshalDto[] props, int nProps);
	public static void LuauInitializeAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, LuauMetadataPropertyMarshalDto[] props)
	{
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(InitializeAirshipComponent(context, thread, unityInstanceId, componentId, props, props.Length));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr PrewarmAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int transformComponentId);

	/// <summary>
	/// Create the reference pointer for the AirshipComponent
	/// </summary>
	internal static void LuauPrewarmAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int transformComponentId) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(PrewarmAirshipComponent(context, thread, unityInstanceId, componentId, transformComponentId));
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
		ThrowIfNotNullPtr(RemoveAirshipComponent(context, thread, unityInstanceId, componentId));
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
		ThrowIfNotNullPtr(WriteToAirshipComponent(context, thread, unityInstanceId, componentId, prop));
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
		ThrowIfNotNullPtr(PushAirshipComponent(context, thread, unityInstanceId, componentId));
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
		ThrowIfNotNullPtr(PushAirshipComponents(context, thread, unityInstanceId, componentIds, componentIds.Length, appendToTable));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr UpdateIndividualAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int updateType, float dt, bool safe);
	public static void LuauUpdateIndividualAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, AirshipComponentUpdateType updateType, float dt, bool safe) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(UpdateIndividualAirshipComponent(context, thread, unityInstanceId, componentId, (int)updateType, dt, true));
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
		ThrowIfNotNullPtr(UpdateCollisionAirshipComponent(context, thread, unityInstanceId, componentId, (int)updateType, collisionObjId));
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
		ThrowIfNotNullPtr(UpdateAllAirshipComponents(context, (int)updateType, dt));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr HasAirshipMethod(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int updateType, ref int result);
	public static bool LuauHasAirshipMethod(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, AirshipComponentUpdateType updateType) {
		ThreadSafetyCheck();
		var result = 0;
		ThrowIfNotNullPtr(HasAirshipMethod(context, thread, unityInstanceId, componentId, (int)updateType, ref result));
		return result != 0;
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr PushSignal(LuauContext context, IntPtr thread, int unityInstanceId, ulong propNameHash, ref int result);
	public static bool LuauPushSignal(LuauContext context, IntPtr thread, int unityInstanceId, ulong propNameHash) {
		ThreadSafetyCheck();
		var result = 0;
		ThrowIfNotNullPtr(PushSignal(context, thread, unityInstanceId, propNameHash, ref result));
		return result != 0;
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr EmitSignal(LuauContext context, IntPtr thread, int unityInstanceId, ulong propNameHash, int numParams, ref int result);
	public static bool LuauEmitSignal(LuauContext context, IntPtr thread, int unityInstanceId, ulong propNameHash, int numParams) {
		ThreadSafetyCheck();
		var result = 0;
		ThrowIfNotNullPtr(EmitSignal(context, thread, unityInstanceId, propNameHash, numParams, ref result));
		return result != 0;
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr DestroySignals(LuauContext context, IntPtr thread, int unityInstanceId);
	public static void LuauDestroySignals(LuauContext context, IntPtr thread, int unityInstanceId) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(DestroySignals(context, thread, unityInstanceId));
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
		ThrowIfNotNullPtr(CacheModuleOnThread(thread, filename));
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
		ThrowIfNotNullPtr(SetThreadDestroyed(thread));
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
	private static extern IntPtr RunThread(IntPtr thread, int nArgs, ref int result);
	public static int LuauRunThread(IntPtr thread, int nArgs = 0) {
        ThreadSafetyCheck();
		//BeginExecutionCheck(CurrentCaller.CreateThread);
        var returnValue = 0;
        ThrowIfNotNullPtr(RunThread(thread, nArgs, ref returnValue));
        //EndExecutionCheck();
        return returnValue;
    }

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr ResumeThread(IntPtr thread, int nArgs, ref int result);
	public static int LuauResumeThread(IntPtr thread, int nArgs = 0) {
		ThreadSafetyCheck();
		var returnValue = 0;
		ThrowIfNotNullPtr(ResumeThread(thread, nArgs, ref returnValue));
		return returnValue;
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr CallMethodOnThread(IntPtr thread, IntPtr methodName, int methodNameSize, int numParameters, ref int result);
	public static int LuauCallMethodOnThread(IntPtr thread, IntPtr methodName, int methodNameSize, int numParameters) {
        ThreadSafetyCheck();
		BeginExecutionCheck(CurrentCaller.CallMethodOnThread);
        var returnValue = 0;
        ThrowIfNotNullPtr(CallMethodOnThread(thread, methodName, methodNameSize, numParameters, ref returnValue));
        EndExecutionCheck();
        return returnValue;
    }

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr DestroyThread(IntPtr thread);
	public static void LuauDestroyThread(IntPtr thread) {
        ThreadSafetyCheck();
        ThrowIfNotNullPtr(DestroyThread(thread));
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr PinThread(IntPtr thread);
	public static void LuauPinThread(IntPtr thread) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(PinThread(thread));
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr UnpinThread(IntPtr thread);
	public static void LuauUnpinThread(IntPtr thread) {
        // Debug.Log("Unpinning thread " + thread);
        ThreadSafetyCheck();
        ThrowIfNotNullPtr(UnpinThread(thread));
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr PushValueToThread(IntPtr thread, int type, IntPtr data, int dataSize);
	public static void LuauPushValueToThread(IntPtr thread, int type, IntPtr data, int dataSize) {
        ThreadSafetyCheck();
        ThrowIfNotNullPtr(PushValueToThread(thread, type, data, dataSize));
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr PushVector3ToThread(IntPtr thread, float x, float y, float z);
	public static void LuauPushVector3ToThread(IntPtr thread, float x, float y, float z) {
        ThreadSafetyCheck();
        ThrowIfNotNullPtr(PushVector3ToThread(thread, x, y, z));
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr PushTableToThread(IntPtr thread, int initialSize);
	public static void LuauPushTableToThread(IntPtr thread, int initialSize = 0) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(PushTableToThread(thread, initialSize));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr ErrorThread(IntPtr thread, IntPtr msg, int msgSize);
	public static void LuauErrorThread(IntPtr thread, IntPtr msg, int msgSize) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(ErrorThread(thread, msg, msgSize));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr GetDebugTrace(IntPtr thread, ref int result);
	public static void LuauGetDebugTrace(IntPtr thread) {
        ThreadSafetyCheck();
        var result = 0;
        ThrowIfNotNullPtr(GetDebugTrace(thread, ref result));
	}

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr RunTaskScheduler(LuauContext context, float now, float unscaledNow);
	public static void LuauRunTaskScheduler(LuauContext context) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(RunTaskScheduler(context, Time.time, Time.unscaledTime));
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
	private static extern void FreeString(IntPtr cStringPtr);
	public static void LuauFreeString(IntPtr cStringPtr) {
		ThreadSafetyCheck();
		FreeString(cStringPtr);
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern LuauBytecodeVersion GetBytecodeVersion();
	public static LuauBytecodeVersion LuauGetBytecodeVersion() {
		return GetBytecodeVersion();
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void SetScriptTimeoutDuration(int duration);
	public static void LuauSetScriptTimeoutDuration(int duration) {
		SetScriptTimeoutDuration(duration);
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void SetIsPaused(int isPaused);
	public static void LuauSetIsPaused(bool isPaused) {
		SetIsPaused(isPaused ? 1 : 0);
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void PushCsError(IntPtr errPtr, int errLen);
	public static void LuauPushCsError(string err) {
		var bytes = System.Text.Encoding.UTF8.GetBytes(err);
		var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
		var bytesPtr = handle.AddrOfPinnedObject();
		PushCsError(bytesPtr, bytes.Length);
		handle.Free();
	}

	public enum LuauGCState {
		Off = 0,
		Step = 1,
		Full = 2,
	}
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void SetGCState(int state);
	public static void LuauSetGCState(LuauGCState state) {
		SetGCState((int)state);
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern int CountGC(int context);
	public static int LuauCountGC(LuauContext context) {
		return CountGC((int)context);
	}
}
