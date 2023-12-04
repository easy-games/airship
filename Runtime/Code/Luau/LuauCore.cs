using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using System;
using UnityEngine;
using Luau;
using System.Threading;
using UnityEngine.Profiling;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

//Singleton
public partial class LuauCore : MonoBehaviour
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    private class LuauCorePrintCallbackLoader
    {
        static LuauCorePrintCallbackLoader()
        {
            LuauPlugin.LuauInitializePrintCallback(printCallback_holder);
        }
    }
#endif

    public enum PODTYPE : int
    {
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
    };

    public static bool s_shutdown = false;
 
    private static LuauCore _instance;
    private static GameObject gameObj;

    private static Type stringType = System.Type.GetType("System.String");
    private static Type intType = System.Type.GetType("System.Int32");
    private static Type uIntType = System.Type.GetType("System.UInt32");
    private static Type boolType = System.Type.GetType("System.Boolean");
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
    private static Type planeType = typeof(UnityEngine.Plane);
    private static Type colorType = typeof(UnityEngine.Color);
    private static Type binaryBlobType = typeof(Assets.Luau.BinaryBlob);

    private bool initialized = false;
    private Coroutine endOfFrameCoroutine;

    private Dictionary<string, Type> shortTypeNames = new Dictionary<string, Type>();

    private List<string> namespaces = new List<string>();
    private Dictionary<string, BaseLuaAPIClass> unityAPIClasses = new Dictionary<string, BaseLuaAPIClass>();
    private Dictionary<Type, BaseLuaAPIClass> unityAPIClassesByType = new Dictionary<Type, BaseLuaAPIClass>();

    private Dictionary<Type, Dictionary<ulong, PropertyInfo>> unityPropertyAlias = new();
    private Dictionary<Type, Dictionary<ulong, FieldInfo>> unityFieldAlias = new();

    private class CallbackRecord
    {
        public IntPtr callback;
        public string trace;
        public CallbackRecord(IntPtr callback, string trace)
        {
            this.callback = callback;
            this.trace = trace;
        }
    }

    private List<CallbackRecord> m_pendingCoroutineResumesA = new();
    private List<CallbackRecord> m_pendingCoroutineResumesB = new();
    private List<CallbackRecord> m_currentBuffer;
    
    private Dictionary<IntPtr, ScriptBinding> m_threads = new Dictionary<IntPtr, ScriptBinding>();

    private Thread m_mainThread;

    public static event Action onResetInstance;

    public static LuauCore Instance
    {
        get
        {
            if (s_shutdown)
            {
                return null;
            }
            if (_instance == null)
            {
                gameObj = new GameObject("LuauCore");
// #if !UNITY_EDITOR
                DontDestroyOnLoad(gameObj);
// #endif
                _instance = gameObj.AddComponent<LuauCore>();
            }
            return _instance;
        }
        private set
        {

        }
    }

    public bool CheckSetup()
    {
        if (initialized) return false;

        initialized = true;

        SetupReflection();
        CreateCallbacks();

        //start it
        m_currentBuffer = m_pendingCoroutineResumesA;

        //Passing strings across the C# barrier to a DLL is a massive buttpain
        int stringCount = unityAPIClasses.Count;
        IntPtr[] stringList = new IntPtr[stringCount];
        GCHandle[] stringAllocations = new GCHandle[stringCount];
        System.Text.Encoding utf8 = System.Text.Encoding.UTF8;
        eventConnections.Clear();

        int counter = 0;
        foreach (var api in unityAPIClasses)
        {
            string name = api.Value.GetAPIType().Name;
            byte[] str = utf8.GetBytes(name);
            byte[] nullTerminatedBytes = new byte[str.Length + 1];
            for (int j = 0; j < str.Length; j++)
            {
                nullTerminatedBytes[j] = str[j];
            }

            stringAllocations[counter] = GCHandle.Alloc(nullTerminatedBytes, GCHandleType.Pinned); //Ok
            stringList[counter] = stringAllocations[counter].AddrOfPinnedObject();
            counter += 1;
        }
        var stringAddresses = GCHandle.Alloc(stringList, GCHandleType.Pinned); //Ok


        //Debug.Log("Starting Luau DLL");
        LuauPlugin.LuauInitializePrintCallback(printCallback_holder);
        LuauPlugin.LuauStartup(
            getPropertyCallback_holder,
            setPropertyCallback_holder,
            callMethodCallback_holder,
            objectGCCallback_holder,
            requireCallback_holder,
            stringAddresses.AddrOfPinnedObject(),
            stringCount,
            requirePathCallback_holder,
            yieldCallback_holder
        );

        stringAddresses.Free();
        //Free up the stringAllocations
        foreach (var alloc in stringAllocations)
        {
            alloc.Free();
        }

        SetupNamespaceStrings();

        return true;
    }

    public void OnDestroy()
    {
        if (_instance) {
            print("Shutting down Luau...");
            LuauPlugin.LuauShutdown();
            _instance = null;
            StopCoroutine(endOfFrameCoroutine);
        }
    }

    public static void ShutdownInstance()
    {
        if (_instance)
        {
#if UNITY_EDITOR            
            DestroyImmediate(_instance.gameObject);
#else
            Destroy(_instance.gameObject);
#endif
        }
    }

    public static void ResetInstance()
    {
        if (!_instance) return;

        if (Application.isPlaying)
        {
            Debug.Log("LuauCore.ResetInstance()");
        }
        LuauCore.onResetInstance?.Invoke();
        ThreadDataManager.OnReset();
        _awaitingTasks.Clear();
        if (_instance.m_currentBuffer != null) {
            _instance.m_currentBuffer.Clear();
        }
        eventConnections.Clear();
        LuauCore.propertyGetCache.Clear();

        LuauPlugin.LuauReset();
    }

    private void Awake()
    {
        _instance = this;
        s_shutdown = false;
    }

    [RuntimeInitializeOnLoadMethod]
    static void RunOnStart()
    {
        // Debug.Log(LuauPlugin.PrintANumber());
        s_shutdown = false;
        var init = LuauCore.Instance;
        init.CheckSetup();

        Application.quitting -= Quit;
    }

    private void Start()
    {
        Application.quitting += Quit;
        LuauPlugin.unityMainThreadId = Thread.CurrentThread.ManagedThreadId;
        StartCoroutine(PrintReferenceAssemblies());
        endOfFrameCoroutine = StartCoroutine(RunAtVeryEndOfFrame());
    }

    public Thread GetMainThread()
    {
        return m_mainThread;
    }

    static void Quit()
    {
        s_shutdown = true;
    }

    public void Update()
    {
        if (initialized == false)
        {
            return;
        }

        List<CallbackRecord> runBuffer = m_currentBuffer;
        if (m_currentBuffer == m_pendingCoroutineResumesA)
        {
            m_currentBuffer = m_pendingCoroutineResumesB;
        }
        else
        {
            m_currentBuffer = m_pendingCoroutineResumesA;
        }

        foreach (CallbackRecord coroutineCallback in runBuffer)
        {
            //context of the callback is in coroutineCallback.trace
            ThreadDataManager.SetThreadYielded(coroutineCallback.callback, false);
            int retValue = LuauPlugin.LuauRunThread(coroutineCallback.callback);
        }
        runBuffer.Clear();

        Profiler.BeginSample("TryResumeAsyncTasks");
        TryResumeAsyncTasks();
        Profiler.EndSample();

        //Run all pending callbacks
        Profiler.BeginSample("InvokeUpdate");
        ThreadDataManager.InvokeUpdate();
        Profiler.EndSample();
        
        Profiler.BeginSample("RunTaskScheduler");
        LuauPlugin.LuauRunTaskScheduler();
        Profiler.EndSample();
        
        // Run airship component update methods
        LuauPlugin.LuauUpdateAllAirshipComponents(AirshipComponentUpdateType.AirshipUpdate, Time.deltaTime);

    
    }

    public void LateUpdate()
    {
        Profiler.BeginSample("InvokeLateUpdate");
        ThreadDataManager.InvokeLateUpdate();
        Profiler.EndSample();
        Profiler.BeginSample("UpdateAllAirshipComponents");
        LuauPlugin.LuauUpdateAllAirshipComponents(AirshipComponentUpdateType.AirshipLateUpdate, Time.deltaTime);
        Profiler.EndSample();
    }
    
    public void FixedUpdate()
    {
        Profiler.BeginSample("InvokeFixedUpdate");
        ThreadDataManager.InvokeFixedUpdate();
        Profiler.EndSample();
        Profiler.BeginSample("UpdateAllAirshipComponents");
        LuauPlugin.LuauUpdateAllAirshipComponents(AirshipComponentUpdateType.AirshipFixedUpdate, Time.fixedDeltaTime);
        Profiler.EndSample();
    }

    IEnumerator RunAtVeryEndOfFrame()
    {
        
        while (true)
        {
            yield return new WaitForEndOfFrame();
        
            Profiler.BeginSample("RunEndOfFrame");
            ThreadDataManager.RunEndOfFrame();
            Profiler.EndSample();
        }
    }

}
