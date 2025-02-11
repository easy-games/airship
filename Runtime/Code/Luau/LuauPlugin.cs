#define DO_THREAD_SAFTEYCHECK
// #define DO_CALL_SAFTEYCHECK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using System.Threading;
using Luau;
using Debug = UnityEngine.Debug;

public static class LuauPlugin {
	public delegate void PrintCallback(LuauContext context, IntPtr thread, int style, int gameObjectId, IntPtr buffer, int length);
	public delegate int GetPropertyCallback(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameSize);
	public delegate int SetPropertyCallback(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameSize, LuauCore.PODTYPE type, IntPtr propertyData, int propertySize, int isTable);
	public delegate int CallMethodCallback(LuauContext context, IntPtr thread, int instanceId, IntPtr className, int classNameSize, IntPtr methodName, int methodNameSize, int numParameters, IntPtr firstParameterType, IntPtr firstParameterData, IntPtr firstParameterSize, IntPtr firstParameterIsTable, IntPtr shouldYield);
	public delegate int ConstructorCallback(LuauContext context, IntPtr thread, IntPtr className, int classNameSize, int numParameters, IntPtr firstParameterType, IntPtr firstParameterData, IntPtr firstParameterSize, IntPtr firstParameterIsTable);
	public delegate int ObjectGCCallback(int instanceId, IntPtr objectDebugPointer);
	public delegate IntPtr RequireCallback(LuauContext context, IntPtr thread, IntPtr fileName, int fileNameSize);
	public delegate int RequirePathCallback(LuauContext context, IntPtr thread, IntPtr fileName, int fileNameSize);
	public delegate void ToStringCallback(IntPtr thread, int instanceId, IntPtr str, int maxLen, out int len);
	public delegate int ToCsArrayCallback(LuauContext context, IntPtr thread, IntPtr arrayPtr, int arrayLen, LuauCore.PODTYPE podType);
	public delegate void ComponentSetEnabledCallback(IntPtr thread, int instanceId, int componentId, int enabled);
	public delegate void ToggleProfilerCallback(int componentId, IntPtr str, int strLen);
	public delegate int IsObjectDestroyedCallback(int instanceId);

	public static int unityMainThreadId = -1;
	public static bool s_currentlyExecuting = false;
	public enum CurrentCaller {
		None,
		RunThread,
		CallMethodOnThread,
		CreateThread
	}

	// Must match BytecodeVersion struct in Plugin.cpp
	[StructLayout(LayoutKind.Sequential)]
	public struct LuauBytecodeVersion {
		public int Min;
		public int Max;
		public int Target;
	}

	// Must match PluginStartup struct in LuauManager.h
	[StructLayout(LayoutKind.Sequential)]
	public struct LuauPluginStartup {
		public GetPropertyCallback getPropertyCallback;
		public SetPropertyCallback setPropertyCallback;
		public CallMethodCallback callMethodCallback;
		public ObjectGCCallback objectGcCallback;
		public RequireCallback requireCallback;
		public RequirePathCallback requirePathCallback;
		public ConstructorCallback constructorCallback;
		public ToStringCallback toStringCallback;
		public ToggleProfilerCallback toggleProfilerCallback;
		public IsObjectDestroyedCallback isObjectDestroyedCallback;
		
		public IntPtr staticList;
		public int staticCount;
		public int isServer;
		public int useUnityAllocator;
	}

	// Must match MemoryCategoryDumpItem struct in Debug.h
	[StructLayout(LayoutKind.Sequential)]
	private struct LuauMemoryCategoryDumpItemInternal {
		private readonly IntPtr NamePtr;
		private readonly ulong NameLen;
		public readonly ulong Bytes;
		
		public string Name => Marshal.PtrToStringUTF8(NamePtr, (int)NameLen);
	}

	public class LuauMemoryCategoryDumpItem {
		public string Name;
		public ulong Bytes;

		private string _shortName = null;
		public string ShortName {
			get {
				if (_shortName != null) {
					return _shortName;
				}
				
				var full = "";
				var names = Name.Split(',');
				foreach (var name in names) {
					if (full != string.Empty) {
						full += ",";
					}

					var transformedName = name;
					
					// Include pathname after the last slash:
					var lastSlashIdx = transformedName.LastIndexOf("/", StringComparison.Ordinal);
					if (lastSlashIdx != -1) {
						transformedName = transformedName.Substring(lastSlashIdx + 1);
					}
					
					// Remove extension:
					var dotIdx = transformedName.LastIndexOf(".", StringComparison.Ordinal);
					if (dotIdx != -1) {
						transformedName = transformedName.Substring(0, dotIdx);
					}

					full += transformedName;
				}

				_shortName = full;
				
				return _shortName;
			}
		}
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
		if (s_currentlyExecuting == true) {
            Debug.LogError("LuauPlugin called " + caller + " while a lua thread was still executing " + s_currentCaller);
        }
        s_currentCaller = caller;
		s_currentlyExecuting = true;
#endif
	}
    public static void EndExecutionCheck() {
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
    private static extern bool InitializeComponentCallbacks(ComponentSetEnabledCallback setEnabledCallback);
    public static bool LuauInitializeComponentCallbacks(ComponentSetEnabledCallback setEnabledCallback) {
	    ThreadSafetyCheck();

	    bool returnValue = InitializeComponentCallbacks(setEnabledCallback);
	    return returnValue;
    }

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
    [DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	private static extern bool Startup(LuauPluginStartup pluginStartup);
	public static bool LuauStartup(LuauPluginStartup pluginStartup) {
        ThreadSafetyCheck();
        return Startup(pluginStartup);
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
	private static extern void SetProfilerEnabled(bool enabled);
	public static void LuauSetProfilerEnabled(bool enabled) {
		ThreadSafetyCheck();
		SetProfilerEnabled(enabled);
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
    private static extern IntPtr RunEndFrameLogic();
    public static void LuauRunEndFrameLogic() {
        ThreadSafetyCheck();
        ThrowIfNotNullPtr(RunEndFrameLogic());
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
	public static void LuauInitializeAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, LuauMetadataPropertyMarshalDto[] props) {
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
	public static void LuauRemoveAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(RemoveAirshipComponent(context, thread, unityInstanceId, componentId));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr WriteToAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, LuauMetadataPropertyMarshalDto prop);
	public static void LuauWriteToAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, LuauMetadataPropertyMarshalDto prop) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(WriteToAirshipComponent(context, thread, unityInstanceId, componentId, prop));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr PushAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId);
	public static void LuauPushAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(PushAirshipComponent(context, thread, unityInstanceId, componentId));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr PushAirshipComponents(LuauContext context, IntPtr thread, int unityInstanceId, [In, Out] int[] componentIds, int nComponents, bool appendToTable);
	public static void LuauPushAirshipComponents(LuauContext context, IntPtr thread, int unityInstanceId, int[] componentIds, bool appendToTable = false) {
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
	public static void LuauUpdateCollisionAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, AirshipComponentUpdateType updateType, int collisionObjId) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(UpdateCollisionAirshipComponent(context, thread, unityInstanceId, componentId, (int)updateType, collisionObjId));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr UpdateAllAirshipComponents(LuauContext context, int updateType, float dt);
	public static void LuauUpdateAllAirshipComponents(LuauContext context, AirshipComponentUpdateType updateType, float dt) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(UpdateAllAirshipComponents(context, (int)updateType, dt));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr GetAirshipComponentEnabled(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, ref int result);
	public static bool GetComponentEnabled(LuauContext context, IntPtr thread, int unityInstanceId, int componentId) {
		ThreadSafetyCheck();
		var result = 0;
		ThrowIfNotNullPtr(GetAirshipComponentEnabled(context, thread, unityInstanceId, componentId, ref result));
		return result != 0;
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr SetAirshipComponentEnabled(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int result);
	public static void LuauSetAirshipComponentEnabled(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, bool enabled) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(SetAirshipComponentEnabled(context, thread, unityInstanceId, componentId, enabled ? 1 : 0));
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
	private static extern IntPtr CreateThread(LuauContext context, IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int gameObjectId, bool nativeCodegen);
	public static IntPtr LuauCreateThread(LuauContext context, byte[] scriptBytecode, string filename, int gameObjectId, bool nativeCodegen) {
		ThreadSafetyCheck();
		BeginExecutionCheck(CurrentCaller.CreateThread);
		
		var scriptBytecodeHandle = GCHandle.Alloc(scriptBytecode, GCHandleType.Pinned);
		var scriptBytecodePtr = scriptBytecodeHandle.AddrOfPinnedObject();
		
		var filenamePtr = Marshal.StringToCoTaskMemUTF8(filename);
		var filenameLength = Encoding.Unicode.GetByteCount(filename);
		
		var returnValue = CreateThread(context, scriptBytecodePtr, scriptBytecode.Length, filenamePtr, filenameLength, gameObjectId, nativeCodegen);
		
		Marshal.FreeCoTaskMem(filenamePtr);
		scriptBytecodeHandle.Free();
		
        EndExecutionCheck();
        
        return returnValue;
    }

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr CreateThreadWithCachedModule(LuauContext context, string filename, int gameObjectId);
	public static IntPtr LuauCreateThreadWithCachedModule(LuauContext context, string filename, int gameObjectId) {
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
	public static void LuauCacheModuleOnThread(IntPtr thread, string filename) {
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
	public static void LuauSetThreadDestroyed(IntPtr thread) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(SetThreadDestroyed(thread));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr SetMutableGlobals(IntPtr strings, IntPtr stringLengths, int numStrings);
	public static unsafe void LuauSetMutableGlobals(string[] mutableGlobals) {
		var strings = stackalloc IntPtr[mutableGlobals.Length];
		var lengths = stackalloc int[mutableGlobals.Length];
        
		for (var i = 0; i < mutableGlobals.Length; i++) {
			var str = mutableGlobals[i];
			var bytes = System.Text.Encoding.UTF8.GetBytes(str);
			var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
			var bytesPtr = handle.AddrOfPinnedObject();
			strings[i] = bytesPtr;
			lengths[i] = bytes.Length;
		}
		
		var res = SetMutableGlobals(new IntPtr(strings), new IntPtr(lengths), mutableGlobals.Length);
		
		ThrowIfNotNullPtr(res);
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr CompileCode(IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int optimizationLevel);
	public static IntPtr LuauCompileCode(IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int optimizationLevel) {
        var returnValue = CompileCode(script, scriptLength, filename, filenameLength, optimizationLevel);
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
	private static extern IntPtr ResumeThreadError(IntPtr thread, ref int result);
	public static int LuauResumeThreadError(IntPtr thread) {
		ThreadSafetyCheck();
		var returnValue = 0;
		ThrowIfNotNullPtr(ResumeThreadError(thread, ref returnValue));
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
	private static extern IntPtr PushValueToThread(IntPtr thread, int type, IntPtr data, int dataSize, int arraySize);
	public static void LuauPushValueToThread(IntPtr thread, int type, IntPtr data, int dataSize, int arraySize = -1) {
        ThreadSafetyCheck();
        ThrowIfNotNullPtr(PushValueToThread(thread, type, data, dataSize, arraySize));
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
	private static extern void ResetTimeCache(LuauContext context, int fixedUpdate);
	public static void LuauResetTimeCache(LuauContext context, bool fixedUpdate) {
		ResetTimeCache(context, fixedUpdate ? 1 : 0);
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
	private static extern IntPtr CopyTableToArray(IntPtr thread, IntPtr array, int type, int size, int idx);
	public static void LuauCopyTableToArray<T>(IntPtr thread, LuauCore.PODTYPE type, int size, int idx, out IList<T> array, bool asList) {
		array = new T[size];
		var gc = GCHandle.Alloc(array, GCHandleType.Pinned);
		var arrayPtr = gc.AddrOfPinnedObject();
		var res = CopyTableToArray(thread, arrayPtr, (int)type, size, idx);
		gc.Free();
		ThrowIfNotNullPtr(res);

		if (asList) {
			array = new List<T>(array);
		}
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
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern int GetLuauPluginVersion(out IntPtr versionPtr);
	public static string LuauGetLuauPluginVersion() {
		var len = GetLuauPluginVersion(out var versionPtr);
		return Marshal.PtrToStringUTF8(versionPtr, len);
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern void DebugPrintStack(IntPtr thread);
	public static void LuauDebugPrintStack(IntPtr thread) {
		DebugPrintStack(thread);
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern ulong GetUnityObjectCount();
	public static ulong LuauGetUnityObjectCount() {
		return GetUnityObjectCount();
	}
	
	/// <summary>
	/// Get the various memory categories from Luau. The memCatDump list should be unique per Luau context.
	/// </summary>
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr GetMemoryCategoryDump(LuauContext context, ref ulong count);
	public static void LuauGetMemoryCategoryDump(LuauContext context, List<LuauMemoryCategoryDumpItem> memCatDump) {
		ulong count = 0;
		var memCatDumpItemsPtr = GetMemoryCategoryDump(context, ref count);

		if (memCatDumpItemsPtr == IntPtr.Zero) {
			throw new Exception("Failed to get memory category dump");
		}
		
		for (var i = 0; i < (int)count; i++) {
			var item = Marshal.PtrToStructure<LuauMemoryCategoryDumpItemInternal>(memCatDumpItemsPtr);
			if (i < memCatDump.Count - 1) {
				memCatDump[i].Bytes = item.Bytes;
			} else {
				memCatDump.Add(new LuauMemoryCategoryDumpItem {
					Bytes = item.Bytes,
					Name = item.Name,
				});
			}
			
			memCatDumpItemsPtr += Marshal.SizeOf<LuauMemoryCategoryDumpItemInternal>();
		}
	}
}
