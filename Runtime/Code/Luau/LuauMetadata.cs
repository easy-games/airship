using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using UnityEngine;

namespace Luau {
    [Serializable]
    public class LuauMetadataArrayProperty {
        public string type;
        public string objectType;
    }

    // This must match up with the C++ version of the struct
    [StructLayout(LayoutKind.Sequential)]
    public struct LuauMetadataPropertyMarshalDto {
        public int unityInstanceId;
        public IntPtr value;
        public int valueSize;
        public int compType;
    }
    
    [Serializable]
    public class LuauMetadataProperty {
        // From the JSON:
        public string name;
        public string type;
        public string objectType;
        public LuauMetadataArrayProperty items;
        public List<string> decorators;
        public bool nullable;
        [JsonProperty("default")]
        public object defaultValue;
        
        // Misc:
        public string serializedValue;
        public UnityEngine.Object serializedObject;
        public bool modified;

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
                    case "null" or "nil":
                        _componentType = AirshipComponentPropertyType.AirshipNil;
                        break;
                    case "object":
                        _componentType = AirshipComponentPropertyType.AirshipObject;
                        break;
                    default:
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
            return decorators.Contains(modifier);
        }

        public bool IsArray() {
            return items != null;
        }

        public LuauMetadataProperty Clone() {
            var clone = new LuauMetadataProperty();
            clone.name = name;
            clone.type = type;
            clone.objectType = objectType;
            clone.decorators = new List<string>(decorators);
            clone.serializedValue = serializedValue;
            return clone;
        }

        public GCHandle AsStructDto(IntPtr thread, int unityInstanceId, int componentId, out LuauMetadataPropertyMarshalDto dto) {
            dto = new LuauMetadataPropertyMarshalDto();
            
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
                throw new Exception("Unexpected null component property value");
            }
            
            var gch = GCHandle.Alloc(obj, GCHandleType.Pinned);
            var valuePtr = gch.AddrOfPinnedObject();

            dto.unityInstanceId = unityInstanceId;
            dto.value = valuePtr;
            dto.valueSize = valueSize;
            dto.compType = (int)componentTypeSend;

            return gch;
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
                    Debug.Log($"WRITING AIRSHIP FLOAT VALUE {value}");
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

        public void SetDefaultAsValue() {
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
                case AirshipComponentPropertyType.AirshipVector3: {
                    // TODO
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
