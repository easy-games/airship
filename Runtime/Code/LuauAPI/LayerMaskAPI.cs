using System;
using Luau;
using UnityEngine;

[LuauAPI]
public class LayerMaskAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(LayerMask);
    }

    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        if (methodName == "GetMask") {
            string[] layerNames = new string[numParameters];
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
            LuauCore.WritePropertyToThread(thread, val, val.GetType());
            return 1;
        }
        if (methodName == "InvertMask") {
            if (numParameters == 1)
            {
                int layerMask = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);

                LuauCore.WritePropertyToThread(thread, ~layerMask, typeof(int));
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

                LuauCore.WritePropertyToThread(thread, LayerMask.NameToLayer(name), typeof(int));
                return 1;
            }
        }

        return -1;
    }
}
