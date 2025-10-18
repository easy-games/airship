
using System.Globalization;
using UnityEditor;
using UnityEngine;

public class AirshipClipboardUtility {
    internal static bool CanCopy(AirshipSerializedValue value) {
        switch (value.type) {
            case AirshipSerializedValue.PropertyType.Vector2:
            case AirshipSerializedValue.PropertyType.Vector3:
            case AirshipSerializedValue.PropertyType.Vector4:
                return true;
            default:
                return false;
        }
    }

    internal static bool CopyValue(AirshipSerializedValue value) {
        switch (value.type) {
            case AirshipSerializedValue.PropertyType.Number:
                GUIUtility.systemCopyBuffer = value.numberValue.ToString(CultureInfo.InvariantCulture);
                return true;
            case AirshipSerializedValue.PropertyType.Boolean: {
                GUIUtility.systemCopyBuffer = value.boolValue ? "True" : "False";
                return true;
            }
            case AirshipSerializedValue.PropertyType.Vector2: {
                var realValue = value.vector2Value;
                GUIUtility.systemCopyBuffer = $"Vector2({realValue.x}, {realValue.y})";
                return true;
            }
            case AirshipSerializedValue.PropertyType.Vector3: {
                var realValue = value.vector3Value;
                GUIUtility.systemCopyBuffer = $"Vector3({realValue.x}, {realValue.y}, {realValue.z})";
                return true;
            }
            case AirshipSerializedValue.PropertyType.Vector4: {
                var realValue = value.vector4Value;
                GUIUtility.systemCopyBuffer = $"Vector4({realValue.x}, {realValue.y}, {realValue.z}, {realValue.w})";
                return true;
            }
        }

        return false;
    }
}
