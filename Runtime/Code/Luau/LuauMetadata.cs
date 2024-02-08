using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Assertions;

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
        AirshipObject,
        AirshipArray,
    }

    
    [Serializable]
    public class LuauMetadataArrayProperty {
        // From JSON
        public string type;
        public string objectType;
        public string[] serializedItems;
        
        // Misc
        // This is inserted to in ScriptBindingEditor (can't have default object references)
        public UnityEngine.Object[] objectRefs = {};
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
    }
    
    [Serializable]
    public class LuauMetadataDecoratorValue : ISerializationCallbackReceiver {
        // From JSON:
        public object value;
        public string type;
        
        // Misc:
        public string serializedValue;

        public void OnBeforeSerialize()
        {
            if (value == null) return;
            serializedValue = JsonConvert.SerializeObject(value);
        }

        public void OnAfterDeserialize()
        {
            value = JsonConvert.DeserializeObject<object>(serializedValue);
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
    
    [Serializable]
    public class LuauMetadataProperty {
        // From the JSON:
        public string name;
        public string type;
        public string objectType;
        public LuauMetadataArrayProperty items;
        public List<LuauMetadataDecoratorElement> decorators = new();
        public bool nullable;
        [JsonProperty("default")]
        public object defaultValue;
        
        // Misc:
        public string serializedValue;
        public UnityEngine.Object serializedObject;
        public bool modified;
        
        // List of valid types for serializable properties
        public static Dictionary<string, Type> _builtInTypes = new(){
            { "Color", typeof(Color) },
            { "Vector4", typeof(Vector4) },
            { "Vector3", typeof(Vector3) },
            { "Vector2", typeof(Vector2) },
            { "Quaternion", typeof(Quaternion) },
            { "Matrix4x4", typeof(Matrix4x4) },
            { "Rect", typeof(Rect) },
            { "LayerMask", typeof(LayerMask) },
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
        private AirshipComponentPropertyType ArrayElementComponentType {
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
            clone.decorators = new List<LuauMetadataDecoratorElement>(decorators);
            clone.serializedValue = serializedValue;
            clone.items = items;
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
                    var element = DeserializeIndividualObject(items.serializedItems[i], objRef, thread, ref elementType);
                    objArray[i] = ObjToIntPtr(element, elementType, gcHandles, stringPtrs);
                }
                obj = objArray;
            } else {
                obj = DeserializeIndividualObject(serializedValue, serializedObject, thread, ref componentTypeSend);
            }

            if (obj == null && componentTypeSend != AirshipComponentPropertyType.AirshipNil) {
                throw new Exception($"Unexpected null component property \"{name}\":\"{componentTypeSend}\" value");
            }

            var namePtr = Marshal.StringToCoTaskMemUTF8(name);
            stringPtrs.Add(namePtr);
            IntPtr valuePtr = ObjToIntPtr(obj, ComponentType, gcHandles, stringPtrs);

            dto = new LuauMetadataPropertyMarshalDto {
                name = namePtr,
                valueContainer = valuePtr,
            };
        }

        private IntPtr ObjToIntPtr(object obj, AirshipComponentPropertyType componentType, List<GCHandle> gcHandles, List<IntPtr> stringPtrs) {
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

        private object DeserializeIndividualObject(string serializedObjectValue, object objectRef, IntPtr thread, ref AirshipComponentPropertyType objectType) {
            object obj = null;
            switch (objectType) {
                case AirshipComponentPropertyType.AirshipNil: {
                    break;
                }
                case AirshipComponentPropertyType.AirshipBoolean: {
                    var value = (byte)(serializedObjectValue != "0" ? 1 : 0);
                    obj = value;
                    break;
                }
                case AirshipComponentPropertyType.AirshipFloat: {
                    float.TryParse(serializedObjectValue, out var value);
                    obj = value;
                    break;
                }
                case AirshipComponentPropertyType.AirshipInt: {
                    int.TryParse(serializedObjectValue, out var value);
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
                case AirshipComponentPropertyType.AirshipObject: {
                    // Reason for possiblyNullObject (Unity calls missing object references "null" while still having a non-null C# reference):
                    // https://embrace.io/blog/understanding-null-reference-exceptions-unity/#:~:text=In%20Unity%2C%20null%20is%20a,different%20in%20a%20key%20way.
                    var possiblyNullObject = ((UnityEngine.Object) objectRef) ?? null;
                    if (possiblyNullObject != null) {
                        var objInstanceId = ThreadDataManager.AddObjectReference(thread, objectRef);
                        obj = objInstanceId;
                    } else {
                        objectType = AirshipComponentPropertyType.AirshipNil;
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
            
            LuauPlugin.LuauWriteToAirshipComponent(thread, unityInstanceId, componentId, dto);

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
                string[] serializedElements = new string[jarray.Count];
                for (var i = 0; i < jarray.Count; i++) {
                    var obj = jarray[i].Value<object>();
                    serializedElements[i] = LuauMetadataPropertySerializer.SerializeAirshipProperty(obj, elementComponentPropertyType);
                }

                items.serializedItems = serializedElements;
                return;
            }
            
            if (defaultValue == null) return;

            serializedValue = LuauMetadataPropertySerializer.SerializeAirshipProperty(defaultValue, ComponentType);
        }
    }

    [Serializable]
    public class LuauMetadata {
        public string name;
        public List<LuauMetadataProperty> properties = new();

        /** Converts json to LuauMetadata (if this errors we return the error message) */
        public static (LuauMetadata, string) FromJson(string json) {
            var metadata = JsonConvert.DeserializeObject<LuauMetadata>(json);

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
