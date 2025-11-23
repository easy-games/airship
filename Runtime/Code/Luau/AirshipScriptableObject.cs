using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Assets.Code.Luau;
using JetBrains.Annotations;
using Luau;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

internal class AirshipScriptableObjectReconcileEventData {
    public AirshipScriptableObject ScriptableObject { get; }
    public bool ShouldReconcile { get; set; } = true;
    public bool UseLegacyReconcile { get; set; } = true;
    public ReconcileSource ReconcileSource { get; }

    public AirshipScriptableObjectReconcileEventData(AirshipScriptableObject scriptableObject, ReconcileSource source) {
        ScriptableObject = scriptableObject;
        ReconcileSource = source;
    }
}
internal delegate void ReconcileAirshipScriptableObject(AirshipScriptableObjectReconcileEventData data);

[LuauAPI(LuauContext.Protected, ContextOverrideMask = (int) LuauContext.Game, ContextOverrideList = new []{ "CreateInstance", "IsInstance" })]
public class AirshipScriptableObject : ScriptableObject, ISerializationCallbackReceiver, IAirshipRuntimeReferenceDependency {
    private class CreateInstanceData {
        public string requirePath { get; set; }
        public AirshipScript runtimeScript { get; set; }
        public LuauMetadata metadata { get; set; }
    }

    private CreateInstanceData _createInstanceData;
    
    private static readonly List<GCHandle> InitGcHandles = new();
    private static readonly List<IntPtr> InitStringPtrs = new();
    
    private IntPtr thread;
    private LuauContext context = LuauContext.Game;
    
#if UNITY_EDITOR
    internal static event ReconcileAirshipScriptableObject Reconcile;
#endif
    
    [SerializeField] protected string _scriptPath;
    protected string luaFilePath => _scriptPath.Replace(".ts", ".lua", StringComparison.OrdinalIgnoreCase);
    [FormerlySerializedAs("script")] [SerializeField]
    private AirshipScript _script;

    public AirshipScript script {
        get => _script;
        set {
            _script = value;
            _scriptPath = _script.m_path;
            context = LuauContext.Game;
        }
    }
    
#if !AIRSHIP_INTERNAL
    [HideInInspector]
#endif
    public LuauMetadata metadata;
    public bool initialized => AirshipScriptableObjectRoot.ContainsScriptableObject(this) && instanceId != 0;
    public int instanceId { get; private set; }
    
    public static bool IsInstance(object obj) {
        return obj is AirshipScriptableObject;
    }
    
    /// <summary>
    /// Creates an instance of the given AirshipScriptableObject at the provided script path
    /// </summary>
    /// <param name="scriptPath">The script path, e.g. Assets/ScriptableObjects/MyScriptableObject.ts</param>
    /// <returns>An AirshipScriptableObject of the given script type</returns>
    /// <exception cref="ArgumentException">If the script path is not pointing to a valid scriptable object</exception>
    public new static AirshipScriptableObject CreateInstance(string scriptPath) {
        if (scriptPath == null) return null;

#if !UNITY_EDITOR || AIRSHIP_PLAYER
        if (scriptPath.EndsWith(".ts")) scriptPath = Path.ChangeExtension(scriptPath, null);
        
        if (!scriptPath.StartsWith("Assets")) {
            scriptPath = "Assets/" + scriptPath;
        }
        
        if (!Path.HasExtension(scriptPath)) scriptPath += ".lua";
        var runtimeScript = LuauScript.AssetBridge.GetBinaryFileFromLuaPath<AirshipScript>(scriptPath.ToLower());
        
        if (runtimeScript == null) {
            throw new ArgumentException($"{scriptPath} is not a valid script path", nameof(scriptPath));
        }
#else
        if (!scriptPath.StartsWith("Assets/")) scriptPath = "Assets/" + scriptPath;
        if (Path.GetExtension(scriptPath) != ".ts") scriptPath += ".ts";
        
        var runtimeScript = AssetDatabase.LoadAssetAtPath<AirshipScript>(scriptPath);
        if (runtimeScript == null || runtimeScript.scriptType != AirshipScriptType.ScriptableObject) {
            throw new ArgumentException("AirshipScriptableObject path provided is not a scriptable object class or file does not exist", nameof(scriptPath));
        }
#endif

        
        var asset = ScriptableObject.CreateInstance<AirshipScriptableObject>();
#if !UNITY_EDITOR || AIRSHIP_PLAYER
        asset._createInstanceData = new CreateInstanceData() {
            requirePath = scriptPath,
            metadata = new LuauMetadata(),
            runtimeScript = runtimeScript,
        };
#else
        asset.script = runtimeScript;
        asset.metadata = new LuauMetadata();  
#endif

        if (!asset.initialized) asset.Init();
        return asset;
    }
    
    public void OnBeforeSerialize() {}

    public void OnAfterDeserialize() {
        if (_script == null) {
            metadata = default;
        } else if (_script.m_metadata != null) {
            this.ReconcileMetadata(ReconcileSource.Deserialization);
        }
    }
    
    public void Init() {
        if (script == null && _createInstanceData == null) return; // no point if no script or init data
        if (!Application.isPlaying) return;
        if (initialized) return;
        
#if !UNITY_EDITOR || AIRSHIP_PLAYER
        if (_createInstanceData != null) {
            script = _createInstanceData.runtimeScript;
            metadata = _createInstanceData.metadata;
        } else {
            // Grab the script from code.zip at runtime
            var runtimeScript = LuauScript.AssetBridge.GetBinaryFileFromLuaPath<AirshipScript>(luaFilePath.ToLower());
            if (runtimeScript) {
                script = runtimeScript;
            }
            else {
                var isPackage = _scriptPath.StartsWith("Assets/AirshipPackage");
                if (_script == null) {
                    var suggestion = isPackage ? "have you published this package?" : "have you done a full publish of this game?";
                    Debug.LogError($"Could not find compiled script from asset bundle '{_scriptPath}' for ScriptableObject {name} (Missing Script Asset) - {suggestion}", this);
                }
                else {
                    Debug.LogError($"Could not find compiled script in code archive '{_script.m_path.ToLower()}' for ScriptableObject {name} (Missing Runtime Script Code)", this);
                }
                return;
            }
        }
#endif
        CreateScriptableObject();
    }

    internal void Unload() {
        instanceId = 0;
        thread = IntPtr.Zero;
        context = LuauContext.Game;
    }

    private void OnEnable() {
        if (!initialized) CreateScriptableObject();
        if (Application.isPlaying) InvokeAirshipLifecycle(AirshipScriptableObjectUpdateType.AirshipEnabled);
    }
    
    private void OnDisable() {
        if (!initialized) return;
        if (Application.isPlaying) InvokeAirshipLifecycle(AirshipScriptableObjectUpdateType.AirshipDisabled);
    }
    
    private void OnDestroy() {
        if (!initialized) return;
        Destroy();
    }

    internal void Destroy() {
        LuauCore.onResetInstance -= OnLuauReset;
        if (thread == IntPtr.Zero) return;
    
        InvokeAirshipLifecycle(AirshipScriptableObjectUpdateType.AirshipDestroy);
        var id = AirshipScriptableObjectRoot.GetIdFromScriptableObject(this);
        
        LuauPlugin.RemoveScriptableObject(context, thread, id);
        AirshipScriptableObjectRoot.CleanIdOnDestroy(this);
        
        if (LuauState.IsContextActive(context)) {
            LuauPlugin.UnpinThread(thread);
            LuauPlugin.DestroyThread(thread);
        }
        
        Unload();
    }
    
    private void OnLuauReset(LuauContext ctx) {
        if (ctx == context) {
            thread = IntPtr.Zero;
            LuauCore.onResetInstance -= OnLuauReset;
        }
    }

    private void CreateScriptableObject() {
        if (!Application.isPlaying) return;
        if (_script == null) return;
        
        thread = LuauScript.LoadAndExecuteScript(this, context, LuauScriptCacheMode.Cached, _script, out var status);
        if (status != 0) {
            thread = IntPtr.Zero;
            if (status == 1) {
                Debug.LogError($"AirshipComponent constructor cannot yield: {_script.m_path}");
            } else {
                Debug.LogError($"Component failed to load: {_script.m_path}");
            }
            return;
        }
        
        int id = AirshipScriptableObjectRoot.GetIdFromScriptableObject(this);
        LuauCore.onResetInstance += OnLuauReset;
        LuauPlugin.CreateScriptableObject(context, thread, id);
        AwakeScriptableObject();
        instanceId = id;
    }

    private void InvokeAirshipLifecycle(AirshipScriptableObjectUpdateType updateType) {
        if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
        LuauPlugin.UpdateIndividualScriptableObject(context, thread, AirshipScriptableObjectRoot.GetIdFromScriptableObject(this), updateType);
    }
    
    private IReadOnlyList<IAirshipRuntimeReferenceDependency> GetDependencies() {
        // right now we can only initialize scriptable objects with scriptable objects, anyway.
        // in future we might have some "loose references" for referencing prefab components - right now, no.
        return metadata.GetRuntimePropertyDependencies(PropertyDependencyFilterFlags.AirshipScriptableObject);
    }
    
    private void AwakeScriptableObject() {
        int id = AirshipScriptableObjectRoot.GetIdFromScriptableObject(this);

        Debug.Log($"[SO] Initializing Scriptable Object {id}");
        foreach (var dependency in GetDependencies()) {
            if (dependency is AirshipScriptableObject aso) {
                Debug.Log($"[SO] Dependency {id} depends on {aso.instanceId}");
            }
            
            dependency.Init();
        }
        
        if (metadata != null) {
            var properties = metadata.properties;
            var propertiesCopied = false;
            
            // Ensure allowed objects
            for (var i = metadata.properties.Count - 1; i >= 0; i--) {
                var property = metadata.properties[i];
                
                switch (property.type) {
                    case "object": {
                        if (!ReflectionList.IsAllowedFromString(property.objectType, context)) {
                            Debug.LogError($"[Airship] Skipping AirshipBehaviour property \"{property.name}\": Type \"{property.objectType}\" is not allowed");
                            if (!propertiesCopied) {
                                // As an optimization, we use the original metadata.properties list until we need to modify it at all, such as here:
                                propertiesCopied = true;
                                properties = new List<LuauMetadataProperty>(metadata.properties);
                            }
                            properties.RemoveAt(i);
                        }

                        break;
                    }
                }
            }

            var propertyDtos = properties.Count <= 1024 ?
                stackalloc LuauMetadataPropertyMarshalDto[properties.Count] : 
                new LuauMetadataPropertyMarshalDto[properties.Count];
		    
            for (var i = 0; i < properties.Count; i++) {
                var property = properties[i];
                property.AsStructDto(thread, InitGcHandles, InitStringPtrs, out var dto);
                propertyDtos[i] = dto;
            }
            
            LuauPlugin.InitializeScriptableObject(context, thread, id, propertyDtos);
            
            // Free handles:
            foreach (var handle in InitGcHandles) {
                handle.Free();
            }
            foreach (var strPtr in InitStringPtrs) {
                Marshal.FreeCoTaskMem(strPtr);
            }
            InitGcHandles.Clear();
            InitStringPtrs.Clear();
            
            InvokeAirshipLifecycle(AirshipScriptableObjectUpdateType.AirshipAwake);
#if AIRSHIP_INTERNAL
            Debug.Log($"Started AirshipScriptableObject with id {id} at assetPath {script.assetPath}");
#endif
        }
    }
    
    private void Awake() {
        Init();
    }

    private void Reset() {
        // TODO: Reset values to defaults
    }

    private void OnValidate() {
        if (Application.isPlaying) return;
        this.ReconcileMetadata(ReconcileSource.ComponentValidate);

        if (_scriptPath == null && _script != null) {
            _scriptPath = _script.m_path;
        }
    }

    internal void ReconcileMetadata(ReconcileSource reconcileSource, [CanBeNull] LuauMetadata sourceMetadata = null) {
#if AIRSHIP_PLAYER
        return;
#endif
        
        var targetMetadata = _script != null ? _script.m_metadata : null;
        if (_script == null || targetMetadata == null || targetMetadata.name == "") {
            return;
        }

        metadata.name = targetMetadata.name;
        
#if UNITY_EDITOR
        var eventData = new AirshipScriptableObjectReconcileEventData(this, reconcileSource);
        Reconcile?.Invoke(eventData);
#endif
    }
}
