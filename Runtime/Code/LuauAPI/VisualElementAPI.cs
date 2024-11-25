using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Luau;

[LuauAPI]
public class VisualElementAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.UIElements.VisualElement); 
    }
    public override Type[] GetDescendantTypes()
    {
        return new Type[] { typeof(UnityEngine.UIElements.TemplateContainer) };
    }

    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName,int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes)
    {
        return -1;
    }
    public override int OverrideMemberMethod(LuauContext context, IntPtr thread, System.Object targetObject, string methodName, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes)
    {
        //Single element version
        if (methodName == "Q")
        {
            if (numParameters < 1 || numParameters > 2)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: Query takes 1 parameter");
                return 0;
            }
            
            string visualName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            string[] visualClasses = null;
            if (numParameters == 2) {
                visualClasses = new string[1] {
                    LuauCore.GetParameterAsString(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                        parameterDataSizes)
                };
            }
            VisualElement visual = (VisualElement)targetObject;
            object returnValue = visual.Query(visualName, visualClasses).Build().First();

            if (returnValue == null)
            {
                LuauCore.WritePropertyToThread(thread, null, null);
            }
            else
            {
                LuauCore.WritePropertyToThread(thread, returnValue, returnValue.GetType());
            }

            return 1;
        }

        //Array version
        if (methodName == "Query")
        {
            if (numParameters != 1)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: Query takes 1 parameter");
                return 0;
            }
           
            string visualName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            VisualElement visual = (VisualElement)targetObject;
            object returnValue = visual.Query(visualName).Build().ToList().ToArray();

            if (returnValue == null)
            {
                LuauCore.WritePropertyToThread(thread, null, null);
            }
            else
            {
                LuauCore.WritePropertyToThread(thread, returnValue, returnValue.GetType());
            }

            return 1;
        }

        return -1;
    }
 
}
