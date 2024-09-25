using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Assets.Code.Luau;
using Luau;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
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

[AddComponentMenu("Airship/Airship Component")]
[LuauAPI(LuauContext.Protected)]
public class AirshipComponent : MonoBehaviour {
    #if UNITY_EDITOR
    private bool hasDelayedValidateCall = false;
    #endif
    private const bool ElevateToProtectedWithinCoreScene = true;
    
    private static int _scriptBindingIdGen;
    
    public AirshipScript scriptFile;
    
    [SerializeField][Obsolete("Do not use for referencing the script - use 'scriptFile'")]
    public string m_fileFullPath;
    public bool m_error = false;
    public bool m_yielded = false;

    public string TypescriptFilePath => m_fileFullPath.Replace(".lua", ".ts");

    [HideInInspector] private bool started = false;
    public bool IsStarted => started;

    private bool _hasInitEarly = false;

    internal bool HasComponentReference { get; private set; }

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

    [NonSerialized] public LuauContext context = LuauContext.Game;
    public bool contextOverwritten = false;

    private bool _isAirshipComponent;

    // private AirshipBehaviourRoot _airshipBehaviourRoot;
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

    public static bool validatedSceneInGameConfig = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnLoad() {
        validatedSceneInGameConfig = false;
    }

    private static bool IsReadyToStart() {
        return LuauCore.IsReady && SceneManager.GetActiveScene().name != "CoreScene";
    }
    
    public bool IsBindableAsComponent(AirshipScript file) {
        if (file.assetPath == scriptFile.assetPath) {
            return true;
        }

        return false;
    }

    public AirshipScript LoadBinaryFileFromPath(string fullFilePath) {
        var cleanPath = CleanupFilePath(fullFilePath);
#if UNITY_EDITOR && !AIRSHIP_PLAYER
        this.m_fileFullPath = fullFilePath;
        return AssetDatabase.LoadAssetAtPath<AirshipScript>("Assets/" + cleanPath.Replace(".lua", ".ts")) 
               ?? AssetDatabase.LoadAssetAtPath<AirshipScript>("Assets/" + cleanPath); // as we have Luau files in core as well
#endif
        AirshipScript script = null;
        if (AssetBridge != null && AssetBridge.IsLoaded()) {
            try {
                var luaPath = cleanPath.Replace(".ts", ".lua");
                script = AssetBridge.LoadAssetInternal<AirshipScript>(luaPath);
            } catch (Exception e) {
                Debug.LogError($"Failed to load asset for script on GameObject \"{this.gameObject.name}\". Path: {fullFilePath}. Message: {e.Message}", gameObject);
                return null;
            }
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
#if AIRSHIP_PLAYER
        if (AssetBridge == null) {
            print("MISSING ASSET BRIDGE: " + gameObject?.name);
            return;
        }
#endif
        // Clear out script if file path doesn't match script path
        if (scriptFile != null) {
            if (scriptFile.m_path != m_fileFullPath) {
                scriptFile = null;
            }
        }
        // Set script from file path
        if (scriptFile == null) {
            if (!string.IsNullOrEmpty(m_fileFullPath)) {
                scriptFile = LoadBinaryFileFromPath(m_fileFullPath);
                
            }

            if (scriptFile == null) {
                return;
            }
        }

        ReconcileMetadata();

        if (Application.isPlaying) {
            WriteChangedComponentProperties();
        }
    }
    
    private void OnValidate() {
        Validate();
    }

    private void Validate() {
        if (IsDestroyed()) return;
        
        if (scriptFile != null && string.IsNullOrEmpty(m_fileFullPath)) {
            m_fileFullPath = scriptFile.m_path;
        }
        
        SetupMetadata();
    }

    private void Reset() {
        SetupMetadata();
    }

    public void ReconcileMetadata() {
#if AIRSHIP_PLAYER
        return;
#endif

        if (scriptFile == null || (scriptFile.m_metadata == null || scriptFile.m_metadata.name == "")) {
            _isAirshipComponent = false;
            return;
        }

        m_metadata.name = scriptFile.m_metadata.name;

        // Add missing properties or reconcile existing ones:
        foreach (var property in scriptFile.m_metadata.properties) {
            var serializedProperty = m_metadata.FindProperty<object>(property.name);
            
            if (serializedProperty == null)
            {
                var element = property.Clone();
                m_metadata.properties.Add(element);
                serializedProperty = element;
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
                        serializedProperty.items.serializedItems = new string[property.items.serializedItems.Length];
                        serializedProperty.items.serializedItems =
                            property.items.serializedItems.Select(a => a).ToArray();
                        serializedProperty.items.objectRefs =
                            property.items.objectRefs.Select(a => a).ToArray();
                    }

                    serializedProperty.items.fileRef = property.fileRef;
                    serializedProperty.items.refPath = property.refPath;
                }
            }
            
            
            serializedProperty.fileRef = property.fileRef;
            serializedProperty.refPath = property.refPath;
        }
        
        // Remove properties that are no longer used:
        List<LuauMetadataProperty> propertiesToRemove = null;
        var seenProperties = new HashSet<string>();
        foreach (var serializedProperty in m_metadata.properties) {
            var property = scriptFile.m_metadata.FindProperty<object>(serializedProperty.name);
            // If it doesn't exist on script or if it is a duplicate property
            if (property == null || seenProperties.Contains(serializedProperty.name)) {
                if (propertiesToRemove == null) {
                    propertiesToRemove = new List<LuauMetadataProperty>();
                }
                propertiesToRemove.Add(serializedProperty);
            }
            seenProperties.Add(serializedProperty.name);
        }
        if (propertiesToRemove != null) {
            foreach (var serializedProperty in propertiesToRemove) {
                m_metadata.properties.Remove(serializedProperty);
            }
        }

        _isAirshipComponent = true;
    }

    private void WriteChangedComponentProperties() {
        if (!AirshipBehaviourRootV2.HasId(gameObject) || m_thread == IntPtr.Zero) return;
        
        foreach (var property in m_metadata.properties) {
            // If all value data is unchanged skip this write
            if (!ShouldWriteToComponent(property)) continue;

            _trackCustomProperties[property.name] = new PropertyValueState {
                serializedValue = property.serializedValue,
                itemObjectRefs = (UnityEngine.Object[]) property.items.objectRefs.Clone(),
                itemSerializedObjects = (string[]) property.items.serializedItems.Clone()
            };
            property.WriteToComponent(m_thread, AirshipBehaviourRootV2.GetId(gameObject), _scriptBindingId);
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
        var hasMethod = LuauPlugin.LuauHasAirshipMethod(context, m_thread, AirshipBehaviourRootV2.GetId(gameObject), _scriptBindingId, updateType);
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

    private bool IsDestroyed() {
        return _isDestroyed || this == null;
    }
    
    private IEnumerator StartAirshipComponentAtEndOfFrame() {
        if (IsDestroyed()) yield return null; // Can't start a dead object
        
        if (RunCore.IsClone()) {
            yield return null; // WaitForEndOfFrame() wasn't firing on the server using MPPM. But this works...
        } else {
            yield return new WaitForEndOfFrame();
        }

        if (!IsReadyToStart()) {
            print("Airship component did not start because LuauCore instance not ready");
            yield break;
        }
        
        StartAirshipComponentImmediately();
    }

    private void InitializeAirshipReference(IntPtr thread) {
        // Warmup the component first, creating a reference table
        var transformInstanceId = ThreadDataManager.GetOrCreateObjectId(transform);
        LuauPlugin.LuauPrewarmAirshipComponent(LuauContext.Game, m_thread, AirshipBehaviourRootV2.GetId(gameObject), _scriptBindingId, transformInstanceId);
    }

    private void InitializeAndAwakeAirshipComponent(IntPtr thread, bool usingExistingThread) {
        InitializeAirshipReference(thread);
        HasComponentReference = true;
        
        // Force dependencies to load earlier
        foreach (var dependency in Dependencies)
        {
            dependency.InitEarly();
        }

        try {
            AwakeAirshipComponent(thread);
        }
        catch (LuauException luauException) {
            Debug.LogError(m_metadata != null
                ? $"Failed to awake component {m_metadata.name} under {gameObject.name}: {luauException.Message}"
                : $"Failed to awake component 'file://{m_fileFullPath}' under {gameObject.name}: {luauException.Message}");
        }
    }

    private void AwakeAirshipComponent(IntPtr thread) {
        // Collect all public properties
        var properties = new List<LuauMetadataProperty>(m_metadata.properties);
        
        // Ensure allowed objects
        for (var i = m_metadata.properties.Count - 1; i >= 0; i--) {
            var property = m_metadata.properties[i];
            
            switch (property.type) {
                case "object": {
                    if (!ReflectionList.IsAllowedFromString(property.objectType, context)) {
                        Debug.LogError($"[Airship] Skipping AirshipBehaviour property \"{property.name}\": Type \"{property.objectType}\" is not allowed");
                        properties.RemoveAt(i);
                    }

                    break;
                }
            }
        }

        var propertyDtos = new LuauMetadataPropertyMarshalDto[properties.Count];
        var gcHandles = new List<GCHandle>();
        var stringPtrs = new List<IntPtr>();
        for (var i = 0; i < properties.Count; i++) {
            var property = properties[i];
            property.AsStructDto(thread, gcHandles, stringPtrs, out var dto);
            propertyDtos[i] = dto;
        }


        LuauPlugin.LuauInitializeAirshipComponent(context, thread, AirshipBehaviourRootV2.GetId(gameObject), _scriptBindingId, propertyDtos);
        // Set enabled property
        LuauPlugin.SetComponentEnabled(context, m_thread, AirshipBehaviourRootV2.GetId(gameObject), _scriptBindingId, enabled);
        
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
            
            // Defer to end of frame
            StartCoroutine(StartAirshipComponentAtEndOfFrame());
        } else {
            _airshipScheduledToStart = false;
        }
    }

    public IReadOnlyList<AirshipComponent> Dependencies {
        get {
            List<AirshipComponent> dependencies = new();
            foreach (var property in m_metadata.properties) {
                if (property.ComponentType == AirshipComponentPropertyType.AirshipComponent) {
                    var obj = property.serializedObject;
                    if (obj == null) continue;
                    dependencies.Add(obj as AirshipComponent);
                } else if (property.ComponentType == AirshipComponentPropertyType.AirshipArray && property.ArrayElementComponentType == AirshipComponentPropertyType.AirshipComponent) {
                    // Add array-bound components as dependencies too!
                    if (property.items.objectRefs == null) continue;
                    dependencies.AddRange(from arrayItem in property.items.objectRefs where arrayItem != null select arrayItem as AirshipComponent);
                }
            }

            return dependencies;
        }
    }

    public bool IsComponentDependencyOf(AirshipComponent other) {
        return other.Dependencies.Contains(this);
    }

    public bool IsCircularDependency(AirshipComponent other) {
        var deps = other.Dependencies;
        foreach (var dependency in deps) {
            if (this == dependency || dependency.IsCircularDependency(this)) {
                return true;
            }
        }

        return false;
    }

    public void InitEarly() {
        if (_hasInitEarly) {
            if (!started && IsReadyToStart()) {
                Init();
            }
            return;
        }
        _hasInitEarly = true;

        if (scriptFile == null && !string.IsNullOrEmpty(m_fileFullPath)) {
            scriptFile = LoadBinaryFileFromPath(m_fileFullPath);
            if (scriptFile == null) {
                Debug.LogWarning($"Failed to reconcile script from path \"{m_fileFullPath}\" on {name}", this.gameObject);
            }
        } else if (scriptFile == null && string.IsNullOrEmpty(m_fileFullPath)) {
            // No script to run; stop here.
            _hasInitEarly = false;
            return;
        }
        
        _isAirshipComponent = this.scriptFile != null && this.scriptFile.airshipBehaviour;
        InitWhenCoreReady();
    }

    private void Awake() {
#if UNITY_EDITOR
        if (!validatedSceneInGameConfig) {
            var sceneName = this.gameObject.scene.name;
            if (!LuauCore.IsProtectedScene(sceneName)) {
                var gameConfig = GameConfig.Load();
                if (gameConfig.gameScenes.ToList().Find((s) => ((SceneAsset)s).name == sceneName) == null) {
                    throw new Exception(
                        $"Tried to load AirshipComponent in a scene not found in GameConfig.scenes. Please add \"{sceneName}\" to your Assets/GameConfig.asset");
                }
            }
            validatedSceneInGameConfig = true;
        }
#endif
        LuauCore.CoreInstance.CheckSetup();
        LuauCore.onResetInstance += OnLuauReset;

        ScriptingEntryPoint.InvokeOnLuauStartup();

        InitEarly();
    }
    
    private void Start() {
        if (scriptFile == null) {
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
        if (IsDestroyed()) {
            DisconnectUnityEvents(); // Ensure any connected events are cleaned up
            return;
        }
        
        if (IsReadyToStart()) {
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
        if (IsDestroyed()) {
            DisconnectUnityEvents(); // Ensure any connected events are cleaned up
            return;
        }
        
        LuauCore.OnInitialized -= OnCoreInitialized;
        if (IsReadyToStart()) {
            _airshipWaitingForLuauCoreReady = false;
            Init();
        } else {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }
    }

    private void OnActiveSceneChanged(Scene current, Scene next) {
        if (IsReadyToStart()) {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            _airshipWaitingForLuauCoreReady = false;
            Init();
        }
    }
    
    public void Init() {
        if (IsDestroyed()) {
            DisconnectUnityEvents(); // Ensure any connected events are cleaned up
            return;
        }
        
        if (started) return;
        started = true;
        
        // Assume protected context for bindings within CoreScene
        if (!this.contextOverwritten && ((gameObject.scene.name is "CoreScene" or "MainMenu") || (SceneManager.GetActiveScene().name is "CoreScene" or "MainMenu")) && ElevateToProtectedWithinCoreScene) {
            context = LuauContext.Protected;
        }

        if (scriptFile == null) {
            Debug.LogWarning($"No script attached to ScriptBinding {gameObject.name}");
            return;
        }
        
        bool res = CreateThread();
    }

    private static string CleanupFilePath(string path) {
        
        string extension = Path.GetExtension(path);

        if (extension == "") {
            // return path + ".lua";
            path += ".lua";
        }

        path = path.ToLower();
        if (path.StartsWith("assets/", StringComparison.Ordinal)) {
            path = path.Substring("assets/".Length);
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
        SetScriptFromPath(fullFilePath, context);
        if (scriptFile == null) {
            return false;
        }

        return CreateThread();
    }
    
    public bool CreateThread() {
        if (m_thread != IntPtr.Zero) {
            return false;
        }

        if (!this.scriptFile.m_compiled) {
            if (string.IsNullOrEmpty(scriptFile.m_compilationError)) {
                throw new Exception($"{scriptFile.assetPath} cannot be executed due to not being compiled");
            }
            else {
                throw new Exception($"Cannot start script at {this.scriptFile.assetPath} with compilation errors: {this.scriptFile.m_compilationError}");
            }
        }

        var cleanPath = CleanupFilePath(this.scriptFile.m_path);
        m_shortFileName = Path.GetFileName(this.scriptFile.m_path);
        m_fileFullPath = this.scriptFile.m_path;

#if !UNITY_EDITOR || AIRSHIP_PLAYER
        var runtimeCompiledScriptFile = AssetBridge.GetBinaryFileFromLuaPath<AirshipScript>(this.scriptFile.m_path.ToLower());
        if (runtimeCompiledScriptFile) {
            this.scriptFile = runtimeCompiledScriptFile;
        } else {
            Debug.LogError($"Failed to find code.zip compiled script. Path: {this.scriptFile.m_path.ToLower()}, GameObject: {this.gameObject.name}", this.gameObject);
            return false;
        }
#endif

        LuauCore.CoreInstance.CheckSetup();

        IntPtr filenameStr = Marshal.StringToCoTaskMemUTF8(cleanPath); //Ok

        //trickery, grab the id before we know the thread
        int id = ThreadDataManager.GetOrCreateObjectId(gameObject);

        // We only want one instance of airship components, so let's see if it already exists
        // in our require cache first.
        if (_isAirshipComponent) {
            var path = LuauCore.GetRequirePath(this, cleanPath);
            var thread = LuauPlugin.LuauCreateThreadWithCachedModule(context, path, id);
            
            // If thread exists, we've found the module and put it onto the top of the thread stack. Use
            // this as our component startup thread:
            if (thread != IntPtr.Zero) {
                m_thread = thread;
                InitializeAndAwakeAirshipComponent(m_thread, false);
                return true;
            }
        }

        var gch = GCHandle.Alloc(this.scriptFile.m_bytes, GCHandleType.Pinned); //Ok

        m_thread = LuauPlugin.LuauCreateThread(context, gch.AddrOfPinnedObject(), this.scriptFile.m_bytes.Length, filenameStr, cleanPath.Length, id, true);

        Marshal.FreeCoTaskMem(filenameStr);
        gch.Free();

        if (m_thread == IntPtr.Zero) {
            Debug.LogError("Script failed to compile" + m_shortFileName);
            m_canResume = false;
            m_error = true;

            return false;
        } else {
            LuauState.FromContext(context).AddThread(m_thread, this); //@@//@@ hmm is this even used anymore?
            m_canResume = true;
        }
        


        if (m_canResume) {
            var retValue = LuauCore.CoreInstance.ResumeScript(context, this);
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
                        InitializeAndAwakeAirshipComponent(m_thread, true);
                    }
                }
            }

        }
        return true;
    }

    public void Update() {
        if (m_error) {
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
            int retValue = LuauCore.CoreInstance.ResumeScript(context, this);
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
    }

 
    public void QueueCoroutineResume(IntPtr thread) {
        m_pendingCoroutineResumes.Add(thread);
    }

    private void OnEnable() {
        // OnDisable stopped the luau-core-ready coroutine, so restart the await if needed:
        if (_airshipRewaitForLuauCoreReady) {
            _airshipRewaitForLuauCoreReady = false;
            _airshipScheduledToStart = false;
            InitWhenCoreReady();
        }
        
        if (_isAirshipComponent && !_airshipScheduledToStart && !_airshipComponentEnabled && IsReadyToStart()) {
            LuauPlugin.SetComponentEnabled(context, m_thread, AirshipBehaviourRootV2.GetId(gameObject), _scriptBindingId, true);
            InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipEnabled);
            _airshipComponentEnabled = true;
            if (_airshipReadyToStart && !_airshipStarted) {
                StartAirshipComponentImmediately();
            }
        }
    }

    private void OnDisable() {
        if (_isAirshipComponent && !_airshipScheduledToStart && _airshipComponentEnabled && IsReadyToStart()) {
            
            // Ensure the thread hasn't been destroyed
            if (m_thread != IntPtr.Zero) {
                LuauPlugin.SetComponentEnabled(context, m_thread, AirshipBehaviourRootV2.GetId(gameObject), _scriptBindingId, false);
            }
            
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
        if (ctx == context) {
            m_thread = IntPtr.Zero;
        }
    }

    private void DisconnectUnityEvents() {
        LuauCore.onResetInstance -= OnLuauReset;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }
    
    private bool _isDestroyed;
    private void OnDestroy() {
        _isDestroyed = true;
        DisconnectUnityEvents();
        
        if (m_thread != IntPtr.Zero) {
            if (LuauCore.IsReady) {
                if (_isAirshipComponent && AirshipBehaviourRootV2.HasId(gameObject)) {
                    var unityInstanceId = AirshipBehaviourRootV2.GetId(gameObject);
                    if (_airshipComponentEnabled) {
                        LuauPlugin.SetComponentEnabled(context, m_thread, AirshipBehaviourRootV2.GetId(gameObject), _scriptBindingId, false);
                        InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipDisabled);
                        _airshipComponentEnabled = false;
                    }

                    InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipDestroy);
                    LuauPlugin.LuauRemoveAirshipComponent(context, m_thread, unityInstanceId, _scriptBindingId);
                }
                LuauState.FromContext(context).RemoveThread(m_thread);
                LuauPlugin.LuauSetThreadDestroyed(m_thread);
                LuauPlugin.LuauDestroyThread(m_thread);
                LuauPlugin.LuauUnpinThread(m_thread);
            }

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

        LuauPlugin.LuauUpdateIndividualAirshipComponent(context, m_thread, AirshipBehaviourRootV2.GetId(gameObject), _scriptBindingId, updateType, 0, true);
    }

    private void InvokeAirshipCollision(AirshipComponentUpdateType updateType, object collision) {
        if (!HasAirshipMethod(updateType)) {
            return;
        }
        
        var collisionObjId = ThreadDataManager.AddObjectReference(m_thread, collision);
        LuauPlugin.LuauUpdateCollisionAirshipComponent(context, m_thread, AirshipBehaviourRootV2.GetId(gameObject), _scriptBindingId, updateType, collisionObjId);
    }

    private IEnumerator SetEnabledAtEndOfFrame(bool nextEnabled) {
        yield return new WaitForEndOfFrame();
        base.enabled = nextEnabled;
    }

    /// <summary>
    /// The enabled state of this AirshipComponent
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public bool enabled {
        get => base.enabled;
        // because of Luau, we need to defer it until end of frame
        set {
            if (Application.isPlaying) {
                StartCoroutine(SetEnabledAtEndOfFrame(value));
            }
            else {
                base.enabled = value;
            }
        }
    }

    public void SetScript(AirshipScript script, bool attemptStartup = false) {
        scriptFile = script;
        
        if (!string.IsNullOrEmpty(script.m_path))
            m_fileFullPath = script.m_path;

        if (Application.isPlaying && attemptStartup) {
            InitEarly();
            Start();
        }
    }   

    public void SetScriptFromPath(string path, LuauContext context, bool attemptStartup = false) {
        var script = LoadBinaryFileFromPath(path);
        if (script != null) {
            this.context = context;
            SetScript(script, attemptStartup);
        } else {
            Debug.LogError($"Failed to load script: {path}", this.gameObject);
        }
    }
}
