using System;
using System.Collections.Generic;
using UnityEngine;

[LuauAPI]
public class GradientAlphaKeyAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(GradientAlphaKey);
    }
}

[LuauAPI]
public class GradientColorKeyAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(GradientColorKey);
    }
}

[LuauAPI]
public class GradientAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Gradient);
    }

    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters,
        ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        
        switch (methodName) {
            case "CreateColorKeyArray": {
                var size = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    parameterDataSizes);
                
                LuauCore.WritePropertyToThread(thread, new GradientColorKey[size], typeof(GradientColorKey[]));
                return 1;
            }
            case "CreateAlphaKeyArray": {
                var size = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    parameterDataSizes);
            
                LuauCore.WritePropertyToThread(thread, new GradientAlphaKey[size], typeof(GradientAlphaKey[]));
                return 1;
            }
            default:
                return base.OverrideStaticMethod(context, thread, methodName, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
        }
    }
}
