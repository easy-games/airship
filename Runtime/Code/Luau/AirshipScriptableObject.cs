using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
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

#if AIRSHIP_INTERNAL
[CreateAssetMenu(menuName = "Airship/Scriptable Object", fileName = "AirshipScriptableObject", order = 0)]
#endif
public class AirshipScriptableObject : ScriptableObject, ISerializationCallbackReceiver, IComponentInitializableDependency {
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
    public int scriptableObjectId { get; private set; } = -1;
    
    public void OnBeforeSerialize() {
        
    }

    public void OnAfterDeserialize() {
        Debug.Log("OnAfterDeserialize");
        
        if (script == null) {
            metadata = default;
        } else if (script.m_metadata != null) {
            this.ReconcileMetadata(ReconcileSource.Deserialization);
        }
    }

    public void Init() {
        if (!initialized) CreateScriptableObject();
    }

    internal void Unload() {
        this.initialized = false;
        this.scriptableObjectId = -1;
        this.thread = IntPtr.Zero;
        this.context = LuauContext.Game;
    }

    private void OnEnable() {
        if (!Application.isPlaying) initialized = false;
        if (!initialized) CreateScriptableObject();
    }

    private void OnDisable() {
        if (!Application.isPlaying) {
            initialized = false;
        }
        
        Debug.Log("ScriptableObject disable");
        // TODO: Method call?
    }

    private void OnDestroy() {
        if (!Application.isPlaying || script == null) return;

        int id = AirshipScriptableObjectRoot.GetIdFromScriptableObject(this);
        LuauPlugin.RemoveScriptableObject(context, thread, id);
        AirshipScriptableObjectRoot.CleanIdOnDestroy(this);
    }

    private void CreateScriptableObject() {
        if (script == null) return;
        Debug.Log($"** create scriptable object {script.scriptType}; {script.assetPath} - {script.m_metadata} {metadata}");
        
        Debug.Log("Create Scriptable Object Thread");
        
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
        Debug.Log($"** Creating scriptable object {name} with id {id}");
        
        LuauPlugin.CreateScriptableObject(context, thread, id);
        InitializeScriptableObject();
        initialized = true;
        scriptableObjectId = id;
    }

    private void InitializeScriptableObject() {
        int id = AirshipScriptableObjectRoot.GetIdFromScriptableObject(this);
        
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
        } else {
            Debug.LogWarning($"** Metadata is missing for {name} at id {id}");
        }
        
        Debug.Log($"** ScriptableObject {name} initialized at id {id}");
    }
    
    private void Awake() {
        if (!Application.isPlaying || script == null) return;
        CreateScriptableObject();
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
