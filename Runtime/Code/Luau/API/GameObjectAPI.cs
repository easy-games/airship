
using System;
using UnityEngine;
using Luau;
using Object = UnityEngine.Object;

[LuauAPI]
public class GameObjectAPI : BaseLuaAPIClass
{

    public override Type GetAPIType()
    {
        return typeof(UnityEngine.GameObject);
    }

    public override int OverrideStaticMethod(IntPtr thread, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {
        if (methodName == "Create")
        {
            string name = "lua_created_gameobject";
            if (numParameters >= 1)
            {
                name = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    paramaterDataSizes);
            }
            var go = new GameObject(name);
            LuauCore.WritePropertyToThread(thread, go, typeof(GameObject));
            return 1;
        }
        if (methodName == "CreateAtPos")
        {
            string name = "lua_created_gameobject";

            var pos = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                paramaterDataSizes);

            if (numParameters >= 2)
            {
                name = LuauCore.GetParameterAsString(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    paramaterDataSizes);
            }
            var go = new GameObject(name);
            go.transform.position = pos;
            LuauCore.WritePropertyToThread(thread, go, typeof(GameObject));
            return 1;
        }
        return -1;
    }

    public override int OverrideMemberMethod(IntPtr thread, System.Object targetObject, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {
        if (methodName == "GetComponentIfExists") {
            string typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (typeName == null) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: GetComponentIfExists takes a parameter");
                return 0;
            }
            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;

            Type objectType = LuauCore.Instance.GetTypeFromString(typeName);
            if (objectType == null)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: GetComponentIfExists component type not found: \"" + typeName + "\". Has it been registered in LuauCoreSystemNamespaces.cs?");
                return 0;
            }
            var newObject = gameObject.GetComponent(objectType);
            if (newObject != null)
            {
                LuauCore.WritePropertyToThread(thread, newObject, objectType);
                return 1;
            }

            LuauCore.WritePropertyToThread(thread, null, null);
            return 1;
        }
        if (methodName == "GetComponentsInChildren") {
            string typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (typeName == null) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: GetComponentsInChildren takes a string parameter.");
                return 0;
            }
            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;

            Type objectType = LuauCore.Instance.GetTypeFromString(typeName);
            if (objectType == null)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: GetComponentsInChildren component type not found: " + typeName + " (consider registering it?)");
                return 0;
            }
            var results = gameObject.GetComponentsInChildren(objectType);
            LuauCore.WritePropertyToThread(thread, results, typeof(Component[]));
            return 1;
        }
        
        if (methodName == "AddComponent")
        {
             
            string param0 = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);

            if (param0 == null)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: AddComponent takes a parameter");
                return 0;
            }
            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;

            Type objectType = LuauCore.Instance.GetTypeFromString(param0);
            if (objectType == null)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: AddComponent component type not found: " + param0 + " (consider registering it?)");
                return 0;
            }
            object newObject = gameObject.AddComponent(objectType);
            LuauCore.WritePropertyToThread(thread, newObject, objectType);
            return 1;
        }

        if (methodName == "OnUpdate")
        {
            if (numParameters != 1)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnUpdate takes 1 parameter");
                return 0;
            }
            if (parameterDataPODTypes[0] != (int)LuauCore.PODTYPE.POD_LUAFUNCTION)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnUpdate parameter must be a function");
                return 0;
            }

           
            int handle = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);

            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;
            ThreadDataManager.SetOnUpdateHandle(thread, handle, gameObject);

            return 1;
        }
        if (methodName == "OnLateUpdate")
        {
            if (numParameters != 1)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnLateUpdate takes 1 parameter");
                return 0;
            }
            if (parameterDataPODTypes[0] != (int)LuauCore.PODTYPE.POD_LUAFUNCTION)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnLateUpdate parameter must be a function");
                return 0;
            }

            
            int handle = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            
            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;
            ThreadDataManager.SetOnLateUpdateHandle(thread, handle, gameObject);
            return 1;
        }
        if (methodName == "OnFixedUpdate")
        {
            if (numParameters != 1)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnFixedUpdate takes 1 parameter");
                return 0;
            }
            if (parameterDataPODTypes[0] != (int)LuauCore.PODTYPE.POD_LUAFUNCTION)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnFixedUpdate parameter must be a function");
                return 0;
            }

            
            int handle = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);

            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;
            ThreadDataManager.SetOnFixedUpdateHandle(thread, handle, gameObject);
            return 1;
        }

        if (methodName == "ClearChildren")
        {
            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;
            foreach (Transform transform in gameObject.transform)
            {
                UnityEngine.GameObject.Destroy(transform.gameObject);
            }

            return 0;
        }

        return -1;
    }
}