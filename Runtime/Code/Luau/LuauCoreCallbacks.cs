using Luau;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

public partial class LuauCore : MonoBehaviour
{

    private static LuauPlugin.PrintCallback printCallback_holder = new LuauPlugin.PrintCallback(printf);

    private LuauPlugin.GetPropertyCallback getPropertyCallback_holder;
    private LuauPlugin.SetPropertyCallback setPropertyCallback_holder;
    private LuauPlugin.CallMethodCallback callMethodCallback_holder;
    private LuauPlugin.ObjectGCCallback objectGCCallback_holder;
    private LuauPlugin.RequireCallback requireCallback_holder;
    private LuauPlugin.RequirePathCallback requirePathCallback_holder;
    private LuauPlugin.YieldCallback yieldCallback_holder;

    private struct AwaitingTask
    {
        public IntPtr Thread;
        public Task Task;
        public MethodInfo Method;
    }

    private struct EventConnection {
        public int id;
        public object target;
        public System.Delegate handler;
        public EventInfo eventInfo;
    }

    private static Dictionary<int, EventConnection> eventConnections = new();
    private static int eventIdCounter = 0;

    private static readonly List<AwaitingTask> _awaitingTasks = new();

    private void CreateCallbacks()
    {
        printCallback_holder = new LuauPlugin.PrintCallback(printf);
        getPropertyCallback_holder = new LuauPlugin.GetPropertyCallback(getProperty);
        setPropertyCallback_holder = new LuauPlugin.SetPropertyCallback(setProperty);
        callMethodCallback_holder = new LuauPlugin.CallMethodCallback(callMethod);
        objectGCCallback_holder = new LuauPlugin.ObjectGCCallback(objectGc);
        requireCallback_holder = new LuauPlugin.RequireCallback(requireCallback);
        requirePathCallback_holder = new LuauPlugin.RequirePathCallback(requirePathCallback);
        yieldCallback_holder = new LuauPlugin.YieldCallback(yieldCallback);
    }


    //when a lua thread prints something to console
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.PrintCallback))]
    static void printf(IntPtr thread, int style, IntPtr buffer, int length)
    {
        string res = LuauCore.PtrToStringUTF8(buffer, length);
        if (res == null)
        {
            return;
        }

        if (style == 1)
        {
            Debug.LogWarning(res, LuauCore._instance);
        }
        else if (style == 2)
        {
            Debug.LogError(res, LuauCore._instance);
            //If its an error, the thread is suspended 
            ThreadDataManager.Error(thread);
            //GetLuauDebugTrace(thread);
        }
        else
        {
            Debug.Log(res, LuauCore._instance);
        }
    }

    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.PrintCallback))]
    static int yieldCallback(IntPtr thread, IntPtr context)
    {
        LuauCore instance = LuauCore.Instance;
        instance.m_threads.TryGetValue(thread, out ScriptBinding binding);
        //Debug.Log("Thread " + thread + " waited");
        ThreadDataManager.SetThreadYielded(thread, true);

        if (binding != null)
        {
            //A luau binding called this, they can resume it
            binding.QueueCoroutineResume(thread);
        }
        else
        {
            //we have to resume it
            instance.m_currentBuffer.Add(thread);
        }

        return 0;
    }


    //when a lua thread gc releases an object, make sure our GC knows too
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.ObjectGCCallback))]
    static unsafe int objectGc(int instanceId, IntPtr objectDebugPointer)
    {
        ThreadDataManager.DeleteObjectReference(instanceId);
        //Debug.Log("GC " + instanceId + " ptr:" + objectDebugPointer);
        return 0;
    }


    //When a lua object wants to set a property
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.SetPropertyCallback))]
    static unsafe int setProperty(IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameLength, LuauCore.PODTYPE type, IntPtr propertyData, int propertyDataSize)
    {

        string propName = LuauCore.PtrToStringUTF8(propertyName, propertyNameLength, out ulong propNameHash);

        // Debug.Log("Setting property" + propName);
        //LuauBinding binding = LuauCore.Instance.m_threads[thread];

        //if (binding == null)
        //{
        //Debug.LogError("ERROR - setProperty thread " + thread + " did luaBinding get destroyed somehow while running it?");
        //return 0;
        //}

        System.Object objectReference = ThreadDataManager.GetObjectReference(thread, instanceId);

        if (objectReference != null)
        {
            Type sourceType = objectReference.GetType();
            Type t = null;

            PropertyInfo property = LuauCore.Instance.GetPropertyInfoForType(sourceType, propName, propNameHash);
            FieldInfo field = null;


            if (property != null)
            {
                t = property.PropertyType;
            }
            else
            {
                field = LuauCore.Instance.GetFieldInfoForType(sourceType, propName, propNameHash);
                if (field != null)
                {
                    t = field.FieldType;
                }
            }

            if (t == null)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("ERROR - (" + objectReference.GetType().Name + ")." + propName + " set property not found");
                GetLuauDebugTrace(thread);
                return 0;
            }

            switch (type)
            {
                case PODTYPE.POD_OBJECT:
                    {
                        int[] intData = new int[1];
                        Marshal.Copy(propertyData, intData, 0, 1);
                        int propertyInstanceId = intData[0];

                        System.Object propertyObjectRef = ThreadDataManager.GetObjectReference(thread, propertyInstanceId);

                        if (t.IsAssignableFrom(propertyObjectRef.GetType()))
                        {
                            if (field != null)
                            {
                                field.SetValue(objectReference, propertyObjectRef);
                            }
                            else
                            {
                                property.SetValue(objectReference, propertyObjectRef);
                            }
                            return 0;
                        }

                        break;
                    }

                case PODTYPE.POD_VECTOR3:
                    {
                        if (t.IsAssignableFrom(vector3Type))
                        {
                            if (field != null)
                            {
                                // Debug.Log(field);
                                Vector3 v = NewVector3FromPointer(propertyData);
                                field.SetValue(objectReference, v);
                            }
                            else
                            {
                                // Debug.Log(property);
                                Vector3 v = NewVector3FromPointer(propertyData);
                                property.SetValue(objectReference, v);
                            }
                            return 0;
                        }
                        if (t.IsAssignableFrom(vector3IntType))
                        {
                            if (field != null)
                            {
                                // Debug.Log(field);
                                Vector3 v = NewVector3FromPointer(propertyData);
                                field.SetValue(objectReference, Vector3Int.FloorToInt(v));
                            }
                            else
                            {
                                // Debug.Log(property);
                                Vector3 v = NewVector3FromPointer(propertyData);
                                property.SetValue(objectReference, Vector3Int.FloorToInt(v));
                            }
                            return 0;
                        }
                        break;
                    }
                case PODTYPE.POD_BOOL:
                    {
                        if (t.IsAssignableFrom(boolType))
                        {
                            int[] ints = new int[1];
                            Marshal.Copy(propertyData, ints, 0, 1);
                            bool val = ints[0] != 0;
                            
                            if (field != null)
                            {
                                field.SetValue(objectReference, val);
                            }
                            else
                            {
                                property.SetValue(objectReference, val);
                            }

                            return 0;
                        }

                        break;
                    }

                case PODTYPE.POD_DOUBLE: //Also integers
                    {
                        double[] doubles = new double[1];
                        Marshal.Copy(propertyData, doubles, 0, 1);
                        if (t.IsAssignableFrom(doubleType))
                        {
                            if (field != null)
                            {
                                field.SetValue(objectReference, (double)doubles[0]);
                            }
                            else
                            {
                                property.SetValue(objectReference, (double)doubles[0]);
                            }

                            return 0;
                        }
                        else
                        if (t.IsAssignableFrom(floatType))
                        {
                            if (field != null)
                            {
                                field.SetValue(objectReference, (System.Single)doubles[0]);
                            }
                            else
                            {
                                property.SetValue(objectReference, (System.Single)doubles[0]);
                            }
                            return 0;
                        }
                        else
                        if (t.IsAssignableFrom(intType) || t.BaseType == enumType || t.IsAssignableFrom(enumType) || t.IsAssignableFrom(byteType))
                        {
                            if (field != null)
                            {
                                field.SetValue(objectReference, (int)doubles[0]);
                            }
                            else
                            {
                                property.SetValue(objectReference, (int)doubles[0]);
                            }
                            return 0;
                        }
                        else if (t.IsAssignableFrom(uIntType))
                        {
                            if (field != null)
                            {
                                field.SetValue(objectReference, unchecked((int)doubles[0]));
                            }
                            else
                            {
                                property.SetValue(objectReference, unchecked((int)doubles[0]));
                            }
                        }

                        break;
                    }

                case PODTYPE.POD_STRING:
                    {
                        if (t.IsAssignableFrom(stringType))
                        {
                            string dataStr = LuauCore.PtrToStringUTF8NullTerminated(propertyData);
                            if (field != null)
                            {
                                field.SetValue(objectReference, dataStr);
                            }
                            else
                            {
                                property.SetValue(objectReference, dataStr);
                            }
                            return 0;
                        }
                        break;
                    }

                case PODTYPE.POD_NULL:
                    {
                        //nulling anything nullable
                        if (Nullable.GetUnderlyingType(t) != null)
                        {
                            if (field != null)
                            {
                                field.SetValue(objectReference, null);
                            }
                            else
                            {
                                property.SetValue(objectReference, null);
                            }
                            return 0;
                        }
                        break;
                    }

                case PODTYPE.POD_RAY:
                    {
                        if (t.IsAssignableFrom(rayType))
                        {
                            if (field != null)
                            {
                                field.SetValue(objectReference, NewRayFromPointer(propertyData));
                            }
                            else
                            {
                                property.SetValue(objectReference, NewRayFromPointer(propertyData));
                            }
                            return 0;
                        }
                        break;
                    }

                case PODTYPE.POD_COLOR:
                    {
                        if (t.IsAssignableFrom(colorType))
                        {
                            if (field != null)
                            {
                                field.SetValue(objectReference, NewColorFromPointer(propertyData));
                            }
                            else
                            {
                                property.SetValue(objectReference, NewColorFromPointer(propertyData));
                            }
                            return 0;
                        }
                        break;
                    }

                case PODTYPE.POD_PLANE:
                    {
                        if (t.IsAssignableFrom(planeType))
                        {
                            if (field != null)
                            {
                                field.SetValue(objectReference, NewPlaneFromPointer(propertyData));
                            }
                            else
                            {
                                property.SetValue(objectReference, NewPlaneFromPointer(propertyData));
                            }
                            return 0;
                        }
                        break;
                    }

                case PODTYPE.POD_QUATERNION:
                    {
                        if (t.IsAssignableFrom(quaternionType))
                        {
                            if (field != null)
                            {
                                field.SetValue(objectReference, NewQuaternionFromPointer(propertyData));
                            }
                            else
                            {
                                property.SetValue(objectReference, NewQuaternionFromPointer(propertyData));
                            }
                            return 0;
                        }
                        break;
                    }

                case PODTYPE.POD_MATRIX:
                    {
                        if (t.IsAssignableFrom(matrixType))
                        {
                            if (field != null)
                            {
                                field.SetValue(objectReference, NewMatrixFromPointer(propertyData));
                            }
                            else
                            {
                                property.SetValue(objectReference, NewMatrixFromPointer(propertyData));
                            }

                            return 0;
                        }
                        break;
                    }

                case PODTYPE.POD_BINARYBLOB:
                    {
                        if (t.IsAssignableFrom(binaryBlobType))
                        {
                            if (field != null)
                            {
                                field.SetValue(objectReference, NewBinaryBlobFromPointer(propertyData, propertyDataSize));
                            }
                            else
                            {
                                property.SetValue(objectReference, NewBinaryBlobFromPointer(propertyData, propertyDataSize));
                            }

                            return 0;
                        }
                        break;
                    }
            }

            //if we get here we didnt write it
            Debug.LogError("ERROR - " + objectReference.ToString() + "." + propName + " unable to set property of type " + t.Name + " with a " + type.ToString());
            ThreadDataManager.Error(thread);
            GetLuauDebugTrace(thread);
            return 0;
        }
        else
        {
            Debug.LogError("Error: InstanceId not currently available. InstanceId=" + instanceId + ", propName=" + propName);
            return 0;
        }

    }


    //When a lua object wants to get a property
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.GetPropertyCallback))]
    static unsafe int getProperty(IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameLength)
    {
        string propName = LuauCore.PtrToStringUTF8(propertyName, propertyNameLength, out ulong propNameHash);
        LuauCore instance = LuauCore.Instance;
        //LuauBinding binding = instance.m_threads[thread];
        //if (binding == null)
        //{
        //    Debug.LogError("ERROR - getProperty thread " + thread + " did luaBinding get destroyed somehow while running it?");
        //    return 0;
        //}

        //This detects STATIC classobjects only - live objects do not report the className
        if (classNameSize != 0)
        {

            string staticClassName = LuauCore.PtrToStringUTF8(classNamePtr, classNameSize);
            instance.unityAPIClasses.TryGetValue(staticClassName, out BaseLuaAPIClass staticClassApi);
            if (staticClassApi == null)
            {
                Debug.LogError("ERROR - type of " + staticClassName + " class not found");
                return 0;
            }
            Type objectType = staticClassApi.GetAPIType();


            PropertyInfo propertyInfo = objectType.GetProperty(propName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (propertyInfo != null)
            {
                Type t = propertyInfo.PropertyType;
                System.Object value = propertyInfo.GetValue(null);
                WritePropertyToThread(thread, value, t);
                return 1;
            }

            FieldInfo fieldInfo = objectType.GetField(propName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (fieldInfo != null)
            {
                Type t = fieldInfo.FieldType;
                System.Object value = fieldInfo.GetValue(null);
                WritePropertyToThread(thread, value, t);
                return 1;
            }

            Debug.LogError("ERROR - " + propName + " get property not found on " + staticClassName);
            return 0;
        }
        else
        {
            System.Object objectReference = ThreadDataManager.GetObjectReference(thread, instanceId);
            if (objectReference == null)
            {
                Debug.LogError("Error: InstanceId not currently available:" + instanceId + ". propName=" + propName);
                return 0;
            }
            Type sourceType = objectReference.GetType();

            // if (sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {
            //     print("checking " + propName);
            //     var method = sourceType.GetProperty(propName).GetGetMethod();
            //     var value = method.Invoke(sourceType, null);
            //     Type t = value.GetType();
            //     WritePropertyToThread(thread, value, t);
            //     return 1;
            //
            //     // Type keyType = sourceType.GetGenericArguments()[0];
            //     // Type valueType = sourceType.GetGenericArguments()[1];
            //     // var dict = sourceType as Dictionary<>;
            //     // System.Object value = 
            //     
            // }


            //Check the dictionary
            PropertyInfo property = instance.GetPropertyInfoForType(sourceType, propName, propNameHash);
            if (property != null)
            {
                // Debug.Log("Found property: " + propName);
                Type t = property.PropertyType;
                try
                {
                    System.Object value = property.GetValue(objectReference);
                    if (value != null)
                    {
                        WritePropertyToThread(thread, value, t);
                        return 1;
                    }
                    else
                    {
                        // Debug.Log("Value was null in dictionary. propName=" + propName + ", object=" + sourceType.Name);
                        WritePropertyToThread(thread, null, null);
                        return 1;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Failed to get property in dictionary. propName=" + propName + ", object=" + sourceType.Name + ", msg=" + e.Message);
                    return 0;
                }
            }

            // Handle case of dictionary direct access
            // example:
            // local t = dict[1]
            var dict = objectReference as IDictionary;
            if (dict != null)
            {
                if (int.TryParse(propName, out int keyInt))
                {
                    if (dict.Contains(keyInt))
                    {
                        object value = dict[keyInt];
                        Type t = value.GetType();
                        WritePropertyToThread(thread, value, t);
                        return 1;
                    }
                    else
                    {
                        Debug.Log("[Luau]: Dictionary had key but value was null. propName=" + propName + ", sourceType=" + sourceType.Name);
                        WritePropertyToThread(thread, null, null);
                        return 1;
                    }
                }

                if (dict.Contains(propName))
                {
                    object value = dict[propName];
                    Type t = value.GetType();
                    WritePropertyToThread(thread, value, t);
                    return 1;
                }
                else
                {
                    Debug.Log("[Luau]: Dictionary was found but key was not found. propName=" + propName + ", sourceType=" + sourceType.Name);
                    WritePropertyToThread(thread, null, null);
                    return 1;
                }
            }

            FieldInfo field = instance.GetFieldInfoForType(sourceType, propName, propNameHash);
            if (field != null)
            {
                Type t = field.FieldType;
                System.Object value = field.GetValue(objectReference);
                WritePropertyToThread(thread, value, t);
                return 1;
            }

            Debug.LogError("ERROR - (" + sourceType.Name + ")." + propName + " property/field not found");
            return 0;
        }
    }


    //Take a random path name from a require and transform it into its path relative to /assets/.
    //The same file always gets the same path, so this is used as a key to return the same table every time from lua land
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.RequireCallback))]
    static unsafe int requirePathCallback(IntPtr thread, IntPtr fileName, int fileNameSize)
    {

        string fileNameStr = LuauCore.PtrToStringUTF8(fileName, fileNameSize);

        if (fileNameStr.Contains("/") == false)
        {
            LuauCore core = LuauCore.Instance;
            core.m_threads.TryGetValue(thread, out ScriptBinding binding);

            if (binding)
            {
                //Get a stripped name
                string fname = GetTidyPathName(binding.m_fileFullPath);

                //Remove just this filename off the end
                List<string> bits = new(fname.Split("/"));
                bits.RemoveAt(bits.Count - 1);
                string bindingPath = Path.Combine(bits.ToArray());

                fileNameStr = bindingPath + "/" + fileNameStr;
            }
        }
        else
        if (fileNameStr.StartsWith("./"))
        {
            LuauCore core = LuauCore.Instance;
            core.m_threads.TryGetValue(thread, out ScriptBinding binding);

            if (binding)
            {
                //Get a stripped name
                string fname = GetTidyPathName(binding.m_fileFullPath);

                //Remove just this filename off the end
                List<string> bits = new(fname.Split("/"));
                bits.RemoveAt(bits.Count - 1);
                string bindingPath = Path.Combine(bits.ToArray());

                fileNameStr = bindingPath + "/" + fileNameStr.Substring(2);
            }
        }
        else if (fileNameStr.StartsWith("../"))
        {
            LuauCore core = LuauCore.Instance;
            core.m_threads.TryGetValue(thread, out ScriptBinding binding);

            if (binding)
            {
                //Get a stripped name
                string fname = GetTidyPathName(binding.m_fileFullPath);

                //Remove two bits of this filename off the end
                List<string> bits = new(fname.Split("/"));
                if (bits.Count > 0) { bits.RemoveAt(bits.Count - 1); }
                if (bits.Count > 0) { bits.RemoveAt(bits.Count - 1); }

                string bindingPath = Path.Combine(bits.ToArray());

                fileNameStr = bindingPath + "/" + fileNameStr.Substring(2);
            }
        }

        //Fully qualify it
        fileNameStr = GetTidyPathName(fileNameStr);

        LuauCore.WritePropertyToThread(thread, fileNameStr, typeof(string));
        return 1;
    }


    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.RequireCallback))]
    static unsafe IntPtr requireCallback(IntPtr thread, IntPtr fileName, int fileNameSize)
    {

        string fileNameStr = LuauCore.PtrToStringUTF8(fileName, fileNameSize);
        // Debug.Log("require " + fileNameStr);

        GameObject obj = new GameObject();
        obj.name = "require(" + fileNameStr + ")";
        ScriptBinding newBinding = obj.AddComponent<ScriptBinding>();

        if (newBinding.CreateThread(fileNameStr) == false)
        {
            ThreadDataManager.Error(thread);
            Debug.LogError("Error require(" + fileNameStr + ") not found.");
            GetLuauDebugTrace(thread);
            return IntPtr.Zero;
        }

        if (newBinding.m_error == true)
        {
            ThreadDataManager.Error(thread);
            Debug.LogError("Error trying to execute module script during require for " + fileNameStr);
            GetLuauDebugTrace(thread);
            return IntPtr.Zero;
        }
        if (newBinding.m_canResume == true)
        {
            ThreadDataManager.Error(thread);
            Debug.LogError("Require() yielded; did not return with a table for " + fileNameStr);
            GetLuauDebugTrace(thread);
            return IntPtr.Zero;
        }

        return newBinding.m_thread;
    }

    public static void DisconnectEvent(int eventId) {
        if (eventConnections.TryGetValue(eventId, out var eventConnection)) {
            eventConnection.eventInfo.RemoveEventHandler(eventConnection.target, eventConnection.handler);
            eventConnections.Remove(eventId);
            Debug.Log("Disconnected eventId " + eventId);
        }
    }

    //When a lua object wants to call a method..
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.CallMethodCallback))]
    static unsafe int callMethod(IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr methodNamePtr, int methodNameLength, int numParameters, IntPtr firstParameterType, IntPtr firstParameterData, IntPtr firstParameterSize, IntPtr shouldYield)
    {
        Marshal.WriteInt32(shouldYield, 0);

        string methodName = LuauCore.PtrToStringUTF8(methodNamePtr, methodNameLength);
        string staticClassName = LuauCore.PtrToStringUTF8(classNamePtr, classNameSize);

        LuauCore instance = LuauCore.Instance;

        System.Object reflectionObject = null;
        Type type = null;

        //Cast/marshal parameter data
        IntPtr[] parameterDataPtrs = new IntPtr[numParameters];
        Marshal.Copy(firstParameterData, parameterDataPtrs, 0, numParameters);
        int[] paramaterDataSizes = new int[numParameters];
        Marshal.Copy(firstParameterSize, paramaterDataSizes, 0, numParameters);
        int[] parameterDataPODTypes = new int[numParameters];
        Marshal.Copy(firstParameterType, parameterDataPODTypes, 0, numParameters);

        //This detects STATIC classobjects only - live objects do not report the className
        instance.unityAPIClasses.TryGetValue(staticClassName, out BaseLuaAPIClass staticClassApi);
        if (staticClassApi != null)
        {
            type = staticClassApi.GetAPIType();
            //This handles where we need to replace a method or implement a method directly in the c# side eg: GameObject.new 
            int retValue = staticClassApi.OverrideStaticMethod(thread, methodName, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (retValue >= 0)
            {
                return retValue;
            }

            //Oh, its a constructor!
            if (methodName == "New")
            {
                return RunConstructor(thread, type, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            }
        }

        if (type == null)
        {
            reflectionObject = ThreadDataManager.GetObjectReference(thread, instanceId);

            if (reflectionObject == null)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: InstanceId not currently available for " + instanceId + " " + methodName + " " + staticClassName + " (0x" + thread + ")");
                GetLuauDebugTrace(thread);
                return 0;
            }
            
            type = reflectionObject.GetType();
        }
        
        if (reflectionObject != null)
        {
            //See if we have any custom methods implemented for this type?
            instance.unityAPIClassesByType.TryGetValue(type, out BaseLuaAPIClass valueTypeAPI);
            if (valueTypeAPI != null)
            {
                int retValue = valueTypeAPI.OverrideMemberMethod(thread, reflectionObject, methodName, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
                if (retValue >= 0)
                {
                    return retValue;
                }
            }
            
            //Check to see if this was an event (OnEventname)  
            if (methodName.ToLower().StartsWith("on") && methodName.Length > 2)
            {
                EventInfo eventInfo = type.GetRuntimeEvent(methodName.Substring(2));
                if (eventInfo == null)
                {
                    eventInfo = type.GetRuntimeEvent(methodName);
                }
                if (eventInfo == null)
                {
                    string firstLetter = methodName.Substring(2, 1);
                    string name = firstLetter.ToLower() + methodName.Substring(3);
                    eventInfo = type.GetRuntimeEvent(name);
                }

                if (eventInfo != null)
                {
                    //There is an event
                    if (numParameters != 1)
                    {
                        ThreadDataManager.Error(thread);
                        Debug.LogError("Error: " + methodName + " takes 1 parameter (a function!)");
                        GetLuauDebugTrace(thread);
                        return 0;
                    }
                    if (parameterDataPODTypes[0] != (int)LuauCore.PODTYPE.POD_LUAFUNCTION)
                    {
                        ThreadDataManager.Error(thread);
                        Debug.LogError("Error: " + methodName + " parameter must be a function");
                        GetLuauDebugTrace(thread);
                        return 0;
                    }

                    int handle = GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
                    ParameterInfo[] eventInfoParams = eventInfo.EventHandlerType.GetMethod("Invoke").GetParameters();

                    foreach (ParameterInfo param in eventInfoParams)
                    {
                        if (param.ParameterType.IsValueType == true && param.ParameterType.IsPrimitive == false)
                        {
                            Debug.LogError("Warning: " + methodName + " parameter " + param.Name + " is a struct, which won't work with GC without you manually pinning it. Try changing it to a class or wrapping it in a class.");
                            return 0;
                        }
                    }

                    //grab the correct one for the number of parameters
                    var callbackWrapper = ThreadDataManager.RegisterCallback(thread, reflectionObject, handle, methodName);
                    string reflectionMethodName = "HandleEventDelayed" + eventInfoParams.Length.ToString();
                    MethodInfo method = callbackWrapper.GetType().GetMethod(reflectionMethodName);

                    Delegate d = Delegate.CreateDelegate(eventInfo.EventHandlerType, callbackWrapper, method);
                    eventInfo.AddEventHandler(reflectionObject, d);

                    int eventConnectionId = eventIdCounter;
                    eventIdCounter++;
                    EventConnection eventConnection = new EventConnection() {
                        id = eventConnectionId,
                        target = reflectionObject,
                        handler = d,
                        eventInfo = eventInfo,
                    };
                    eventConnections.Add(eventConnectionId, eventConnection);

                    LuauCore.WritePropertyToThread(thread, eventConnectionId, typeof(int));
                    return 1;
                }
            }
        }

        //Use reflection to try and find the method now
        bool countFound = false;
        bool nameFound = false;
        ParameterInfo[] finalParameters = null;
        MethodInfo finalMethod = null;

        object[] podObjects = UnrollPodObjects(thread, numParameters, parameterDataPODTypes, parameterDataPtrs);

        Profiler.BeginSample("LuauCore.FindMethod");
        FindMethod(type, methodName, numParameters, parameterDataPODTypes, podObjects, out nameFound, out countFound, out finalParameters, out finalMethod, out var finalExtensionMethod);
        Profiler.EndSample();

        if (finalMethod == null)
        {
            if (nameFound == false)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: Method " + methodName + " not found on " + type.Name + "(" + instanceId + ")");
                GetLuauDebugTrace(thread);
            }
            else
            if (nameFound == true && countFound == false)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: No version of " + methodName + " on " + type.Name + "(" + instanceId + ") takes " + numParameters + " parameters.");
                GetLuauDebugTrace(thread);
            }
            else
            if (nameFound == true && countFound == true)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: Method " + methodName + " could not match parameter types on " + type.Name + "(" + instanceId + ")");
                GetLuauDebugTrace(thread);
            }

            return 0;
        }


        object[] parsedData = null;
        bool success = ParseParameterData(thread, numParameters, parameterDataPtrs, parameterDataPODTypes, finalParameters, paramaterDataSizes, podObjects, out parsedData);
        if (success == false)
        {
            ThreadDataManager.Error(thread);
            Debug.LogError("Error: Unable to parse parameters for " + type.Name + " " + finalMethod.Name);
            GetLuauDebugTrace(thread);
            return 0;
        }

        //We have parameters
        object returnValue;
        object invokeObj = reflectionObject;

        var returnCount = 1;
        for (var j = 0; j < finalParameters.Length; j++)
        {
            if (finalParameters[j].IsOut)
            {
                returnCount += 1;
            }
        }

        if (finalExtensionMethod)
        {
            invokeObj = null;
            parsedData = parsedData.Prepend(reflectionObject).ToArray();
        }

        // Async method
        if (finalMethod.ReturnType == typeof(Task) || (finalMethod.ReturnType.IsGenericType &&
                                                       finalMethod.ReturnType.GetGenericTypeDefinition() ==
                                                       typeof(Task<>)))
        {
            var shouldYieldBool = InvokeMethodAsync(thread, type, finalMethod, invokeObj, parsedData);
            Marshal.WriteInt32(shouldYield, shouldYieldBool ? 1 : 0);
            return returnCount;
        }

        try
        {
            returnValue = finalMethod.Invoke(invokeObj, parsedData);
        }
        catch (Exception e)
        {
            ThreadDataManager.Error(thread);
            Debug.LogError("Error: Exception thrown in " + type.Name + " " + finalMethod.Name + " " + e.Message);
            Debug.LogError(e);
            GetLuauDebugTrace(thread);
            return 0;
        }

        WriteMethodReturnValuesToThread(thread, type, finalMethod.ReturnType, finalParameters, returnValue, parsedData);
        return returnCount;
    }

    private static void WriteMethodReturnValuesToThread(IntPtr thread, Type type, Type returnType, ParameterInfo[] finalParameters, object returnValue, object[] parsedData)
    {
        if (type.IsSZArray == true)
        {
            //When returning array types, finalMethod.ReturnType is wrong
            returnType = type.GetElementType();
        }
        //Write the final param
        WritePropertyToThread(thread, returnValue, returnType);

        //Write the out params
        for (var j = 0; j < finalParameters.Length; j++)
        {
            if (finalParameters[j].IsOut)
            {
                WritePropertyToThread(thread, parsedData[j], finalParameters[j].ParameterType.GetElementType());
            }
        }
    }

    private static bool InvokeMethodAsync(IntPtr thread, Type type, MethodInfo method, object obj, object[] parameters)
    {
        try
        {
            var task = (Task)method.Invoke(obj, parameters);
            var awaitingTask = new AwaitingTask
            {
                Thread = thread,
                Task = task,
                Method = method,
            };

            if (task.IsCompleted)
            {
                ResumeAsyncTask(awaitingTask, true);
                return false;
            }

            _awaitingTasks.Add(awaitingTask);

            LuauCore.Instance.m_threads.TryGetValue(thread, out var binding);
            if (binding != null)
            {
                binding.m_asyncYield = true;
            }
            else
            {
                LuauPlugin.LuauPinThread(thread);
            }

            ThreadDataManager.SetThreadYielded(thread, true);

            return true;
        }
        catch (Exception e)
        {
            ThreadDataManager.Error(thread);
            Debug.LogError("Error: Exception thrown in " + type.Name + " " + method.Name + " " + e.Message);
            Debug.LogError(e);
            GetLuauDebugTrace(thread);
            return false;
        }
    }

    private static void ResumeAsyncTask(AwaitingTask awaitingTask, bool immediate = false)
    {
        var thread = awaitingTask.Thread;

        if (!immediate)
        {
            ThreadDataManager.SetThreadYielded(thread, false);
        }

        LuauCore.Instance.m_threads.TryGetValue(thread, out var binding);

        if (binding == null)
        {
            LuauPlugin.LuauUnpinThread(thread);
        }

        if (awaitingTask.Task.IsFaulted)
        {
            ThreadDataManager.Error(thread);
            Debug.LogException(awaitingTask.Task.Exception);
            GetLuauDebugTrace(thread);
            return;
        }

        var nArgs = 0;

        var retType = awaitingTask.Method.ReturnType;
        if (retType.IsGenericType && retType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            nArgs = 1;
            var resPropInfo = retType.GetProperty("Result")!;
            var resValue = resPropInfo.GetValue(awaitingTask.Task);
            var resType = resValue.GetType();
            WritePropertyToThread(thread, resValue, resType);
        }

        if (!immediate)
        {
            var result = LuauPlugin.LuauRunThread(thread, nArgs);
            if (binding != null)
            {
                binding.m_asyncYield = false;
                binding.m_canResume = result == 1;
            }
        }
    }

    public static void TryResumeAsyncTasks()
    {
        for (var i = _awaitingTasks.Count - 1; i >= 0; i--)
        {
            var awaitingTask = _awaitingTasks[i];
            if (!awaitingTask.Task.IsCompleted) continue;

            // Task has completed. Remove from list and resume lua thread:
            _awaitingTasks.RemoveAt(i);
            ResumeAsyncTask(awaitingTask);
        }
    }

    /// Get the string representation of a Lua thread in the same format that Lua would print a thread.
    private static string LuaThreadToString(IntPtr thread)
    {
        return $"thread: 0x{thread.ToInt64():x16}";
    }

    private static void GetLuauDebugTrace(IntPtr thread)
    {
        //Call this to get a bunch of prints of the current thread execution state
        LuauPlugin.LuauGetDebugTrace(thread);
    }
}