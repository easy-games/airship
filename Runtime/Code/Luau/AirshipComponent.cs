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
public class AirshipComponent : AirshipRuntimeScript {
    #if UNITY_EDITOR
    private bool hasDelayedValidateCall = false;
    #endif
    
    public static Dictionary<int, string> componentIdToScriptName = new();
    private static int _scriptBindingIdGen;

    public string TypescriptFilePath => m_fileFullPath.Replace(".lua", ".ts");
    public bool IsStarted => started;



    internal bool HasComponentReference { get; private set; }

    private byte[] m_fileContents;

    
    [HideInInspector]
    public LuauMetadata m_metadata = new();
    private readonly int _scriptBindingId = _scriptBindingIdGen++;




    private bool _airshipComponentEnabled = false;
    private bool _airshipReadyToStart = false;

    private bool _airshipStarted = false;

    private bool _airshipRewaitForLuauCoreReady = false;
    private Dictionary<AirshipComponentUpdateType, bool> _hasAirshipUpdateMethods = new(); 
    
    public bool IsAirshipComponent => _isAirshipComponent;
    public bool IsAirshipComponentEnabled => _airshipComponentEnabled;

    public static bool validatedSceneInGameConfig = false;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnLoad() {
        validatedSceneInGameConfig = false;
    }


    
    public bool IsBindableAsComponent(AirshipScript file) {
        if (file.assetPath == scriptFile.assetPath) {
            return true;
        }

        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnReload() {
        _scriptBindingIdGen = 0;
        componentIdToScriptName.Clear();
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
        
        _isAirshipComponent = true;
        
        // // Need to recompile
        // if (scriptFile.HasFileChanged) {
        //     return;
        // }
        
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
        LuauPlugin.LuauSetAirshipComponentEnabled(context, thread, AirshipBehaviourRootV2.GetId(gameObject), _scriptBindingId, enabled);
        
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

    private void Awake() {
        if (scriptFile != null) {
            componentIdToScriptName[_scriptBindingId] = scriptFile.name;
        }
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
        base.Awake();
    }

    protected override void OnScriptStart() {
        if (!_isAirshipComponent) return;
        if (_airshipReadyToStart && !_airshipScheduledToStart && !_airshipStarted) {
            StartAirshipComponentImmediately();
        }
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

    protected override IntPtr GetCachedThread(string path, int gameObjectId) {
        var requirePath = LuauCore.GetRequirePath(this, path);
        var thread = LuauPlugin.LuauCreateThreadWithCachedModule(context, requirePath, gameObjectId);
            
        // If thread exists, we've found the module and put it onto the top of the thread stack. Use
        // this as our component startup thread:
        if (thread != IntPtr.Zero) {
            m_thread = thread;
            InitializeAndAwakeAirshipComponent(m_thread, false);
            return thread;
        }

        return IntPtr.Zero;
    }

    protected override void OnThreadCreated(IntPtr thread, string path) {
        // Cache the module when the thread is created, so we can reuse the module for each component
        var requirePath = LuauCore.GetRequirePath(this, path);
        LuauPlugin.LuauCacheModuleOnThread(thread, requirePath);
        InitializeAndAwakeAirshipComponent(thread, true);
    }

    private void OnEnable() {
        // OnDisable stopped the luau-core-ready coroutine, so restart the await if needed:
        if (_airshipRewaitForLuauCoreReady) {
            _airshipRewaitForLuauCoreReady = false;
            _airshipScheduledToStart = false;
            InitWhenCoreReady();
        }
        
        if (_isAirshipComponent && !_airshipScheduledToStart && !_airshipComponentEnabled && IsReadyToStart()) {
            LuauPlugin.LuauSetAirshipComponentEnabled(context, m_thread, AirshipBehaviourRootV2.GetId(gameObject), _scriptBindingId, true);
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
                LuauPlugin.LuauSetAirshipComponentEnabled(context, m_thread, AirshipBehaviourRootV2.GetId(gameObject), _scriptBindingId, false);
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
    private void OnDestroy() {
        _isDestroyed = true;
        DisconnectUnityEvents();
        
        if (m_thread != IntPtr.Zero) {
            if (LuauCore.IsReady) {
                if (_isAirshipComponent && AirshipBehaviourRootV2.HasId(gameObject)) {
                    var unityInstanceId = AirshipBehaviourRootV2.GetId(gameObject);
                    if (_airshipComponentEnabled) {
                        LuauPlugin.LuauSetAirshipComponentEnabled(context, m_thread, AirshipBehaviourRootV2.GetId(gameObject), _scriptBindingId, false);
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
                AirshipBehaviourRootV2.CleanIdOnDestroy(gameObject, this);
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

    protected override void OnSetScriptStartup() {
        InitEarly();
        Start();
    }
}
