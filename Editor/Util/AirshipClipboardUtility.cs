
using System.Globalization;
using UnityEditor;
using UnityEngine;

public class AirshipClipboardUtility {
    internal static bool CanCopy(AirshipSerializedValue value) {
        switch (value.type) {
            case AirshipSerializedType.Vector2:
            case AirshipSerializedType.Vector3:
            case AirshipSerializedType.Vector4:
                return true;
            default:
                return false;
        }
    }

    internal static bool CopyValue(AirshipSerializedValue value) {
        switch (value.type) {
            case AirshipSerializedType.Number:
                GUIUtility.systemCopyBuffer = value.numberValue.ToString(CultureInfo.InvariantCulture);
                return true;
            case AirshipSerializedType.Boolean: {
                GUIUtility.systemCopyBuffer = value.boolValue ? "True" : "False";
                return true;
            }
            case AirshipSerializedType.Vector2: {
                var realValue = value.vector2Value;
                GUIUtility.systemCopyBuffer = $"Vector2({realValue.x}, {realValue.y})";
                return true;
            }
            case AirshipSerializedType.Vector3: {
                var realValue = value.vector3Value;
                GUIUtility.systemCopyBuffer = $"Vector3({realValue.x}, {realValue.y}, {realValue.z})";
                return true;
            }
            case AirshipSerializedType.Vector4: {
                var realValue = value.vector4Value;
                GUIUtility.systemCopyBuffer = $"Vector4({realValue.x}, {realValue.y}, {realValue.z}, {realValue.w})";
                return true;
            }
        }

        return false;
    }
}
