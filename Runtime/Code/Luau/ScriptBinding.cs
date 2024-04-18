using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Luau;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
public struct PropertyValueState {
    public string serializedValue;
    public UnityEngine.Object[] itemObjectRefs;
    public string[] itemSerializedObjects;
}
#endif

[AddComponentMenu("Airship/Script Binding")]
public class ScriptBinding : MonoBehaviour {
    private const bool ElevateToProtectedWithinCoreScene = false;
    
    private static int _scriptBindingIdGen;
    
    [NonSerialized]
    public BinaryFile luauFile;

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

    private LuauContext _context = LuauContext.Game;
    
    private bool _isAirshipComponent;

    private AirshipBehaviourRoot _airshipBehaviourRoot;
    private bool _airshipComponentEnabled = false;
    private bool _airshipReadyToStart = false;
    private bool _airshipScheduledToStart = false;
    private bool _airshipStarted = false;
    private bool _airshipWaitingForLuauCoreReady = false;
    private bool _airshipRewaitForLuauCoreReady = false;
    private bool _scriptBindingStarted = false;
    private Dictionary<AirshipComponentUpdateType, bool> _hasAirshipUpdateMethods = new(); 
    
    public bool IsAirshipComponent => _isAirshipComponent;
    public bool IsAirshipComponentEnabled => _airshipComponentEnabled;
    
    // Injected from LuauHelper
    public static IAssetBridge AssetBridge;

    public BinaryFile LoadBinaryFileFromPath(string fullFilePath) {
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
                Debug.LogError($"Failed to load asset for script on GameObject \"{this.gameObject.name}\". Path: {fullFilePath}. Message: {e.Message}", gameObject);
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
    private Dictionary<string, PropertyValueState> _trackCustomProperties = new();

    private void SetupMetadata() {
        if (AssetBridge == null) {
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
        
        // Clear out script if file path doesn't match script path
        if (luauFile != null) {
            if (luauFile.m_path != m_fileFullPath) {
                luauFile = null;
            }
        }
        // Set script from file path
        if (luauFile == null) {
            if (!string.IsNullOrEmpty(m_fileFullPath)) {
                luauFile = LoadBinaryFileFromPath(m_fileFullPath);
                
            }

            if (luauFile == null) {
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
#if AIRSHIP_PLAYER
        return;
#endif

        // print("Reconciling Metadata");
        if (luauFile == null || (luauFile.m_metadata == null || luauFile.m_metadata.name == "")) {
            if (m_metadata.properties != null) {
                m_metadata.properties.Clear();
            }
            _isAirshipComponent = false;
            return;
        }

        m_metadata.name = luauFile.m_metadata.name;

        // Add missing properties or reconcile existing ones:
        foreach (var property in luauFile.m_metadata.properties) {
            var serializedProperty = m_metadata.FindProperty<object>(property.name);
            
            if (serializedProperty == null)
            {
                var element = property.Clone();
                m_metadata.properties.Add(element);
            } else {
                if (serializedProperty.type != property.type || serializedProperty.objectType != property.objectType) {
                    serializedProperty.type = property.type;
                    serializedProperty.objectType = property.objectType;
                    serializedProperty.serializedValue = property.serializedValue;
                    serializedProperty.serializedObject = property.serializedObject;
                    serializedProperty.modified = false;
                }

                if (property.items != null) {
                    if (serializedProperty.items.type != property.items.type ||
                        serializedProperty.items.objectType != property.items.objectType) {
                        serializedProperty.items.type = property.items.type;
                        serializedProperty.items.objectType = property.items.objectType;
                        serializedProperty.items.serializedItems = property.items.serializedItems;
                    }
                }
            }
        }
        
        // Remove properties that are no longer used:
        List<LuauMetadataProperty> propertiesToRemove = null;
        foreach (var serializedProperty in m_metadata.properties) {
            var property = luauFile.m_metadata.FindProperty<object>(serializedProperty.name);
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
        var airshipComponent = gameObject.GetComponent<AirshipBehaviourRoot>();
        if (airshipComponent == null || m_thread == IntPtr.Zero) return;
        
        foreach (var property in m_metadata.properties) { 
            // If all value data is unchanged skip this write
            if (!ShouldWriteToComponent(property)) continue;

            _trackCustomProperties[property.name] = new PropertyValueState {
                serializedValue = property.serializedValue,
                itemObjectRefs = (UnityEngine.Object[]) property.items.objectRefs.Clone(),
                itemSerializedObjects = (string[]) property.items.serializedItems.Clone()
            };
            property.WriteToComponent(m_thread, airshipComponent.Id, _scriptBindingId);
        }
    }

    private bool ShouldWriteToComponent(LuauMetadataProperty property) {
        var valueExisted = _trackCustomProperties.TryGetValue(property.name, out var lastValue);
        if (!valueExisted) return true;
        if (lastValue.serializedValue != property.serializedValue) return true;

        if (property.ComponentType == AirshipComponentPropertyType.AirshipArray) {
            if (property.items.serializedItems.Length != lastValue.itemSerializedObjects.Length) return true;
            for (var i = 0; i < property.items.serializedItems.Length; i++) {
                if (property.items.serializedItems[i] != lastValue.itemSerializedObjects[i]) return true;
            }
            
            if (property.items.objectRefs.Length != lastValue.itemObjectRefs.Length) return true;
            for (var i = 0; i < property.items.objectRefs.Length; i++) {
                if (property.items.objectRefs[i] != lastValue.itemObjectRefs[i]) return true;
            }
        }

        return false;
    }
#endif

    private bool HasAirshipMethod(AirshipComponentUpdateType updateType) {
        if (_hasAirshipUpdateMethods.TryGetValue(updateType, out var has)) {
            return has;
        }
        
        // Fetch from Luau plugin & cache the result:
        var hasMethod = LuauPlugin.LuauHasAirshipMethod(_context, m_thread, _airshipBehaviourRoot.Id, _scriptBindingId, updateType);
        _hasAirshipUpdateMethods.Add(updateType, hasMethod);
        
        return hasMethod;
    }

    public string GetAirshipComponentName() {
        if (!_isAirshipComponent) return null;
        return m_metadata.name;
    }

    public int GetAirshipComponentId() {
        return _scriptBindingId;
    }

    private void StartAirshipComponentImmediately() {
        _airshipScheduledToStart = false;
        if (!_airshipComponentEnabled) {
            InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipEnabled);
            _airshipComponentEnabled = true;
        }
        InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipStart);
        _airshipStarted = true;
    }

    private IEnumerator StartAirshipComponentAtEndOfFrame() {
        if (RunCore.IsClone()) {
            yield return null; // WaitForEndOfFrame() wasn't firing on the server using MPPM. But this works...
        } else {
            yield return new WaitForEndOfFrame();
        }

        if (!LuauCore.IsReady) {
            print("Airship component did not start because LuauCore instance not ready");
            yield break;
        }
        
        StartAirshipComponentImmediately();
    }

    private void AwakeAirshipComponent(IntPtr thread) {
        _airshipBehaviourRoot = gameObject.GetComponent<AirshipBehaviourRoot>() ?? gameObject.AddComponent<AirshipBehaviourRoot>();
        
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

        var transformInstanceId = ThreadDataManager.GetOrCreateObjectId(gameObject.transform);
        LuauPlugin.LuauCreateAirshipComponent(_context, thread, _airshipBehaviourRoot.Id, _scriptBindingId, propertyDtos, transformInstanceId);
        
        // Free all GCHandles and name pointers
        foreach (var ptr in stringPtrs) {
            Marshal.FreeCoTaskMem(ptr);
        }
        foreach (var dtoGch in gcHandles) {
            dtoGch.Free();
        }
        
        InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipAwake);

        _airshipReadyToStart = true;
        
        if (isActiveAndEnabled && _scriptBindingStarted) {
            _airshipScheduledToStart = true;
            if (LuauCore.IsReady) {
                StartAirshipComponentImmediately();
            } else {
                StartCoroutine(StartAirshipComponentAtEndOfFrame());
            }
        } else {
            _airshipScheduledToStart = false;
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

        if (luauFile == null && !string.IsNullOrEmpty(m_fileFullPath)) {
            luauFile = LoadBinaryFileFromPath(m_fileFullPath);
            if (luauFile == null) {
                Debug.LogWarning($"Failed to reconcile script from path \"{m_fileFullPath}\" on {name}");
            }
        } else if (luauFile == null && string.IsNullOrEmpty(m_fileFullPath)) {
            // No script to run; stop here.
            _hasInitEarly = false;
            return;
        }
        
        // _isAirshipComponent = luauFile != null && luauFile.m_metadata != null &&
        //                       luauFile.m_metadata.name != "";
        _isAirshipComponent = this.luauFile != null && this.luauFile.airshipBehaviour;

        if (_isAirshipComponent) {
            InitWhenCoreReady();
        }
    }

    private void Awake() {
        LuauCore.onResetInstance += OnLuauReset;
        
        // Assume protected context for bindings within CoreScene
        if (gameObject.scene.name == "CoreScene" && ElevateToProtectedWithinCoreScene) {
            _context = LuauContext.Protected;
        }
        
        InitEarly();
    }
    
    private void Start() {
        if (luauFile == null) {
            return;
        }
        
        _scriptBindingStarted = true;

        if (_isAirshipComponent) {
            if (_airshipReadyToStart && !_airshipScheduledToStart && !_airshipStarted) {
                StartAirshipComponentImmediately();
            }
            return;
        }
        
        InitWhenCoreReady();
    }

    private void InitWhenCoreReady() {
        if (LuauCore.IsReady) {
            Init();
        } else {
            if (_isAirshipComponent && isActiveAndEnabled) {
                _airshipScheduledToStart = true;
            }
            AwaitCoreThenInit();
        }
    }

    private void AwaitCoreThenInit() {
        _airshipWaitingForLuauCoreReady = true;
        LuauCore.OnInitialized += OnCoreInitialized;
        if (LuauCore.IsReady) {
            OnCoreInitialized();
        }
    }

    private void OnCoreInitialized() {
        LuauCore.OnInitialized -= OnCoreInitialized;
        _airshipWaitingForLuauCoreReady = false;
        Init();
    }

    public void Init() {
        if (started) return;
        started = true;
        
        if (luauFile == null) {
            Debug.LogWarning($"No script attached to ScriptBinding {gameObject.name}");
            return;
        }

        bool res = CreateThread(luauFile);
    }

    private static string CleanupFilePath(string path) {

        string extension = Path.GetExtension(path);

        if (extension == "") {
            // return path + ".lua";
            path += ".lua";
        }

        path = path.ToLower();
        if (path.StartsWith("assets/bundles/", StringComparison.Ordinal)) {
            path = path.Substring("assets/bundles/".Length);
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
        string extension = Path.GetExtension(path);

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

    public bool CreateThreadFromPath(string fullFilePath, LuauContext context) {
        // var script = LoadBinaryFileFromPath(fullFilePath);
        //
        // if (script == null) {
        //     Debug.LogError("Asset " + fullFilePath + " not found");
        //     return false;
        // }
        //
        // m_script = script;
        SetScriptFromPath(fullFilePath, context);
        if (luauFile == null) {
            return false;
        }

        return CreateThread(luauFile);
    }

    // public bool CreateThread(string fullFilePath)
    public bool CreateThread(BinaryFile script) {
        if (m_thread != IntPtr.Zero) {
            return false;
        }

        var cleanPath = CleanupFilePath(script.m_path);
        m_shortFileName = Path.GetFileName(script.m_path);
        m_fileFullPath = script.m_path;
        
        LuauCore.CoreInstance.CheckSetup();

        IntPtr filenameStr = Marshal.StringToCoTaskMemUTF8(cleanPath); //Ok

        //trickery, grab the id before we know the thread
        int id = ThreadDataManager.GetOrCreateObjectId(gameObject);

        // We only want one instance of airship components, so let's see if it already exists
        // in our require cache first.
        if (_isAirshipComponent) {
            var path = LuauCore.GetRequirePath(this, cleanPath);
            var thread = LuauPlugin.LuauCreateThreadWithCachedModule(_context, path, id);
            
            // If thread exists, we've found the module and put it onto the top of the thread stack. Use
            // this as our component startup thread:
            if (thread != IntPtr.Zero) {
                m_thread = thread;
                AwakeAirshipComponent(m_thread);
                return true;
            }
        }

        var gch = GCHandle.Alloc(script.m_bytes, GCHandleType.Pinned); //Ok

        m_thread = LuauPlugin.LuauCreateThread(_context, gch.AddrOfPinnedObject(), script.m_bytes.Length, filenameStr, cleanPath.Length, id, true);
        //Debug.Log("Thread created " + m_thread.ToString("X") + " :" + fullFilePath);

        Marshal.FreeCoTaskMem(filenameStr);
        //Marshal.FreeCoTaskMem(dataStr);
        gch.Free();

        if (m_thread == IntPtr.Zero) {
            Debug.LogError("Script failed to compile" + m_shortFileName);
            m_canResume = false;
            m_error = true;

            return false;
        } else {
            ThreadDataManager.AddObjectReference(m_thread, gameObject);
            LuauState.FromContext(_context).AddThread(m_thread, this); //@@//@@ hmm is this even used anymore?
            m_canResume = true;
        }

        if (m_canResume) {
            var retValue = LuauCore.CoreInstance.ResumeScript(_context, this);
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
                        var path = LuauCore.GetRequirePath(this, cleanPath);
                        LuauPlugin.LuauCacheModuleOnThread(m_thread, path);
                        AwakeAirshipComponent(m_thread);
                    }
                }
            }

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
            int retValue = LuauCore.CoreInstance.ResumeScript(_context, this);
            if (retValue != 1) {
                //we hit an error
                Debug.LogError("ResumeScript hit an error.", gameObject);
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
        // LuauCore.onResetInstance += OnLuauReset;
        
        // OnDisable stopped the luau-core-ready coroutine, so restart the await if needed:
        if (_airshipRewaitForLuauCoreReady) {
            _airshipRewaitForLuauCoreReady = false;
            _airshipScheduledToStart = false;
            InitWhenCoreReady();
        }
        
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

        // OnDisable stopped the luau-core-ready coroutine, so reset some flags:
        if (_airshipWaitingForLuauCoreReady) {
            LuauCore.OnInitialized -= OnCoreInitialized;
            _airshipWaitingForLuauCoreReady = false;
            _airshipRewaitForLuauCoreReady = true;
        }
    }

    private void OnLuauReset(LuauContext ctx) {
        if (ctx == _context) {
            // Debug.Log($"CLEARING THREAD POINTER SINCE CONTEXT HAS BEEN RESET {m_script.m_metadata?.name ?? name}");
            m_thread = IntPtr.Zero;
        }
    }

    private void OnDestroy() {
        LuauCore.onResetInstance -= OnLuauReset;
        if (m_thread != IntPtr.Zero) {
            if (LuauCore.IsReady) {
                if (_isAirshipComponent && _airshipBehaviourRoot != null) {
                    // Debug.Log($"DESTROYING AIRSHIP COMPONENT {m_script.m_metadata?.name ?? name}");
                    var unityInstanceId = _airshipBehaviourRoot.Id;
                    if (_airshipComponentEnabled) {
                        InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipDisabled);
                        _airshipComponentEnabled = false;
                    }

                    InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipDestroy);
                    LuauPlugin.LuauRemoveAirshipComponent(_context, m_thread, unityInstanceId, _scriptBindingId);
                }
                LuauPlugin.LuauSetThreadDestroyed(m_thread);
            }

            //  LuauPlugin.LuauDestroyThread(m_thread); //TODO FIXME - Crashes on app shutdown? (Is already fixed I think)
            m_thread = IntPtr.Zero;
        }

    }

    #region Collision Events
    private void OnCollisionEnter(Collision other) {
        if (_airshipStarted) {
            InvokeAirshipCollision(AirshipComponentUpdateType.AirshipCollisionEnter, other);
        }
    }

    private void OnCollisionStay(Collision other) {
        if (_airshipStarted) {
            InvokeAirshipCollision(AirshipComponentUpdateType.AirshipCollisionStay, other);
        }
    }

    private void OnCollisionExit(Collision other) {
        if (_airshipStarted) {
            InvokeAirshipCollision(AirshipComponentUpdateType.AirshipCollisionExit, other);
        }
    }

    private void OnCollisionEnter2D(Collision2D other) {
        if (_airshipStarted) {
            InvokeAirshipCollision(AirshipComponentUpdateType.AirshipCollisionEnter2D, other);
        }
    }

    private void OnCollisionStay2D(Collision2D other) {
        if (_airshipStarted) {
            InvokeAirshipCollision(AirshipComponentUpdateType.AirshipCollisionStay2D, other);
        }
    }

    private void OnCollisionExit2D(Collision2D other) {
        if (_airshipStarted) {
            InvokeAirshipCollision(AirshipComponentUpdateType.AirshipCollisionExit2D, other);
        }
    }
    #endregion

    #region Trigger Events
    private void OnTriggerEnter(Collider other) {
        if (_airshipStarted) {
            InvokeAirshipCollision(AirshipComponentUpdateType.AirshipTriggerEnter, other);
        }
    }

    private void OnTriggerStay(Collider other) {
        if (_airshipStarted) {
            InvokeAirshipCollision(AirshipComponentUpdateType.AirshipTriggerStay, other);
        }
    }

    private void OnTriggerExit(Collider other) {
        if (_airshipStarted) {
            InvokeAirshipCollision(AirshipComponentUpdateType.AirshipTriggerExit, other);
        }
    }

    private void OnTriggerEnter2D(Collider2D other) {
        if (_airshipStarted) {
            InvokeAirshipCollision(AirshipComponentUpdateType.AirshipTriggerEnter2D, other);
        }
    }

    private void OnTriggerStay2D(Collider2D other) {
        if (_airshipStarted) {
            InvokeAirshipCollision(AirshipComponentUpdateType.AirshipTriggerStay2D, other);
        }
    }

    private void OnTriggerExit2D(Collider2D other) {
        if (_airshipStarted) {
            InvokeAirshipCollision(AirshipComponentUpdateType.AirshipTriggerExit2D, other);
        }
    }
    #endregion

    private void InvokeAirshipLifecycle(AirshipComponentUpdateType updateType) {
        if (m_thread == IntPtr.Zero) {
            return;
        }

        // if (updateType == AirshipComponentUpdateType.AirshipStart) {
        //     Debug.Log($"STARTING AIRSHIP COMPONENT {m_script.m_metadata?.name ?? name}");
        // }
        LuauPlugin.LuauUpdateIndividualAirshipComponent(_context, m_thread, _airshipBehaviourRoot.Id, _scriptBindingId, updateType, 0, true);
    }

    private void InvokeAirshipCollision(AirshipComponentUpdateType updateType, object collision) {
        if (!HasAirshipMethod(updateType)) {
            return;
        }
        
        var collisionObjId = ThreadDataManager.AddObjectReference(m_thread, collision);
        LuauPlugin.LuauUpdateCollisionAirshipComponent(_context, m_thread, _airshipBehaviourRoot.Id, _scriptBindingId, updateType, collisionObjId);
    }
    
    public void SetScript(BinaryFile script, bool attemptStartup = false) {
        luauFile = script;
        m_fileFullPath = script.m_path;
        if (Application.isPlaying && attemptStartup) {
            InitEarly();
            Start();
        }
    }   

    public void SetScriptFromPath(string path, LuauContext context, bool attemptStartup = false) {
        var script = LoadBinaryFileFromPath(path);
        if (script != null) {
            _context = context;
            SetScript(script, attemptStartup);
        } else {
            Debug.LogError($"Failed to load script: {path}");
        }
    }
}
