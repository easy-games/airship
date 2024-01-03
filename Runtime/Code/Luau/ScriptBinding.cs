using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Luau;
using UnityEngine.Profiling;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ScriptBinding : MonoBehaviour {
    private static int _scriptBindingIdGen;
    
    [NonSerialized]
    public BinaryFile m_script;

    public string m_fileFullPath;
    public bool m_error = false;
    public bool m_yielded = false;

#if UNITY_EDITOR
    public string m_assetPath;
    // public BinaryFile m_binaryFile;
#endif

    [HideInInspector] private bool started = false;
    public bool IsStarted => started;

    private bool _hasInitEarly = false;

    [HideInInspector]
    public bool m_canResume = false;
    [HideInInspector]
    public bool m_asyncYield = false;
    [HideInInspector]
    public IntPtr m_thread = IntPtr.Zero;
    [HideInInspector]
    public int m_onUpdateHandle = -1;
    [HideInInspector]
    public int m_onLateUpdateHandle = -1;

    [HideInInspector]
    public string m_shortFileName;
    private byte[] m_fileContents;

    private List<IntPtr> m_pendingCoroutineResumes = new List<IntPtr>();
    
    [HideInInspector]
    public LuauMetadata m_metadata = new();
    private readonly int _scriptBindingId = _scriptBindingIdGen++;
    
    private bool _isAirshipComponent;

    private LuauAirshipComponent _airshipComponent;
    private bool _airshipComponentEnabled = false;
    private bool _airshipScheduledToStart = false;
    
    public bool IsAirshipComponent => _isAirshipComponent;
    public bool IsAirshipComponentEnabled => _airshipComponentEnabled;
    
    // Injected from LuauHelper
    public static IAssetBridge AssetBridge;

    public static BinaryFile LoadBinaryFileFromPath(string fullFilePath) {
#if UNITY_EDITOR
        if (!Application.isPlaying) {
            return AssetDatabase.LoadAssetAtPath<BinaryFile>(fullFilePath);
        }
#endif
        var cleanPath = CleanupFilePath(fullFilePath);
        BinaryFile script = null;
        if (AssetBridge != null && AssetBridge.IsLoaded()) {
            try {
                script = AssetBridge.LoadAssetInternal<BinaryFile>(cleanPath);
            } catch (Exception e) {
                Debug.LogError($"Failed to load asset for script. Path: {fullFilePath}. Message: {e.Message}");
                Profiler.EndSample();
                return null;
            }
        } else {
#if UNITY_EDITOR
            // Fallback for editor mode
            script = AssetDatabase.LoadAssetAtPath<BinaryFile>(fullFilePath);
#endif
        }

        return script;
    }

    public void Error() {
        m_error = true;
        m_canResume = false;
    }
    
#if UNITY_EDITOR
    private Dictionary<string, string> _trackCustomProperties = new();

    private void SetupMetadata() {
        if (AssetBridge == null) {
            // Debug.LogWarning("AssetBridge null");
            return;
        }
        /*
        var binaryFile = AssetDatabase.LoadAssetAtPath<BinaryFile>(m_assetPath);
        if (binaryFile == null)
        {
            // Debug.LogWarning("BinaryFile null");
            return;
        }
        // Debug.Log("Got BinaryFile");
        m_binaryFile = binaryFile;
        */
        if (m_script == null) {
            if (!string.IsNullOrEmpty(m_fileFullPath)) {
                m_script = LoadBinaryFileFromPath(m_fileFullPath);
            }

            if (m_script == null) {
                return;
            }
        }
        ReconcileMetadata();

        if (Application.isPlaying) {
            WriteChangedComponentProperties();
        }
    }
    
    private void OnValidate() {
        SetupMetadata();
    }

    private void Reset() {
        SetupMetadata();
    }

    public void ReconcileMetadata() {
        // Debug.Log("Reconciling metadata");
        if (m_script == null || (m_script.m_metadata == null || m_script.m_metadata.name == "")) {
            if (m_metadata.properties != null) {
                m_metadata.properties.Clear();
            }
            _isAirshipComponent = false;
            return;
        }

        m_metadata.name = m_script.m_metadata.name;
        
        // Add missing properties or reconcile existing ones:
        foreach (var property in m_script.m_metadata.properties) {
            var serializedProperty = m_metadata.FindProperty<object>(property.name);
            if (serializedProperty == null) {
                m_metadata.properties.Add(property.Clone());
            } else {
                serializedProperty.type = property.type;
                serializedProperty.objectType = property.objectType;
                serializedProperty.items.type = property.items.type;
                serializedProperty.items.objectType = property.items.objectType;
            }
        }

        // Remove properties that are no longer used:
        List<LuauMetadataProperty> propertiesToRemove = null;
        foreach (var serializedProperty in m_metadata.properties) {
            var property = m_script.m_metadata.FindProperty<object>(serializedProperty.name);
            if (property == null) {
                if (propertiesToRemove == null) {
                    propertiesToRemove = new List<LuauMetadataProperty>();
                }
                propertiesToRemove.Add(serializedProperty);
            }
        }
        if (propertiesToRemove != null) {
            foreach (var serializedProperty in propertiesToRemove) {
                m_metadata.properties.Remove(serializedProperty);
            }
        }

        _isAirshipComponent = true;
    }

    private void WriteChangedComponentProperties() {
        var airshipComponent = gameObject.GetComponent<LuauAirshipComponent>();
        if (airshipComponent == null || m_thread == IntPtr.Zero) return;
        
        foreach (var property in m_metadata.properties) {
            _trackCustomProperties.TryAdd(property.name, "");
            var lastValue = _trackCustomProperties[property.name];
            if (lastValue == property.serializedValue) continue;

            _trackCustomProperties[property.name] = property.serializedValue;
            property.WriteToComponent(m_thread, airshipComponent.Id, _scriptBindingId);
        }
    }
#endif

    public string GetAirshipComponentName() {
        if (!_isAirshipComponent) return null;
        return m_metadata.name;
    }

    public int GetAirshipComponentId() {
        return _scriptBindingId;
    }

    private IEnumerator StartAirshipComponentAtEndOfFrame() {
        yield return new WaitForEndOfFrame();
        // yield return null; // WaitForEndOfFrame() wasn't firing on the server using MPPM. But this works...

        if (!LuauCore.IsReady) {
            print("Airship component did not start because LuauCore instance not ready");
            yield break;
        }
        
        _airshipScheduledToStart = false;
        if (!_airshipComponentEnabled) {
            InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipEnabled);
            _airshipComponentEnabled = true;
        }
        InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipStart);
    }

    private void StartAirshipComponent(IntPtr thread) {
        _airshipComponent = gameObject.GetComponent<LuauAirshipComponent>() ?? gameObject.AddComponent<LuauAirshipComponent>();
        
        // Collect all public properties
        var nProps = m_metadata.properties.Count;
        var propertyDtos = new LuauMetadataPropertyMarshalDto[nProps];
        var gcHandles = new List<GCHandle>();
        var stringPtrs = new List<IntPtr>();
        for (var i = 0; i < nProps; i++) {
            var property = m_metadata.properties[i];
            property.AsStructDto(thread, gcHandles, stringPtrs, out var dto);
            propertyDtos[i] = dto;
        }

        LuauPlugin.LuauCreateAirshipComponent(thread, _airshipComponent.Id, _scriptBindingId, propertyDtos);
        
        // Free all GCHandles and name pointers
        foreach (var ptr in stringPtrs) {
            Marshal.FreeCoTaskMem(ptr);
        }
        foreach (var dtoGch in gcHandles) {
            dtoGch.Free();
        }
        
        InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipAwake);
        
        _airshipScheduledToStart = true;
        if (isActiveAndEnabled) {
            StartCoroutine(StartAirshipComponentAtEndOfFrame());
        }
    }

    public void InitEarly() {
        if (_hasInitEarly) {
            // print($"Already called InitEarly on object {name}");
            if (!started && LuauCore.IsReady) {
                Init();
            }
            return;
        }
        _hasInitEarly = true;
        
        if (m_script == null && !string.IsNullOrEmpty(m_fileFullPath)) {
            m_script = LoadBinaryFileFromPath(m_fileFullPath);
            if (m_script == null) {
                Debug.LogWarning($"Failed to reconcile script from path \"{m_fileFullPath}\" on {name}");
            }
        }
        
        _isAirshipComponent = m_script != null && m_script.m_metadata != null &&
                              m_script.m_metadata.name != "";

        if (_isAirshipComponent) {
            InitWhenCoreReady();
        }
    }

    private void Awake() { 
        InitEarly();
    }
    
    private void Start() {
        if (_isAirshipComponent) return;
        
        InitWhenCoreReady();
    }

    private void InitWhenCoreReady() {
        if (LuauCore.IsReady) {
            Init();
        } else {
            if (_isAirshipComponent) {
                _airshipScheduledToStart = true;
            }
            StartCoroutine(AwaitCoreThenInit());
        }
    }

    private IEnumerator AwaitCoreThenInit() {
        yield return new WaitUntil(() => LuauCore.IsReady);
        Init();
    }

    [Obsolete]
    private IEnumerator LateStart() {
        yield return null;
        Init();
    }

    public void Init() {
        if (this.started) return;
        this.started = true;

        //Just dont do anything if empty
        // if (m_fileFullPath == "")
        // {
        //     return;
        // }
        if (m_script == null) {
            Debug.LogWarning($"No script attached to binding {gameObject.name}");
            return;
        }

        Profiler.BeginSample("LuauBinding.Start");
        bool res = CreateThread(m_script);
        Profiler.EndSample();
    }

    private static string CleanupFilePath(string path) {

        string extension = System.IO.Path.GetExtension(path);

        if (extension == "") {
            // return path + ".lua";
            path += ".lua";
        }

        if (path.StartsWith("Assets/Bundles/", StringComparison.Ordinal)) {
            path = path.Substring("Assets/Bundles/".Length);
        }
        /*
         string noExtension = path.Substring(0, path.Length - extension.Length);

         if (noExtension.StartsWith("Assets/Resources/"))
         {
             noExtension = noExtension.Substring(new String("Assets/Resources/").Length);
         }

         if (noExtension.StartsWith("/"))
         {
             noExtension = noExtension.Substring(1);
         }

         return noExtension;*/
        return path;
    }


    private static string CleanupFilePathForResourceSystem(string path) {
        string extension = System.IO.Path.GetExtension(path);

        string noExtension = path.Substring(0, path.Length - extension.Length);

        if (noExtension.StartsWith("Assets/Resources/")) {
            noExtension = noExtension.Substring(new string("Assets/Resources/").Length);
        }
        if (noExtension.StartsWith("Resources/")) {
            noExtension = noExtension.Substring(new string("Resources/").Length);
        }

        if (noExtension.StartsWith("/")) {
            noExtension = noExtension.Substring(1);
        }

        return noExtension;
    }

    public bool CreateThreadFromPath(string fullFilePath) {
        // var script = LoadBinaryFileFromPath(fullFilePath);
        //
        // if (script == null) {
        //     Debug.LogError("Asset " + fullFilePath + " not found");
        //     return false;
        // }
        //
        // m_script = script;
        SetScriptFromPath(fullFilePath);
        if (m_script == null) {
            return false;
        }

        return CreateThread(m_script);
    }

    // public bool CreateThread(string fullFilePath)
    public bool CreateThread(BinaryFile script) {
        if (m_thread != IntPtr.Zero) {
            return false;
        }

        var cleanPath = CleanupFilePath(script.m_path);
        m_shortFileName = System.IO.Path.GetFileName(script.m_path);
        m_fileFullPath = script.m_path;

        LuauCore core = LuauCore.Instance;
        core.CheckSetup();


        Profiler.BeginSample("Marshal");
        IntPtr filenameStr = Marshal.StringToCoTaskMemUTF8(cleanPath); //Ok
        Profiler.EndSample();
        
        Profiler.BeginSample("GCHandle.Alloc");
        var gch = GCHandle.Alloc(script.m_bytes, GCHandleType.Pinned); //Ok
        Profiler.EndSample();

        //trickery, grab the id before we know the thread
        int id = ThreadDataManager.GetOrCreateObjectId(gameObject);

        Profiler.BeginSample("LuauCreateThread");
        m_thread = LuauPlugin.LuauCreateThread(gch.AddrOfPinnedObject(), script.m_bytes.Length, filenameStr, cleanPath.Length, id, true);
        Profiler.EndSample();
        //Debug.Log("Thread created " + m_thread.ToString("X") + " :" + fullFilePath);

        Profiler.BeginSample("MarshalFree");
        Marshal.FreeCoTaskMem(filenameStr);
        //Marshal.FreeCoTaskMem(dataStr);
        gch.Free();
        Profiler.EndSample();

        if (m_thread == IntPtr.Zero) {
            Debug.LogError("Script failed to compile" + m_shortFileName);
            m_canResume = false;
            m_error = true;

            return false;
        } else {
            Profiler.BeginSample("ThreadDataManager.AddObjectReference");
            ThreadDataManager.AddObjectReference(m_thread, gameObject);
            core.AddThread(m_thread, this); //@@//@@ hmm is this even used anymore?
            m_canResume = true;
            Profiler.EndSample();
        }

        if (m_canResume) {
            Profiler.BeginSample("ResumeScript");
            int retValue = LuauCore.Instance.ResumeScript(this);
            //Debug.Log("Thread result:" + retValue);
            if (retValue == 1) {
                //We yielded
                m_canResume = true;
            } else {
                m_canResume = false;
                if (retValue == -1) {
                    m_error = true;
                } else {
                    // Start airship component if applicable:
                    if (_isAirshipComponent) {
                        StartAirshipComponent(m_thread);
                    }
                }
            }
            Profiler.EndSample();

        }
        return true;
    }

    unsafe public void Update()
    {

        if (m_error == true) {
            return;
        }

        //Run any pending coroutines that waiting last frame
     
        foreach (IntPtr coroutinePtr in m_pendingCoroutineResumes) {
            if (coroutinePtr == m_thread) {
                //This is us, we dont need to resume ourselves here,
                //just set the flag to do it.
                m_canResume = true;
                continue;
            }
            
            ThreadDataManager.SetThreadYielded(m_thread, false);
            int retValue = LuauPlugin.LuauRunThread(coroutinePtr);
          
            if (retValue == -1) {
                m_canResume = false;
                m_error = true;
                break;
            }
        }
        m_pendingCoroutineResumes.Clear();


        double time = Time.realtimeSinceStartupAsDouble;
        if (m_canResume && !m_asyncYield) {
            ThreadDataManager.SetThreadYielded(m_thread, false);
            int retValue = LuauCore.Instance.ResumeScript(this);
            if (retValue != 1) {
                //we hit an error
                m_canResume = false;
            }
            if (retValue == -1) {
                m_error = true;
            }

        }

        // double elapsed = (Time.realtimeSinceStartupAsDouble - time)*1000.0f;
        //Debug.Log("execution: " + elapsed  + "ms");
    }

 
    public void QueueCoroutineResume(IntPtr thread) {
        m_pendingCoroutineResumes.Add(thread);
    }

    private void OnEnable() {
        if (_isAirshipComponent && !_airshipScheduledToStart && !_airshipComponentEnabled && LuauCore.IsReady) {
            InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipEnabled);
            _airshipComponentEnabled = true;
        }
    }

    private void OnDisable() {
        if (_isAirshipComponent && !_airshipScheduledToStart && _airshipComponentEnabled && LuauCore.IsReady) {
            InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipDisabled);
            _airshipComponentEnabled = false;
        }
    }

    private void OnDestroy() {
        if (m_thread != IntPtr.Zero) {
            if (LuauCore.IsReady) {
                if (_isAirshipComponent && _airshipComponent != null) {
                    var unityInstanceId = _airshipComponent.Id;
                    if (_airshipComponentEnabled) {
                        InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipDisabled);
                        _airshipComponentEnabled = false;
                    }

                    InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipDestroy);
                    LuauPlugin.LuauRemoveAirshipComponent(m_thread, unityInstanceId, _scriptBindingId);
                }
                LuauPlugin.LuauSetThreadDestroyed(m_thread);
            }
            
            //  LuauPlugin.LuauDestroyThread(m_thread); //TODO FIXME - Crashes on app shutdown? (Is already fixed I think)
            m_thread = IntPtr.Zero;
        }

    }

    private void InvokeAirshipLifecycle(AirshipComponentUpdateType updateType) {
        LuauPlugin.LuauUpdateIndividualAirshipComponent(m_thread, _airshipComponent.Id, _scriptBindingId, updateType, 0, true);
    }
    
    public void SetScript(BinaryFile script) {
        m_script = script;
        m_fileFullPath = script.m_path;
    }

    public void SetScriptFromPath(string path) {
        var script = LoadBinaryFileFromPath(path);
        if (script != null) {
            SetScript(script);
        } else {
            Debug.LogError($"Failed to load script: {path}");
        }
    }
}
