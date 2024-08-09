using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Luau {
    public class LuauMetadataPropertySerializer {
        // List of valid types for serializable properties
        private static Dictionary<string, Type> _builtInTypes = new(){
            { "Color", typeof(Color) },
            { "Vector4", typeof(Vector4) },
            { "Vector3", typeof(Vector3) },
            { "Vector2", typeof(Vector2) },
            { "Quaternion", typeof(Quaternion) },
            { "Matrix4x4", typeof(Matrix4x4) },
            { "Rect", typeof(Rect) },
            { "LayerMask", typeof(LayerMask) },
            { "AnimationCurve", typeof(AnimationCurve) },
        };
        
        public static string SerializeAirshipProperty(object obj, AirshipComponentPropertyType objectType) {
            switch (objectType) {
                case AirshipComponentPropertyType.AirshipFloat: {
                    return Convert.ToSingle(obj).ToString(CultureInfo.InvariantCulture);
                }
                case AirshipComponentPropertyType.AirshipBoolean:
                {
                    if (obj is bool boolObj) {
                        return boolObj ? "1" : "0";
                    }
                    return ((JValue) obj).Value<bool>() ? "1" : "0";
                }
                case AirshipComponentPropertyType.AirshipInt: {
                    return Convert.ToInt32(obj).ToString(CultureInfo.InvariantCulture);
                }
                case AirshipComponentPropertyType.AirshipString: {
                    return Convert.ToString(obj);
                }
                case AirshipComponentPropertyType.AirshipAnimationCurve:
                case AirshipComponentPropertyType.AirshipVector3:
                case AirshipComponentPropertyType.AirshipPod: {
                    var objDefaultVal =
                        JsonConvert.DeserializeObject<LuauMetdataObjectDefaultValue>(obj.ToString());
                    // Get type of object
                    if (_builtInTypes.TryGetValue(objDefaultVal.type, out var objType))
                    {
                        // Check if we're doing a static property access (i.e. Color.blue)
                        if (objDefaultVal.target == "property")
                        {
                            var propertyInfo = objType.GetProperty(objDefaultVal.member);
                            obj = propertyInfo?.GetValue(null);
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
                            obj = Activator.CreateInstance(objType, args);
                        }

                        // Can't use JSONUtility on AnimationCurve
                        if (objectType == AirshipComponentPropertyType.AirshipAnimationCurve) {
                            return SerializeAnimationCurve(obj as AnimationCurve);
                        }

                        return JsonUtility.ToJson(obj);
                    }
                    else
                    {
                        Debug.Log($"Type not found {objDefaultVal.type}");
                        
                    }
                    break;
                }
            }
            Debug.Log($"Failed to serialize object: {obj.ToString()}");
            return "";
        }
        
        public static string SerializeAnimationCurve(AnimationCurve curve) {
            StringBuilder sb = new StringBuilder();
            foreach (Keyframe key in curve.keys)
            {
                sb.Append($"{key.time},{key.value},{key.inTangent},{key.outTangent},{key.inWeight},{key.outWeight};");
            }
            return sb.ToString().TrimEnd(';');
        }

        public static AnimationCurve DeserializeAnimationCurve(string serializedCurve) {
            var curve = new AnimationCurve();
            string[] keyframeStrings = serializedCurve.Split(';');
        
            foreach (string keyframeString in keyframeStrings)
            {
                string[] values = keyframeString.Split(',');
                if (values.Length == 6)
                {
                    float time = float.Parse(values[0]);
                    float value = float.Parse(values[1]);
                    float inTangent = float.Parse(values[2]);
                    float outTangent = float.Parse(values[3]);
                    float inWeight = float.Parse(values[4]);
                    float outWeight = float.Parse(values[5]);

                    Keyframe keyframe = new Keyframe(time, value, inTangent, outTangent)
                    {
                        inWeight = inWeight,
                        outWeight = outWeight
                    };
                    curve.AddKey(keyframe);
                }
            }

            return curve;
        }
        
        /**
         * If useIntForNumber is true we will treat a number as as int, otherwise it'll be a float
         */
        public static AirshipComponentPropertyType GetAirshipComponentPropertyTypeFromString(string typeString, bool useIntForNumber) {
            switch (typeString) {
                case "Array":
                    return AirshipComponentPropertyType.AirshipArray;
                case "string":
                    return AirshipComponentPropertyType.AirshipString;
                case "bool" or "boolean":
                    return AirshipComponentPropertyType.AirshipBoolean;
                case "number": {
                    if (useIntForNumber) {
                        return AirshipComponentPropertyType.AirshipInt;
                    }
                    else {
                        return AirshipComponentPropertyType.AirshipFloat;
                    }
                }
                case "StringEnum": {
                    return AirshipComponentPropertyType.AirshipString;
                }
                case "IntEnum": {
                    return AirshipComponentPropertyType.AirshipInt;
                }
                case "AirshipBehaviour": {
                    return AirshipComponentPropertyType.AirshipComponent;
                }
                case "Vector3": {
                    return AirshipComponentPropertyType.AirshipVector3;
                }
                case "AnimationCurve": {
                    return AirshipComponentPropertyType.AirshipAnimationCurve;
                }
                case "null" or "nil":
                    return AirshipComponentPropertyType.AirshipNil;
                case "object":
                    return AirshipComponentPropertyType.AirshipObject;
                default:
                    // Check built in dictionary
                    if (_builtInTypes.ContainsKey(typeString))
                    {
                        return AirshipComponentPropertyType.AirshipPod;
                    }
                        
                    return AirshipComponentPropertyType.AirshipUnknown;
            }
        }
    }
}