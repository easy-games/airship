using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace Luau {
    [Serializable]
    public class LuauMetadataArrayProperty {
        public string type;
    }
    
    [Serializable]
    public class LuauMetadataProperty {
        public string name;
        public string type;
        public LuauMetadataArrayProperty items;
        public List<string> decorators;
        public string serializedValue;
        public object serializedObject;

        private AirshipComponentPropertyType _componentType = AirshipComponentPropertyType.AirshipUnknown;
        private AirshipComponentPropertyType ComponentType {
            get {
                if (_componentType != AirshipComponentPropertyType.AirshipUnknown) return _componentType;
                
                var switchType = items != null && !string.IsNullOrEmpty(items.type) ? items.type : type;
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
            clone.decorators = new List<string>(decorators);
            clone.serializedValue = serializedValue;
            return clone;
        }

        private void WriteObjectToComponent(IntPtr thread, int unityInstanceId, int componentId, object obj, int valueSize, AirshipComponentPropertyType compType) {
            var gch = GCHandle.Alloc(obj, GCHandleType.Pinned);
            var value = gch.AddrOfPinnedObject();
            LuauPlugin.LuauWriteToAirshipComponent(thread, unityInstanceId, componentId, name, value, valueSize, (int)ComponentType);
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
    }

    [Serializable]
    public class LuauMetadata {
        public string name;
        public List<LuauMetadataProperty> properties;

        public static LuauMetadata FromJson(string json) {
            return JsonConvert.DeserializeObject<LuauMetadata>(json);
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
