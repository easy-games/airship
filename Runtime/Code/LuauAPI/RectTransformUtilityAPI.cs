using System;
using UnityEngine;

[LuauAPI]
public class RectTransformUtilityAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(RectTransformUtility);
    }

    public override int OverrideStaticMethod(
        LuauContext context,
        IntPtr thread,
        string methodName,
        int numParameters,
        Span<int> parameterDataPODTypes,
        Span<IntPtr> parameterDataPtrs,
        Span<int> parameterDataSizes) {
        if (methodName == "ScreenPointToLocalPointInRectangle") {
            var localPos = Vector2.zero;
            var rect = (RectTransform)LuauCore.GetParameterAsObject(0, numParameters, parameterDataPODTypes,
                parameterDataPtrs, parameterDataSizes, thread);
            var screenPoint = LuauCore.GetParameterAsVector2(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);
            Camera cam = null;
            if (numParameters > 2) {
                cam = (Camera)LuauCore.GetParameterAsObject(2, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    parameterDataSizes, thread);
            }

            var pointWasInRect
                = RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, cam, out localPos);
            LuauCore.WritePropertyToThreadBoolean(thread, pointWasInRect);
            LuauCore.WritePropertyToThreadVector2(thread, localPos);
            return 2;
        }

        if (methodName == "ScreenPointToWorldPointInRectangle") {
            var worldPos = Vector3.zero;
            var rect = (RectTransform)LuauCore.GetParameterAsObject(0, numParameters, parameterDataPODTypes,
                parameterDataPtrs, parameterDataSizes, thread);
            var screenPoint = LuauCore.GetParameterAsVector2(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);
            Camera cam = null;
            if (numParameters > 2) {
                cam = (Camera)LuauCore.GetParameterAsObject(2, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    parameterDataSizes, thread);
            }

            var pointWasInRect
                = RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, screenPoint, cam, out worldPos);
            LuauCore.WritePropertyToThreadBoolean(thread, pointWasInRect);
            LuauCore.WritePropertyToThreadVector3(thread, worldPos);
            return 2;
        }

        return -1;
    }
}