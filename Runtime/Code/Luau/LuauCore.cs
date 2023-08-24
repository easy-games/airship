
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using System;
using UnityEngine;
using Luau;
using System.IO;
using System.Threading;
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
    private static Type planeType = typeof(UnityEngine.Plane);
    private static Type colorType = typeof(UnityEngine.Color);
    private static Type binaryBlobType = typeof(Assets.Luau.BinaryBlob);

    private bool initialized = false;

    private Dictionary<string, Type> shortTypeNames = new Dictionary<string, Type>();

    private List<string> namespaces = new List<string>();
    private Dictionary<string, BaseLuaAPIClass> unityAPIClasses = new Dictionary<string, BaseLuaAPIClass>();
    private Dictionary<Type, BaseLuaAPIClass> unityAPIClassesByType = new Dictionary<Type, BaseLuaAPIClass>();

    private Dictionary<Type, Dictionary<ulong, PropertyInfo>> unityPropertyAlias = new();
    private Dictionary<Type, Dictionary<ulong, FieldInfo>> unityFieldAlias = new();

    private List<IntPtr> m_pendingCoroutineResumesA = new List<IntPtr>();
    private List<IntPtr> m_pendingCoroutineResumesB = new List<IntPtr>();
    private List<IntPtr> m_currentBuffer;
    
    private Dictionary<IntPtr, LuauBinding> m_threads = new Dictionary<IntPtr, LuauBinding>();

    private Thread m_mainThread;

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
                Debug.Log("Creating LuauCore");
                gameObj = new GameObject("LuauCore");
#if !UNITY_EDITOR
                DontDestroyOnLoad(gameObj);
#endif
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

        SetupUnityAPIClasses();

        //start it
        m_currentBuffer = m_pendingCoroutineResumesA;

        //Passing strings across the C# barrier to a DLL is a massive buttpain
        int stringCount = unityAPIClasses.Count;
        IntPtr[] stringList = new IntPtr[stringCount];
        GCHandle[] stringAllocations = new GCHandle[stringCount];
        System.Text.Encoding utf8 = System.Text.Encoding.UTF8;

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

            stringAllocations[counter] = GCHandle.Alloc(nullTerminatedBytes, GCHandleType.Pinned);
            stringList[counter] = stringAllocations[counter].AddrOfPinnedObject();
            counter += 1;
        }
        var stringAddresses = GCHandle.Alloc(stringList, GCHandleType.Pinned);


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
        foreach (var alloc in stringAllocations)
        {
            alloc.Free();
        }

        SetupNamespaceStrings();

        return true;
    }

    public void OnDestroy()
    {
        Debug.Log("LuauCore.OnDestroy()");
        if (_instance)
        {
            LuauPlugin.LuauShutdown();
            _instance = null;
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
        ThreadDataManager.OnReset();
        _awaitingTasks.Clear();
        if (_instance.m_currentBuffer != null) {
            _instance.m_currentBuffer.Clear();
        }


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
        LuauPlugin.s_unityMainThread = Thread.CurrentThread;
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

        List<IntPtr> runBuffer = m_currentBuffer;
        if (m_currentBuffer == m_pendingCoroutineResumesA)
        {
            m_currentBuffer = m_pendingCoroutineResumesB;
        }
        else
        {
            m_currentBuffer = m_pendingCoroutineResumesA;
        }

        foreach (IntPtr coroutinePtr in runBuffer)
        {
            ThreadDataManager.SetThreadYielded(coroutinePtr, false);
            int retValue = LuauPlugin.LuauRunThread(coroutinePtr);

        }
        runBuffer.Clear();

        TryResumeAsyncTasks();

        //Run all pending callbacks
        ThreadDataManager.InvokeUpdate();
    }

    public void LateUpdate()
    {
        ThreadDataManager.InvokeLateUpdate();
        ThreadDataManager.RunEndOfFrame();
    }
    
    public void FixedUpdate()
    {
        ThreadDataManager.InvokeFixedUpdate();
    }
}
