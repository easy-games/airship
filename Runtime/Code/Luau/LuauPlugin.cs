#define DO_THREAD_SAFTEYCHECK
// #define DO_CALL_SAFTEYCHECK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using System.Threading;
using Code.Luau.LuauAssembly;
using Luau;
using Debug = UnityEngine.Debug;

public static class LuauPlugin {
	public static int unityMainThreadId = -1;
	public static bool s_currentlyExecuting = false;
	public enum CurrentCaller {
		None,
		RunThread,
		CallMethodOnThread,
		CreateThread
	}

	public enum LuauOptimizationLevel {
		/// No optimizations.
		None = 0,
		/// Baseline optimizations.
		Baseline = 1,
		/// Max optimizations. Inlining, constant folding, loop unrolling, etc.
		Max = 2,
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

	private static void BeginExecutionCheck(CurrentCaller caller) {
#if DO_CALL_SAFTEYCHECK
		if (s_currentlyExecuting == true) {
            Debug.LogError("LuauPlugin called " + caller + " while a lua thread was still executing " + s_currentCaller);
        }
        s_currentCaller = caller;
		s_currentlyExecuting = true;
#endif
	}
	private static void EndExecutionCheck() {
#if DO_CALL_SAFTEYCHECK
        s_currentlyExecuting = false;
		s_currentCaller = CurrentCaller.None;
#endif
    }
	
    public static void LuauInitializeComponentCallbacks(LuauPluginNative.ComponentSetEnabledCallback setEnabledCallback) {
	    ThreadSafetyCheck();
		LuauPluginNative.InitializeComponentCallbacks(setEnabledCallback);
    }
    
	public static bool LuauStartup(LuauPluginNative.LuauPluginStartup pluginStartup) {
        ThreadSafetyCheck();
        return LuauPluginNative.Startup(pluginStartup);
    }
	
	public static void LuauInitializePrintCallback(LuauPluginNative.PrintCallback printCallback) {
		LuauPluginNative.TryInitPlugin();
		ThreadSafetyCheck();
		LuauPluginNative.InitializePrintCallback(printCallback);
	}

	public static void LuauSubsystemRegistration() {
#if !UNITY_EDITOR // The SubsystemRegistration call is done automatically within LuauPluginNative at editor time
		ThreadSafetyCheck();
		LuauPluginNative.SubsystemRegistration();
#endif
	}
	
	public static void LuauSetProfilerEnabled(bool enabled) {
		ThreadSafetyCheck();
		LuauPluginNative.SetProfilerEnabled(enabled);
	}
	
	public static bool LuauOpenState(LuauContext context) {
		ThreadSafetyCheck();
		return LuauPluginNative.OpenState(context);
	}
	
	public static bool LuauCloseState(LuauContext context) {
		ThreadSafetyCheck();
		return LuauPluginNative.CloseState(context);
	}
	
	public static void LuauReset(LuauContext context) {
        ThreadSafetyCheck();
        LuauPluginNative.Reset(context);
	}
	
	public static unsafe ReadOnlySpan<int> LuauGetUniqueInstanceIds(LuauContext context) {
		var count = LuauPluginNative.GetUniqueInstanceIdCount(context);
		var ids = new int[count];
		
		ulong countFetched;
		fixed (int* idsPtr = ids) {
			countFetched = LuauPluginNative.GetUniqueInstanceIds(context, new IntPtr(idsPtr), count);
		}

		return new ReadOnlySpan<int>(ids, 0, (int)countFetched);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void LuauRunBeginFrameLogic() {
		ThreadSafetyCheck();
		LuauPluginNative.RunBeginFrameLogic();
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LuauRunEndFrameLogic() {
        ThreadSafetyCheck();
        ThrowIfNotNullPtr(LuauPluginNative.RunEndFrameLogic());
    }
	
	public static void LuauShutdown() {
		ThreadSafetyCheck();
		LuauPluginNative.Shutdown();
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe void LuauInitializeAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, Span<LuauMetadataPropertyMarshalDto> props) {
		ThreadSafetyCheck();
		fixed (LuauMetadataPropertyMarshalDto* ptr = &MemoryMarshal.GetReference(props)) {
			ThrowIfNotNullPtr(LuauPluginNative.InitializeAirshipComponent(context, thread, unityInstanceId, componentId, ptr, props.Length));
		}
	}

	/// <summary>
	/// Create the reference pointer for the AirshipComponent
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void LuauPrewarmAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int transformComponentId) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(LuauPluginNative.PrewarmAirshipComponent(context, thread, unityInstanceId, componentId, transformComponentId));
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void LuauRemoveAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(LuauPluginNative.RemoveAirshipComponent(context, thread, unityInstanceId, componentId));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void LuauWriteToAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, LuauMetadataPropertyMarshalDto prop) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(LuauPluginNative.WriteToAirshipComponent(context, thread, unityInstanceId, componentId, prop));
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void LuauPushAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(LuauPluginNative.PushAirshipComponent(context, thread, unityInstanceId, componentId));
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void LuauPushAirshipComponents(LuauContext context, IntPtr thread, int unityInstanceId, int[] componentIds, bool appendToTable = false) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(LuauPluginNative.PushAirshipComponents(context, thread, unityInstanceId, componentIds, componentIds.Length, appendToTable));
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void LuauUpdateIndividualAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, AirshipComponentUpdateType updateType, float dt, bool safe) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(LuauPluginNative.UpdateIndividualAirshipComponent(context, thread, unityInstanceId, componentId, (int)updateType, dt, true));
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void LuauUpdateCollisionAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, AirshipComponentUpdateType updateType, int collisionObjId) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(LuauPluginNative.UpdateCollisionAirshipComponent(context, thread, unityInstanceId, componentId, (int)updateType, collisionObjId));
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void LuauUpdateAllAirshipComponents(LuauContext context, AirshipComponentUpdateType updateType, float dt) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(LuauPluginNative.UpdateAllAirshipComponents(context, (int)updateType, dt));
	}
	
	public static bool GetComponentEnabled(LuauContext context, IntPtr thread, int unityInstanceId, int componentId) {
		ThreadSafetyCheck();
		var result = 0;
		ThrowIfNotNullPtr(LuauPluginNative.GetAirshipComponentEnabled(context, thread, unityInstanceId, componentId, ref result));
		return result != 0;
	}
	
	public static void LuauSetAirshipComponentEnabled(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, bool enabled) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(LuauPluginNative.SetAirshipComponentEnabled(context, thread, unityInstanceId, componentId, enabled ? 1 : 0));
	}
	
	public static bool LuauHasAirshipMethod(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, AirshipComponentUpdateType updateType) {
		ThreadSafetyCheck();
		var result = 0;
		ThrowIfNotNullPtr(LuauPluginNative.HasAirshipMethod(context, thread, unityInstanceId, componentId, (int)updateType, ref result));
		return result != 0;
	}
	
	public static bool LuauPushSignal(LuauContext context, IntPtr thread, int unityInstanceId, ulong propNameHash) {
		ThreadSafetyCheck();
		var result = 0;
		ThrowIfNotNullPtr(LuauPluginNative.PushSignal(context, thread, unityInstanceId, propNameHash, ref result));
		return result != 0;
	}
	
	public static bool LuauEmitSignal(LuauContext context, IntPtr thread, int unityInstanceId, ulong propNameHash, int numParams) {
		ThreadSafetyCheck();
		var result = 0;
		ThrowIfNotNullPtr(LuauPluginNative.EmitSignal(context, thread, unityInstanceId, propNameHash, numParams, ref result));
		return result != 0;
	}
	
	public static void LuauDestroySignals(LuauContext context, IntPtr thread, int unityInstanceId) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(LuauPluginNative.DestroySignals(context, thread, unityInstanceId));
	}
	
	public static unsafe IntPtr LuauCreateThread(LuauContext context, byte[] scriptBytecode, string filename, int gameObjectId, bool nativeCodegen) {
		ThreadSafetyCheck();
		BeginExecutionCheck(CurrentCaller.CreateThread);
		
		var filenamePtr = Marshal.StringToCoTaskMemUTF8(filename);
		var filenameLength = Encoding.UTF8.GetByteCount(filename);

		IntPtr returnValue;
		fixed (byte* bytecodePtr = scriptBytecode) {
			returnValue = LuauPluginNative.CreateThread(context, bytecodePtr, scriptBytecode.Length, filenamePtr, filenameLength, gameObjectId, nativeCodegen);
		}
		
		Marshal.FreeCoTaskMem(filenamePtr);
		
        EndExecutionCheck();
        
        return returnValue;
    }
	
	public static IntPtr LuauCreateThreadWithCachedModule(LuauContext context, string filename, int gameObjectId) {
		ThreadSafetyCheck();
		var returnValue = LuauPluginNative.CreateThreadWithCachedModule(context, filename, gameObjectId);
		EndExecutionCheck();
		return returnValue;
	}
	
	public static void LuauCacheModuleOnThread(IntPtr thread, string filename) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(LuauPluginNative.CacheModuleOnThread(thread, filename));
		EndExecutionCheck();
	}
	
	public static void LuauSetThreadDestroyed(IntPtr thread) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(LuauPluginNative.SetThreadDestroyed(thread));
	}
	
	public static unsafe void LuauSetMutableGlobals(string[] mutableGlobals) {
		Span<IntPtr> strings = stackalloc IntPtr[mutableGlobals.Length];
		var lengths = stackalloc int[mutableGlobals.Length];
        
		for (var i = 0; i < mutableGlobals.Length; i++) {
			var str = mutableGlobals[i];
			var strPtr = Marshal.StringToCoTaskMemUTF8(str);
			var len = Encoding.UTF8.GetByteCount(str);
			strings[i] = strPtr;
			lengths[i] = len;
		}

		IntPtr res;
		fixed (IntPtr* stringsPtr = &MemoryMarshal.GetReference(strings)) {
			res = LuauPluginNative.SetMutableGlobals(stringsPtr, new IntPtr(lengths), mutableGlobals.Length);
		}

		foreach (var strPtr in strings) {
			Marshal.FreeCoTaskMem(strPtr);
		}
		
		ThrowIfNotNullPtr(res);
	}
	
	public static IntPtr LuauCompileCode(IntPtr script, int scriptLength, IntPtr filename, int filenameLength, LuauOptimizationLevel optimizationLevel) {
        var returnValue = LuauPluginNative.CompileCode(script, scriptLength, filename, filenameLength, (int)optimizationLevel);
		return returnValue;
	}
	
	public static int LuauRunThread(IntPtr thread, int nArgs = 0) {
        ThreadSafetyCheck();
		//BeginExecutionCheck(CurrentCaller.CreateThread);
        var returnValue = 0;
        ThrowIfNotNullPtr(LuauPluginNative.RunThread(thread, nArgs, ref returnValue));
        //EndExecutionCheck();
        return returnValue;
    }
	
	public static int LuauResumeThread(IntPtr thread, int nArgs = 0) {
		ThreadSafetyCheck();
		var returnValue = 0;
		ThrowIfNotNullPtr(LuauPluginNative.ResumeThread(thread, nArgs, ref returnValue));
		return returnValue;
	}
	
	public static int LuauResumeThreadError(IntPtr thread) {
		ThreadSafetyCheck();
		var returnValue = 0;
		ThrowIfNotNullPtr(LuauPluginNative.ResumeThreadError(thread, ref returnValue));
		return returnValue;
	}
	
	public static int LuauCallMethodOnThread(IntPtr thread, IntPtr methodName, int methodNameSize, int numParameters) {
        ThreadSafetyCheck();
		BeginExecutionCheck(CurrentCaller.CallMethodOnThread);
        var returnValue = 0;
        ThrowIfNotNullPtr(LuauPluginNative.CallMethodOnThread(thread, methodName, methodNameSize, numParameters, ref returnValue));
        EndExecutionCheck();
        return returnValue;
    }
	
	public static void LuauDestroyThread(IntPtr thread) {
        ThreadSafetyCheck();
        ThrowIfNotNullPtr(LuauPluginNative.DestroyThread(thread));
	}
	
	public static void LuauPinThread(IntPtr thread) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(LuauPluginNative.PinThread(thread));
	}
	
	public static void LuauUnpinThread(IntPtr thread) {
        // Debug.Log("Unpinning thread " + thread);
        ThreadSafetyCheck();
        ThrowIfNotNullPtr(LuauPluginNative.UnpinThread(thread));
	}
	
	public static void LuauPushValueToThread(IntPtr thread, int type, IntPtr data, int dataSize, int arraySize = -1) {
        ThreadSafetyCheck();
        ThrowIfNotNullPtr(LuauPluginNative.PushValueToThread(thread, type, data, dataSize, arraySize));
	}
	
	public static void LuauPushVector3ToThread(IntPtr thread, float x, float y, float z) {
        ThreadSafetyCheck();
        ThrowIfNotNullPtr(LuauPluginNative.PushVector3ToThread(thread, x, y, z));
	}
	
	public static void LuauPushTableToThread(IntPtr thread, int initialSize = 0) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(LuauPluginNative.PushTableToThread(thread, initialSize));
	}
	
	public static void LuauErrorThread(IntPtr thread, IntPtr msg, int msgSize) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(LuauPluginNative.ErrorThread(thread, msg, msgSize));
	}
	
	public static void LuauGetDebugTrace(IntPtr thread) {
        ThreadSafetyCheck();
        var result = 0;
        ThrowIfNotNullPtr(LuauPluginNative.GetDebugTrace(thread, ref result));
	}
	
	public static void LuauRunTaskScheduler(LuauContext context) {
		ThreadSafetyCheck();
		ThrowIfNotNullPtr(LuauPluginNative.RunTaskScheduler(context, Time.time, Time.unscaledTime));
	}
	
	public static void LuauResetTimeCache(LuauContext context, bool fixedUpdate) {
		LuauPluginNative.ResetTimeCache(context, fixedUpdate ? 1 : 0);
	}
	
	public static LuauContext LuauGetContextFromThread(IntPtr thread) {
		ThreadSafetyCheck();
		return LuauPluginNative.GetContextFromThread(thread);
	}
	
	public static LuauPluginNative.LuauBytecodeVersion LuauGetBytecodeVersion() {
		return LuauPluginNative.GetBytecodeVersion();
	}
	
	public static void LuauSetScriptTimeoutDuration(int duration) {
		LuauPluginNative.SetScriptTimeoutDuration(duration);
	}
	
	public static void LuauSetIsPaused(bool isPaused) {
		LuauPluginNative.SetIsPaused(isPaused ? 1 : 0);
	}
	
	public static unsafe void LuauCopyTableToArray<T>(IntPtr thread, PODTYPE type, int size, int idx, out IList<T> array, bool asList) where T : unmanaged {
		var arr = new T[size];
		array = arr;
		IntPtr res;
		fixed (T* arrayPtr = arr) {
			res = LuauPluginNative.CopyTableToArray(thread, new IntPtr(arrayPtr), (int)type, size, idx);
		}
		ThrowIfNotNullPtr(res);

		if (asList) {
			array = new List<T>(array);
		}
	}
	
	public static int LuauRegisterStringAtom(string str) {
		var strPtr = Marshal.StringToCoTaskMemUTF8(str);
		var strLen = (ulong)Encoding.UTF8.GetByteCount(str);
		var atom = LuauPluginNative.RegisterStringAtom(strPtr, strLen);
		Marshal.FreeCoTaskMem(strPtr);
		return atom;
	}
	
	public static void LuauPushCsError(string err) {
		var errPtr = Marshal.StringToCoTaskMemUTF8(err);
		var errLen = Encoding.UTF8.GetByteCount(err);
		LuauPluginNative.PushCsError(errPtr, errLen);
		Marshal.FreeCoTaskMem(errPtr);
	}

	public enum LuauGCState {
		Off = 0,
		Step = 1,
		Full = 2,
	}
	public static void LuauSetGCState(LuauGCState state) {
		LuauPluginNative.SetGCState((int)state);
	}
	
	public static int LuauCountGC(LuauContext context) {
		return LuauPluginNative.CountGC((int)context);
	}
	
	public static string LuauGetLuauPluginVersion() {
		var len = LuauPluginNative.GetLuauPluginVersion(out var versionPtr);
		return Marshal.PtrToStringUTF8(versionPtr, len);
	}
	
	public static void LuauDebugPrintStack(IntPtr thread) {
		LuauPluginNative.DebugPrintStack(thread);
	}
	
	public static ulong LuauGetUnityObjectCount() {
		return LuauPluginNative.GetUnityObjectCount();
	}
	
	public static void LuauGetMemoryCategoryDump(LuauContext context, List<LuauMemoryCategoryDumpItem> memCatDump) {
		ulong count = 0;
		var memCatDumpItemsPtr = LuauPluginNative.GetMemoryCategoryDump(context, ref count);

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
	
	public static string LuauDebugCountAllRegistryItems(LuauContext context) {
		var strLen = LuauPluginNative.DebugCountAllRegistryItems(context, out var strPtr);
		return Marshal.PtrToStringUTF8(strPtr, strLen);
	}
	
	public static int[] LuauDebugGetAllTrackedInstanceIds(LuauContext context) {
		var listLen = LuauPluginNative.DebugGetAllTrackedInstanceIds(context, out var arrPtr);
		var list = new int[listLen];
		var elementSize = Marshal.SizeOf<int>();
		for (var i = 0; i < listLen; i++) {
			list[i] = Marshal.ReadInt32(arrPtr, i * elementSize);
		}
		return list;
	}
}
