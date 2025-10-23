using System;
using System.IO;
using System.Runtime.InteropServices;
using Luau;
using UnityEngine;
#if UNITY_EDITOR
using NativePlugins;
#endif

public static class LuauPluginNative {
	public delegate void PrintCallback(LuauContext context, IntPtr thread, LuauLogLevel style, int gameObjectId, IntPtr buffer, int length);
	public delegate int GetPropertyCallback(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameSize, int propertyNameAtom);
	public delegate int SetPropertyCallback(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameSize, int propertyNameAtom, int type, IntPtr propertyData, ulong propertySize, byte isTable);
	public delegate int CallMethodCallback(LuauContext context, IntPtr thread, int instanceId, IntPtr className, int classNameSize, IntPtr methodName, int methodNameSize, int methodNameAtom, int numParameters, IntPtr firstParameterType, IntPtr firstParameterData, IntPtr firstParameterSize, IntPtr firstParameterIsTable, IntPtr shouldYield);
	public delegate int ConstructorCallback(LuauContext context, IntPtr thread, IntPtr className, int classNameSize, int classNameAtom, int numParameters, IntPtr firstParameterType, IntPtr firstParameterData, IntPtr firstParameterSize, IntPtr firstParameterIsTable);
	public delegate int ObjectGCCallback(int instanceId, IntPtr objectDebugPointer);
	public delegate IntPtr RequireCallback(LuauContext context, IntPtr thread, IntPtr fileName, int fileNameSize);
	public delegate void RequirePathCallback(LuauContext context, IntPtr thread, IntPtr scriptName, int scriptNameLen, IntPtr fileName, int fileNameLen);
	public delegate void ToStringCallback(IntPtr thread, int instanceId, IntPtr str, int maxLen, out int len);
	public delegate void ComponentSetEnabledCallback(IntPtr thread, int instanceId, int componentId, int enabled);
	public delegate int IsObjectDestroyedCallback(int instanceId);
	public delegate void GetUnityObjectName(IntPtr thread, int instanceId, IntPtr str, int maxLen, out int len);

#if UNITY_EDITOR
	private const string BasePluginsPath = "/Packages/gg.easy.airship/Runtime/Plugins";
#if UNITY_EDITOR_OSX
	private const string LuauLibPath = BasePluginsPath + "/Mac/LuauPlugin.bundle/Contents/MacOS/LuauPlugin";
#elif UNITY_EDITOR_LINUX
	private const string LuauLibPath = BasePluginsPath + "/Linux/libLuauPlugin.so";
#elif UNITY_EDITOR_WIN
	private const string LuauLibPath = BasePluginsPath + "/Windows/x64/LuauPlugin.dll";
#endif
	
	[NativePlugin(LuauLibPath)]
	public static IntPtr LibHandle;
	
	// All delegates for Editor-time plugin access:
	internal delegate bool StartupDelegate(LuauPluginStartup pluginStartup);
	[NativeDelegate] internal static StartupDelegate Startup;
    
	internal delegate bool InitializePrintCallbackDelegate(PrintCallback printCallback);
	[NativeDelegate] internal static InitializePrintCallbackDelegate InitializePrintCallback;

	internal delegate bool InitializeComponentCallbacksDelegate(ComponentSetEnabledCallback setEnabledCallback);
	[NativeDelegate] internal static InitializeComponentCallbacksDelegate InitializeComponentCallbacks;
    
	internal delegate void SubsystemRegistrationDelegate();
	[NativeDelegate] internal static SubsystemRegistrationDelegate SubsystemRegistration;

	internal delegate void SetProfilerEnabledDelegate(bool enabled);
	[NativeDelegate] internal static SetProfilerEnabledDelegate SetProfilerEnabled;
	
	internal delegate bool OpenStateDelegate(LuauContext context);
	[NativeDelegate] internal static OpenStateDelegate OpenState;
	
	internal delegate bool CloseStateDelegate(LuauContext context);
	[NativeDelegate] internal static CloseStateDelegate CloseState;
	
	internal delegate void ResetDelegate(LuauContext context);
	[NativeDelegate] internal static ResetDelegate Reset;
	
	internal delegate ulong GetUniqueInstanceIdCountDelegate(LuauContext context);
	[NativeDelegate] internal static GetUniqueInstanceIdCountDelegate GetUniqueInstanceIdCount;
	
	internal delegate ulong GetUniqueInstanceIdsDelegate(LuauContext context, IntPtr arr, ulong arrSize);
	[NativeDelegate] internal static GetUniqueInstanceIdsDelegate GetUniqueInstanceIds;
	
	internal delegate void RunBeginFrameLogicDelegate();
	[NativeDelegate] internal static RunBeginFrameLogicDelegate RunBeginFrameLogic;
	
	internal delegate IntPtr RunEndFrameLogicDelegate();
	[NativeDelegate] internal static RunEndFrameLogicDelegate RunEndFrameLogic;
	
	internal delegate void ShutdownDelegate();
	[NativeDelegate] internal static ShutdownDelegate Shutdown;
	
	internal unsafe delegate IntPtr InitializeAirshipComponentDelegate(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, LuauMetadataPropertyMarshalDto* props, int nProps);
	[NativeDelegate] internal static InitializeAirshipComponentDelegate InitializeAirshipComponent;
	
	internal delegate IntPtr PrewarmAirshipComponentDelegate(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int transformComponentId);
	[NativeDelegate] internal static PrewarmAirshipComponentDelegate PrewarmAirshipComponent;
	
	internal delegate IntPtr RemoveAirshipComponentDelegate(LuauContext context, IntPtr thread, int unityInstanceId, int componentId);
	[NativeDelegate] internal static RemoveAirshipComponentDelegate RemoveAirshipComponent;
	
	internal delegate IntPtr WriteToAirshipComponentDelegate(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, LuauMetadataPropertyMarshalDto prop);
	[NativeDelegate] internal static WriteToAirshipComponentDelegate WriteToAirshipComponent;
	
	internal delegate IntPtr PushAirshipComponentDelegate(LuauContext context, IntPtr thread, int unityInstanceId, int componentId);
	[NativeDelegate] internal static PushAirshipComponentDelegate PushAirshipComponent;
	
	internal delegate IntPtr PushAirshipComponentsDelegate(LuauContext context, IntPtr thread, int unityInstanceId, [In, Out] int[] componentIds, int nComponents, bool appendToTable);
	[NativeDelegate] internal static PushAirshipComponentsDelegate PushAirshipComponents;
	
	internal delegate IntPtr UpdateIndividualAirshipComponentDelegate(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int updateType, float dt, bool safe);
	[NativeDelegate] internal static UpdateIndividualAirshipComponentDelegate UpdateIndividualAirshipComponent;
	
	internal delegate IntPtr UpdateCollisionAirshipComponentDelegate(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int updateType, int collisionObjId);
	[NativeDelegate] internal static UpdateCollisionAirshipComponentDelegate UpdateCollisionAirshipComponent;
	
	internal delegate IntPtr UpdateAllAirshipComponentsDelegate(LuauContext context, int updateType, float dt);
	[NativeDelegate] internal static UpdateAllAirshipComponentsDelegate UpdateAllAirshipComponents;
	
	internal delegate IntPtr GetAirshipComponentEnabledDelegate(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, ref int result);
	[NativeDelegate] internal static GetAirshipComponentEnabledDelegate GetAirshipComponentEnabled;
	
	internal delegate IntPtr SetAirshipComponentEnabledDelegate(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int result);
	[NativeDelegate] internal static SetAirshipComponentEnabledDelegate SetAirshipComponentEnabled;
	
	internal delegate IntPtr HasAirshipMethodDelegate(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int updateType, ref int result);
	[NativeDelegate] internal static HasAirshipMethodDelegate HasAirshipMethod;
	
	internal delegate IntPtr PushSignalDelegate(LuauContext context, IntPtr thread, int unityInstanceId, ulong propNameHash, ref int result);
	[NativeDelegate] internal static PushSignalDelegate PushSignal;
	
	internal delegate IntPtr EmitSignalDelegate(LuauContext context, IntPtr thread, int unityInstanceId, ulong propNameHash, int numParams, ref int result);
	[NativeDelegate] internal static EmitSignalDelegate EmitSignal;
	
	internal delegate IntPtr DestroySignalsDelegate(LuauContext context, IntPtr thread, int unityInstanceId);
	[NativeDelegate] internal static DestroySignalsDelegate DestroySignals;
	
	internal unsafe delegate IntPtr CreateThreadDelegate(LuauContext context, byte* scriptBytecode, int scriptLength, IntPtr filename, int filenameLength, int gameObjectId, bool nativeCodegen);
	[NativeDelegate] internal static CreateThreadDelegate CreateThread;
	
	internal delegate IntPtr CreateThreadWithCachedModuleDelegate(LuauContext context, string filename, int gameObjectId);
	[NativeDelegate] internal static CreateThreadWithCachedModuleDelegate CreateThreadWithCachedModule;
	
	internal delegate IntPtr CacheModuleOnThreadDelegate(IntPtr thread, string filename);
	[NativeDelegate] internal static CacheModuleOnThreadDelegate CacheModuleOnThread;
	
	internal delegate IntPtr SetThreadDestroyedDelegate(IntPtr thread);
	[NativeDelegate] internal static SetThreadDestroyedDelegate SetThreadDestroyed;
	
	internal unsafe delegate IntPtr SetMutableGlobalsDelegate(IntPtr* strings, IntPtr stringLengths, int numStrings);
	[NativeDelegate] internal static SetMutableGlobalsDelegate SetMutableGlobals;
	
	internal delegate IntPtr CompileCodeDelegate(IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int optimizationLevel);
	[NativeDelegate] internal static CompileCodeDelegate CompileCode;
	
	internal delegate IntPtr RunThreadDelegate(IntPtr thread, int nArgs, ref int result);
	[NativeDelegate] internal static RunThreadDelegate RunThread;
	
	internal delegate IntPtr ResumeThreadDelegate(IntPtr thread, int nArgs, ref int result);
	[NativeDelegate] internal static ResumeThreadDelegate ResumeThread;
	
	internal delegate IntPtr ResumeThreadErrorDelegate(IntPtr thread, ref int result);
	[NativeDelegate] internal static ResumeThreadErrorDelegate ResumeThreadError;
	
	internal delegate IntPtr CallMethodOnThreadDelegate(IntPtr thread, IntPtr methodName, int methodNameSize, int numParameters, ref int result);
	[NativeDelegate] internal static CallMethodOnThreadDelegate CallMethodOnThread;
	
	internal delegate IntPtr DestroyThreadDelegate(IntPtr thread);
	[NativeDelegate] internal static DestroyThreadDelegate DestroyThread;
	
	internal delegate IntPtr PinThreadDelegate(IntPtr thread);
	[NativeDelegate] internal static PinThreadDelegate PinThread;
	
	internal delegate IntPtr UnpinThreadDelegate(IntPtr thread);
	[NativeDelegate] internal static UnpinThreadDelegate UnpinThread;
	
	internal delegate IntPtr PushValueToThreadDelegate(IntPtr thread, int type, IntPtr data, ulong dataSize, int arraySize);
	[NativeDelegate] internal static PushValueToThreadDelegate PushValueToThread;
	
	internal delegate IntPtr PushVector3ToThreadDelegate(IntPtr thread, float x, float y, float z);
	[NativeDelegate] internal static PushVector3ToThreadDelegate PushVector3ToThread;
	
	internal delegate IntPtr PushTableToThreadDelegate(IntPtr thread, int initialSize);
	[NativeDelegate] internal static PushTableToThreadDelegate PushTableToThread;
	
	internal delegate IntPtr ErrorThreadDelegate(IntPtr thread, IntPtr msg, int msgSize);
	[NativeDelegate] internal static ErrorThreadDelegate ErrorThread;
	
	internal delegate IntPtr GetDebugTraceDelegate(IntPtr thread, ref int result);
	[NativeDelegate] internal static GetDebugTraceDelegate GetDebugTrace;
	
	internal delegate IntPtr GetTracebackDelegate(IntPtr thread, out IntPtr strPtr);
	[NativeDelegate] internal static GetTracebackDelegate GetTraceback;
	
	internal delegate IntPtr RunTaskSchedulerDelegate(LuauContext context, float now, float unscaledNow);
	[NativeDelegate] internal static RunTaskSchedulerDelegate RunTaskScheduler;
	
	internal delegate void ResetTimeCacheDelegate(LuauContext context, int fixedUpdate);
	[NativeDelegate] internal static ResetTimeCacheDelegate ResetTimeCache;
	
	internal delegate LuauContext GetContextFromThreadDelegate(IntPtr thread);
	[NativeDelegate] internal static GetContextFromThreadDelegate GetContextFromThread;
	
	internal delegate LuauBytecodeVersion GetBytecodeVersionDelegate();
	[NativeDelegate] internal static GetBytecodeVersionDelegate GetBytecodeVersion;
	
	internal delegate void SetScriptTimeoutDurationDelegate(int duration);
	[NativeDelegate] internal static SetScriptTimeoutDurationDelegate SetScriptTimeoutDuration;
	
	internal delegate void SetIsPausedDelegate(int isPaused);
	[NativeDelegate] internal static SetIsPausedDelegate SetIsPaused;
	
	internal delegate IntPtr CopyTableToArrayDelegate(IntPtr thread, IntPtr array, int type, int size, int idx);
	[NativeDelegate] internal static CopyTableToArrayDelegate CopyTableToArray;
	
	internal delegate int RegisterStringAtomDelegate(IntPtr strPtr, ulong strLen);
	[NativeDelegate] internal static RegisterStringAtomDelegate RegisterStringAtom;
	
	internal delegate void PushCsErrorDelegate(IntPtr errPtr, int errLen);
	[NativeDelegate] internal static PushCsErrorDelegate PushCsError;
	
	internal delegate void SetGCStateDelegate(int state);
	[NativeDelegate] internal static SetGCStateDelegate SetGCState;
	
	internal delegate int CountGCDelegate(int context);
	[NativeDelegate] internal static CountGCDelegate CountGC;
	
	internal delegate int GetLuauPluginVersionDelegate(out IntPtr versionPtr);
	[NativeDelegate] internal static GetLuauPluginVersionDelegate GetLuauPluginVersion;
	
	internal delegate void DebugPrintStackDelegate(IntPtr thread);
	[NativeDelegate] internal static DebugPrintStackDelegate DebugPrintStack;
	
	internal delegate ulong GetUnityObjectCountDelegate();
	[NativeDelegate] internal static GetUnityObjectCountDelegate GetUnityObjectCount;
	
	internal delegate IntPtr GetMemoryCategoryDumpDelegate(LuauContext context, ref ulong count);
	[NativeDelegate] internal static GetMemoryCategoryDumpDelegate GetMemoryCategoryDump;
	
	internal delegate int DebugCountAllRegistryItemsDelegate(LuauContext context, out IntPtr str);
	[NativeDelegate] internal static DebugCountAllRegistryItemsDelegate DebugCountAllRegistryItems;
	
	internal delegate int DebugGetAllTrackedInstanceIdsDelegate(LuauContext context, out IntPtr ids);
	[NativeDelegate] internal static DebugGetAllTrackedInstanceIdsDelegate DebugGetAllTrackedInstanceIds;
	
	internal delegate IntPtr LuaNewTableDelegate(IntPtr thread, int nArray, int nRecord);
	[NativeDelegate] internal static LuaNewTableDelegate LuaNewTable;
	
	internal delegate IntPtr LuaPushNilDelegate(IntPtr thread);
	[NativeDelegate] internal static LuaPushNilDelegate LuaPushNil;
	
	internal delegate IntPtr LuaPushIntegerDelegate(IntPtr thread, int n);
	[NativeDelegate] internal static LuaPushIntegerDelegate LuaPushInteger;
	
	internal delegate IntPtr LuaPushUnsignedIntegerDelegate(IntPtr thread, uint n);
	[NativeDelegate] internal static LuaPushUnsignedIntegerDelegate LuaPushUnsignedInteger;
	
	internal delegate IntPtr LuaPushVectorDelegate(IntPtr thread, float x, float y, float z);
	[NativeDelegate] internal static LuaPushVectorDelegate LuaPushVector;
	
	internal delegate IntPtr LuaPushBooleanDelegate(IntPtr thread, int b);
	[NativeDelegate] internal static LuaPushBooleanDelegate LuaPushBoolean;
	
	internal delegate IntPtr LuaPushStringDelegate(IntPtr thread, IntPtr str, int len);
	[NativeDelegate] internal static LuaPushStringDelegate LuaPushString;
	
	internal delegate IntPtr LuaPushThreadDelegate(IntPtr thread);
	[NativeDelegate] internal static LuaPushThreadDelegate LuaPushThread;
	
	internal delegate IntPtr LuaRawSetIDelegate(IntPtr thread, int idx, int n);
	[NativeDelegate] internal static LuaRawSetIDelegate LuaRawSetI;
	
	internal delegate IntPtr LuaPopDelegate(IntPtr thread, int n);
	[NativeDelegate] internal static LuaPopDelegate LuaPop;
	
	internal delegate IntPtr LuaSetReadonlyDelegate(IntPtr thread, int idx, int enabled);
	[NativeDelegate] internal static LuaSetReadonlyDelegate LuaSetReadonly;
	
	internal delegate IntPtr LuaRefDelegate(IntPtr thread, int idx, ref int refVal);
	[NativeDelegate] internal static LuaRefDelegate LuaRef;
	
	internal delegate IntPtr LuaUnrefDelegate(IntPtr thread, int refVal);
	[NativeDelegate] internal static LuaUnrefDelegate LuaUnref;
	
	internal delegate IntPtr LuaGetRefDelegate(IntPtr thread, int refVal);
	[NativeDelegate] internal static LuaGetRefDelegate LuaGetRef;
	
	internal delegate IntPtr LuaGetTopDelegate(IntPtr thread, ref int top);
	[NativeDelegate] internal static LuaGetTopDelegate LuaGetTop;
#else
	// All extern plugin APIs:
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
    [DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
    internal static extern bool Startup(LuauPluginStartup pluginStartup);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
    [DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
    internal static extern bool InitializePrintCallback(PrintCallback printCallback);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
    [DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
    internal static extern bool InitializeComponentCallbacks(ComponentSetEnabledCallback setEnabledCallback);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	internal static extern void SubsystemRegistration();
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	internal static extern void SetProfilerEnabled(bool enabled);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	internal static extern bool OpenState(LuauContext context);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	internal static extern bool CloseState(LuauContext context);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	internal static extern void Reset(LuauContext context);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	internal static extern ulong GetUniqueInstanceIdCount(LuauContext context);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	internal static extern ulong GetUniqueInstanceIds(LuauContext context, IntPtr arr, ulong arrSize);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	internal static extern void RunBeginFrameLogic();
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
    [DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
    internal static extern IntPtr RunEndFrameLogic();
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
    [DllImport("LuauPlugin", CallingConvention = CallingConvention.Cdecl)]
#endif
	internal static extern void Shutdown();
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern unsafe IntPtr InitializeAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, LuauMetadataPropertyMarshalDto* props, int nProps);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr PrewarmAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int transformComponentId);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr RemoveAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr WriteToAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, LuauMetadataPropertyMarshalDto prop);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr PushAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr PushAirshipComponents(LuauContext context, IntPtr thread, int unityInstanceId, [In, Out] int[] componentIds, int nComponents, bool appendToTable);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr UpdateIndividualAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int updateType, float dt, bool safe);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr UpdateCollisionAirshipComponent(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int updateType, int collisionObjId);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr UpdateAllAirshipComponents(LuauContext context, int updateType, float dt);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr GetAirshipComponentEnabled(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, ref int result);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr SetAirshipComponentEnabled(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int result);
    
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr HasAirshipMethod(LuauContext context, IntPtr thread, int unityInstanceId, int componentId, int updateType, ref int result);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr PushSignal(LuauContext context, IntPtr thread, int unityInstanceId, ulong propNameHash, ref int result);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr EmitSignal(LuauContext context, IntPtr thread, int unityInstanceId, ulong propNameHash, int numParams, ref int result);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr DestroySignals(LuauContext context, IntPtr thread, int unityInstanceId);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern unsafe IntPtr CreateThread(LuauContext context, byte* scriptBytecode, int scriptLength, IntPtr filename, int filenameLength, int gameObjectId, bool nativeCodegen);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr CreateThreadWithCachedModule(LuauContext context, string filename, int gameObjectId);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr CacheModuleOnThread(IntPtr thread, string filename);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr SetThreadDestroyed(IntPtr thread);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern unsafe IntPtr SetMutableGlobals(IntPtr* strings, IntPtr stringLengths, int numStrings);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr CompileCode(IntPtr script, int scriptLength, IntPtr filename, int filenameLength, int optimizationLevel);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr RunThread(IntPtr thread, int nArgs, ref int result);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr ResumeThread(IntPtr thread, int nArgs, ref int result);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr ResumeThreadError(IntPtr thread, ref int result);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr CallMethodOnThread(IntPtr thread, IntPtr methodName, int methodNameSize, int numParameters, ref int result);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr DestroyThread(IntPtr thread);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr PinThread(IntPtr thread);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr UnpinThread(IntPtr thread);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr PushValueToThread(IntPtr thread, int type, IntPtr data, ulong dataSize, int arraySize);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr PushVector3ToThread(IntPtr thread, float x, float y, float z);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr PushTableToThread(IntPtr thread, int initialSize);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr ErrorThread(IntPtr thread, IntPtr msg, int msgSize);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr GetDebugTrace(IntPtr thread, ref int result);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr GetTraceback(IntPtr thread, out IntPtr strPtr);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr RunTaskScheduler(LuauContext context, float now, float unscaledNow);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern void ResetTimeCache(LuauContext context, int fixedUpdate);

#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern LuauContext GetContextFromThread(IntPtr thread);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern LuauBytecodeVersion GetBytecodeVersion();
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern void SetScriptTimeoutDuration(int duration);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern void SetIsPaused(int isPaused);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr CopyTableToArray(IntPtr thread, IntPtr array, int type, int size, int idx);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern int RegisterStringAtom(IntPtr strPtr, ulong strLen);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern void PushCsError(IntPtr errPtr, int errLen);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern void SetGCState(int state);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern int CountGC(int context);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern int GetLuauPluginVersion(out IntPtr versionPtr);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern void DebugPrintStack(IntPtr thread);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern ulong GetUnityObjectCount();
	
	/// <summary>
	/// Get the various memory categories from Luau. The memCatDump list should be unique per Luau context.
	/// </summary>
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr GetMemoryCategoryDump(LuauContext context, ref ulong count);
	
	/// <summary>
	/// Fetch a string that contains the count of all registry item tables.
	/// </summary>
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern int DebugCountAllRegistryItems(LuauContext context, out IntPtr str);
	
	/// <summary>
	/// Fetch a list of all UnityObject Instance IDs tracked by the plugin.
	/// </summary>
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern int DebugGetAllTrackedInstanceIds(LuauContext context, out IntPtr ids);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr LuaNewTable(IntPtr thread, int nArray, int nRecord);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr LuaPushNil(IntPtr thread);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr LuaPushInteger(IntPtr thread, int n);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr LuaPushUnsignedInteger(IntPtr thread, uint n);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr LuaPushVector(IntPtr thread, float x, float y, float z);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr LuaPushBoolean(IntPtr thread, int b);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr LuaPushString(IntPtr thread, IntPtr str, int len);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr LuaPushThread(IntPtr thread);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr LuaRawSetI(IntPtr thread, int idx, int n);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr LuaPop(IntPtr thread, int n);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr LuaSetReadonly(IntPtr thread, int idx, int enabled);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr LuaRef(IntPtr thread, int idx, ref int refVal);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr LuaUnref(IntPtr thread, int refVal);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr LuaGetRef(IntPtr thread, int refVal);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	internal static extern IntPtr LuaGetTop(IntPtr thread, ref int top);
#endif

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void InitPlugin() {
#if UNITY_EDITOR
		// LibHandle = NativePluginHandles.LibLuauPluginHandle;
		// NativeLibUtil.BindDelegates(typeof(LuauPluginNative), LibHandle);
		NativePluginHandles.RegisterPlugin(typeof(LuauPluginNative));

		SubsystemRegistration();
#endif
	}

	internal static void TryInitPlugin() {
#if UNITY_EDITOR
		if (LibHandle == IntPtr.Zero) {
			InitPlugin();
		}
#endif
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
		public IsObjectDestroyedCallback isObjectDestroyedCallback;
		public GetUnityObjectName getUnityObjectNameCallback;
		
		public IntPtr staticList;
		public IntPtr staticListStrLen;
		public int staticCount;
		public int isServer;
		public int useUnityAllocator;
	}

	// Must match BytecodeVersion struct in Plugin.cpp
	[StructLayout(LayoutKind.Sequential)]
	public struct LuauBytecodeVersion {
		public int Min;
		public int Max;
		public int Target;
	}
}
