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
        POD_AIRSHIP_COMPONENT = 16,
    };

    public static bool s_shutdown = false;
 
    private static LuauCore _coreInstance;
    private static GameObject gameObj;
    

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
    private static Type actionType = typeof(Action);

    private static readonly string[] protectedScenesNames = {
        "corescene", "mainmenu", "login", "disconnected", "airshipupdateapp"
    };
    private static HashSet<int> protectedSceneHandles = new HashSet<int>();

    private bool initialized = false;
    private Coroutine endOfFrameCoroutine;

    private Dictionary<string, Type> shortTypeNames = new Dictionary<string, Type>();

    private List<string> namespaces = new List<string>();
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
    public static event Action OnInitialized;

    public static LuauCore CoreInstance {
        get {
            if (s_shutdown) {
                return null;
            }
            if (_coreInstance == null) {
                gameObj = new GameObject("LuauCore");
// #if !UNITY_EDITOR
                DontDestroyOnLoad(gameObj);
// #endif
                _coreInstance = gameObj.AddComponent<LuauCore>();
            }
            return _coreInstance;
        }
    }

    public static LuauState GetInstance(LuauContext context) {
        return LuauState.FromContext(context);
    }

    public bool CheckSetup() {
        if (initialized) return false;

        initialized = true;

        SetupProtectedSceneHandleListener();
        SetupReflection();
        CreateCallbacks();

        //start it
        // m_currentBuffer = m_pendingCoroutineResumesA;

        //Passing strings across the C# barrier to a DLL is a massive buttpain
        int stringCount = unityAPIClasses.Count;
        IntPtr[] stringList = new IntPtr[stringCount];
        GCHandle[] stringAllocations = new GCHandle[stringCount];
        System.Text.Encoding utf8 = System.Text.Encoding.UTF8;
        eventConnections.Clear();

        int counter = 0;
        foreach (var api in unityAPIClasses) {
            string apiName = api.Value.GetAPIType().Name;
            byte[] str = utf8.GetBytes(apiName);
            byte[] nullTerminatedBytes = new byte[str.Length + 1];
            for (int j = 0; j < str.Length; j++) {
                nullTerminatedBytes[j] = str[j];
            }

            stringAllocations[counter] = GCHandle.Alloc(nullTerminatedBytes, GCHandleType.Pinned); //Ok
            stringList[counter] = stringAllocations[counter].AddrOfPinnedObject();
            counter += 1;
        }
        var stringAddresses = GCHandle.Alloc(stringList, GCHandleType.Pinned); //Ok


        //Debug.Log("Starting Luau DLL");
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
                yieldCallback = yieldCallback_holder,
                toStringCallback = toStringCallback_holder,
                toggleProfilerCallback = toggleProfilerCallback_holder,
                isObjectDestroyedCallback = isObjectDestroyedCallback_holder,
                staticList = stringAddresses.AddrOfPinnedObject(),
                staticCount = stringCount,
                isServer = RunCore.IsServer() ? 1 : 0,
            }
        );

        // Force states to open:
        LuauState.FromContext(LuauContext.Protected);
        LuauState.FromContext(LuauContext.Game);

        stringAddresses.Free();
        //Free up the stringAllocations
        foreach (var alloc in stringAllocations) {
            alloc.Free();
        }

        SetupNamespaceStrings();

        StartCoroutine(InvokeOnInitializedNextFrame());
        
        return true;
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

    private IEnumerator InvokeOnInitializedNextFrame() {
        yield return null;
        OnInitialized?.Invoke();
    }

    public void OnDestroy() {
#if UNITY_EDITOR
        EditorApplication.pauseStateChanged -= OnPauseStateChanged;
#endif
        Profiler.BeginSample("ShutdownLuauState");
        LuauState.ShutdownAll();
        Profiler.EndSample();
        if (_coreInstance) {
            initialized = false;
            Profiler.BeginSample("ShutdownLuauPlugin");
            LuauPlugin.LuauShutdown();
            Profiler.EndSample();
            _coreInstance = null;
            if (endOfFrameCoroutine != null) {
                StopCoroutine(endOfFrameCoroutine);
            }
        }
    }

    public static void ShutdownInstance() {
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnSubsystemRegistration() {
        ResetStaticFields();
        LuauPlugin.LuauSubsystemRegistration();
    }
    
    private static void ResetStaticFields() {
        _awaitingTasks.Clear();
        eventConnections.Clear();
        propertyGetCache.Clear();
        protectedSceneHandles.Clear();
        _cache.Clear();
        writeMethodFunctions.Clear();
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
        _coreInstance = this;
        s_shutdown = false;
        CheckSetup();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnReload() {
        CurrentContext = LuauContext.Game;
        _coreInstance = null;
        s_shutdown = false;
        gameObj = null;
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
        if (context != LuauContext.Protected && IsProtectedScene(gameObject.scene)) {
            if (gameObject.transform.parent?.name is "GameReadAccess" || gameObject.transform.parent?.parent?.name is "GameReadAccess") {
                return false;
            }

            return false;
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
        
        LuauPlugin.LuauRunBeginFrameLogic();

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
        TryResumeAsyncTasks();
        Profiler.EndSample();

        //Run all pending callbacks
        Profiler.BeginSample("InvokeUpdate");
        ThreadDataManager.InvokeUpdate();
        Profiler.EndSample();
        
        // Profiler.BeginSample("RunTaskScheduler");
        // LuauPlugin.LuauRunTaskScheduler();
        // Profiler.EndSample();
        //
        // // Run airship component update methods
        // LuauPlugin.LuauUpdateAllAirshipComponents(AirshipComponentUpdateType.AirshipUpdate, Time.deltaTime);
        LuauState.UpdateAll();
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
