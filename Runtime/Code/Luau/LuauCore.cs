using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using System;
using UnityEngine;
using Luau;
using System.Threading;
using UnityEngine.Profiling;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

//Singleton
public partial class LuauCore : MonoBehaviour {
#if UNITY_EDITOR
    [InitializeOnLoad]
    private class LuauCorePrintCallbackLoader {
        static LuauCorePrintCallbackLoader() {
            LuauPlugin.LuauInitializePrintCallback(printCallback_holder);
        }
    }
#endif

    public enum PODTYPE : int {
        POD_DOUBLE = 0,
        POD_OBJECT = 1,
        POD_STRING = 2,
        POD_INT32 = 3,
        POD_VECTOR3 = 4,
        POD_BOOL = 5,
        POD_NULL = 6,
        POD_RAY = 7,
        POD_MATRIX = 8,
        POD_QUATERNION = 9,
        POD_PLANE = 10,
        POD_COLOR = 11,
        POD_LUAFUNCTION = 12,
        POD_BINARYBLOB = 13,
        POD_VECTOR2 = 14,
        POD_VECTOR4 = 15,
        POD_FLOAT = 16,
        POD_AIRSHIP_COMPONENT = 17,
        POD_BUFFER = 18,
    };

    private static bool s_shutdown = false;
 
    private static LuauCore _coreInstance;

    private static Type stringType = System.Type.GetType("System.String");
    private static Type intType = System.Type.GetType("System.Int32");
    private static Type uIntType = System.Type.GetType("System.UInt32");
    private static Type longType = System.Type.GetType("System.Int64");
    private static Type uLongType = System.Type.GetType("System.UInt64");
    private static Type boolType = System.Type.GetType("System.Boolean");
    private static Type componentType = typeof(Component);
    private static Type floatType = System.Type.GetType("System.Single");

    private static Type ushortType = System.Type.GetType("System.UInt16");
    private static Type byteType = System.Type.GetType("System.Byte");

    private static Type doubleType = System.Type.GetType("System.Double");
    private static Type enumType = System.Type.GetType("System.Enum");

    private static Type systemObjectType = typeof(System.Object);
    private static Type gameObjectType = typeof(UnityEngine.GameObject);

    private static Type transformType = typeof(UnityEngine.Transform);
    private static Type vector3Type = typeof(UnityEngine.Vector3);
    private static Type vector3IntType = typeof(UnityEngine.Vector3Int);
    private static Type rayType = typeof(UnityEngine.Ray);
    private static Type matrixType = typeof(UnityEngine.Matrix4x4);
    private static Type quaternionType = typeof(UnityEngine.Quaternion);
    private static Type vector2Type = typeof(UnityEngine.Vector2);
    private static Type vector2IntType = typeof(UnityEngine.Vector2Int);
    private static Type vector4Type = typeof(UnityEngine.Vector4);
    private static Type planeType = typeof(UnityEngine.Plane);
    private static Type colorType = typeof(UnityEngine.Color);
    private static Type binaryBlobType = typeof(Assets.Luau.BinaryBlob);
    private static Type luauBufferType = typeof(LuauBuffer);
    private static Type actionType = typeof(Action);

    private static readonly string[] protectedScenesNames = {
        "corescene", "mainmenu", "login", "disconnected", "airshipupdateapp", "dontdestroyonload",
    };
    private static HashSet<int> protectedSceneHandles = new HashSet<int>();

    private bool initialized = false;
    private Coroutine endOfFrameCoroutine;
    
    private Dictionary<string, BaseLuaAPIClass> unityAPIClasses = new Dictionary<string, BaseLuaAPIClass>();
    private Dictionary<Type, BaseLuaAPIClass> unityAPIClassesByType = new Dictionary<Type, BaseLuaAPIClass>();

    private Dictionary<Type, Dictionary<ulong, PropertyInfo>> unityPropertyAlias = new();
    private Dictionary<Type, Dictionary<ulong, FieldInfo>> unityFieldAlias = new();

    private class CallbackRecord {
        public IntPtr callback;
        public string trace;
        public CallbackRecord(IntPtr callback, string trace) {
            this.callback = callback;
            this.trace = trace;
        }
    }

    // private List<CallbackRecord> m_pendingCoroutineResumesA = new();
    // private List<CallbackRecord> m_pendingCoroutineResumesB = new();
    // private List<CallbackRecord> m_currentBuffer;
    
    // private Dictionary<IntPtr, ScriptBinding> m_threads = new Dictionary<IntPtr, ScriptBinding>();

    private Thread m_mainThread;

    public static event Action<LuauContext> onResetInstance;

    public static bool IsReady => !s_shutdown && _coreInstance != null && _coreInstance.initialized;
    
    public static LuauCore CoreInstance => _coreInstance;

    public static LuauState GetInstance(LuauContext context) {
        return LuauState.FromContext(context);
    }

    private void CheckSetup() {
        if (initialized) return;

        initialized = true;

        SetupProtectedSceneHandleListener();
        SetupReflection();
        CreateCallbacks();

        var stringCount = unityAPIClasses.Count;
        var stringList = new IntPtr[stringCount];
        eventConnections.Clear();
        
        var counter = 0;
        foreach (var api in unityAPIClasses) {
            var apiName = api.Value.GetAPIType().Name;
            var strPtr = Marshal.StringToCoTaskMemUTF8(apiName);
            stringList[counter] = strPtr;
            counter += 1;
        }
        var stringAddresses = GCHandle.Alloc(stringList, GCHandleType.Pinned);
        
        LuauPlugin.LuauInitializePrintCallback(printCallback_holder);
        LuauPlugin.LuauInitializeComponentCallbacks(componentSetEnabledCallback_holder);
        LuauPlugin.LuauStartup(
            new LuauPlugin.LuauPluginStartup {
                getPropertyCallback = getPropertyCallback_holder,
                setPropertyCallback = setPropertyCallback_holder,
                callMethodCallback = callMethodCallback_holder,
                objectGcCallback = objectGCCallback_holder,
                requireCallback = requireCallback_holder,
                requirePathCallback = requirePathCallback_holder,
                constructorCallback = constructorCallback_holder,
                toStringCallback = toStringCallback_holder,
                isObjectDestroyedCallback = isObjectDestroyedCallback_holder,
                getUnityObjectNameCallback = getUnityObjectNameCallback_holder,
                staticList = stringAddresses.AddrOfPinnedObject(),
                staticCount = stringCount,
                isServer = RunCore.IsServer() ? 1 : 0,
#if UNITY_EDITOR
                useUnityAllocator = 1,
#else
                useUnityAllocator = 0,
#endif
            }
        );

        // Force states to open:
        LuauState.FromContext(LuauContext.Protected);
        LuauState.FromContext(LuauContext.Game);

        stringAddresses.Free();
        
        // Free up the strings:
        foreach (var ptr in stringList) {
            Marshal.FreeCoTaskMem(ptr);
        }

        SetupNamespaceStrings();
    }

    private void SetupProtectedSceneHandleListener() {
        for (var i = 0; i < SceneManager.sceneCount; i++) {
            var scene = SceneManager.GetSceneAt(i);
            RegisterPossiblyProtectedScene(scene);
        }

        SceneManager.sceneLoaded += (scene, mode) => {
            RegisterPossiblyProtectedScene(scene);
        };
        SceneManager.sceneUnloaded += scene => {
            protectedSceneHandles.Remove(scene.handle);
        };
    }

    private void RegisterPossiblyProtectedScene(Scene scene) {
        if (IsProtectedSceneName(scene.name)) {
            protectedSceneHandles.Add(scene.handle);
        }
    }

    public void OnDestroy() {
#if UNITY_EDITOR
        EditorApplication.pauseStateChanged -= OnPauseStateChanged;
#endif
        if (_coreInstance == this) {
            _coreInstance = null;
            initialized = false;
            LuauState.ShutdownAll();
            Profiler.BeginSample("ShutdownLuauPlugin");
            LuauPlugin.LuauShutdown();
            Profiler.EndSample();
            if (endOfFrameCoroutine != null) {
                StopCoroutine(endOfFrameCoroutine);
            }
        }
    }

    private static void ShutdownInstance() {
        if (_coreInstance) {
#if UNITY_EDITOR
            DestroyImmediate(_coreInstance.gameObject);
#else
            Destroy(_coreInstance.gameObject);
#endif
        }
    }

    public static void ShutdownContext(LuauContext context) {
        LuauState.Shutdown(context);
    }
    
    private static void ResetStaticFields() {
        _awaitingTasks.Clear();
        eventConnections.Clear();
        propertyGetCache.Clear();
        protectedSceneHandles.Clear();
        _propertySetterCache.Clear();
        WriteMethodFunctions.Clear();
        CurrentContext = LuauContext.Game;
        s_shutdown = false;
    }

    public static void ResetContext(LuauContext context) {
        if (!_coreInstance) return;

        if (Application.isPlaying) {
            Debug.Log("LuauCore.ResetContext()");
        }
        
        LuauCore.onResetInstance?.Invoke(context);
        
        ResetStaticFields();
        
        ThreadDataManager.ResetContext(context);
        // ThreadDataManager.OnReset();
        // if (_instance.m_currentBuffer != null) {
        //     _instance.m_currentBuffer.Clear();
        // }

        LuauState.FromContext(context).Reset();

        // _instance.initialized = false;
    }

    private void Awake() {
        if (_coreInstance != null) {
            // Ensure only one CoreInstance exists
            Destroy(gameObject);
            return;
        }
        
        _coreInstance = this;
        DontDestroyOnLoad(gameObject);
        s_shutdown = false;
        CheckSetup();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnReload() {
        ResetStaticFields();
        _coreInstance = null;
        LuauPlugin.LuauSubsystemRegistration();
        Application.quitting -= Quit;
    }

#if UNITY_EDITOR
    private void OnPauseStateChanged(PauseState state) {
        LuauPlugin.LuauSetIsPaused(state == PauseState.Paused);
    }
#endif

    private void Start() {
        Application.quitting += Quit;
        LuauPlugin.unityMainThreadId = Thread.CurrentThread.ManagedThreadId;
        StartCoroutine(PrintReferenceAssemblies());
        endOfFrameCoroutine = StartCoroutine(RunAtVeryEndOfFrame());
        
#if UNITY_EDITOR
        EditorApplication.pauseStateChanged += OnPauseStateChanged;
#endif
    }

    public Thread GetMainThread() {
        return m_mainThread;
    }

    private static void Quit() {
        s_shutdown = true;
    }

    public static bool IsAccessBlocked(LuauContext context, GameObject gameObject) {
        if (gameObject == null) return false;
        if (context != LuauContext.Protected && IsProtectedScene(gameObject.scene)) {
            if (gameObject.transform.parent?.name is "GameReadAccess" || gameObject.transform.parent?.parent?.name is "GameReadAccess") {
                return false;
            }

            return true;
        }

        return false;
    }

    public static bool IsProtectedScene(Scene scene) {
        return protectedSceneHandles.Contains(scene.handle);
    }

    /// <summary>
    /// Unless you only have scene name you should use IsProtectedScene
    /// </summary>
    public static bool IsProtectedSceneName(string sceneName) {
        if (string.IsNullOrEmpty(sceneName)) return false;

        foreach (var protectedSceneName in protectedScenesNames) {
            if (protectedSceneName.Equals(sceneName, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }
        return false;
    }

    public void Update() {
        if (!initialized) {
            return;
        }
        
        Profiler.BeginSample("BeginFrameLogic");
        LuauPlugin.LuauRunBeginFrameLogic();
        Profiler.EndSample();

        // List<CallbackRecord> runBuffer = m_currentBuffer;
        // if (m_currentBuffer == m_pendingCoroutineResumesA) {
        //     m_currentBuffer = m_pendingCoroutineResumesB;
        // } else {
        //     m_currentBuffer = m_pendingCoroutineResumesA;
        // }
        //
        // foreach (CallbackRecord coroutineCallback in runBuffer) {
        //     //context of the callback is in coroutineCallback.trace
        //     ThreadDataManager.SetThreadYielded(coroutineCallback.callback, false);
        //     int retValue = LuauPlugin.LuauRunThread(coroutineCallback.callback);
        // }
        // runBuffer.Clear();

        Profiler.BeginSample("TryResumeAsyncTasks");
        try {
            TryResumeAsyncTasks();
        } catch (Exception err) {
            Debug.LogError(err);
        } finally {
            Profiler.EndSample();
        }

        //Run all pending callbacks
        Profiler.BeginSample("InvokeUpdate");
        try {
            ThreadDataManager.InvokeUpdate();
        } catch (Exception err) {
            Debug.LogError(err);
        } finally {
            Profiler.EndSample();
        }
        
        // Profiler.BeginSample("RunTaskScheduler");
        // LuauPlugin.LuauRunTaskScheduler();
        // Profiler.EndSample();
        //
        // // Run airship component update methods
        // LuauPlugin.LuauUpdateAllAirshipComponents(AirshipComponentUpdateType.AirshipUpdate, Time.deltaTime);
        Profiler.BeginSample("UpdateAll");
        LuauState.UpdateAll();
        Profiler.EndSample();
    }

    public void LateUpdate() {
        Profiler.BeginSample("InvokeLateUpdate");
        ThreadDataManager.InvokeLateUpdate();
        // Profiler.EndSample();
        // Profiler.BeginSample("UpdateAllAirshipComponents");
        // LuauPlugin.LuauUpdateAllAirshipComponents(AirshipComponentUpdateType.AirshipLateUpdate, Time.deltaTime);
        LuauState.LateUpdateAll();
        Profiler.EndSample();
    }
    
    public void FixedUpdate() {
        Profiler.BeginSample("InvokeFixedUpdate");
        ThreadDataManager.InvokeFixedUpdate();
        // Profiler.EndSample();
        // Profiler.BeginSample("UpdateAllAirshipComponents");
        // LuauPlugin.LuauUpdateAllAirshipComponents(AirshipComponentUpdateType.AirshipFixedUpdate, Time.fixedDeltaTime);
        LuauState.FixedUpdateAll();
        Profiler.EndSample();
    }

    private IEnumerator RunAtVeryEndOfFrame() {
        while (!initialized) {
            yield return new WaitForEndOfFrame();
        }
        
        while (true) {
            yield return new WaitForEndOfFrame();
        
            Profiler.BeginSample("RunEndOfFrame");
            ThreadDataManager.RunEndOfFrame();
            Profiler.EndSample();
        }
    }

}
