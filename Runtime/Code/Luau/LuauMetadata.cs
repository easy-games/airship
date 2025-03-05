using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

namespace Luau {
    // Matches same enum in AirshipComponent.h plugin file
    public enum AirshipComponentPropertyType {
        AirshipUnknown,
        AirshipNil,
        AirshipBoolean,
        AirshipFloat,
        AirshipInt,
        AirshipVector3,
        AirshipString,
        AirshipAnimationCurve,
        AirshipObject,
        AirshipArray,
        AirshipPod,
        AirshipComponent,
    }
    
    [Serializable]
    public class LuauMetadataArrayProperty {
        // From JSON
        public string type;
        public string objectType;
        public string[] serializedItems;
        [NonSerialized] public string fileRef;
        [NonSerialized] public string refPath;
        
        // Misc
        // This is inserted to in ScriptBindingEditor (can't have default object references)
        public UnityEngine.Object[] objectRefs = {};

        public LuauMetadataArrayProperty Clone() {
            return new LuauMetadataArrayProperty() {
                type = type,
                objectType = objectType,
                serializedItems = (string[])serializedItems.Clone(),
                objectRefs = (UnityEngine.Object[])objectRefs.Clone(),
            };
        }
    }
    
    // This must match up with the C++ version of the struct
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct LuauMetadataValueContainerDto {
        // Defaults of ValueContainer
        public IntPtr value;
        public int valueType;
    }
    
    // This must match up with the C++ version of the struct
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct LuauMetadataStringValueContainerDto {
        // Defaults of ValueContainer
        public IntPtr value;
        public int valueType;
        
        // Custom
        public int size;
    }
    
    // This must match up with the C++ version of the struct
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct LuauMetadataPodValueContainerDto {
        // Defaults of ValueContainer
        public IntPtr value;
        public int valueType;
        
        // Custom
        public LuauCore.PODTYPE podType;
    }
    
    // This must match up with the C++ version of the struct
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal class AirshipComponentRef {
        public int unityInstanceId;
        public int airshipComponentId;

        public AirshipComponentRef() {}
        
        public AirshipComponentRef(int unityInstanceId, int airshipComponentId) {
            this.airshipComponentId = airshipComponentId;
            this.unityInstanceId = unityInstanceId;
        }

        public AirshipComponent AsUnityComponent() {
            var component = AirshipBehaviourRootV2.GetComponent(unityInstanceId, airshipComponentId);
            return component;
        }
    }
    
    // This must match up with the C++ version of the struct
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct LuauMetadataAirshipComponentRefContainerDto {
        public IntPtr value;
        public int valueType;
    }
    
    // This must match up with the C++ version of the struct
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct LuauMetadataArrayValueContainerDto {
        // Defaults of ValueContainer
        public IntPtr value;
        public int valueType;
        
        // Custom
        public int size;
    }
    
    // This must match up with the C++ version of the struct
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct LuauMetadataPropertyMarshalDto {
        public IntPtr name;
        public IntPtr valueContainer;
        public int modified;
    }
    
    [Serializable]
    public class LuauMetadataDecoratorValue : ISerializationCallbackReceiver {
        // From JSON:
        public object value;
        public string type;
        
        // Misc:
        public string serializedValue;

        public void OnBeforeSerialize() {
            if (value == null) return;
            serializedValue = JsonConvert.SerializeObject(value);
        }

        public void OnAfterDeserialize() {
            value = JsonConvert.DeserializeObject<object>(serializedValue);
        }

        public bool TryGetString(out string value) {
            if (this.value is string stringValue) {
                value = stringValue;
                return true;
            }

            value = null;
            return false;
        }
    }
    
    [Serializable]
    public class LuauMetadataDecoratorElement {
        // From JSON:
        public string name;
        public List<LuauMetadataDecoratorValue> parameters = new();
    }
    
    [Serializable]
    public class LuauMetdataObjectDefaultValue {
        // From JSON:
        public string target;
        public string member;
        public string type;
        public List<object> arguments;
    }
    
    [Serializable, JsonConverter(typeof(LuauMetadataEnum))]
    public class LuauMetadataEnum : JsonConverter {
        [Serializable]
         public class EnumItem {
             public string name;
             
             public string stringValue;
             public int intValue;
         }

         public List<EnumItem> items;
         
         public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
             throw new NotImplementedException();
         }

         public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
             var jsonObject = JObject.Load(reader);
             var properties = jsonObject.Properties().ToList();
             return new LuauMetadataEnum() {
                 items = properties.Select(prop => new EnumItem() {
                     name = prop.Name,
                     stringValue = prop.Value.Type == JTokenType.String ? (string) prop.Value : null,
                     intValue = prop.Value.Type == JTokenType.Integer ? (int) prop.Value : 0,
                 }).ToList()
             };
         }

         public override bool CanConvert(Type objectType) {
             return true;
         }
     }
    
    [Serializable]
    public class LuauMetadataJsDocTag {
        public string name;
        [CanBeNull] public string value;
    }
    
    [Serializable]
    public class LuauMetadataJsDoc {
        [JsonProperty("text")][SerializeField]
        private List<string> text = new();
        
        [SerializeField]
        private List<LuauMetadataJsDocTag> tags = new();
        
        /// <summary>
        /// Gets the documentation formatted as a tooltip
        /// </summary>
        public string RichText => string.Join("", text);
        public IReadOnlyList<LuauMetadataJsDocTag> Tags => tags;
    }
    
    [Serializable]
    public class LuauMetadataProperty {
        // From the JSON:
        public string name;
        public string type;
        public string objectType;
        public LuauMetadataArrayProperty items;
        
        /// <summary>
        /// Path to a type reference
        /// </summary>
        [JsonProperty("ref")]
        public string refPath;
        
        /// <summary>
        /// Path to a file
        /// </summary>
        public string fileRef;
        
        [JsonProperty("jsdoc")][SerializeField]
        private LuauMetadataJsDoc jsDocs;
        public LuauMetadataJsDoc JsDoc => jsDocs;
        public string Documentation => JsDoc?.RichText;
        
        #if UNITY_EDITOR
        [JsonProperty][SerializeField]
        #endif
        private List<LuauMetadataDecoratorElement> decorators = new();
        public bool nullable;
        [JsonProperty("default")]
        public object defaultValue;
        
        // Misc:
        public string serializedValue;
        public UnityEngine.Object serializedObject;
        public bool modified;
        
        // List of valid types for serializable properties
        public static Dictionary<string, LuauCore.PODTYPE> _builtInTypes = new(){
            { "Color", LuauCore.PODTYPE.POD_COLOR },
            { "Vector4", LuauCore.PODTYPE.POD_VECTOR4 },
            { "Vector3", LuauCore.PODTYPE.POD_VECTOR3 },
            { "Vector2", LuauCore.PODTYPE.POD_VECTOR2 },
            { "Quaternion", LuauCore.PODTYPE.POD_QUATERNION },
            { "Matrix4x4", LuauCore.PODTYPE.POD_MATRIX },
            // { "Rect", LuauCore.PODTYPE.POD_RECT }, // POD_RECT doesn't exist
            // { "LayerMask", LuauCore.PODTYPE.POD_LAYERMASK }, // POD_LAYERMASK doesn't exist
        };

        private AirshipComponentPropertyType _componentType = AirshipComponentPropertyType.AirshipUnknown;
        public AirshipComponentPropertyType ComponentType {
            get {
                if (_componentType != AirshipComponentPropertyType.AirshipUnknown) return _componentType;
                
                _componentType = LuauMetadataPropertySerializer.GetAirshipComponentPropertyTypeFromString(type, HasDecorator("int"));
                return _componentType;
            }
        }
        
        private AirshipComponentPropertyType _arrayElementComponentType = AirshipComponentPropertyType.AirshipUnknown; 
        public AirshipComponentPropertyType ArrayElementComponentType {
            get {
                Assert.AreEqual(AirshipComponentPropertyType.AirshipArray, ComponentType, "Can't get element type of non-array property");
                if (_arrayElementComponentType != AirshipComponentPropertyType.AirshipUnknown) return _arrayElementComponentType;
                
                _arrayElementComponentType = LuauMetadataPropertySerializer.GetAirshipComponentPropertyTypeFromString(items.type, HasDecorator("int"));
                return _arrayElementComponentType;
            }
        }
        public bool HasDecorator(string modifier) {
            return decorators.Exists((element) => element.name == modifier);
        }
        
        public LuauMetadataProperty Clone() {
            var clone = new LuauMetadataProperty();
            clone.name = name;
            clone.type = type;
            clone.objectType = objectType;
            clone.refPath = refPath;
            clone.fileRef = fileRef;
            clone.decorators = new List<LuauMetadataDecoratorElement>(decorators);
            clone.serializedValue = serializedValue;
            clone.items = items.Clone();
            return clone;
        }

        public void AsStructDto(IntPtr thread, List<GCHandle> gcHandles, List<IntPtr> stringPtrs, out LuauMetadataPropertyMarshalDto dto) {
            var componentTypeSend = ComponentType;
            object obj = null;

            if (ComponentType == AirshipComponentPropertyType.AirshipArray) {
                var objArray = new IntPtr[items.serializedItems.Length];
                for (var i = 0; i < items.serializedItems.Length; i++) {
                    var objRef = items.objectRefs.Length > i ? items.objectRefs[i] : null;
                    var elementType = ArrayElementComponentType;
                    var element = DeserializeIndividualObject(items.serializedItems[i], objRef, thread, ref elementType, items.type);
                    objArray[i] = ObjToIntPtr(element, items.type, elementType, gcHandles, stringPtrs);
                }
                obj = objArray;
            } else {
                obj = DeserializeIndividualObject(serializedValue, serializedObject, thread, ref componentTypeSend, type);
            }

            if (obj == null && componentTypeSend != AirshipComponentPropertyType.AirshipNil) {
                throw new Exception($"Unexpected null component property \"{name}\":\"{componentTypeSend}\" value");
            }

            var namePtr = Marshal.StringToCoTaskMemUTF8(name);
            stringPtrs.Add(namePtr);
            IntPtr valuePtr = ObjToIntPtr(obj, type, componentTypeSend, gcHandles, stringPtrs);

            dto = new LuauMetadataPropertyMarshalDto {
                name = namePtr,
                valueContainer = valuePtr,
                modified = modified ? 1 : 0
            };
        }

        public List<LuauMetadataDecoratorElement> GetDecorators() {
            return this.decorators;
        }

        private IntPtr ObjToIntPtr(object obj, string objTypeStr, AirshipComponentPropertyType componentType, List<GCHandle> gcHandles, List<IntPtr> stringPtrs) {
            // Function to get value container object
            object GetValueContainer() {
                // String should be allocated separately from normal values
                if (componentType == AirshipComponentPropertyType.AirshipString) {
                    var str = Convert.ToString(obj);
                    var strPtr = Marshal.StringToCoTaskMemUTF8(str);
                    stringPtrs.Add(strPtr);
                    return new LuauMetadataStringValueContainerDto {
                        value = strPtr,
                        valueType = (int) componentType,
                        size = str.Length,
                    };
                }
                
                // Allocate memory for value
                var valueGch = GCHandle.Alloc(obj, GCHandleType.Pinned);
                gcHandles.Add(valueGch);
                var valuePtr = valueGch.AddrOfPinnedObject();
            
                // Array needs to additionally add "size" field
                if (componentType == AirshipComponentPropertyType.AirshipArray) {
                    var size = ((IntPtr[])obj).Length;
                    return new LuauMetadataStringValueContainerDto {
                        value = valuePtr,
                        valueType = (int) componentType,
                        size = size,
                    };
                }
                
                if (componentType == AirshipComponentPropertyType.AirshipComponent) {
                    return new LuauMetadataAirshipComponentRefContainerDto() {
                        value = valuePtr,
                        valueType = (int) componentType,
                    };
                }
                
                // Pod needs to additionally add "type" field
                if (componentType == AirshipComponentPropertyType.AirshipPod) {
                    if (!_builtInTypes.TryGetValue(objTypeStr, out var podType)) {
                        throw new Exception($"Could not find pod type: \"{podType}\"");
                    }
                    return new LuauMetadataPodValueContainerDto {
                        value = valuePtr,
                        valueType = (int) componentType,
                        podType = podType
                    };
                }

                // Default propertyDto assignment
                return new LuauMetadataValueContainerDto {
                    value = valuePtr,
                    valueType = (int) componentType
                };
            };
            
            // Allocate memory for full dto
            var propertyDto = GetValueContainer();
            var dtoGch = GCHandle.Alloc(propertyDto, GCHandleType.Pinned);
            gcHandles.Add(dtoGch);
            return dtoGch.AddrOfPinnedObject();
        }

        private object DeserializeIndividualObject(string serializedObjectValue, object objectRef, IntPtr thread, ref AirshipComponentPropertyType propType, string typeStr) {
            object obj = null;
            switch (propType) {
                case AirshipComponentPropertyType.AirshipNil: {
                    break;
                }
                case AirshipComponentPropertyType.AirshipBoolean: {
                    // Default to false if not set
                    if (serializedObjectValue == "") {
                        obj = false;
                        break;
                    }
                    var value = (byte)(serializedObjectValue != "0" ? 1 : 0);
                    obj = value;
                    break;
                }
                case AirshipComponentPropertyType.AirshipFloat: {
                    // Default to 0 if not set
                    if (serializedObjectValue == "") {
                        obj = 0.0f;
                        break;
                    }
                    float.TryParse(serializedObjectValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value);
                    obj = value;
                    break;
                }
                case AirshipComponentPropertyType.AirshipInt: {
                    // Default to 0 if not set
                    if (serializedObjectValue == "") {
                        obj = 0;
                        break;
                    }
                    int.TryParse(serializedObjectValue, NumberStyles.Integer, CultureInfo.InvariantCulture,  out var value);
                    obj = value;
                    break;
                }
                case AirshipComponentPropertyType.AirshipVector3: {
                    obj = serializedObjectValue == "" ? new Vector3() : JsonUtility.FromJson<Vector3>(serializedObjectValue);
                    break;
                }
                case AirshipComponentPropertyType.AirshipString: {
                    obj = serializedObjectValue;
                    break;
                }
                case AirshipComponentPropertyType.AirshipAnimationCurve: {
                    var deserializedCurve = LuauMetadataPropertySerializer.DeserializeAnimationCurve(serializedObjectValue);
                    var objInstanceId = ThreadDataManager.AddObjectReference(thread, deserializedCurve);
                    obj = objInstanceId;
                    break;
                }
                case AirshipComponentPropertyType.AirshipPod: {
                    var objType = TypeReflection.GetTypeFromString(typeStr);
                    obj = JsonUtility.FromJson(serializedObjectValue, objType);
                    if (obj == null) {
                        obj = Activator.CreateInstance(objType);
                        // propType = AirshipComponentPropertyType.AirshipNil;
                        // obj = -1; // Reference to null
                    }
                    break;
                }
                case AirshipComponentPropertyType.AirshipComponent: {
                    if (objectRef is AirshipComponent scriptBinding) {
                        var gameObject = scriptBinding.gameObject;
                        // if (!AirshipBehaviourRootV2.HasId(gameObject)) {
                        //     // See if it just needs to be started first:
                        //     var foundAny = false;
                        //     foreach (var binding in gameObject.GetComponents<AirshipComponent>()) {
                        //         foundAny = true;
                        //     }
                        //
                        //     // Retry getting AirshipBehaviourRoot:
                        //     // if (foundAny) {
                        //         // airshipComponent = gameObject.GetComponent<AirshipBehaviourRoot>();
                        //     // }
                        // }

                        if (AirshipBehaviourRootV2.HasId(gameObject)) {
                            // We need to just pass the unity instance id + component ids to Luau since it's Luau-side
                            var unityInstanceId = AirshipBehaviourRootV2.GetId(gameObject);
                            var targetComponentId = scriptBinding.GetAirshipComponentId();

                            obj = new AirshipComponentRef(unityInstanceId, targetComponentId);
                        }
                        else {
                            propType = AirshipComponentPropertyType.AirshipNil;
                            obj = -1; // Reference to null
                        }
                    }
                    else {
                        propType = AirshipComponentPropertyType.AirshipNil;
                        obj = -1; // Reference to null
                    }
                    
                    break;
                }
                case AirshipComponentPropertyType.AirshipObject: {
                    // Reason for possiblyNullObject (Unity calls missing object references "null" while still having a non-null C# reference):
                    // https://embrace.io/blog/understanding-null-reference-exceptions-unity/#:~:text=In%20Unity%2C%20null%20is%20a,different%20in%20a%20key%20way.
                    var possiblyNullObject = ((UnityEngine.Object) objectRef) ?? null;
                    // This is not a Unity object (for example Color)
                    if (possiblyNullObject == null && serializedObjectValue.Length > 0) {
                        Debug.LogError($"Deserializing a non-Unity Object as a Unity Object, report this. name={name} typeString={typeStr} serializedObjectValue={serializedObjectValue}");
                        var objType = TypeReflection.GetTypeFromString(typeStr);
                        obj = JsonUtility.FromJson(serializedObjectValue, objType);
                        break;
                    }

                    if (possiblyNullObject != null) {
                        var objInstanceId = ThreadDataManager.AddObjectReference(thread, objectRef);
                        obj = objInstanceId;
                    } else {
                        propType = AirshipComponentPropertyType.AirshipNil;
                        obj = -1; // Reference to null
                    }
                    break;
                }
            }
            return obj;
        }

        public void WriteToComponent(IntPtr thread, int unityInstanceId, int componentId) {
            var gcHandles = new List<GCHandle>();
            var strPtrs = new List<IntPtr>();
            AsStructDto(thread, gcHandles, strPtrs, out var dto);
            
            LuauPlugin.LuauWriteToAirshipComponent(LuauContext.Game, thread, unityInstanceId, componentId, dto);

            foreach (var handle in gcHandles) {
                handle.Free();
            }
            foreach (var strPtr in strPtrs) {
                Marshal.FreeCoTaskMem(strPtr);
            }
        }
        
        public void SetDefaultAsValue() {
            if (type == "Array") {
                Newtonsoft.Json.Linq.JArray jarray = (Newtonsoft.Json.Linq.JArray) defaultValue;
                var elementComponentPropertyType = LuauMetadataPropertySerializer.GetAirshipComponentPropertyTypeFromString(items.type, HasDecorator("int"));
                var jarraySize = jarray == null ? 0 : jarray.Count;
                string[] serializedElements = new string[jarraySize];
                for (var i = 0; i < jarraySize; i++) {
                    var obj = jarray[i].Value<object>();
                    serializedElements[i] = LuauMetadataPropertySerializer.SerializeAirshipProperty(obj, elementComponentPropertyType);
                }

                items.serializedItems = serializedElements;
                return;
            }

            // void AirshipBehaviours or Components
            if (type is "AirshipBehaviour" or "object") {
                serializedObject = null;
            }
            
            if (defaultValue == null) return;
            
            serializedValue = LuauMetadataPropertySerializer.SerializeAirshipProperty(defaultValue, ComponentType);
        }
    }

    [Serializable]
    public class LuauMetadata {
        public string name;
        public bool singleton;
        #if UNITY_EDITOR
        [JsonProperty][SerializeField]
        #endif
        private List<LuauMetadataDecoratorElement> decorators = new();
        public List<LuauMetadataProperty> properties = new();
        [CanBeNull] public Texture2D displayIcon;

        public string displayName;
        
        /** Converts json to LuauMetadata (if this errors we return the error message) */
        public static (LuauMetadata, string) FromJson(string json) {
            var metadata = JsonConvert.DeserializeObject<LuauMetadata>(json);

            // Display name is only needed by editor
#if UNITY_EDITOR
            var airshipComponentMenu = metadata.FindClassDecorator("AirshipComponentMenu");
            if (airshipComponentMenu != null && airshipComponentMenu.parameters[0].TryGetString(out var componentPath)) {
                var value = componentPath.Split("/");
                metadata.displayName = ObjectNames.NicifyVariableName(value.Last());
            }
            else {
                metadata.displayName = ObjectNames.NicifyVariableName(metadata.name);
            }

            var airshipIcon = metadata.FindClassDecorator("AirshipComponentIcon");
            if (airshipIcon != null && airshipIcon.parameters[0].TryGetString(out var airshipIconPath)) {
                metadata.displayIcon = File.Exists(airshipIconPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(airshipIconPath) : null;
            }
            else {
                metadata.displayIcon = null;
            }
#endif

            // Validate that there are no duplicate property entries
            var seenProps = new HashSet<string>();
            foreach (var property in metadata.properties) {
                if (seenProps.Contains(property.name)) {
                    return (null, $"The same field name \"{property.name}\" is serialized multiple times in the class or its parent class.");
                }
                seenProps.Add(property.name);
            }
            
            // Set default values:
            foreach (var property in metadata.properties) {
                property.SetDefaultAsValue();
            }
            
            return (metadata, null);
        }

        public List<LuauMetadataDecoratorElement> GetDecorators() {
            return decorators;
        }

        public LuauMetadataDecoratorElement FindClassDecorator(string decoratorName) {
            foreach (var property in decorators) {
                if (property.name == decoratorName) {
                    return property;
                }
            }

            return null;
        }

        public LuauMetadataProperty FindProperty<T>(string propertyName) {
            foreach (var property in properties) {
                if (property.name == propertyName) {
                    return property;
                }
            }

            return null;
        }
    }
}
