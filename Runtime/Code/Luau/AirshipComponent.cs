using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Assets.Code.Luau;
using Code.Luau;
using JetBrains.Annotations;
using Luau;
using UnityEngine;
using UnityEngine.Serialization;
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

internal enum MetadataChangeState {
	ComponentIsMismatched = 1,
	Ok = 0,
	ScriptIsMismatched = 1,
}

internal enum ReconcileSource {
	/// <summary>
	/// When the component is calling 'OnValidate'
	/// </summary>
	ComponentValidate,
	/// <summary>
	/// When the component properties are changed in the inspector
	/// </summary>
	Inspector,
	/// <summary>
	/// When the compiler is in the post-compile import state
	/// </summary>
	PostCompile,
	ForceReconcile,
}

internal class AirshipReconcileEventData {
	public AirshipComponent Component { get; }
	public bool ShouldReconcile { get; set; } = true;
	public bool UseLegacyReconcile { get; set; } = true;
	public ReconcileSource ReconcileSource { get; }

	public AirshipReconcileEventData(AirshipComponent component, ReconcileSource source) {
		Component = component;
		ReconcileSource = source;
	}
}
internal delegate void ReconcileAirshipComponent(AirshipReconcileEventData data);

[AddComponentMenu("Airship/Airship Component")]
[HelpURL("https://docs.airship.gg/typescript/airshipbehaviour")]
[LuauAPI(LuauContext.Protected)]
public class AirshipComponent : MonoBehaviour, ITriggerReceiver {
	internal static bool UsePostCompileReconciliation { get; set; } = true;
	private const bool ElevateToProtectedWithinCoreScene = true;
	
	private static readonly List<GCHandle> InitGcHandles = new();
	private static readonly List<IntPtr> InitStringPtrs = new();
	
	public static LuauScript.AwakeData QueuedAwakeData = null;
	public static readonly Dictionary<int, string> ComponentIdToScriptName = new();
	
	private static int _airshipComponentIdGen = 10000000;
	private static bool _validatedSceneInGameConfig = false;

	private bool _init = false;

#if UNITY_EDITOR
	internal static event ReconcileAirshipComponent Reconcile;
	[SerializeField] internal string guid;
	// [SerializeField] private string hash;

	[Obsolete]
	internal string componentHash {
		get {
			return "";
		}
		set {
			_ = value;
		}
	}

	internal string scriptHash => script.sourceFileHash;
#endif
	public AirshipScript script;

	[FormerlySerializedAs("m_fileFullPath")] [HideInInspector] public string scriptPath;

	public IntPtr thread;
	[NonSerialized] public LuauContext context = LuauContext.Game;
	[HideInInspector] public bool forceContext = false;
#if !AIRSHIP_DEBUG
	[HideInInspector]
#endif
	[FormerlySerializedAs("m_metadata")]  public LuauMetadata metadata = new();
	
	private readonly int _airshipComponentId = _airshipComponentIdGen++;
	private readonly Dictionary<AirshipComponentUpdateType, bool> _hasAirshipUpdateMethods = new(); 
	
	public string TypescriptFilePath => script.m_path.Replace(".lua", ".ts", StringComparison.OrdinalIgnoreCase);
	public string LuaFilePath => scriptPath.Replace(".ts", ".lua", StringComparison.OrdinalIgnoreCase);

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void OnReload() {
		_airshipComponentIdGen = 10000000;
		_validatedSceneInGameConfig = false;
		QueuedAwakeData = null;
		ComponentIdToScriptName.Clear();
		InitGcHandles.Clear();
		InitStringPtrs.Clear();
	}
	
	public static AirshipComponent Create(GameObject go, string scriptPath, LuauContext context) {
		var script = LuauScript.LoadAirshipScriptFromPath(scriptPath);
		if (script == null) {
			throw new Exception($"Failed to load script from file: {scriptPath}");
		}

		return Create(go, script, context);
	}

	public static AirshipComponent Create(GameObject go, AirshipScript script, LuauContext context) {
		var awakeData = new LuauScript.AwakeData() {
			Script = script,
			Context = context,
			ForceContext = false,
		};
		QueuedAwakeData = awakeData;
		
		var component = go.AddComponent<AirshipComponent>();

		// If QueuedAwakeData is still set, then the component didn't awake right away.
		// In this case, just set it ourselves. This would happen if the GameObject was
		// in an inactive state.
		if (QueuedAwakeData == awakeData) {
			QueuedAwakeData = null;
			component.script = script;
			component.scriptPath = script.m_path;
			component.context = context;
		}
		
		return component;
	}

	private void Awake() {
		Init();
		
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipAwake);
	}
	
	// Init is separate from Awake because there are instances where an AirshipComponent
	// is fetched using GetComponent BEFORE its Awake method is called. Those areas in
	// the code can call Init() here to ensure everything is set up in time, even if
	// Awake() hasn't been called yet.
	public void Init() {
		if (_init) return;
		_init = true;
		
		if (QueuedAwakeData != null) {
			script = QueuedAwakeData.Script;
			scriptPath = script.m_path;
			context = QueuedAwakeData.Context;
			forceContext = QueuedAwakeData.ForceContext;
			QueuedAwakeData = null;
		}
		
#if !UNITY_EDITOR || AIRSHIP_PLAYER
		// Grab the script from code.zip at runtime
		var runtimeScript = LuauScript.AssetBridge.GetBinaryFileFromLuaPath<AirshipScript>(LuaFilePath.ToLower());
		if (runtimeScript) {
			script = runtimeScript;
		}
		else {
			Debug.LogError($"Failed to find code.zip compiled script. Path: {script.m_path.ToLower()}, GameObject: {gameObject.name}", gameObject);
			return;
		}
#endif

		if (script == null) {
			Debug.LogError($"No script assigned to AirshipComponent ({gameObject.name})", gameObject);
			return;
		}
		
		scriptPath = script.m_path;
		ComponentIdToScriptName[_airshipComponentId] = string.Intern(script.name);
		
		// Invoke startup scripts if they haven't been executed yet
		ScriptingEntryPoint.InvokeOnLuauStartup();
		
#if UNITY_EDITOR && !AIRSHIP_PLAYER
		if (!_validatedSceneInGameConfig) {
			var scene = gameObject.scene;
			if (!LuauCore.IsProtectedScene(scene)) {
				var sceneName = scene.name;
				var gameConfig = GameConfig.Load();
				if (gameConfig.gameScenes.ToList().Find((s) => s.name == sceneName) == null) {
					throw new Exception(
						$"Tried to load AirshipComponent ({name}) on GameObject ({gameObject.name}) in a scene not found in GameConfig.scenes. Please add \"{sceneName}\" to your Assets/GameConfig.asset");
				}
			}
			_validatedSceneInGameConfig = true;
		}
#endif

		// Assume protected context for bindings within CoreScene
		if (!forceContext && gameObject.scene.name is "CoreScene" or "MainMenu" && ElevateToProtectedWithinCoreScene) {
			context = LuauContext.Protected;
		}
		
		// Load the component onto the thread
		thread = LuauScript.LoadAndExecuteScript(gameObject, context, LuauScriptCacheMode.Cached, script, out var status);
		if (status != 0) {
			thread = IntPtr.Zero;
			if (status == 1) {
				Debug.LogError($"AirshipComponent constructor cannot yield: {script.m_path}");
			} else {
				Debug.LogError($"Component failed to load: {script.m_path}");
			}
			return;
		}
		
		LuauCore.onResetInstance += OnLuauReset;

		InitAirshipComponent();
		InitTriggerEvents();
	}

	private unsafe void InitAirshipComponent() {
		InitializeAirshipReference();
		
		// Ensure all AirshipComponent dependencies are ready first
		foreach (var dependency in GetDependencies()) {
			dependency.Init();
		}
		
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
		
		LuauPlugin.LuauInitializeAirshipComponent(context, thread, AirshipBehaviourRootV2.GetId(gameObject), _airshipComponentId, propertyDtos);

		// Free handles:
		foreach (var handle in InitGcHandles) {
			handle.Free();
		}
		foreach (var strPtr in InitStringPtrs) {
			Marshal.FreeCoTaskMem(strPtr);
		}
		InitGcHandles.Clear();
		InitStringPtrs.Clear();
	}

	private void InitTriggerEvents() {
		if (HasAirshipMethod(AirshipComponentUpdateType.AirshipTriggerStay)) {
			var triggerEvent = gameObject.AddComponent<AirshipComponentTriggerEvents>();
			triggerEvent.AttachReceiver(this);
		}
	}

	private void Start() {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipStart);
	}

	private void OnEnable() {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		LuauPlugin.LuauSetAirshipComponentEnabled(context, thread, AirshipBehaviourRootV2.GetId(gameObject), _airshipComponentId, true);
		InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipEnabled);
	}

	private void OnDisable() {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		LuauPlugin.LuauSetAirshipComponentEnabled(context, thread, AirshipBehaviourRootV2.GetId(gameObject), _airshipComponentId, false);
		InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipDisabled);
	}
	
	private void OnDestroy() {
		ComponentIdToScriptName.Remove(_airshipComponentId);
		
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipDestroy);
		LuauPlugin.LuauRemoveAirshipComponent(context, thread, AirshipBehaviourRootV2.GetId(gameObject), _airshipComponentId);
		AirshipBehaviourRootV2.CleanIdOnDestroy(gameObject, this);
		if (LuauState.IsContextActive(context)) {
			LuauPlugin.LuauUnpinThread(thread);
			LuauPlugin.LuauDestroyThread(thread);
		}
		thread = IntPtr.Zero;
		LuauCore.onResetInstance -= OnLuauReset;
	}

	#region Collision Events
	private void OnCollisionEnter(Collision other) {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		InvokeAirshipCollision(AirshipComponentUpdateType.AirshipCollisionEnter, other);
	}

	private void OnCollisionStay(Collision other) {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		InvokeAirshipCollision(AirshipComponentUpdateType.AirshipCollisionStay, other);
	}

	private void OnCollisionExit(Collision other) {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		InvokeAirshipCollision(AirshipComponentUpdateType.AirshipCollisionExit, other);
	}

	private void OnCollisionEnter2D(Collision2D other) {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		InvokeAirshipCollision(AirshipComponentUpdateType.AirshipCollisionEnter2D, other);
	}

	private void OnCollisionStay2D(Collision2D other) {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		InvokeAirshipCollision(AirshipComponentUpdateType.AirshipCollisionStay2D, other);
	}

	private void OnCollisionExit2D(Collision2D other) {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		InvokeAirshipCollision(AirshipComponentUpdateType.AirshipCollisionExit2D, other);
	}
	#endregion

	#region Trigger Events
	private void OnTriggerEnter(Collider other) {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		InvokeAirshipCollision(AirshipComponentUpdateType.AirshipTriggerEnter, other);
	}

	public void OnTriggerStayReceiver(Collider other) {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		InvokeAirshipCollision(AirshipComponentUpdateType.AirshipTriggerStay, other);	
	}
	
	private void OnTriggerExit(Collider other) {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		InvokeAirshipCollision(AirshipComponentUpdateType.AirshipTriggerExit, other);
	}
	
	private void OnTriggerEnter2D(Collider2D other) {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		InvokeAirshipCollision(AirshipComponentUpdateType.AirshipTriggerEnter2D, other);
	}
	
	private void OnTriggerStay2D(Collider2D other) {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		InvokeAirshipCollision(AirshipComponentUpdateType.AirshipTriggerStay2D, other);
	}
	
	private void OnTriggerExit2D(Collider2D other) {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		InvokeAirshipCollision(AirshipComponentUpdateType.AirshipTriggerExit2D, other);
	}
	#endregion

	private void InvokeAirshipLifecycle(AirshipComponentUpdateType updateType) {
		LuauPlugin.LuauUpdateIndividualAirshipComponent(context, thread, AirshipBehaviourRootV2.GetId(gameObject), _airshipComponentId, updateType, 0, true);
	}

	private void InvokeAirshipCollision(AirshipComponentUpdateType updateType, object obj) {
		var argObjId = ThreadDataManager.AddObjectReference(thread, obj);
		LuauPlugin.LuauUpdateCollisionAirshipComponent(context, thread, AirshipBehaviourRootV2.GetId(gameObject), _airshipComponentId, updateType, argObjId);
	}

	private IReadOnlyList<AirshipComponent> GetDependencies() {
		List<AirshipComponent> dependencies = new();
		
		foreach (var property in metadata.properties) {
			if (property.ComponentType == AirshipComponentPropertyType.AirshipComponent) {
				var obj = property.serializedObject;
				if (obj == null) continue;
				dependencies.Add(obj as AirshipComponent);
			} else if (property.ComponentType == AirshipComponentPropertyType.AirshipArray && property.ArrayElementComponentType == AirshipComponentPropertyType.AirshipComponent) {
				if (property.items.objectRefs == null) continue;
				foreach (var arrayItem in property.items.objectRefs) {
					if (arrayItem != null) {
						dependencies.Add(arrayItem as AirshipComponent);
					}
				}
			}
		}

		return dependencies;
	}

	private bool HasAirshipMethod(AirshipComponentUpdateType updateType) {
		if (_hasAirshipUpdateMethods.TryGetValue(updateType, out var has)) {
			return has;
		}
        
		// Fetch from Luau plugin & cache the result:
		var hasMethod = LuauPlugin.LuauHasAirshipMethod(context, thread, AirshipBehaviourRootV2.GetId(gameObject), _airshipComponentId, updateType);
		_hasAirshipUpdateMethods.Add(updateType, hasMethod);
        
		return hasMethod;
	}

	private void OnLuauReset(LuauContext ctx) {
		if (ctx == context) {
			thread = IntPtr.Zero;
		}
	}

	private void InitializeAirshipReference() {
		// Warmup the component first, creating a reference table
		var transformInstanceId = ThreadDataManager.GetOrCreateObjectId(transform);
		AirshipBehaviourRootV2.LinkComponentToGameObject(this, out var unityInstanceId);
        
		LuauPlugin.LuauPrewarmAirshipComponent(context, thread, unityInstanceId, _airshipComponentId, transformInstanceId);
	}

	public string GetAirshipComponentName() {
		return metadata.name;
	}

	public int GetAirshipComponentId() {
		return _airshipComponentId;
	}

#if UNITY_EDITOR
    private readonly Dictionary<string, PropertyValueState> _trackCustomProperties = new();

    private void SetupMetadata() {
        // Clear out script if file path doesn't match script path
        if (script != null) {
            if (script.m_path != scriptPath) {
                script = null;
            }
        }
        // Set script from file path
        if (script == null) {
            if (!string.IsNullOrEmpty(scriptPath)) {
                script = LuauScript.LoadAirshipScriptFromPath(scriptPath);
            }
        
            if (script == null) {
                return;
            }
        }

        ReconcileMetadata(ReconcileSource.ComponentValidate);

        if (Application.isPlaying) {
            WriteChangedComponentProperties();
        }
    }
    
    private void OnValidate() {
	    if (Application.isPlaying) {
		    return;
	    }
	    Validate();
    }

    private void Validate() {
        if (this == null) return;

        if (script != null && string.IsNullOrEmpty(scriptPath)) {
	        scriptPath = script.m_path;
        }

        SetupMetadata();
    }
    
    internal void ReconcileMetadata(ReconcileSource reconcileSource, [CanBeNull] LuauMetadata sourceMetadata = null) {
#if AIRSHIP_PLAYER
        return;
#else
	    var targetMetadata = script.m_metadata;
	    if (script == null || targetMetadata == null || targetMetadata.name == "") {
		    return;
	    }

	    metadata.name = targetMetadata.name;
	    
	    var eventData = new AirshipReconcileEventData(this, reconcileSource);
	    Reconcile?.Invoke(eventData);
#endif
    }

    private void WriteChangedComponentProperties() {
        if (!AirshipBehaviourRootV2.HasId(gameObject) || thread == IntPtr.Zero) return;
        
        foreach (var property in metadata.properties) {
            // If all value data is unchanged skip this write
            if (!ShouldWriteToComponent(property)) continue;

            _trackCustomProperties[property.name] = new PropertyValueState {
                serializedValue = property.serializedValue,
                itemObjectRefs = (UnityEngine.Object[]) property.items.objectRefs.Clone(),
                itemSerializedObjects = (string[]) property.items.serializedItems.Clone()
            };
            property.WriteToComponent(thread, AirshipBehaviourRootV2.GetId(gameObject), _airshipComponentId);
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
}
