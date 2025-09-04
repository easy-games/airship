using System;
using System.Buffers;
using System.Collections.Generic;
using Luau;
using UnityEngine;

[LuauAPI]
public class LayerMaskAPI : BaseLuaAPIClass
{
    private static Dictionary<int, string[]> layerArrays = new();
    
    public override Type GetAPIType()
    {
        return typeof(LayerMask);
    }

    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters, Span<int> parameterDataPODTypes, Span<IntPtr> parameterDataPtrs, Span<int> parameterDataSizes) {
        if (methodName == "GetMask") {
            var layerNames = GetMaskArray(numParameters);
            var gameConfig = AssetBridge.Instance.LoadGameConfigAtRuntime();
            for (int i = 0; i < numParameters; i++) {
                string name = LuauCore.GetParameterAsString(i, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    parameterDataSizes);

                // Map game layer name to normalized airship-player layer name.
#if AIRSHIP_PLAYER
                if (gameConfig) {
                    int index = Array.IndexOf(gameConfig.gameLayers, name);
                    if (index > -1) {
                        name = LayerMask.LayerToName(index);
                    }
                }
#endif

                layerNames[i] = name;
            }

            var val = LayerMask.GetMask(layerNames);
            LuauCore.WritePropertyToThreadInt32(thread, val);
            return 1;
        }
        if (methodName == "InvertMask") {
            if (numParameters == 1)
            {
                int layerMask = LuauCore.GetParameterAsInt32(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);

                LuauCore.WritePropertyToThreadInt32(thread, ~layerMask);
                return 1;
            }
        }

        if (methodName == "NameToLayer") {
            if (numParameters == 1) {
                var name = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    parameterDataSizes);

                // Map game layer name to normalized airship-player layer name.
                var gameConfig = AssetBridge.Instance.LoadGameConfigAtRuntime();
#if AIRSHIP_PLAYER
                if (gameConfig) {
                    int index = Array.IndexOf(gameConfig.gameLayers, name);
                    if (index > -1) {
                        name = LayerMask.LayerToName(index);
                    }
                }
#endif

                LuauCore.WritePropertyToThreadInt32(thread, LayerMask.NameToLayer(name));
                return 1;
            }
        }

        return -1;
    }

    private string[] GetMaskArray(int numElements) {
        if (!layerArrays.TryGetValue(numElements, out var arr)) {
            arr = new string[numElements];
            layerArrays[numElements] = arr;
        }
        return arr;
    }
}
