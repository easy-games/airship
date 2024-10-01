using Luau;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Code.Luau;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Profiling;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using System.Text.RegularExpressions;
#endif

public partial class LuauCore : MonoBehaviour {
    /// The Luau context from the most recent call from the Luau plugin.
    public static LuauContext CurrentContext = LuauContext.Game;

    private static LuauPlugin.PrintCallback printCallback_holder = printf;
    
    private LuauPlugin.ComponentSetEnabledCallback componentSetEnabledCallback_holder;
    private LuauPlugin.GetPropertyCallback getPropertyCallback_holder;
    private LuauPlugin.SetPropertyCallback setPropertyCallback_holder;
    private LuauPlugin.CallMethodCallback callMethodCallback_holder;
    private LuauPlugin.ObjectGCCallback objectGCCallback_holder;
    private LuauPlugin.RequireCallback requireCallback_holder;
    private LuauPlugin.ConstructorCallback constructorCallback_holder;
    private LuauPlugin.RequirePathCallback requirePathCallback_holder;
    private LuauPlugin.YieldCallback yieldCallback_holder;
    private LuauPlugin.ToStringCallback toStringCallback_holder;
    private LuauPlugin.ToggleProfilerCallback toggleProfilerCallback_holder;
    

    private struct AwaitingTask
    {
        public IntPtr Thread;
        public Task Task;
        public MethodInfo Method;
        public LuauContext Context;
    }

    private struct PropertyGetReflectionCache
    {
        public Type t;
        [FormerlySerializedAs("pi")] public PropertyInfo propertyInfo;
        // public Func<object, object> getProperty;
    }
    public struct EventConnection {
        public int id;
        public object target;
        public System.Delegate handler;
        public EventInfo eventInfo;
    }

    private static Dictionary<(Type, string), PropertyGetReflectionCache> propertyGetCache = new();
    
    public static Dictionary<int, EventConnection> eventConnections = new();
    private static int eventIdCounter = 0;

    private static readonly List<AwaitingTask> _awaitingTasks = new();

    public static GameObject luauModulesFolder;

    private void CreateCallbacks()
    {
        printCallback_holder = new LuauPlugin.PrintCallback(printf);
        getPropertyCallback_holder = new LuauPlugin.GetPropertyCallback(getProperty);
        setPropertyCallback_holder = new LuauPlugin.SetPropertyCallback(setProperty);
        callMethodCallback_holder = new LuauPlugin.CallMethodCallback(callMethod);
        objectGCCallback_holder = new LuauPlugin.ObjectGCCallback(objectGc);
        requireCallback_holder = new LuauPlugin.RequireCallback(requireCallback);
        constructorCallback_holder = new LuauPlugin.ConstructorCallback(constructorCallback);
        requirePathCallback_holder = new LuauPlugin.RequirePathCallback(requirePathCallback);
        yieldCallback_holder = new LuauPlugin.YieldCallback(yieldCallback);
        toStringCallback_holder = new LuauPlugin.ToStringCallback(toStringCallback);
        componentSetEnabledCallback_holder = new LuauPlugin.ComponentSetEnabledCallback(SetComponentEnabled);
        toggleProfilerCallback_holder = new LuauPlugin.ToggleProfilerCallback(ToggleProfilerCallback);
    }

    private static int LuauError(IntPtr thread, string err) {
        LuauPlugin.LuauPushCsError(err);
        ThreadDataManager.Error(thread);
        return -1;
    }

#if UNITY_EDITOR
    private static string InjectAnchorLinkToLuaScript(string logMessage) {
        // e.g. "path/to/my/script.lua:10: an error occurred"
        var rx = new Regex(@"(\S+\.lua):(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        return rx.Replace(logMessage, (m) => {
            var scriptPath = m.Groups[1].Value;
            var line = m.Groups[2].Value;
            
            return $"<a href=\"#\" file=\"out://{scriptPath}\" line=\"{line}\" column=\"0\">{scriptPath}:{line}</a>";
        });
    }
#endif


    //when a lua thread prints something to console
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.PrintCallback))]
    static void printf(LuauContext context, IntPtr thread, int style, int gameObjectId, IntPtr buffer, int length, IntPtr ptr) {
        CurrentContext = context;
        
        string res = LuauCore.PtrToStringUTF8(buffer, length);
        if (res == null) {
            LuauPlugin.LuauFreeString(ptr);
            return;
        }
        
#if UNITY_EDITOR
        if (style == 1 || style == 2) {
            if (res.Contains(".lua:")) {
                res = InjectAnchorLinkToLuaScript(res);
            }
        }
#endif

        UnityEngine.Object logContext = _coreInstance;
        if (gameObjectId >= 0) {
            var obj = ThreadDataManager.GetObjectReference(thread, gameObjectId, true);
            if (obj is UnityEngine.Object unityObj) {
                logContext = unityObj;
            }
        }

        if (style == 1) {
            Debug.LogWarning(res, logContext);
        } else if (style == 2) {
            Debug.LogError(res, logContext);
            //If its an error, the thread is suspended 
            ThreadDataManager.Error(thread);
            //GetLuauDebugTrace(thread);
        } else {
            Debug.Log(res, logContext);
        }
        
        LuauPlugin.LuauFreeString(ptr);
    }

    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.YieldCallback))]
    static int yieldCallback(LuauContext luauContext, IntPtr thread, IntPtr context, IntPtr trace, int traceSize) {
        CurrentContext = luauContext;
        
        var state = LuauState.FromContext(luauContext);
        state.TryGetScriptBindingFromThread(thread, out var binding);

        var res = LuauCore.PtrToStringUTF8(trace, traceSize);
        
        ThreadDataManager.SetThreadYielded(thread, true);

        if (binding != null) {
            //A luau binding called this, they can resume it
            binding.QueueCoroutineResume(thread);
        } else {
            //we have to resume it
            // instance.m_currentBuffer.Add(new CallbackRecord(thread, res));
            state.AddCallbackToBuffer(thread, res);
        }

        return 0;
    }

    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.ToStringCallback))]
    static void toStringCallback(IntPtr thread, int instanceId, IntPtr str, int maxLen, out int len) {
        var obj = ThreadDataManager.GetObjectReference(thread, instanceId, true, true);
        
        var toString = obj != null ? obj.ToString() : "null";
        
        var bytes = Encoding.UTF8.GetBytes(toString);
        len = bytes.Length > maxLen ? maxLen : bytes.Length;

        Marshal.Copy(bytes, 0, str, len);
    }
    
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.ToggleProfilerCallback))]
    static void ToggleProfilerCallback(int componentId, IntPtr strPtr, int strLen) {
        // Disable
        if (componentId == -1) {
            Profiler.EndSample();
            return;
        }

        if (AirshipComponent.componentIdToScriptName.TryGetValue(componentId, out var componentName)) {
            if (strLen > 0) {
                var str = PtrToStringUTF8(strPtr, strLen);
                Profiler.BeginSample($"{componentName}{str}");
                LuauPlugin.LuauFreeString(strPtr);
            }
            else {
                Profiler.BeginSample($"{componentName}");
            }
        }
    }

    //when a lua thread gc releases an object, make sure our GC knows too
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.ObjectGCCallback))]
    static unsafe int objectGc(int instanceId, IntPtr objectDebugPointer) {
        ThreadDataManager.DeleteObjectReference(instanceId);
        //Debug.Log("GC " + instanceId + " ptr:" + objectDebugPointer);
        return 0;
    }


    //When a lua object wants to set a property
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.SetPropertyCallback))]
    static unsafe int setProperty(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameLength, LuauCore.PODTYPE type, IntPtr propertyData, int propertyDataSize) {
        CurrentContext = context;
        
        string propName = LuauCore.PtrToStringUTF8(propertyName, propertyNameLength, out ulong propNameHash);
        
        // Debug.Log("Setting property" + propName);
        //LuauBinding binding = LuauCore.Instance.m_threads[thread];

        //if (binding == null)
        //{
        //Debug.LogError("ERROR - setProperty thread " + thread + " did luaBinding get destroyed somehow while running it?");
        //return 0;
        //}

        object objectReference = null;
        Type sourceType = null;
        if (classNameSize != 0) {
            string staticClassName = LuauCore.PtrToStringUTF8(classNamePtr, classNameSize);
            LuauCore.CoreInstance.unityAPIClasses.TryGetValue(staticClassName, out BaseLuaAPIClass staticClassApi);
            if (staticClassApi == null) {
                return LuauError(thread, "ERROR - type of " + staticClassName + " class not found");
            }
            sourceType = staticClassApi.GetAPIType();
        } else {
            objectReference = ThreadDataManager.GetObjectReference(thread, instanceId);
            sourceType = objectReference.GetType();
        }

        if (objectReference != null || classNameSize != 0) {
            // Scene Protection
            if (context != LuauContext.Protected) {
                if (sourceType == typeof(GameObject)) {
                    var target = (GameObject) objectReference;
                    if (IsAccessBlocked(context, target)) {
                        return target != null ?
                            LuauError(thread, "[Airship] Access denied when trying to set property " + target.name + "." + propName) :
                            LuauError(thread, "[Airship] Access denied when trying to set property (unknown)." + propName);
                    }
                } else if (sourceType.IsSubclassOf(typeof(Component)) || sourceType == typeof(Component)) {
                    var target = (Component) objectReference;
                    if (target != null && target.gameObject != null && IsAccessBlocked(context, target.gameObject)) {
                        return LuauError(thread, "[Airship] Access denied when trying to set property " + target.name + "." + propName);
                    }
                }
            }

            _coreInstance.unityAPIClassesByType.TryGetValue(sourceType, out var valueTypeAPI);

            Type t = null;
            PropertyInfo property = null;
            FieldInfo field = null;
            
            if (classNameSize != 0) {
                property = sourceType.GetProperty(propName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            } else {
                property = LuauCore.CoreInstance.GetPropertyInfoForType(sourceType, propName, propNameHash);
            }

            if (property != null) {
                t = property.PropertyType;
            } else {
                if (classNameSize != 0) {
                    field = sourceType.GetField(propName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                } else {
                    field = LuauCore.CoreInstance.GetFieldInfoForType(sourceType, propName, propNameHash);
                }
                
                if (field != null) {
                    t = field.FieldType;
                }
            }

            if (t == null) {
                return LuauError(thread, "ERROR - (" + sourceType.Name + ")." + propName + " set property not found");
            }

            if (printReferenceAssemblies) {
                referencedAssemblies.Add(sourceType.Assembly.FullName);
            }

            if (valueTypeAPI != null) {
                var retValue = valueTypeAPI.OverrideMemberSetter(context, thread, objectReference, propName, type, propertyData,
                    propertyDataSize);
                if (retValue >= 0) {
                    return retValue;
                }
            }

            switch (type) {
                case PODTYPE.POD_OBJECT: {
                    int[] intData = new int[1];
                    Marshal.Copy(propertyData, intData, 0, 1);
                    int propertyInstanceId = intData[0];

                    System.Object propertyObjectRef = ThreadDataManager.GetObjectReference(thread, propertyInstanceId);

                    if (t.IsAssignableFrom(propertyObjectRef.GetType())) {
                        if (
                            propName == "parent"
                            && context != LuauContext.Protected
                            && objectReference.GetType() == typeof(Transform)
                            && propertyObjectRef.GetType() == typeof(Transform)
                        ) {
                            var targetTransform = (Transform)objectReference;
                            if (IsProtectedScene(targetTransform.gameObject.scene.name)) {
                                return LuauError(thread, "[Airship] Access denied when trying to set parent of protected object " + targetTransform.gameObject.name);
                            }

                            var valueTransform = (Transform)propertyObjectRef;
                            if (IsProtectedScene(valueTransform.gameObject.scene.name)) {
                                return LuauError(thread, "[Airship] Access denied when trying to set parent of " + targetTransform.gameObject.name + " to a child of scene " + valueTransform.gameObject.scene.name);
                            }
                        }
                        
                        if (field != null) {
                            field.SetValue(objectReference, propertyObjectRef);
                        } else {
                            property.SetValue(objectReference, propertyObjectRef);
                        }
                        return 0;
                    }

                    break;
                }

                case PODTYPE.POD_VECTOR3: {
                    if (t.IsAssignableFrom(vector3Type)) {
                        if (field != null) {
                            // Debug.Log(field);
                            Vector3 v = NewVector3FromPointer(propertyData);
                            field.SetValue(objectReference, v);
                        } else {
                            // Debug.Log(property);
                            Vector3 v = NewVector3FromPointer(propertyData);
                            property.SetValue(objectReference, v);
                        }
                        return 0;
                    }
                    if (t.IsAssignableFrom(vector3IntType)) {
                        if (field != null) {
                            // Debug.Log(field);
                            Vector3 v = NewVector3FromPointer(propertyData);
                            field.SetValue(objectReference, Vector3Int.FloorToInt(v));
                        } else {
                            // Debug.Log(property);
                            Vector3 v = NewVector3FromPointer(propertyData);
                            property.SetValue(objectReference, Vector3Int.FloorToInt(v));
                        }
                        return 0;
                    }
                    break;
                }
                case PODTYPE.POD_BOOL: {
                    if (t.IsAssignableFrom(boolType)) {
                        int[] ints = new int[1];
                        Marshal.Copy(propertyData, ints, 0, 1);
                        bool val = ints[0] != 0;
                        
                        if (field != null) {
                            field.SetValue(objectReference, val);
                        } else {
                            property.SetValue(objectReference, val);
                        }

                        return 0;
                    }

                    break;
                }

                case PODTYPE.POD_DOUBLE: { // Also integers
                    double[] doubles = new double[1];
                    Marshal.Copy(propertyData, doubles, 0, 1);
                    if (t.IsAssignableFrom(doubleType)) {
                        if (field != null) {
                            field.SetValue(objectReference, (double)doubles[0]);
                        } else {
                            property.SetValue(objectReference, (double)doubles[0]);
                        }

                        return 0;
                    } else if (t.IsAssignableFrom(ushortType)) {
                        if (field != null) {
                            field.SetValue(objectReference, (ushort) doubles[0]);
                        } else {
                            property.SetValue(objectReference, (ushort) doubles[0]);
                        }

                        return 0;
                    } else if (t.IsAssignableFrom(floatType)) {
                        if (field != null) {
                            field.SetValue(objectReference, (System.Single)doubles[0]);
                        } else {
                            property.SetValue(objectReference, (System.Single)doubles[0]);
                        }
                        return 0;
                    } else if (t.IsAssignableFrom(intType) || t.BaseType == enumType || t.IsAssignableFrom(enumType) || t.IsAssignableFrom(byteType)) {
                        if (field != null) {
                            field.SetValue(objectReference, (int)doubles[0]);
                        } else {
                            property.SetValue(objectReference, (int)doubles[0]);
                        }
                        return 0;
                    } else if (t.IsAssignableFrom(uIntType)) {
                        if (field != null) {
                            field.SetValue(objectReference, unchecked((int)doubles[0]));
                        } else {
                            property.SetValue(objectReference, unchecked((int)doubles[0]));
                        }
                    } else if (t.IsAssignableFrom(longType)) {
                        if (field != null) {
                            field.SetValue(objectReference, (long)doubles[0]);
                        } else {
                            property.SetValue(objectReference, (long)doubles[0]);
                        }
                        return 0;
                    } else if (t.IsAssignableFrom(uLongType)) {
                        if (field != null) {
                            field.SetValue(objectReference, (ulong)doubles[0]);
                        } else {
                            property.SetValue(objectReference, (ulong)doubles[0]);
                        }
                        return 0;
                    }

                    break;
                }

                case PODTYPE.POD_STRING: {
                    if (t.IsAssignableFrom(stringType)) {
                        string dataStr = LuauCore.PtrToStringUTF8NullTerminated(propertyData);
                        if (field != null) {
                            field.SetValue(objectReference, dataStr);
                        } else {
                            property.SetValue(objectReference, dataStr);
                        }
                        return 0;
                    }
                    break;
                }

                case PODTYPE.POD_NULL: {
                    //nulling anything nullable
                    if (Nullable.GetUnderlyingType(t) != null) {
                        if (field != null) {
                            field.SetValue(objectReference, null);
                        } else {
                            property.SetValue(objectReference, null);
                        }
                        return 0;
                    }
                    break;
                }

                case PODTYPE.POD_RAY: {
                    if (t.IsAssignableFrom(rayType)) {
                        if (field != null) {
                            field.SetValue(objectReference, NewRayFromPointer(propertyData));
                        } else {
                            property.SetValue(objectReference, NewRayFromPointer(propertyData));
                        }
                        return 0;
                    }
                    break;
                }

                case PODTYPE.POD_COLOR: {
                    if (t.IsAssignableFrom(colorType)) {
                        if (field != null) {
                            field.SetValue(objectReference, NewColorFromPointer(propertyData));
                        } else {
                            property.SetValue(objectReference, NewColorFromPointer(propertyData));
                        }
                        return 0;
                    }
                    break;
                }

                case PODTYPE.POD_PLANE: {
                    if (t.IsAssignableFrom(planeType)) {
                        if (field != null) {
                            field.SetValue(objectReference, NewPlaneFromPointer(propertyData));
                        } else {
                            property.SetValue(objectReference, NewPlaneFromPointer(propertyData));
                        }
                        return 0;
                    }
                    break;
                }

                case PODTYPE.POD_QUATERNION: {
                    if (t.IsAssignableFrom(quaternionType)) {
                        if (field != null) {
                            field.SetValue(objectReference, NewQuaternionFromPointer(propertyData));
                        } else {
                            property.SetValue(objectReference, NewQuaternionFromPointer(propertyData));
                        }
                        return 0;
                    }
                    break;
                }

                case PODTYPE.POD_VECTOR2: {
                    if (t.IsAssignableFrom(vector2Type)) {
                        if (field != null) {
                            field.SetValue(objectReference, NewVector2FromPointer(propertyData));
                        } else {
                            property.SetValue(objectReference, NewVector2FromPointer(propertyData));
                        }
                        return 0;
                    }
                    break;
                }

                case PODTYPE.POD_VECTOR4: {
                    if (t.IsAssignableFrom(vector4Type)) {
                        if (field != null) {
                            field.SetValue(objectReference, NewVector4FromPointer(propertyData));
                        } else {
                            property.SetValue(objectReference, NewVector4FromPointer(propertyData));
                        }
                        return 0;
                    }
                    break;
                }

                case PODTYPE.POD_MATRIX: {
                    if (t.IsAssignableFrom(matrixType)) {
                        if (field != null) {
                            field.SetValue(objectReference, NewMatrixFromPointer(propertyData));
                        } else {
                            property.SetValue(objectReference, NewMatrixFromPointer(propertyData));
                        }

                        return 0;
                    }
                    break;
                }

                case PODTYPE.POD_BINARYBLOB: {
                    if (t.IsAssignableFrom(binaryBlobType)) {
                        if (field != null) {
                            field.SetValue(objectReference, NewBinaryBlobFromPointer(propertyData, propertyDataSize));
                        } else {
                            property.SetValue(objectReference, NewBinaryBlobFromPointer(propertyData, propertyDataSize));
                        }

                        return 0;
                    }
                    break;
                }
            }

            // if we get here we didn't write it
            return LuauError(thread, "ERROR - " + objectReference.ToString() + "." + propName + " unable to set property of type " + t.Name + " with a " + type.ToString());
        } else {
            return LuauError(thread, "Error: InstanceId not currently available. InstanceId=" + instanceId + ", propName=" + propName);
        }
    }

    //When a lua object wants to get a property
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.GetPropertyCallback))]
    static unsafe int getProperty(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameLength) {
        CurrentContext = context;
        
        string propName = LuauCore.PtrToStringUTF8(propertyName, propertyNameLength, out ulong propNameHash);
        LuauCore instance = LuauCore.CoreInstance;
        
        //This detects STATIC classobjects only - live objects do not report the className
        if (classNameSize != 0)
        {
            string staticClassName = LuauCore.PtrToStringUTF8(classNamePtr, classNameSize);
            instance.unityAPIClasses.TryGetValue(staticClassName, out BaseLuaAPIClass staticClassApi);
            if (staticClassApi == null)
            {
                return LuauError(thread, "ERROR - type of " + staticClassName + " class not found");
            }
            Type objectType = staticClassApi.GetAPIType();
            if (printReferenceAssemblies) {
                referencedAssemblies.Add(objectType.Assembly.FullName);
            }
            
            // Get PropertyInfo from cache if possible -- otherwise put it in cache
            PropertyGetReflectionCache? cacheData;
            if (!(cacheData = LuauCore.GetPropertyCacheValue(objectType, propName)).HasValue)
            {
                var propertyInfo = objectType.GetProperty(propName,
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (propertyInfo != null)
                {
                    // var getProperty = LuauCore.BuildUntypedGetter(propertyInfo, true);
                    cacheData = LuauCore.SetPropertyCacheValue(objectType, propName, propertyInfo);
                }
            }

            if (cacheData != null)
            {
                // Type t = propertyInfo.PropertyType;
                System.Object value = cacheData.Value.propertyInfo.GetValue(null);
                WritePropertyToThread(thread, value, cacheData.Value.t);
                return 1;
            }
            
            // Get C# event:
            var eventInfo = objectType.GetRuntimeEvent(propName);
            if (eventInfo != null) {
                return LuauSignalWrapper.HandleCsEvent(context, thread, staticClassApi, instanceId, propNameHash, eventInfo, true);
            }

            FieldInfo fieldInfo = objectType.GetField(propName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (fieldInfo != null)
            {
                Type t = fieldInfo.FieldType;
                System.Object value = fieldInfo.GetValue(null);
                WritePropertyToThread(thread, value, t);
                return 1;
            }

            return LuauError(thread, "ERROR - " + propName + " get property not found on class " + staticClassName);
        } else {
            System.Object objectReference = ThreadDataManager.GetObjectReference(thread, instanceId);
            if (objectReference == null) {
                return LuauError(thread, "Error: InstanceId not currently available:" + instanceId + ". propName=" + propName);
            }

            Type sourceType = objectReference.GetType();

            // Scene Protection
            if (context != LuauContext.Protected) {
                if (sourceType == typeof(GameObject)) {
                    var target = (GameObject)objectReference;
                    if (IsAccessBlocked(context, target)) {
                        return LuauError(thread, "[Airship] Access denied when trying to read " + target.name + ".");
                    }
                } else if (sourceType.IsSubclassOf(typeof(Component)) || sourceType == typeof(Component)) {
                    var target = (Component)objectReference;
                    if (target && target.gameObject && IsAccessBlocked(context, target.gameObject)) {
                        return LuauError(thread, "[Airship] Access denied when trying to read " + target.name + ".");
                    }
                }
            }
            
            _coreInstance.unityAPIClassesByType.TryGetValue(sourceType, out var valueTypeAPI);
            if (valueTypeAPI != null) {
                var retValue = valueTypeAPI.OverrideMemberGetter(context, thread, objectReference, propName);
                if (retValue >= 0) {
                    return retValue;
                }
            }

            // Get property info from cache if possible, otherwise set it
            PropertyGetReflectionCache? cacheData;
            if (!(cacheData = LuauCore.GetPropertyCacheValue(sourceType, propName)).HasValue) {
                var propertyInfo = instance.GetPropertyInfoForType(sourceType, propName, propNameHash);
                if (propertyInfo != null) {
                    // var getProperty = LuauCore.BuildUntypedGetter(propertyInfo, false);
                    cacheData = LuauCore.SetPropertyCacheValue(sourceType, propName, propertyInfo);
                }
            }

            if (cacheData != null) {
                // Debug.Log("Found property: " + propName);
                Type t = cacheData.Value.t;
                try {
                    // System.Object value = cacheData.Value.getProperty.Invoke(objectReference); // property.GetValue(objectReference);
                    System.Object value = cacheData.Value.propertyInfo.GetValue(objectReference);
                    if (value != null) {
                        var valueType = value.GetType();
                        if (value is UnityEvent unityEvent0) {
                            return LuauSignalWrapper.HandleUnityEvent0(context, thread, objectReference, instanceId,
                                propNameHash, unityEvent0);
                        } else if (valueType.IsGenericType) {
                            var genericTypeDef = valueType.GetGenericTypeDefinition();
                            if (genericTypeDef == typeof(UnityEvent<>)) {
                                var unityEvent1 = (UnityEvent<object>)value;
                                return LuauSignalWrapper.HandleUnityEvent1(context, thread, objectReference,
                                    instanceId, propNameHash, unityEvent1);
                            } else if (genericTypeDef == typeof(UnityEvent<,>)) {
                                var unityEvent2 = (UnityEvent<object, object>)value;
                                return LuauSignalWrapper.HandleUnityEvent2(context, thread, objectReference,
                                    instanceId, propNameHash, unityEvent2);
                            } else if (genericTypeDef == typeof(UnityEvent<,,>)) {
                                var unityEvent3 = (UnityEvent<object, object, object>)value;
                                return LuauSignalWrapper.HandleUnityEvent3(context, thread, objectReference,
                                    instanceId, propNameHash, unityEvent3);
                            } else if (genericTypeDef == typeof(UnityEvent<,,,>)) {
                                var unityEvent4 = (UnityEvent<object, object, object, object>)value;
                                return LuauSignalWrapper.HandleUnityEvent4(context, thread, objectReference,
                                    instanceId, propNameHash, unityEvent4);
                            }
                        }

                        WritePropertyToThread(thread, value, t);
                        return 1;
                    } else {
                        // Debug.Log("Value was null in dictionary. propName=" + propName + ", object=" + sourceType.Name);
                        WritePropertyToThread(thread, null, null);
                        return 1;
                    }
                } catch (NotSupportedException e) {
                    return LuauError(
                        thread,
                        $"Failed reflection when getting property \"{propName}\". Please note that ref types are not supported. " +
                        e.Message);
                } catch (Exception e) {
                    // If we failed to get a reference to a non-primitive, just assume a null value (write nil to the stack):
                    if (!cacheData.Value.propertyInfo.PropertyType.IsPrimitive) {
                        WritePropertyToThread(thread, null, null);
                        return 1;
                    }

                    return LuauError(thread, "Failed to get property in dictionary. propName=" + propName + ", object=" +
                                             sourceType.Name + ", msg=" + e.Message);
                }
            }

            // Handle case of dictionary direct access
            // example:
            // local t = dict[1]
            var dict = objectReference as IDictionary;
            if (dict != null) {
                if (int.TryParse(propName, out int keyInt)) {
                    if (dict.Contains(keyInt)) {
                        object value = dict[keyInt];
                        Type t = value.GetType();
                        WritePropertyToThread(thread, value, t);
                        return 1;
                    }

                    if (dict.Contains((uint)keyInt)) {
                        object value = dict[(uint)keyInt];
                        Type t = value.GetType();
                        WritePropertyToThread(thread, value, t);
                        return 1;
                    }

                    // print("key: " + propName + " " + keyInt);
                    // Debug.Log("[Luau]: Dictionary had key but value was null. propName=" + propName + ", sourceType=" + sourceType.Name + ", obj=" + objectReference);
                    WritePropertyToThread(thread, null, null);
                    return 1;
                }

                if (dict.Contains(propName)) {
                    object value = dict[propName];
                    Type t = value.GetType();
                    WritePropertyToThread(thread, value, t);
                    return 1;
                } else {
                    Debug.Log("[Luau]: Dictionary was found but key was not found. propName=" + propName +
                              ", sourceType=" + sourceType.Name);
                    WritePropertyToThread(thread, null, null);
                    return 1;
                }
            }

            // Get C# event:
            var eventInfo = sourceType.GetRuntimeEvent(propName);
            if (eventInfo != null) {
                return LuauSignalWrapper.HandleCsEvent(context, thread, objectReference, instanceId, propNameHash,
                    eventInfo, false);
            }

            // Get field:
            FieldInfo field = instance.GetFieldInfoForType(sourceType, propName, propNameHash);
            if (field != null) {
                Type t = field.FieldType;
                System.Object value = field.GetValue(objectReference);
                // if (value is UnityEvent) {
                //     HandleEvent();
                // }
                WritePropertyToThread(thread, value, t);
                return 1;
            }

            // Debug.LogError("ERROR - (" + sourceType.Name + ")." + propName + " property/field not found");
            // return 0;
            return LuauError(thread, $"ERROR - ({sourceType.Name}).{propName} property/field not found");
        }
    }

    public static string GetRequirePath(AirshipRuntimeScript binding, string fileNameStr) {
        if (binding != null) {
            if (fileNameStr.Contains("/") == false) {
                //Get a stripped name
                var fname = GetTidyPathNameForLuaFile(binding.m_fileFullPath);
                fileNameStr = fname;
            } else if (fileNameStr.StartsWith("./")) {
                //Get a stripped name
                var fname = GetTidyPathNameForLuaFile(binding.m_fileFullPath);

                //Remove just this filename off the end
                var bits = new List<string>(fname.Split("/"));
                bits.RemoveAt(bits.Count - 1);
                var bindingPath = Path.Combine(bits.ToArray());

                fileNameStr = bindingPath + "/" + fileNameStr.Substring(2);
            } else if (fileNameStr.StartsWith("../")) {
                var fname = GetTidyPathNameForLuaFile(binding.m_fileFullPath);

                //Remove two bits of this filename off the end
                var bits = new List<string>(fname.Split("/"));
                if (bits.Count > 0) {
                    bits.RemoveAt(bits.Count - 1);
                }

                if (bits.Count > 0) {
                    bits.RemoveAt(bits.Count - 1);
                }

                var bindingPath = Path.Combine(bits.ToArray());

                fileNameStr = bindingPath + "/" + fileNameStr.Substring(2);
            }
        }
        
        //Fully qualify it
        fileNameStr = GetTidyPathNameForLuaFile(fileNameStr);

        return fileNameStr;
    }

    //Take a random path name from a require and transform it into its path relative to /assets/.
    //The same file always gets the same path, so this is used as a key to return the same table every time from lua land
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.RequireCallback))]
    static unsafe int requirePathCallback(LuauContext context, IntPtr thread, IntPtr fileName, int fileNameSize) {
        CurrentContext = context;
        
        var fileNameStr = LuauCore.PtrToStringUTF8(fileName, fileNameSize);
        
        LuauState.FromContext(context).TryGetScriptBindingFromThread(thread, out var binding);
        var fileRequirePath = GetRequirePath(binding, fileNameStr);
        
        LuauCore.WritePropertyToThread(thread, fileRequirePath, typeof(string));
        
        return 1;
    }


    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.RequireCallback))]
    static unsafe IntPtr requireCallback(LuauContext context, IntPtr thread, IntPtr fileName, int fileNameSize) {
        CurrentContext = context;

        string fileNameStr = LuauCore.PtrToStringUTF8(fileName, fileNameSize);
        // Debug.Log("require " + fileNameStr);

        GameObject obj = new GameObject($"require({fileNameStr})");
        // if (luauModulesFolder == null) {
        //     var coreGo = GameObject.Find("AirshipCore");
        //     if (!coreGo) {
        //         coreGo = new GameObject("AirshipCore");
        //     }
        //     luauModulesFolder = new GameObject("LuauModules");
        //     luauModulesFolder.transform.SetParent(coreGo.transform);
        // }
        // obj.transform.parent = luauModulesFolder.transform;
        obj.transform.parent = LuauState.FromContext(context).GetRequireGameObject().transform;
        AirshipComponent newBinding = obj.AddComponent<AirshipComponent>();

        if (newBinding.CreateThreadFromPath(fileNameStr, context) == false)
        {
            ThreadDataManager.Error(thread);
            Debug.LogError("Error require(" + fileNameStr + ") not found.");
            GetLuauDebugTrace(thread);
            return IntPtr.Zero;
        }

        if (newBinding.m_error == true)
        {
            ThreadDataManager.Error(thread);
            Debug.LogError("Error trying to execute module script during require for " + fileNameStr + ". Context=" + LuauCore.CurrentContext);
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
        }
        // Debug.Log("event connections: " + eventConnections.Count);
    }
    
    /// When lua wants to toggle the enabled state of a component
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.ComponentSetEnabledCallback))]
    private static void SetComponentEnabled(IntPtr thread, int instanceId, int componentId, int enabled) {
        var gameObject = AirshipBehaviourRootV2.GetGameObject(instanceId);
        if (gameObject == null) {
            Debug.LogError($"Could not find GameObject by id {instanceId} while trying to set enabled state");
            return;
        }
        
        var component = AirshipBehaviourRootV2.GetComponent(gameObject, componentId);
        if (component == null) {
            Debug.LogError($"Could not set component {componentId} enabled to {enabled} for {gameObject.name}", gameObject);
            return;
        }
        
        component.enabled = (enabled != 0);
    }
    
    
    //When a lua object wants to call a method..
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.CallMethodCallback))]
    static unsafe int callMethod(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr methodNamePtr, int methodNameLength, int numParameters, IntPtr firstParameterType, IntPtr firstParameterData, IntPtr firstParameterSize, IntPtr shouldYield) {
        CurrentContext = context;
        
        // if (s_shutdown) return 0;
        Marshal.WriteInt32(shouldYield, 0);
        if (!IsReady) return 0;

        string methodName = LuauCore.PtrToStringUTF8(methodNamePtr, methodNameLength);
        string staticClassName = LuauCore.PtrToStringUTF8(classNamePtr, classNameSize);
        
        LuauCore instance = LuauCore.CoreInstance;

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
            int retValue = staticClassApi.OverrideStaticMethod(context, thread, methodName, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (retValue >= 0)
            {
                return retValue;
            }
        }

        if (type == null)
        {
            reflectionObject = ThreadDataManager.GetObjectReference(thread, instanceId);

            if (reflectionObject == null)
            {
                return LuauError(thread, $"Error: InstanceId not currently available for {instanceId} {methodName} {staticClassName} ({LuaThreadToString(thread)})");
            }
            
            type = reflectionObject.GetType();
        }

        if (reflectionObject != null) {
            //See if we have any custom methods implemented for this type?
            instance.unityAPIClassesByType.TryGetValue(type, out BaseLuaAPIClass valueTypeAPI);
            if (valueTypeAPI != null) {
                // Scene Protection
                if (context != LuauContext.Protected) {
                    if (type == typeof(GameObject)) {
                        var target = (GameObject) reflectionObject;
                        if (IsAccessBlocked(context, target)) {
                            return LuauError(thread, $"[Airship] Access denied when trying to call method {target.name}.{methodName}. Full type name: {type.FullName}");
                        }
                    } else if (type.IsSubclassOf(typeof(Component)) || type == typeof(Component)) {
                        var target = (Component) reflectionObject;
                        if (target.gameObject && IsAccessBlocked(context, target.gameObject)) {
                            return LuauError(thread, $"[Airship] Access denied when trying to call method {target.name}.{methodName}. Full type name: {type.FullName}");
                        }
                    }
                }

                int retValue = valueTypeAPI.OverrideMemberMethod(context, thread, reflectionObject, methodName, numParameters,
                    parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
                if (retValue >= 0) {
                    return retValue;
                }
            }
        }
        
        // Check for IsA call:
        if (methodName == "IsA") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            
            var t = ReflectionList.AttemptGetTypeFromString(typeName);

            if (t == null) {
                return LuauError(thread, $"Error: Unknown type \"{typeName}\" when calling {type.Name}.IsA");
            }

            var isA = t.IsAssignableFrom(type);
            WritePropertyToThread(thread, isA, typeof(bool));

            return 1;
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

            if (eventInfo != null) {
                //There is an event
                if (numParameters != 1) {
                    return LuauError(thread, $"Error: {methodName} takes 1 parameter (a function!)");
                }
                if (parameterDataPODTypes[0] != (int)LuauCore.PODTYPE.POD_LUAFUNCTION) {
                    return LuauError(thread, $"Error: {methodName} parameter must be a function");
                }

                int handle = GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
                ParameterInfo[] eventInfoParams = eventInfo.EventHandlerType.GetMethod("Invoke").GetParameters();

                foreach (ParameterInfo param in eventInfoParams) {
                    if (param.ParameterType.IsValueType == true && param.ParameterType.IsPrimitive == false && param.ParameterType.IsEnum == false) {
                        return LuauError(thread, $"Error: {methodName} parameter {param.Name} is a struct, which won't work with GC without you manually pinning it. Try changing it to a class or wrapping it in a class.");
                    }
                }

                var attachContextToEvent = eventInfo.GetCustomAttribute<AttachContext>() != null;

                //grab the correct one for the number of parameters
                var callbackWrapper = ThreadDataManager.RegisterCallback(context, thread, handle, methodName, attachContextToEvent);
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
                // print("added eventConnection (" + eventConnections.Count + "): " + methodName);

                LuauCore.WritePropertyToThread(thread, eventConnectionId, typeof(int));
                return 1;
            }
        }

        //Use reflection to try and find the method now
        bool countFound = false;
        bool nameFound = false;
        ParameterInfo[] finalParameters = null;
        MethodInfo finalMethod = null;

        object[] podObjects = UnrollPodObjects(thread, numParameters, parameterDataPODTypes, parameterDataPtrs);

        Profiler.BeginSample("LuauCore.FindMethod");
        FindMethod(context, type, methodName, numParameters, parameterDataPODTypes, podObjects, out nameFound, out countFound, out finalParameters, out finalMethod, out var finalExtensionMethod, out var insufficientContext, out var attachContext);
        Profiler.EndSample();

        if (finalMethod == null) {
            if (insufficientContext) {
#if AIRSHIP_INTERNAL
                return LuauError(thread, $"Error: Method {methodName} on {type.Name} is not allowed in this context ({context}). Add the type with the desired context to ReflectionList.cs: {type.FullName}");
#else
                return LuauError(thread, $"Error: Method {methodName} on {type.Name} is not allowed in this context ({context}). Full type name: {type.FullName}");
#endif
            } else if (!nameFound) {
                return LuauError(thread, "Error: Method " + methodName + " not found on " + type.Name + "(" + instanceId + ")");
            } else if (nameFound && !countFound) {
                return LuauError(thread, "Error: No version of " + methodName + " on " + type.Name + "(" + instanceId + ") takes " + numParameters + " parameters.");
            } else if (nameFound && countFound) {
                return LuauError(thread, "Error: Method " + methodName + " could not match parameter types on " + type.Name + "(" + instanceId + ")");
            }

            return LuauError(thread, "Error: Failed to get method");
        }

        object[] parsedData = null;
        bool success = ParseParameterData(thread, numParameters, parameterDataPtrs, parameterDataPODTypes, finalParameters, paramaterDataSizes, podObjects, attachContext, out parsedData);
        if (attachContext) {
            parsedData[0] = context;
        }
        if (success == false) {
            return LuauError(thread, $"Error: Unable to parse parameters for {type.Name} {finalMethod.Name}");
        }

        // Luau Context Security
        if (context != LuauContext.Protected) {
            if (methodName == "Instantiate" && type == typeof(Object)) {
                Transform targetTransform = null;
                if (finalParameters.Length is >= 2 and <= 3) {
                    if (parsedData[1].GetType() == typeof(Transform)) {
                        targetTransform = (Transform) parsedData[1];
                    }
                } else if (finalParameters.Length == 4) {
                    if (parsedData[3].GetType() == typeof(Transform)) {
                        targetTransform = (Transform) parsedData[3];
                    }
                }

                if (targetTransform != null && IsProtectedScene(targetTransform.gameObject.scene.name)) {
                    return LuauError(thread, $"[Airship] Access denied when trying call Object.Instantiate() with a parent transform inside a protected scene \"{targetTransform.gameObject.scene.name}\"");
                }
            } else if ((methodName == "Destroy" || methodName == "DestroyImmediate") && type == typeof(Object)) {
                if (finalParameters.Length >= 1 && parsedData[0] != null) {
                    var paramType = parsedData[0].GetType();
                    if (paramType == typeof(GameObject)) {
                        var param = parsedData[0] as GameObject;
                        if (param != null && IsProtectedScene(param.scene.name)) {
                            return LuauError(thread, $"[Airship] Access denied when trying to destroy a protected GameObject \"{param.name}\"");
                        }
                    } else if (paramType == typeof(Component)) {
                        var param = parsedData[0] as Component;
                        if (param != null && IsProtectedScene(param.gameObject.scene.name)) {
                            return LuauError(thread, $"[Airship] Access denied when trying to destroy a protected Component \"{param.gameObject.name}\"");
                        }
                    }
                }
            } else if (methodName == "SetParent" && type == typeof(Transform)) {
                var callingTransform = reflectionObject as Transform;
                if (callingTransform != null && IsAccessBlocked(context, callingTransform.gameObject)) {
                    return LuauError(thread, $"[Airship] Access denied when trying set parent of a transform inside a protected scene \"{callingTransform.gameObject.scene.name}\"");
                }

                if (parsedData[0] != null && parsedData[0].GetType() == typeof(Transform)) {
                    var targetTransform = (Transform)parsedData[0];
                    if (targetTransform != null && IsAccessBlocked(context, targetTransform.gameObject)) {
                        return LuauError(thread, $"[Airship] Access denied when trying set parent to a transform inside a protected scene \"{targetTransform.gameObject.scene.name}\"");
                    }
                }
            }
        }

        //We have parameters
        object returnValue;
        object invokeObj = reflectionObject;

        var returnCount = 1;
        for (var j = 0; j < finalParameters.Length; j++) {
            if (finalParameters[j].IsOut) {
                returnCount += 1;
            }
        }

        if (finalExtensionMethod) {
            invokeObj = null;
            parsedData = parsedData.Prepend(reflectionObject).ToArray();
        }

        // Async method
        if (finalMethod.ReturnType == typeof(Task) || (finalMethod.ReturnType.IsGenericType &&
                                                       finalMethod.ReturnType.GetGenericTypeDefinition() ==
                                                       typeof(Task<>))) {
            var ret = InvokeMethodAsync(context, thread, type, finalMethod, invokeObj, parsedData, out var shouldYieldBool);
            if (ret == -1) {
                return ret;
            }
            Marshal.WriteInt32(shouldYield, shouldYieldBool ? 1 : 0);
            return returnCount;
        }

        try {
            returnValue = finalMethod.Invoke(invokeObj, parsedData);
        } catch (TargetInvocationException e) {
            return LuauError(thread, "Error: Exception thrown in method " + type.Name + "." + finalMethod.Name + ": " + e.InnerException.Message);
        } catch (Exception e) {
            return LuauError(thread, "Error: Exception thrown in method " + type.Name + "." + finalMethod.Name + ": " + e.Message);
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
    
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.ConstructorCallback))]
    static unsafe int constructorCallback(LuauContext context, IntPtr thread, IntPtr classNamePtr, int classNameSize, int numParameters, IntPtr firstParameterType, IntPtr firstParameterData, IntPtr firstParameterSize) {
        CurrentContext = context;
        
        if (!IsReady) return 0;
        
        string staticClassName = LuauCore.PtrToStringUTF8(classNamePtr, classNameSize);
        
        LuauCore instance = LuauCore.CoreInstance;

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
        if (staticClassApi == null) {
            Debug.Log("Constructor on " + staticClassName + " failed. Types not found.");
            return 0;
        }
        
        
        type = staticClassApi.GetAPIType();
        // !!! This could be broken
        //This handles where we need to replace a method or implement a method directly in the c# side eg: GameObject.new 
        int retValue = staticClassApi.OverrideStaticMethod(context, thread, "new", numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
        if (retValue >= 0)
        {
            return retValue;
        }
        
        return RunConstructor(thread, type, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
    }

    private static int InvokeMethodAsync(LuauContext context, IntPtr thread, Type type, MethodInfo method, object obj, object[] parameters, out bool shouldYield) {
        try {
            var task = (Task)method.Invoke(obj, parameters);
            var awaitingTask = new AwaitingTask {
                Thread = thread,
                Task = task,
                Method = method,
                Context = context,
            };

            if (task.IsCompleted) {
                ResumeAsyncTask(awaitingTask, true);
                shouldYield = false;
                return 0;
            }

            _awaitingTasks.Add(awaitingTask);
            LuauState.FromContext(context).TryGetScriptBindingFromThread(thread, out var binding);
            
            if (binding != null) {
                binding.m_asyncYield = true;
            } else {
                LuauPlugin.LuauPinThread(thread);
            }

            ThreadDataManager.SetThreadYielded(thread, true);

            shouldYield = true;
            return 0;
        } catch (Exception e) {
            shouldYield = false;
            return LuauError(thread, $"Error: Exception thrown in {type.Name} {method.Name}: {e.Message}");
        }
    }

    private static void ResumeAsyncTask(AwaitingTask awaitingTask, bool immediate = false) {
        var thread = awaitingTask.Thread;

        if (!immediate) {
            ThreadDataManager.SetThreadYielded(thread, false);
        }

        LuauState.FromContext(awaitingTask.Context).TryGetScriptBindingFromThread(thread, out var binding);
        if (binding == null) {
            LuauPlugin.LuauUnpinThread(thread);
        }

        if (awaitingTask.Task.IsFaulted) {
            ThreadDataManager.Error(thread);
            Debug.LogException(awaitingTask.Task.Exception);
            GetLuauDebugTrace(thread);
            return;
        }

        var nArgs = 0;

        var retType = awaitingTask.Method.ReturnType;
        if (retType.IsGenericType && retType.GetGenericTypeDefinition() == typeof(Task<>)) {
            nArgs = 1;
            var resPropInfo = retType.GetProperty("Result")!;
            var resValue = resPropInfo.GetValue(awaitingTask.Task);
            if (resValue == null) {
                WritePropertyToThread(thread, null, null);
            } else {
                var resType = resValue.GetType();
                WritePropertyToThread(thread, resValue, resType);
            }
        }

        if (!immediate) {
            var result = LuauPlugin.LuauResumeThread(thread, nArgs);
            if (binding != null) {
                binding.m_asyncYield = false;
                binding.m_canResume = result == 1;
            }
        }
    }

    public static void TryResumeAsyncTasks() {
        for (var i = _awaitingTasks.Count - 1; i >= 0; i--) {
            var awaitingTask = _awaitingTasks[i];
            if (!awaitingTask.Task.IsCompleted) continue;

            // Task has completed. Remove from list and resume lua thread:
            _awaitingTasks.RemoveAt(i);
            ResumeAsyncTask(awaitingTask);
        }
    }

    /// Get the string representation of a Lua thread in the same format that Lua would print a thread.
    public static string LuaThreadToString(IntPtr thread) {
        return $"thread: 0x{thread.ToInt64():x16}";
    }

    private static void GetLuauDebugTrace(IntPtr thread) {
        //Call this to get a bunch of prints of the current thread execution state
        LuauPlugin.LuauGetDebugTrace(thread);
    }

    private static PropertyGetReflectionCache? GetPropertyCacheValue(Type objectType, string propName) {
        // if (!LuauCore.propertyGetCache.TryGetValue(objectType.FullName ?? "", out var propDictionary)) return null;
        // return propDictionary.TryGetValue(propName, out PropertyGetReflectionCache data) ? data : null;
        
        var key = (objectType, propName);

        // Note: only caching on type full name + prop name. Possible collision on assemblies
        if (LuauCore.propertyGetCache.TryGetValue(key, out var data)) {
            return data;
        }

        return null;
    }

    private static PropertyGetReflectionCache SetPropertyCacheValue(Type objectType, string propName, PropertyInfo propertyInfo) {
        var cacheData = new PropertyGetReflectionCache {
            t = propertyInfo.PropertyType,
            propertyInfo = propertyInfo,
        };
        LuauCore.propertyGetCache[(objectType, propName)] = cacheData;
        return cacheData;
    }
    
    private static Func<object, object> BuildUntypedGetter(MemberInfo memberInfo, bool isStaticAccess) {
        var targetType = memberInfo.DeclaringType;

        // Create a ParameterExpression of type System.Object
        var arg = Expression.Parameter(typeof(object), "t");

        // Use the casted argument directly in the member access
        var exMemberAccess = Expression.MakeMemberAccess(
            isStaticAccess ? null : Expression.Convert(arg, targetType),
            memberInfo);

        // Convert(t.PropertyName, typeof(object))
        var exConvertMemberToObject = Expression.Convert(exMemberAccess, typeof(object));

        // Lambda expression
        var lambda = Expression.Lambda<Func<object, object>>(exConvertMemberToObject, arg);

        var action = lambda.Compile();
        return action;
    }
}
