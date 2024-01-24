using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Codice.Client.BaseCommands;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Luau {
    [Serializable]
    public class LuauMetadataArrayProperty {
        public string type;
        public string objectType;
    }

    // This must match up with the C++ version of the struct
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct LuauMetadataPropertyMarshalDto {
        public IntPtr name;
        public IntPtr value;
        public int valueSize;
        public int compType;
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
        private AirshipComponentPropertyType ComponentType {
            get {
                if (_componentType != AirshipComponentPropertyType.AirshipUnknown) return _componentType;
                
                var switchType = type == "Array" ? items.type : type;
                switch (switchType) {
                    case "string":
                        _componentType = AirshipComponentPropertyType.AirshipString;
                        break;
                    case "bool" or "boolean":
                        _componentType = AirshipComponentPropertyType.AirshipBoolean;
                        break;
                    case "number": {
                        if (HasDecorator("int")) {
                            _componentType = AirshipComponentPropertyType.AirshipInt;
                        }
                        else {
                            _componentType = AirshipComponentPropertyType.AirshipFloat;
                        }
                        break;
                    }
                    case "Vector3": {
                        _componentType = AirshipComponentPropertyType.AirshipVector3;
                        break;
                    }
                    case "null" or "nil":
                        _componentType = AirshipComponentPropertyType.AirshipNil;
                        break;
                    case "object":
                        _componentType = AirshipComponentPropertyType.AirshipObject;
                        break;
                    default:
                        // Check built in dictionary
                        if (_builtInTypes.ContainsKey(switchType))
                        {
                            _componentType = AirshipComponentPropertyType.AirshipObject;
                            break;
                        }
                        
                        _componentType = AirshipComponentPropertyType.AirshipUnknown;
                        break;
                }

                return _componentType;
            }
        }

        // Matches same enum in AirshipComponent.h plugin file
        private enum AirshipComponentPropertyType {
            AirshipUnknown,
            AirshipNil,
            AirshipBoolean,
            AirshipFloat,
            AirshipInt,
            AirshipVector3,
            AirshipString,
            AirshipObject,
        }

        public bool HasDecorator(string modifier) {
            return decorators.Exists((element) => element.name == modifier);
        }

        public bool IsArray() {
            return items != null;
        }

        public LuauMetadataProperty Clone() {
            var clone = new LuauMetadataProperty();
            clone.name = name;
            clone.type = type;
            clone.objectType = objectType;
            clone.decorators = new List<LuauMetadataDecoratorElement>(decorators);
            clone.serializedValue = serializedValue;
            return clone;
        }

        public void AsStructDto(IntPtr thread, List<GCHandle> gcHandles, List<IntPtr> stringPtrs, out LuauMetadataPropertyMarshalDto dto) {
            var valueSize = 0;
            var expectNull = false;
            object obj = null;

            var componentTypeSend = ComponentType;
            
            switch (ComponentType) {
                case AirshipComponentPropertyType.AirshipNil: {
                    expectNull = true;
                    obj = null;
                    break;
                }
                case AirshipComponentPropertyType.AirshipBoolean: {
                    var value = (byte)(serializedValue != "0" ? 1 : 0);
                    obj = value;
                    break;
                }
                case AirshipComponentPropertyType.AirshipFloat: {
                    float.TryParse(serializedValue, out var value);
                    obj = value;
                    break;
                }
                case AirshipComponentPropertyType.AirshipInt: {
                    int.TryParse(serializedValue, out var value);
                    obj = value;
                    break;
                }
                case AirshipComponentPropertyType.AirshipVector3: {
                    var values = serializedValue.Split(",");
                    float[] vec = { 0f, 0f, 0f };
                    if (values.Length == 3) {
                        float.TryParse(values[0], out vec[0]);
                        float.TryParse(values[1], out vec[1]);
                        float.TryParse(values[2], out vec[2]);
                    }
                    obj = vec;
                    break;
                }
                case AirshipComponentPropertyType.AirshipString: {
                    obj = serializedValue;
                    valueSize = serializedValue.Length;
                    break;
                }
                case AirshipComponentPropertyType.AirshipObject: {
                    if (serializedObject == null) {
                        obj = null;
                        expectNull = true;
                        componentTypeSend = AirshipComponentPropertyType.AirshipNil;
                    } else {
                        var objInstanceId = ThreadDataManager.AddObjectReference(thread, serializedObject);
                        obj = objInstanceId;
                    }
                    break;
                }
            }

            if (obj == null && !expectNull) {
                throw new Exception($"Unexpected null component property \"{name}\":\"{componentTypeSend}\" value");
            }

            var namePtr = Marshal.StringToCoTaskMemUTF8(name);
            stringPtrs.Add(namePtr);

            IntPtr valuePtr;
            if (componentTypeSend == AirshipComponentPropertyType.AirshipString) {
                valuePtr = Marshal.StringToCoTaskMemUTF8(Convert.ToString(obj));
            } else {
                var valueGch = GCHandle.Alloc(obj, GCHandleType.Pinned);
                valuePtr = valueGch.AddrOfPinnedObject();
            }

            dto = new LuauMetadataPropertyMarshalDto {
                name = namePtr,
                value = valuePtr,
                valueSize = valueSize,
                compType = (int)componentTypeSend
            };
        }

        private void WriteObjectToComponent(IntPtr thread, int unityInstanceId, int componentId, object obj, int valueSize, AirshipComponentPropertyType compType) {
            var gch = GCHandle.Alloc(obj, GCHandleType.Pinned);
            var value = gch.AddrOfPinnedObject();
            LuauPlugin.LuauWriteToAirshipComponent(thread, unityInstanceId, componentId, name, value, valueSize, (int)compType);
            gch.Free();
        }

        public void WriteToComponent(IntPtr thread, int unityInstanceId, int componentId) {
            var valueSize = 0;
            var expectNull = false;
            object obj = null;

            var componentTypeSend = ComponentType;
            
            switch (ComponentType) {
                case AirshipComponentPropertyType.AirshipNil: {
                    expectNull = true;
                    obj = null;
                    break;
                }
                case AirshipComponentPropertyType.AirshipBoolean: {
                    var value = (byte)(serializedValue != "0" ? 1 : 0);
                    obj = value;
                    break;
                }
                case AirshipComponentPropertyType.AirshipFloat: {
                    float.TryParse(serializedValue, out var value);
                    // Debug.Log($"WRITING AIRSHIP FLOAT VALUE {value}");
                    obj = value;
                    break;
                }
                case AirshipComponentPropertyType.AirshipInt: {
                    int.TryParse(serializedValue, out var value);
                    obj = value;
                    break;
                }
                case AirshipComponentPropertyType.AirshipVector3: {
                    var values = serializedValue.Split(",");
                    float[] vec = { 0f, 0f, 0f };
                    if (values.Length == 3) {
                        float.TryParse(values[0], out vec[0]);
                        float.TryParse(values[1], out vec[1]);
                        float.TryParse(values[2], out vec[2]);
                    }
                    obj = vec;
                    break;
                }
                case AirshipComponentPropertyType.AirshipString: {
                    obj = serializedValue;
                    valueSize = serializedValue.Length;
                    break;
                }
                case AirshipComponentPropertyType.AirshipObject: {
                    if (serializedObject == null) {
                        obj = null;
                        expectNull = true;
                        componentTypeSend = AirshipComponentPropertyType.AirshipNil;
                    } else {
                        var objInstanceId = ThreadDataManager.AddObjectReference(thread, serializedObject);
                        obj = objInstanceId;
                    }
                    break;
                }
            }

            if (obj == null && !expectNull) {
                throw new Exception("Unexpected null component property value");
            }
            WriteObjectToComponent(thread, unityInstanceId, componentId, obj, valueSize, componentTypeSend);
        }
        
        public void SetDefaultAsValue()
        {
            if (defaultValue == null) return;
            
            switch (ComponentType) {
                case AirshipComponentPropertyType.AirshipFloat: {
                    serializedValue = Convert.ToSingle(defaultValue).ToString(CultureInfo.InvariantCulture);
                    break;
                }
                case AirshipComponentPropertyType.AirshipInt: {
                    serializedValue = Convert.ToInt32(defaultValue).ToString(CultureInfo.InvariantCulture);
                    break;
                }
                case AirshipComponentPropertyType.AirshipString: {
                    serializedValue = Convert.ToString(defaultValue);
                    break;
                }
                case AirshipComponentPropertyType.AirshipVector3:
                case AirshipComponentPropertyType.AirshipObject: {
                    var objDefaultVal =
                        JsonConvert.DeserializeObject<LuauMetdataObjectDefaultValue>(defaultValue.ToString());
                    // Get type of object
                    if (_builtInTypes.TryGetValue(objDefaultVal.type, out var objType))
                    {
                        // Check if we're doing a static property access (i.e. Color.blue)
                        if (objDefaultVal.target == "property")
                        {
                            var propertyInfo = objType.GetProperty(objDefaultVal.member);
                            defaultValue = propertyInfo?.GetValue(null);
                        }
                        // Check for constructor instantiation (i.e. new Color(1, 0, 0, 0))
                        else if (objDefaultVal.target == "constructor")
                        {
                            // Replace all doubles with floats...
                            var args = objDefaultVal.arguments.ToArray();
                            for (var i = 0; i < args.Length; i++)
                            {
                                if (args[i] is double)
                                {
                                    args[i] = Convert.ToSingle(args[i]);
                                }
                            }
                            defaultValue = Activator.CreateInstance(objType, args);
                        }

                        serializedValue = JsonUtility.ToJson(defaultValue);
                    }
                    else
                    {
                        Debug.Log($"Type not found {objDefaultVal.type}");
                    }
                    break;
                }
            }
        }
    }

    [Serializable]
    public class LuauMetadata {
        public string name;
        public List<LuauMetadataProperty> properties = new();

        public static LuauMetadata FromJson(string json) {
            var metadata = JsonConvert.DeserializeObject<LuauMetadata>(json);
            
            // Set default values:
            foreach (var property in metadata.properties) {
                property.SetDefaultAsValue();
            }
            
            return metadata;
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
