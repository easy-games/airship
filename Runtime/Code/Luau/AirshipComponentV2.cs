using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Luau;
using UnityEngine;

public class AirshipComponentV2 : MonoBehaviour {
	public static AwakeData QueuedAwakeData = null;
	private static int _airshipComponentIdGen = 10000000;
	
	public AirshipScript script;

	public IntPtr thread;
	[HideInInspector] public LuauContext context = LuauContext.Game;
	
	[HideInInspector] public LuauMetadata metadata = new();
	
	private readonly int _airshipComponentId = _airshipComponentIdGen++;
	private readonly Dictionary<AirshipComponentUpdateType, bool> _hasAirshipUpdateMethods = new(); 

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void OnReload() {
		_airshipComponentIdGen = 10000000;
	}
	
	private void Awake() {
		if (QueuedAwakeData != null) {
			script = QueuedAwakeData.script;
			context = QueuedAwakeData.context;
			QueuedAwakeData = null;
		}
		
		// Load the component onto the thread:
		thread = LuauScript.LoadAndExecuteScript(gameObject, context, LuauScriptCacheMode.Cached, script, true);
		if (thread == IntPtr.Zero) {
			Debug.LogError($"Component failed to load: {script.m_path}");
			return;
		}
		
		LuauCore.onResetInstance += OnLuauReset;
		
		print("C# Awake");
		AwakeAirshipComponent();
	}

	private void AwakeAirshipComponent() {
		InitializeAirshipReference();
		
		var properties = new List<LuauMetadataProperty>(metadata.properties);
        
		// Ensure allowed objects
		for (var i = metadata.properties.Count - 1; i >= 0; i--) {
			var property = metadata.properties[i];
            
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
		
		LuauPlugin.LuauInitializeAirshipComponent(context, thread, AirshipBehaviourRootV3.GetId(gameObject), _airshipComponentId, propertyDtos);
		InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipAwake);
	}

	private void Start() {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		print("C# Start");
		InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipStart);
	}

	private void OnEnable() {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		print("C# OnEnable");
		LuauPlugin.LuauSetAirshipComponentEnabled(context, thread, AirshipBehaviourRootV3.GetId(gameObject), _airshipComponentId, true);
		InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipEnabled);
	}

	private void OnDisable() {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		// NOTE TO SELF: It seems like maybe the wrong LuauCore is being used at first?
		// At least, if the AirshipComponent's GO starts inactive, then is activated, it works fine!
		// But if it starts right away, subsequent calls to OnEnable/OnDisable/etc. fail because the thread is dead, even though the thread is pinned
		// The only thing I can think of is that LuauCore might not exist.
		// Yeah, removing the duplicate LuauCore seems to fix it...
		
		print("C# OnDisable");
		LuauPlugin.LuauSetAirshipComponentEnabled(context, thread, AirshipBehaviourRootV3.GetId(gameObject), _airshipComponentId, false);
		InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipDisabled);
	}

	private void OnDestroy() {
		if (thread == IntPtr.Zero || !LuauCore.IsReady) return;
		
		print("C# OnDestroy");
		InvokeAirshipLifecycle(AirshipComponentUpdateType.AirshipDestroy);
		LuauPlugin.LuauRemoveAirshipComponent(context, thread, AirshipBehaviourRootV3.GetId(gameObject), _airshipComponentId);
		AirshipBehaviourRootV3.CleanIdOnDestroy(gameObject, this);
		if (LuauState.IsContextActive(context)) {
			LuauPlugin.LuauUnpinThread(thread);
		}
		thread = IntPtr.Zero;
		LuauCore.onResetInstance -= OnLuauReset;
	}

	private void InvokeAirshipLifecycle(AirshipComponentUpdateType updateType) {
		LuauPlugin.LuauUpdateIndividualAirshipComponent(context, thread, AirshipBehaviourRootV3.GetId(gameObject), _airshipComponentId, updateType, 0, true);
	}

	private bool HasAirshipMethod(AirshipComponentUpdateType updateType) {
		if (_hasAirshipUpdateMethods.TryGetValue(updateType, out var has)) {
			return has;
		}
        
		// Fetch from Luau plugin & cache the result:
		var hasMethod = LuauPlugin.LuauHasAirshipMethod(context, thread, AirshipBehaviourRootV3.GetId(gameObject), _airshipComponentId, updateType);
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
		AirshipBehaviourRootV3.LinkComponentToGameObject(this, out var unityInstanceId);
        
		LuauPlugin.LuauPrewarmAirshipComponent(LuauContext.Game, thread, unityInstanceId, _airshipComponentId, transformInstanceId);
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
#if AIRSHIP_PLAYER
        if (AssetBridge == null) {
            print("MISSING ASSET BRIDGE: " + gameObject?.name);
            return;
        }
#endif
        // Clear out script if file path doesn't match script path
        // if (scriptFile != null) {
        //     if (scriptFile.m_path != m_fileFullPath) {
        //         scriptFile = null;
        //     }
        // }
        // // Set script from file path
        // if (scriptFile == null) {
        //     if (!string.IsNullOrEmpty(m_fileFullPath)) {
        //         scriptFile = LoadBinaryFileFromPath(m_fileFullPath);
        //         
        //     }
        //
        //     if (scriptFile == null) {
        //         return;
        //     }
        // }

        ReconcileMetadata();

        if (Application.isPlaying) {
            WriteChangedComponentProperties();
        }
    }
    
    private void OnValidate() {
        Validate();
    }

    private void Validate() {
        if (this == null) return;
        
        SetupMetadata();
    }

    private void Reset() {
        SetupMetadata();
    }

    public void ReconcileMetadata() {
#if AIRSHIP_PLAYER
        return;
#endif

        if (script == null || script.m_metadata == null || script.m_metadata.name == "") {
            return;
        }

        metadata.name = script.m_metadata.name;

        // Add missing properties or reconcile existing ones:
        foreach (var property in script.m_metadata.properties) {
            var serializedProperty = metadata.FindProperty<object>(property.name);
            
            if (serializedProperty == null)
            {
                var element = property.Clone();
                metadata.properties.Add(element);
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
        
        // // Need to recompile
        // if (scriptFile.HasFileChanged) {
        //     return;
        // }
        
        // Remove properties that are no longer used:
        List<LuauMetadataProperty> propertiesToRemove = null;
        var seenProperties = new HashSet<string>();
        foreach (var serializedProperty in metadata.properties) {
            var property = script.m_metadata.FindProperty<object>(serializedProperty.name);
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
                metadata.properties.Remove(serializedProperty);
            }
        }
    }

    private void WriteChangedComponentProperties() {
        if (!AirshipBehaviourRootV3.HasId(gameObject) || thread == IntPtr.Zero) return;
        
        foreach (var property in metadata.properties) {
            // If all value data is unchanged skip this write
            if (!ShouldWriteToComponent(property)) continue;

            _trackCustomProperties[property.name] = new PropertyValueState {
                serializedValue = property.serializedValue,
                itemObjectRefs = (UnityEngine.Object[]) property.items.objectRefs.Clone(),
                itemSerializedObjects = (string[]) property.items.serializedItems.Clone()
            };
            property.WriteToComponent(thread, AirshipBehaviourRootV3.GetId(gameObject), _airshipComponentId);
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

	public class AwakeData {
		public readonly AirshipScript script;
		public readonly LuauContext context;
	}
}
