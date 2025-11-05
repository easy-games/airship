using System;
using System.Collections.Generic;
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
public class AirshipScriptableObject : ScriptableObject, ISerializationCallbackReceiver {
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

    public void OnBeforeSerialize() {
        
    }

    public void OnAfterDeserialize() {
        if (script == null || script.scriptType != AirshipScriptType.ScriptableObject) {
            metadata = default;
        } else if (script.m_metadata != null) {
            this.ReconcileMetadata(ReconcileSource.ForceReconcile);
        }
    }

    private void OnEnable() {

    }

    private void OnDisable() {
        
    }

    private void OnDestroy() {
        if (!Application.isPlaying || script == null) return;
        Debug.Log("Scriptable Object Destroy");
        LuauPlugin.RemoveScriptableObject(context, thread, GetInstanceID());
    }

    private void Awake() {
        if (!Application.isPlaying || script == null) return;
        Debug.Log("Scriptable Object Awake");
        
        thread = LuauScript.LoadAndExecuteScript(null, LuauContext.Game, LuauScriptCacheMode.Cached, script,
            out var status);
        
        if (status != 0) {
            thread = IntPtr.Zero;
            if (status == 1) {
                Debug.LogError($"AirshipScriptableObject constructor cannot yield: {script.m_path}");
            } else {
                Debug.LogError($"Scriptable Object failed to load: {script.m_path}");
            }
            return;
        }
        
        LuauPlugin.CreateScriptableObject(context, thread, GetInstanceID());
    }

    private void Reset() {
        
    }

    private void OnValidate() {
        this.ReconcileMetadata(ReconcileSource.ComponentValidate);
    }

    internal void ReconcileMetadata(ReconcileSource reconcileSource, [CanBeNull] LuauMetadata sourceMetadata = null) {
#if AIRSHIP_PLAYER
        return;
#endif
        
        var targetMetadata = script.m_metadata;
        if (script == null || targetMetadata == null || targetMetadata.name == "") {
            return;
        }

        metadata.name = targetMetadata.name;
        
        var eventData = new AirshipScriptableObjectReconcileEventData(this, reconcileSource);
        Reconcile?.Invoke(eventData);
    }
}
