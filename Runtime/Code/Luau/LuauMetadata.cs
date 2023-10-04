using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace Luau
{
    [Serializable]
    public class LuauMetadataProperty
    {
        public string name;
        public string type;
        public List<string> modifiers;
        public string serializedValue;

        private AirshipComponentPropertyType _componentType = AirshipComponentPropertyType.AirshipUnknown;
        private AirshipComponentPropertyType ComponentType
        {
            get
            {
                if (_componentType != AirshipComponentPropertyType.AirshipUnknown) return _componentType;
                
                switch (type)
                {
                    case "string":
                        _componentType = AirshipComponentPropertyType.AirshipString;
                        break;
                    case "bool" or "boolean":
                        _componentType = AirshipComponentPropertyType.AirshipBoolean;
                        break;
                    case "number":
                    {
                        if (HasModifier("int"))
                        {
                            _componentType = AirshipComponentPropertyType.AirshipInt;
                        }
                        else
                        {
                            _componentType = AirshipComponentPropertyType.AirshipFloat;
                        }
                        break;
                    }
                    case "null" or "nil":
                        _componentType = AirshipComponentPropertyType.AirshipNil;
                        break;
                    default:
                        _componentType = AirshipComponentPropertyType.AirshipUnknown;
                        break;
                }

                return _componentType;
            }
        } 

        // Matches same enum in AirshipComponent.h plugin file
        private enum AirshipComponentPropertyType
        {
            AirshipUnknown,
            AirshipNil,
            AirshipBoolean,
            AirshipFloat,
            AirshipInt,
            AirshipVector,
            AirshipString,
        }

        public bool HasModifier(string modifier)
        {
            return modifiers.Contains(modifier);
        }

        public LuauMetadataProperty Clone()
        {
            var clone = new LuauMetadataProperty();
            clone.name = name;
            clone.type = type;
            clone.modifiers = new List<string>(modifiers);
            clone.serializedValue = serializedValue;
            return clone;
        }

        private void WriteObjectToComponent(IntPtr thread, int unityInstanceId, int componentId, object obj, int valueSize)
        {
            var gch = GCHandle.Alloc(obj, GCHandleType.Pinned);
            var value = gch.AddrOfPinnedObject();
            LuauPlugin.LuauWriteToAirshipComponent(thread, unityInstanceId, componentId, name, value, valueSize, (int)ComponentType);
            gch.Free();
        }

        public void WriteToComponent(IntPtr thread, int unityInstanceId, int componentId)
        {
            var valueSize = 0;
            var expectNull = false;
            object obj = null;
            
            switch (ComponentType)
            {
                case AirshipComponentPropertyType.AirshipNil:
                {
                    expectNull = true;
                    obj = null;
                    break;
                }
                case AirshipComponentPropertyType.AirshipBoolean:
                {
                    var value = (byte)(serializedValue != "0" ? 1 : 0);
                    obj = value;
                    break;
                }
                case AirshipComponentPropertyType.AirshipFloat:
                {
                    float.TryParse(serializedValue, out var value);
                    obj = value;
                    break;
                }
                case AirshipComponentPropertyType.AirshipInt:
                {
                    int.TryParse(serializedValue, out var value);
                    obj = value;
                    break;
                }
                case AirshipComponentPropertyType.AirshipVector:
                {
                    throw new Exception("AirshipVector not yet implemented");
                }
                case AirshipComponentPropertyType.AirshipString:
                {
                    obj = serializedValue;
                    valueSize = serializedValue.Length;
                    break;
                }
            }

            if (obj == null && !expectNull)
            {
                throw new Exception("Unexpected null component property value");
            }
            WriteObjectToComponent(thread, unityInstanceId, componentId, obj, valueSize);
        }
    }

    [System.Serializable]
    public class LuauMetadata
    {
        public List<LuauMetadataProperty> properties;

        public static LuauMetadata FromJson(string json)
        {
            return JsonConvert.DeserializeObject<LuauMetadata>(json);
        }

        public LuauMetadataProperty FindProperty<T>(string name)
        {
            foreach (var property in properties)
            {
                if (property.name == name)
                {
                    return property;
                }
            }

            return null;
        }
    }
}
