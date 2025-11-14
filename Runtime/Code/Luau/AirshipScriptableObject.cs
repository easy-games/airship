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
    private static readonly List<GCHandle> InitGcHandles = new();
    private static readonly List<IntPtr> InitStringPtrs = new();
    
    private IntPtr thread;
    private LuauContext context = LuauContext.Game;
    
#if UNITY_EDITOR
    internal static event ReconcileAirshipScriptableObject Reconcile;
#endif
    
    public AirshipScript script;
#if !AIRSHIP_INTERNAL
    [HideInInspector]
#endif
    public LuauMetadata metadata;
    public bool initialized { get; private set; }
    public int instanceId { get; private set; } = -1;

    public static bool IsInstance(object obj) {
        return obj is AirshipScriptableObject; //
    }
    
    public static AirshipScriptableObject CreateInstance(string luaRequirePath) {
        if (luaRequirePath == null) return null;
        AirshipScript runtimeScript;

#if !UNITY_EDITOR || AIRSHIP_PLAYER
        scriptPath = scriptPath + ".lua";
        runtimeScript = LuauScript.AssetBridge.GetBinaryFileFromLuaPath<AirshipScript>(scriptPath.ToLower());
#else
        runtimeScript = AssetDatabase.LoadAssetAtPath<AirshipScript>("Assets/" + luaRequirePath + ".ts");
#endif
        if (runtimeScript == null) {
            throw new ArgumentException($"{luaRequirePath} is not a valid script path", nameof(luaRequirePath));
        }
        
        var asset = ScriptableObject.CreateInstance<AirshipScriptableObject>();
        asset.script = runtimeScript;
        asset.metadata = runtimeScript.m_metadata;
        if (!asset.initialized) asset.Init();
        return asset;
    }
    
    public void OnBeforeSerialize() {
        
    }

    public void OnAfterDeserialize() {
        if (script == null) {
            metadata = default;
        } else if (script.m_metadata != null) {
            this.ReconcileMetadata(ReconcileSource.Deserialization);
        }
    }
    
    public void Init() {
        if (!Application.isPlaying) return;
        if (initialized) return;
        
#if !UNITY_EDITOR || AIRSHIP_PLAYER
		// Grab the script from code.zip at runtime
		var runtimeScript = LuauScript.AssetBridge.GetBinaryFileFromLuaPath<AirshipScript>(LuaFilePath.ToLower());
		if (runtimeScript) {
			script = runtimeScript;
		}
		else {
			var isPackage = scriptPath.StartsWith("Assets/AirshipPackage");
			if (script == null) {
				var suggestion = isPackage ? "have you published this package?" : "have you done a full publish of this game?";
				Debug.LogError($"Could not find compiled script from asset bundle '{scriptPath}' for GameObject {gameObject.name} (Missing Script Asset) - {suggestion}", gameObject);
			}
			else {
				Debug.LogError($"Could not find compiled script in code archive '{script.m_path.ToLower()}' for GameObject {gameObject.name} (Missing Runtime Script Code)", gameObject);
			}
			return;
		}
#endif
        CreateScriptableObject();
    }

    internal void Unload() {
        this.initialized = false;
        this.instanceId = -1;
        this.thread = IntPtr.Zero;
        this.context = LuauContext.Game;
    }

    private void OnEnable() {
        if (!Application.isPlaying) initialized = false;
        if (!initialized) CreateScriptableObject();
        if (Application.isPlaying) InvokeAirshipLifecycle(AirshipScriptableObjectUpdateType.AirshipEnabled);
    }
    
    private void OnDisable() {
        if (!initialized) return;
        if (Application.isPlaying) InvokeAirshipLifecycle(AirshipScriptableObjectUpdateType.AirshipDisabled);
    }
    
    private void OnDestroy() {
        if (!Application.isPlaying || script == null) return;
    
        if (Application.isPlaying) InvokeAirshipLifecycle(AirshipScriptableObjectUpdateType.AirshipDestroy);
        int id = AirshipScriptableObjectRoot.GetIdFromScriptableObject(this);
        LuauPlugin.RemoveScriptableObject(context, thread, id);
        AirshipScriptableObjectRoot.CleanIdOnDestroy(this);
    }
    
    private void OnLuauReset(LuauContext ctx) {
        if (ctx == context) {
            thread = IntPtr.Zero;
            LuauCore.onResetInstance -= OnLuauReset;
        }
    }

    private void CreateScriptableObject() {
        if (!Application.isPlaying) return;
        if (script == null) return;
        
        thread = LuauScript.LoadAndExecuteScript(this, context, LuauScriptCacheMode.Cached, script, out var status);
        if (status != 0) {
            thread = IntPtr.Zero;
            if (status == 1) {
                Debug.LogError($"AirshipComponent constructor cannot yield: {script.m_path}");
            } else {
                Debug.LogError($"Component failed to load: {script.m_path}");
            }
            return;
        }

        
        int id = AirshipScriptableObjectRoot.GetIdFromScriptableObject(this);

        LuauCore.onResetInstance += OnLuauReset;
        LuauPlugin.CreateScriptableObject(context, thread, id);
        AwakeScriptableObject();
        initialized = true;
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

        foreach (var dependency in GetDependencies()) {
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
            Debug.Log($"Started AirshipScriptableObject with id {id} ('{name}') at assetPath {script.assetPath}");
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
    }

    internal void ReconcileMetadata(ReconcileSource reconcileSource, [CanBeNull] LuauMetadata sourceMetadata = null) {
#if AIRSHIP_PLAYER
        return;
#endif
        
        var targetMetadata = script != null ? script.m_metadata : null;
        if (script == null || targetMetadata == null || targetMetadata.name == "") {
            return;
        }

        metadata.name = targetMetadata.name;
        
        var eventData = new AirshipScriptableObjectReconcileEventData(this, reconcileSource);
        Reconcile?.Invoke(eventData);
    }
}
