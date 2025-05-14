using Luau;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Assets.Luau;
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

    private static LuauPlugin.PrintCallback printCallback_holder = PrintCallback;

    private const int MaxParameters = 20;
    private const int MaxParsedObjects = 100;
    
    private LuauPlugin.ComponentSetEnabledCallback componentSetEnabledCallback_holder;
    private LuauPlugin.GetPropertyCallback getPropertyCallback_holder;
    private LuauPlugin.SetPropertyCallback setPropertyCallback_holder;
    private LuauPlugin.CallMethodCallback callMethodCallback_holder;
    private LuauPlugin.ObjectGCCallback objectGCCallback_holder;
    private LuauPlugin.RequireCallback requireCallback_holder;
    private LuauPlugin.ConstructorCallback constructorCallback_holder;
    private LuauPlugin.RequirePathCallback requirePathCallback_holder;
    private LuauPlugin.ToStringCallback toStringCallback_holder;
    private LuauPlugin.ToggleProfilerCallback toggleProfilerCallback_holder;
    private LuauPlugin.IsObjectDestroyedCallback isObjectDestroyedCallback_holder;
    

    private struct AwaitingTask {
#if UNITY_EDITOR
        public string DebugName;
#endif
        public IntPtr Thread;
        public int ThreadRef;
        public Task Task;
        public MethodInfo Method;
        public LuauContext Context;
        public Type Type;
    }

    private struct PropertyGetReflectionCache {
        public Type t;
        [FormerlySerializedAs("pi")] public PropertyInfo propertyInfo;
        public Delegate GetProperty;
        public bool HasGetPropertyFunc;
        public bool IsNativeClass;
    }

    // Hopefully faster dictionary comparison / hash time
    private readonly struct PropertyCacheKey : IEquatable<PropertyCacheKey> {
        private readonly Type _type;
        private readonly string _propertyName;
        private readonly int _hashCode;

        public PropertyCacheKey(Type type, string propertyName) {
            _type = type;
            _propertyName = propertyName;
            // Pre-compute hash code to avoid repeated calculations
            _hashCode = HashCode.Combine(type.GetHashCode(), propertyName.GetHashCode());
        }

        public override int GetHashCode() {
            return _hashCode;
        }

        public bool Equals(PropertyCacheKey other) {
            return ReferenceEquals(_type, other._type) && string.Equals(_propertyName, other._propertyName);
        }
    }
    
    public struct EventConnection {
        public int id;
        public object target;
        public System.Delegate handler;
        public EventInfo eventInfo;
        public CallbackWrapper callbackWrapper;
    }

    private static Dictionary<PropertyCacheKey, PropertyGetReflectionCache> propertyGetCache = new();
    
    public static Dictionary<int, EventConnection> eventConnections = new();
    private static int eventIdCounter = 0;

    private static readonly List<AwaitingTask> _awaitingTasks = new();

    public static GameObject luauModulesFolder;

    private void CreateCallbacks() {
        printCallback_holder = PrintCallback;
        getPropertyCallback_holder = GetPropertySafeCallback;
        setPropertyCallback_holder = SetPropertySafeCallback;
        callMethodCallback_holder = CallMethodCallback;
        objectGCCallback_holder = ObjectGcCallback;
        requireCallback_holder = RequireCallback;
        constructorCallback_holder = ConstructorCallback;
        requirePathCallback_holder = RequirePathCallback;
        toStringCallback_holder = ToStringCallback;
        componentSetEnabledCallback_holder = SetComponentEnabledCallback;
        toggleProfilerCallback_holder = ToggleProfilerCallback;
        isObjectDestroyedCallback_holder = IsObjectDestroyedCallback;
    }

    private static int LuauError(IntPtr thread, string err) {
        LuauPlugin.LuauPushCsError(err);
        ThreadDataManager.Error(thread);
        return -1;
    }

#if UNITY_EDITOR
    private static readonly Regex AnchorLinkPattern = new Regex(@"(\S+\.lua):(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static string InjectAnchorLinkToLuaScript(string logMessage) {
        // e.g. "path/to/my/script.lua:10: an error occurred"
        return AnchorLinkPattern.Replace(logMessage, (m) => {
            var scriptPath = m.Groups[1].Value;
            var line = m.Groups[2].Value;
            
            return $"<a href=\"#\" file=\"out://{scriptPath}\" line=\"{line}\" column=\"0\">{scriptPath}:{line}</a>";
        });
    }
#endif


    //when a lua thread prints something to console
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.PrintCallback))]
    static void PrintCallback(LuauContext context, IntPtr thread, int style, int gameObjectId, IntPtr buffer, int length) {
        CurrentContext = context;
        
        var res = LuauCore.PtrToStringUTF8(buffer, length);
        
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
            // The STANDALONE here is just a test:
#if UNITY_STANDALONE && !UNITY_EDITOR
            Debug.LogWarning("[ERROR] " + res, logContext);
#else
            Debug.LogError(res, logContext);
#endif
            //If it's an error, the thread is suspended 
            ThreadDataManager.Error(thread);
        } else {
            Debug.Log(res, logContext);
        }
    }
    
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.ToStringCallback))]
    static void ToStringCallback(IntPtr thread, int instanceId, IntPtr str, int maxLen, out int len) {
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
        // Not tagged to component
        if (componentId < -1) {
            if (strLen > 0) {
                // No need to free strPtr -- it is stack allocated
                var str = PtrToStringUTF8(strPtr, strLen);
                Profiler.BeginSample($"{str}");
                return;
            }
        }
        

        if (AirshipComponent.ComponentIdToScriptName.TryGetValue(componentId, out var componentName)) {
            if (strLen > 0) {
                var str = PtrToStringUTF8(strPtr, strLen);
                Profiler.BeginSample($"{componentName}{str}");
            }
            else {
                Profiler.BeginSample($"{componentName}");
            }
        }
    }

    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.IsObjectDestroyedCallback))]
    static int IsObjectDestroyedCallback(int instanceId) {
        return ThreadDataManager.IsUnityObjectReferenceDestroyed(instanceId) ? 1 : 0;
    }

    //when a lua thread gc releases an object, make sure our GC knows too
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.ObjectGCCallback))]
    static unsafe int ObjectGcCallback(int instanceId, IntPtr objectDebugPointer) {
        ThreadDataManager.DeleteObjectReference(instanceId);
        //Debug.Log("GC " + instanceId + " ptr:" + objectDebugPointer);
        return 0;
    }

    // When a lua object wants to set a property
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.SetPropertyCallback))]
    private static int SetPropertySafeCallback(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameLength, LuauCore.PODTYPE type, IntPtr propertyData, int propertyDataSize, int isTable) {
        var ret = 0;
        try {
            ret = SetProperty(context, thread, instanceId, classNamePtr, classNameSize, propertyName, propertyNameLength, type, propertyData, propertyDataSize, isTable);
        } catch (Exception e) {
            ret = LuauError(thread, e.Message);
        }

        return ret;
    }
    
    private static int SetProperty(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameLength, LuauCore.PODTYPE type, IntPtr propertyData, int propertyDataSize, int isTable) {
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

            if (isTable != 0 && t.IsArray) {
                var success = ParseTableParameter(thread, type, t, propertyDataSize, -1, out var value);
                if (!success) {
                    return LuauError(thread, $"Value of type {type} not valid table type");
                }
                if (field != null) {
                    field.SetValue(objectReference, value);
                } else {
                    property.SetValue(objectReference, value);
                }
                return 0;
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
                            if (IsProtectedScene(targetTransform.gameObject.scene)) {
                                return LuauError(thread, "[Airship] Access denied when trying to set parent of protected object " + targetTransform.gameObject.name);
                            }

                            var valueTransform = (Transform)propertyObjectRef;
                            if (IsProtectedScene(valueTransform.gameObject.scene)) {
                                return LuauError(thread, "[Airship] Access denied when trying to set parent of " + targetTransform.gameObject.name + " to a child of scene " + valueTransform.gameObject.scene.name);
                            }
                        }
                        
                        if (field != null) {
                            field.SetValue(objectReference, propertyObjectRef);
                        } else {
                            SetValue<object>(objectReference, propertyObjectRef, property);
                        }
                        return 0;
                    }

                    break;
                }

                case PODTYPE.POD_VECTOR3: {
                    if (t.IsAssignableFrom(vector3Type)) {
                        Profiler.BeginSample("AssignVec3");
                        if (field != null) {
                            // Debug.Log(field);
                            Vector3 v = NewVector3FromPointer(propertyData);
                            field.SetValue(objectReference, v);
                        } else {
                            // Debug.Log(property);
                            Vector3 v = NewVector3FromPointer(propertyData);
                            SetValue<Vector3>(objectReference, v, property);
                            // property.SetValue(objectReference, v);
                        }
                        Profiler.EndSample();
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
                            SetValue<Vector3Int>(objectReference, Vector3Int.FloorToInt(v), property);
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
                            SetValue<bool>(objectReference, val, property);
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
                            SetValue<double>(objectReference, doubles[0], property);
                        }

                        return 0;
                    } else if (t.IsAssignableFrom(ushortType)) {
                        if (field != null) {
                            field.SetValue(objectReference, (ushort) doubles[0]);
                        } else {
                            SetValue<ushort>(objectReference, (ushort) doubles[0], property);
                        }

                        return 0;
                    } else if (t.IsAssignableFrom(floatType)) {
                        if (field != null) {
                            field.SetValue(objectReference, (System.Single)doubles[0]);
                        } else {
                            SetValue<float>(objectReference, (System.Single) doubles[0], property);
                        }
                        return 0;
                    } else if (t.IsAssignableFrom(intType) || t.BaseType == enumType || t.IsAssignableFrom(enumType) || t.IsAssignableFrom(byteType)) {
                        if (field != null) {
                            field.SetValue(objectReference, (int)doubles[0]);
                        } else {
                            SetValue<int>(objectReference, (int)doubles[0], property);
                        }
                        return 0;
                    } else if (t.IsAssignableFrom(uIntType)) {
                        if (field != null) {
                            field.SetValue(objectReference, unchecked((int)doubles[0]));
                        } else {
                            SetValue<uint>(objectReference, unchecked((uint) doubles[0]), property);
                        }
                    } else if (t.IsAssignableFrom(longType)) {
                        if (field != null) {
                            field.SetValue(objectReference, (long)doubles[0]);
                        } else {
                            SetValue<long>(objectReference, (long) doubles[0], property);
                        }
                        return 0;
                    } else if (t.IsAssignableFrom(uLongType)) {
                        if (field != null) {
                            field.SetValue(objectReference, (ulong)doubles[0]);
                        } else {
                            SetValue<ulong>(objectReference, (ulong) doubles[0], property);
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
                            SetValue<string>(objectReference, dataStr, property);
                        }
                        return 0;
                    }
                    break;
                }

                case PODTYPE.POD_NULL: {
                    //nulling anything nullable
                    // if (Nullable.GetUnderlyingType(t) != null) {
                    if (t.IsClass) {
                        if (field != null) {
                            field.SetValue(objectReference, null);
                        } else {
                            SetValue<object>(objectReference, null, property);
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
                            SetValue<Ray>(objectReference, NewRayFromPointer(propertyData), property);
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
                            SetValue<Color>(objectReference, NewColorFromPointer(propertyData), property);
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
                            SetValue<Plane>(objectReference, NewPlaneFromPointer(propertyData), property);
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
                            SetValue<Quaternion>(objectReference, NewQuaternionFromPointer(propertyData), property);
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
                            SetValue<Vector2>(objectReference, NewVector2FromPointer(propertyData), property);
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
                            SetValue<Vector4>(objectReference, NewVector4FromPointer(propertyData), property);
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
                            SetValue<Matrix4x4>(objectReference, NewMatrixFromPointer(propertyData), property);
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
                            SetValue<BinaryBlob>(objectReference, NewBinaryBlobFromPointer(propertyData, propertyDataSize), property);
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
    
    private static readonly Dictionary<(bool, Type, string), Delegate> _propertySetterCache = 
        new Dictionary<(bool, Type, string), Delegate>();
    private delegate T Getter<T>(object target);
    private delegate void Setter<T>(object target, T val);
    private delegate void StaticSetter<T>(T val);
    
    private static Delegate CreateSetter<T>(PropertyInfo propertyInfo, bool isStatic) {
        var setMethod = propertyInfo.GetSetMethod();

        var declaringType = propertyInfo.DeclaringType;
        unsafe {
            var setPointer = setMethod
                .MethodHandle
                .GetFunctionPointer();
            
            if (setPointer == IntPtr.Zero || declaringType.IsValueType) {
                // Just direct reflection for this case (like ParticleEmitter modules -- weird Unity niche)
                return new Action<object, T>((object target, T value) => { propertyInfo.SetValue(target, value); });
            }


            if (!isStatic) {
                // Original class handling
                delegate*<object, T, void> funcPtr = (delegate*<object, T, void>)setPointer.ToPointer();;

                var setter = new Setter<T>((obj, val) => { funcPtr(obj, val); });
                return setter;
            } else {
                delegate*<T, void> funcPtr = (delegate*<T, void>)setPointer.ToPointer();;
                var setter = new StaticSetter<T>((val) => { funcPtr(val); });
                return setter;
            }
        }
    }

    private static T GetValue<T>(object instance, PropertyGetReflectionCache cacheData) {
        if (typeof(T) == typeof(object) || cacheData.IsNativeClass) {
            return (T) cacheData.propertyInfo.GetMethod.Invoke(instance, null);
        }
    
        if (!cacheData.HasGetPropertyFunc) {
            var getMethod = cacheData.propertyInfo.GetGetMethod();

            unsafe {
                delegate*<object, T> funcPtr = (delegate*<object, T>)getMethod
                    .MethodHandle
                    .GetFunctionPointer()
                    .ToPointer();
            
                // Create a delegate that wraps the function pointer
                var getter = new Getter<T>(obj => {
                    unsafe {
                        return funcPtr(obj);
                    }
                });
            
                cacheData.HasGetPropertyFunc = true;
                cacheData.GetProperty = getter;
                LuauCore.propertyGetCache[new PropertyCacheKey(instance.GetType(), cacheData.propertyInfo.Name)] = cacheData;
            }
        }
            
        return ((Getter<T>) cacheData.GetProperty)(instance);
    }
    
    private static void SetValue<T>(object instance, T value, PropertyInfo pi) {
        var staticSet = instance == null;
        if (!_propertySetterCache.TryGetValue((staticSet, pi.DeclaringType, pi.Name), out var setter)) {
            setter = CreateSetter<T>(pi, staticSet);
            _propertySetterCache[(staticSet, pi.DeclaringType, pi.Name)] = setter; 
        }
        
        if (pi.GetSetMethod().MethodHandle.GetFunctionPointer() == IntPtr.Zero || pi.DeclaringType.IsValueType) {
            ((Action<object, T>)setter)(instance, value);
            return;
        }
        
        if (staticSet) {
            ((StaticSetter<T>) setter)(value);
        } else {
            ((Setter<T>)setter)(instance, value);       
        }
    }

    // When a lua object wants to get a property
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.GetPropertyCallback))]
    private static int GetPropertySafeCallback(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameLength) {
        var ret = 0;
        try {
            ret = GetProperty(context, thread, instanceId, classNamePtr, classNameSize, propertyName, propertyNameLength);
        } catch (Exception e) {
            ret = LuauError(thread, e.Message);
        }

        return ret;
    }

    private static int GetProperty(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameLength) {
        Profiler.BeginSample("LuauCore.GetProperty");
        CurrentContext = context;

        string propName = LuauCore.PtrToStringUTF8(propertyName, propertyNameLength, out ulong propNameHash);
        LuauCore instance = LuauCore.CoreInstance;

        //This detects STATIC classobjects only - live objects do not report the className
        if (classNameSize != 0) {
            string staticClassName = LuauCore.PtrToStringUTF8(classNamePtr, classNameSize);
            instance.unityAPIClasses.TryGetValue(staticClassName, out BaseLuaAPIClass staticClassApi);
            if (staticClassApi == null) {
                return LuauError(thread, "ERROR - type of " + staticClassName + " class not found");
            }

            Type objectType = staticClassApi.GetAPIType();
            if (printReferenceAssemblies) {
                referencedAssemblies.Add(objectType.Assembly.FullName);
            }

            // Get PropertyInfo from cache if possible -- otherwise put it in cache
            PropertyGetReflectionCache? cacheData;
            if (!(cacheData = LuauCore.GetPropertyCacheValue(objectType, propName)).HasValue) {
                var propertyInfo = objectType.GetProperty(propName,
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (propertyInfo != null) {
                    // var getProperty = LuauCore.BuildUntypedGetter(propertyInfo, true);
                    cacheData = LuauCore.SetPropertyCacheValue(objectType, propName, propertyInfo);
                }
            }

            if (cacheData != null) {
                // Type t = propertyInfo.PropertyType;
                System.Object value = cacheData.Value.propertyInfo.GetValue(null);
                WritePropertyToThread(thread, value, cacheData.Value.t);
                Profiler.EndSample();
                return 1;
            }

            // Get C# event:
            var eventInfo = objectType.GetRuntimeEvent(propName);
            if (eventInfo != null) {
                Profiler.EndSample();
                return LuauSignalWrapper.HandleCsEvent(context, thread, staticClassApi, instanceId, propNameHash,
                    eventInfo, true);
            }

            FieldInfo fieldInfo = objectType.GetField(propName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (fieldInfo != null) {
                Type t = fieldInfo.FieldType;
                System.Object value = fieldInfo.GetValue(null);
                WritePropertyToThread(thread, value, t);
                Profiler.EndSample();
                return 1;
            }

            Profiler.EndSample();
            return LuauError(thread, "ERROR - " + propName + " get property not found on class " + staticClassName);
        }
        else {
            // Not a static class object:
            
            // Profiler.BeginSample("GetRef");
            System.Object objectReference = ThreadDataManager.GetObjectReference(thread, instanceId);
            // Profiler.EndSample();
            if (objectReference == null) {
                Profiler.EndSample();
                return LuauError(thread,
                    "Error: InstanceId not currently available:" + instanceId + ". propName=" + propName);
            }

            Type sourceType = objectReference.GetType();

            // Scene Protection
            // Profiler.BeginSample("SceneProtection");
            if (context != LuauContext.Protected) {
                if (objectReference is GameObject targetGo) {
                    // var target = (GameObject)objectReference;
                    if (IsAccessBlocked(context, targetGo)) {
                        Profiler.EndSample();
                        return LuauError(thread,
                            "[Airship] Access denied when trying to read " + targetGo.name + ".");
                    }
                }
                else if (sourceType.IsAssignableFrom(typeof(Component))) {
                    var target = (Component)objectReference;
                    if (target && IsAccessBlocked(context, target.gameObject)) {
                        Profiler.EndSample();
                        return LuauError(thread,
                            "[Airship] Access denied when trying to read " + target.name + ".");
                    }
                }
            }
            // Profiler.EndSample();

            _coreInstance.unityAPIClassesByType.TryGetValue(sourceType, out var valueTypeAPI);
            if (valueTypeAPI != null) {
                var retValue = valueTypeAPI.OverrideMemberGetter(context, thread, objectReference, propName);
                if (retValue >= 0) {
                    Profiler.EndSample();
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
                Type t = cacheData.Value.t;
                try {
                    // Try a fast write on value type (Vector3, int, etc. Not objects)
                    if (FastGetAndWriteValueProperty(thread, objectReference, cacheData.Value)) {
                        Profiler.EndSample();
                        return 1;
                    }

                    var value = GetValue<object>(objectReference, cacheData.Value);
                    if (value != null) {
                        var valueType = value.GetType();
                        if (value is UnityEvent unityEvent0) {
                            return LuauSignalWrapper.HandleUnityEvent0(context, thread, objectReference, instanceId,
                                propNameHash, unityEvent0);
                        }
                        else if (valueType.IsGenericType) {
                            var genericTypeDef = valueType.GetGenericTypeDefinition();
                            if (genericTypeDef == typeof(UnityEvent<>)) {
                                var unityEvent1 = (UnityEvent<object>)value;
                                return LuauSignalWrapper.HandleUnityEvent1(context, thread, objectReference,
                                    instanceId, propNameHash, unityEvent1);
                            }
                            else if (genericTypeDef == typeof(UnityEvent<,>)) {
                                var unityEvent2 = (UnityEvent<object, object>)value;
                                return LuauSignalWrapper.HandleUnityEvent2(context, thread, objectReference,
                                    instanceId, propNameHash, unityEvent2);
                            }
                            else if (genericTypeDef == typeof(UnityEvent<,,>)) {
                                var unityEvent3 = (UnityEvent<object, object, object>)value;
                                return LuauSignalWrapper.HandleUnityEvent3(context, thread, objectReference,
                                    instanceId, propNameHash, unityEvent3);
                            }
                            else if (genericTypeDef == typeof(UnityEvent<,,,>)) {
                                var unityEvent4 = (UnityEvent<object, object, object, object>)value;
                                return LuauSignalWrapper.HandleUnityEvent4(context, thread, objectReference,
                                    instanceId, propNameHash, unityEvent4);
                            }
                        }

                        // Profiler.BeginSample("WriteToThread");
                        WritePropertyToThread(thread, value, t);
                        // Profiler.EndSample();
                        Profiler.EndSample();
                        return 1;
                    }
                    else {
                        // Debug.Log("Value was null in dictionary. propName=" + propName + ", object=" + sourceType.Name);
                        WritePropertyToThread(thread, null, null);
                        Profiler.EndSample();
                        return 1;
                    }
                }
                catch (NotSupportedException e) {
                    return LuauError(
                        thread,
                        $"Failed reflection when getting property \"{propName}\". Please note that ref types are not supported. " +
                        e);
                }
                catch (TargetInvocationException e) {
                    return LuauError(thread, "Error fetching property " + propName + ": " + e.InnerException);
                }
                catch (Exception e) {
                    // If we failed to get a reference to a non-primitive, just assume a null value (write nil to the stack):
                    if (!cacheData.Value.propertyInfo.PropertyType.IsPrimitive) {
                        WritePropertyToThread(thread, null, null);
                        Profiler.EndSample();
                        return 1;
                    }

                    Profiler.EndSample();
                    return LuauError(thread, "Failed to get property in dictionary. propName=" + propName +
                                             ", object=" +
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
                    Profiler.EndSample();
                    return 1;
                }

                if (dict.Contains(propName)) {
                    object value = dict[propName];
                    Type t = value.GetType();
                    WritePropertyToThread(thread, value, t);
                    Profiler.EndSample();
                    return 1;
                }
                else {
                    Debug.Log("[Luau]: Dictionary was found but key was not found. propName=" + propName +
                              ", sourceType=" + sourceType.Name);
                    WritePropertyToThread(thread, null, null);
                    Profiler.EndSample();
                    return 1;
                }
            }

            // Get C# event:
            var eventInfo = sourceType.GetRuntimeEvent(propName);
            if (eventInfo != null) {
                Profiler.EndSample();
                return LuauSignalWrapper.HandleCsEvent(context, thread, objectReference, instanceId, propNameHash,
                    eventInfo, false);
            }

            // Get field:
            FieldInfo field = instance.GetFieldInfoForType(sourceType, propName, propNameHash);
            if (field != null) {
                Type t = field.FieldType;
                System.Object value = field.GetValue(objectReference);
                WritePropertyToThread(thread, value, t);
                Profiler.EndSample();
                return 1;
            }

            Profiler.EndSample();
            return LuauError(thread, $"ERROR - ({sourceType.Name}).{propName} property/field not found");
        }
    }

    /// <summary>
    /// If the property info is a value property (int/vec) this will speed up the get/write process
    /// because we avoid boxing (no heap allocations).
    /// </summary>
    /// <returns>True if successful, otherwise false if nothing was written.</returns>
    private static bool FastGetAndWriteValueProperty(IntPtr thread, object objectReference, PropertyGetReflectionCache cacheData) {
        var propType = cacheData.propertyInfo.PropertyType;
        if (propType == intType) {
            var intValue = GetValue<int>(objectReference, cacheData);
            WritePropertyToThreadInt32(thread, intValue);
            return true;
        }
        if (propType == doubleType) {
            var doubleVal = GetValue<double>(objectReference, cacheData);
            WritePropertyToThreadDouble(thread, doubleVal);
            return true;
        }
        if (propType == floatType) {
            var shortVal = GetValue<float>(objectReference, cacheData);
            WritePropertyToThreadSingle(thread, shortVal);
            return true;
        }
        if (propType == vector3Type) {
            var vecValue = GetValue<Vector3>(objectReference, cacheData);
            WritePropertyToThreadVector3(thread, vecValue);
            return true;
        }
        if (propType == quaternionType) {
            var quatValue = GetValue<Quaternion>(objectReference, cacheData);
            WritePropertyToThreadQuaternion(thread, quatValue);
            return true;
        }
        return false;
    }
    
    public static string GetRequirePath(string originalScriptPath, string fileNameStr) {
        if (!string.IsNullOrEmpty(originalScriptPath)) {
            if (!fileNameStr.Contains("/")) {
                // Get a stripped name
                fileNameStr = GetTidyPathNameForLuaFile(originalScriptPath);
            } else if (fileNameStr.StartsWith("./")) {
                // Get a stripped name
                var fName = GetTidyPathNameForLuaFile(originalScriptPath);

                //Remove just this filename off the end
                var bits = new List<string>(fName.Split("/"));
                bits.RemoveAt(bits.Count - 1);
                var bindingPath = Path.Combine(bits.ToArray());
                
                fileNameStr = bindingPath + "/" + fileNameStr.Substring(2);
            } else if (fileNameStr.StartsWith("../")) {
                var fName = GetTidyPathNameForLuaFile(originalScriptPath);

                //Remove two bits of this filename off the end
                var bits = new List<string>(fName.Split("/"));
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
    private static void RequirePathCallback(LuauContext context, IntPtr thread, IntPtr scriptName, int scriptNameLen, IntPtr fileName, int fileNameLen) {
        CurrentContext = context;
        
        var fileNameStr = LuauCore.PtrToStringUTF8(fileName, fileNameLen);
        var scriptNameStr = LuauCore.PtrToStringUTF8(scriptName, scriptNameLen);
        
        // LuauState.FromContext(context).TryGetScriptBindingFromThread(thread, out var binding);
        var fileRequirePath = GetRequirePath(scriptNameStr, fileNameStr);
        
        // LuauCore.WritePropertyToThread(thread, fileRequirePath, typeof(string));
        LuauPluginRaw.PushString(thread, fileRequirePath);
    }
    
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.RequireCallback))]
    private static IntPtr RequireCallback(LuauContext context, IntPtr thread, IntPtr fileName, int fileNameSize) {
        CurrentContext = context;

        var fileNameStr = LuauCore.PtrToStringUTF8(fileName, fileNameSize);

        var obj = new GameObject($"require({fileNameStr})");
        obj.transform.parent = LuauState.FromContext(context).GetRequireGameObject().transform;
        // var obj = LuauState.FromContext(context).GetRequireGameObject();
        
        // var newBinding = obj.AddComponent<AirshipComponent>();
        //
        // if (newBinding.CreateThreadFromPath(fileNameStr, context) == false) {
        //     ThreadDataManager.Error(thread);
        //     Debug.LogError("Error require(" + fileNameStr + ") not found.");
        //     GetLuauDebugTrace(thread);
        //     return IntPtr.Zero;
        // }
        //
        // if (newBinding.m_error == true) {
        //     ThreadDataManager.Error(thread);
        //     Debug.LogError("Error trying to execute module script during require for " + fileNameStr + ". Context=" + LuauCore.CurrentContext);
        //     GetLuauDebugTrace(thread);
        //     return IntPtr.Zero;
        // }
        // if (newBinding.m_canResume == true) {
        //     ThreadDataManager.Error(thread);
        //     Debug.LogError("Require() yielded; did not return with a table for " + fileNameStr);
        //     GetLuauDebugTrace(thread);
        //     return IntPtr.Zero;
        // }
        //
        // return newBinding.m_thread;

        try {
            var newScript = LuauScript.Create(obj, fileNameStr, context, false);
            return newScript.thread;
        } catch (Exception e) {
            Debug.LogException(e);
            return IntPtr.Zero;
        }
    }

    public static void DisconnectEvent(int eventId) {
        if (eventConnections.TryGetValue(eventId, out var eventConnection)) {
            ThreadDataManager.UnregisterCallback(eventConnection.callbackWrapper);
            eventConnection.eventInfo.RemoveEventHandler(eventConnection.target, eventConnection.handler);
            eventConnections.Remove(eventId);
        }
        // Debug.Log("event connections: " + eventConnections.Count);
    }
    
    /// When lua wants to toggle the enabled state of a component
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.ComponentSetEnabledCallback))]
    private static void SetComponentEnabledCallback(IntPtr thread, int instanceId, int componentId, int enabled) {
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
    
    
    private static IntPtr[] _parameterDataPtrs = new IntPtr[MaxParameters];
    private static int[] _parameterDataSizes = new int[MaxParameters];
    private static int[] _parameterDataPODTypes = new int[MaxParameters];
    private static int[] _parameterIsTable = new int[MaxParameters];
    
    // When a lua object wants to call a method
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.CallMethodCallback))]
    static unsafe int CallMethodCallback(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr methodNamePtr, int methodNameLength, int numParameters, IntPtr firstParameterType, IntPtr firstParameterData, IntPtr firstParameterSize, IntPtr firstParameterIsTable, IntPtr shouldYield) {
        Profiler.BeginSample("LuauCore.CallMethod");
        CurrentContext = context;
        
        // if (s_shutdown) return 0;
        Marshal.WriteInt32(shouldYield, 0);
        if (!IsReady) {
            Profiler.EndSample();
            return 0;
        }

        var methodName = LuauCore.PtrToStringUTF8(methodNamePtr, methodNameLength);
        var staticClassName = LuauCore.PtrToStringUTF8(classNamePtr, classNameSize);
        
        var instance = LuauCore.CoreInstance;

        object reflectionObject = null;
        Type type = null;

        //Cast/marshal parameter data
        Marshal.Copy(firstParameterData, _parameterDataPtrs, 0, numParameters);
        Marshal.Copy(firstParameterSize, _parameterDataSizes, 0, numParameters);
        Marshal.Copy(firstParameterType, _parameterDataPODTypes, 0, numParameters);
        Marshal.Copy(firstParameterIsTable, _parameterIsTable, 0, numParameters);

        var parameterDataPtrs = new ArraySegment<IntPtr>(_parameterDataPtrs, 0, numParameters);
        var parameterDataSizes = new ArraySegment<int>(_parameterDataSizes, 0, numParameters);
        var parameterDataPODTypes = new ArraySegment<int>(_parameterDataPODTypes, 0, numParameters);
        var parameterIsTable = new ArraySegment<int>(_parameterIsTable, 0, numParameters);
        
        //This detects STATIC classobjects only - live objects do not report the className
        instance.unityAPIClasses.TryGetValue(staticClassName, out BaseLuaAPIClass staticClassApi);
        if (staticClassApi != null) {
            type = staticClassApi.GetAPIType();
            //This handles where we need to replace a method or implement a method directly in the c# side eg: GameObject.new 
            int retValue = staticClassApi.OverrideStaticMethod(context, thread, methodName, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (retValue >= 0) {
                Profiler.EndSample();
                return retValue;
            }
        }

        if (type == null) {
            reflectionObject = ThreadDataManager.GetObjectReference(thread, instanceId);

            if (reflectionObject == null) {
                Profiler.EndSample();
                return LuauError(thread, $"Error: InstanceId not currently available for {instanceId} {methodName} {staticClassName} ({LuaThreadToString(thread)})");
            }
            
            type = reflectionObject.GetType();
        }

        if (reflectionObject != null) {
            //See if we have any custom methods implemented for this type?
            instance.unityAPIClassesByType.TryGetValue(type, out BaseLuaAPIClass valueTypeAPI);
            if (valueTypeAPI != null) {
                // Destroyed protection
                if (type.IsSubclassOf(typeof(UnityEngine.Object))) {
                    if ((Object) reflectionObject == null) {
                        return LuauError(thread,
                            $"Attempt to call method {type.Name}.{methodName} but the object is already destroyed. You may need to check if the object is undefined before calling this method.");
                    }
                }
                
                // Scene Protection
                if (context != LuauContext.Protected) {
                    if (type == typeof(GameObject)) {
                        var target = (GameObject) reflectionObject;
                        if (IsAccessBlocked(context, target)) {
                            Profiler.EndSample();
                            return LuauError(thread, $"[Airship] Access denied when trying to call method {target.name}.{methodName}. Full type name: {type.FullName}");
                        }
                    } else if (type.IsSubclassOf(typeof(Component)) || type == typeof(Component)) {
                        var target = (Component) reflectionObject;
                        if (target.gameObject && IsAccessBlocked(context, target.gameObject)) {
                            Profiler.EndSample();
                            return LuauError(thread, $"[Airship] Access denied when trying to call method {target.name}.{methodName}. Full type name: {type.FullName}");
                        }
                    }
                }

                int retValue = valueTypeAPI.OverrideMemberMethod(context, thread, reflectionObject, methodName, numParameters,
                    parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
                if (retValue >= 0) {
                    Profiler.EndSample();
                    return retValue;
                }
            }
        }
        
        // Check for IsA call:
        if (methodName == "IsA") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            
            var t = ReflectionList.AttemptGetTypeFromString(typeName);

            if (t == null) {
                Profiler.EndSample();
                return LuauError(thread, $"Error: Unknown type \"{typeName}\" when calling {type.Name}.IsA");
            }

            var isA = t.IsAssignableFrom(type);
            WritePropertyToThread(thread, isA, typeof(bool));

            Profiler.EndSample();
            return 1;
        }

        //Check to see if this was an event (OnEventname)  
        if (methodName.StartsWith("on", StringComparison.OrdinalIgnoreCase) && methodName.Length > 2)
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
                    Profiler.EndSample();
                    return LuauError(thread, $"Error: {methodName} takes 1 parameter (a function!)");
                }
                if (parameterDataPODTypes[0] != (int)LuauCore.PODTYPE.POD_LUAFUNCTION) {
                    Profiler.EndSample();
                    return LuauError(thread, $"Error: {methodName} parameter must be a function");
                }

                int handle = GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
                ParameterInfo[] eventInfoParams = eventInfo.EventHandlerType.GetMethod("Invoke").GetParameters();

                foreach (ParameterInfo param in eventInfoParams) {
                    if (param.ParameterType.IsValueType == true && param.ParameterType.IsPrimitive == false && param.ParameterType.IsEnum == false) {
                        Profiler.EndSample();
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
                    callbackWrapper = callbackWrapper
                };
                eventConnections.Add(eventConnectionId, eventConnection);
                // print("added eventConnection (" + eventConnections.Count + "): " + methodName);

                LuauCore.WritePropertyToThread(thread, eventConnectionId, typeof(int));
                Profiler.EndSample();
                return 1;
            }
        }

        //Use reflection to try and find the method now
        bool countFound = false;
        bool nameFound = false;
        ParameterInfo[] finalParameters = null;
        MethodInfo finalMethod = null;

        var podObjects = UnrollPodObjects(thread, numParameters, parameterDataPODTypes, parameterDataPtrs);

        Profiler.BeginSample("LuauCore.FindMethod");
        FindMethod(context, type, methodName, numParameters, parameterDataPODTypes, podObjects, parameterIsTable, out nameFound, out countFound, out finalParameters, out finalMethod, out var finalExtensionMethod, out var insufficientContext, out var attachContext);
        Profiler.EndSample();

        if (finalMethod == null) {
            Profiler.EndSample();
            
            if (insufficientContext) {
#if AIRSHIP_INTERNAL
                return LuauError(thread, $"Error: Method {methodName} on {type.Name} is not allowed in this context ({context}). Add the type with the desired context to ReflectionList.cs: {type.FullName}");
#else
                return LuauError(thread, $"Error: Method {methodName} on {type.Name} is not allowed in this context ({context}). Full type name: {type.FullName}");
#endif
            }
            if (!nameFound) {
                return LuauError(thread, "Error: Method " + methodName + " not found on " + type.Name + "(" + instanceId + ")");
            }
            if (!countFound) {
                return LuauError(thread, "Error: No version of " + methodName + " on " + type.Name + "(" + instanceId + ") takes " + numParameters + " parameters.");
            }
            return LuauError(thread, "Error: Method " + methodName + " could not match parameter types on " + type.Name + "(" + instanceId + ")");
        }

        // object[] parsedData = null;
        var success = ParseParameterData(thread, numParameters, parameterDataPtrs, parameterDataPODTypes, finalParameters, parameterDataSizes, parameterIsTable, podObjects, attachContext, out var parsedData);
        if (attachContext) {
            parsedData[0] = context;
        }
        if (success == false) {
            Profiler.EndSample();
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

                if (targetTransform != null && IsProtectedScene(targetTransform.gameObject.scene)) {
                    Profiler.EndSample();
                    return LuauError(thread, $"[Airship] Access denied when trying call Object.Instantiate() with a parent transform inside a protected scene \"{targetTransform.gameObject.scene.name}\"");
                }
            } else if ((methodName == "Destroy" || methodName == "DestroyImmediate") && type == typeof(Object)) {
                if (finalParameters.Length >= 1 && parsedData[0] != null) {
                    var paramType = parsedData[0].GetType();
                    if (paramType == typeof(GameObject)) {
                        var param = parsedData[0] as GameObject;
                        if (param != null && IsProtectedScene(param.scene)) {
                            Profiler.EndSample();
                            return LuauError(thread, $"[Airship] Access denied when trying to destroy a protected GameObject \"{param.name}\"");
                        }
                    } else if (paramType == typeof(Component)) {
                        var param = parsedData[0] as Component;
                        if (param != null && IsProtectedScene(param.gameObject.scene)) {
                            Profiler.EndSample();
                            return LuauError(thread, $"[Airship] Access denied when trying to destroy a protected Component \"{param.gameObject.name}\"");
                        }
                    }
                }
            } else if (methodName == "SetParent" && type == typeof(Transform)) {
                var callingTransform = reflectionObject as Transform;
                if (callingTransform != null && IsAccessBlocked(context, callingTransform.gameObject)) {
                    Profiler.EndSample();
                    return LuauError(thread, $"[Airship] Access denied when trying set parent of a transform inside a protected scene \"{callingTransform.gameObject.scene.name}\"");
                }

                if (parsedData[0] != null && parsedData[0].GetType() == typeof(Transform)) {
                    var targetTransform = (Transform)parsedData[0];
                    if (targetTransform != null && IsAccessBlocked(context, targetTransform.gameObject)) {
                        Profiler.EndSample();
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
                Profiler.EndSample();
                return ret;
            }
            Marshal.WriteInt32(shouldYield, shouldYieldBool ? 1 : 0);
            Profiler.EndSample();
            return returnCount;
        }

        Profiler.BeginSample("LuauCore.InvokeMethod");
        try {
            returnValue = finalMethod.Invoke(invokeObj, parsedData.Array);
        } catch (TargetInvocationException e) {
            Profiler.EndSample();
            return LuauError(thread, "Error: Exception thrown in method " + type.Name + "." + finalMethod.Name + ": " + e.InnerException);
        } catch (Exception e) {
            Profiler.EndSample();
            return LuauError(thread, "Error: Exception thrown in method " + type.Name + "." + finalMethod.Name + ": " + e);
        } finally {
            Profiler.EndSample();
        }

        WriteMethodReturnValuesToThread(thread, type, finalMethod.ReturnType, finalParameters, returnValue, parsedData.Array);
        Profiler.EndSample();
        return returnCount;
    }

    private static void WriteMethodReturnValuesToThread(IntPtr thread, Type type, Type returnType, ParameterInfo[] finalParameters, object returnValue, object[] parsedData) {
        if (type.IsSZArray) {
            //When returning array types, finalMethod.ReturnType is wrong
            returnType = type.GetElementType();
        }
        //Write the final param
        WritePropertyToThread(thread, returnValue, returnType);

        //Write the out params
        for (var j = 0; j < finalParameters.Length; j++) {
            if (finalParameters[j].IsOut) {
                WritePropertyToThread(thread, parsedData[j], finalParameters[j].ParameterType.GetElementType());
            }
        }
    }
    
    [AOT.MonoPInvokeCallback(typeof(LuauPlugin.ConstructorCallback))]
    static unsafe int ConstructorCallback(LuauContext context, IntPtr thread, IntPtr classNamePtr, int classNameSize, int numParameters, IntPtr firstParameterType, IntPtr firstParameterData, IntPtr firstParameterSize, IntPtr firstParameterIsTable) {
        CurrentContext = context;
        
        if (!IsReady) return 0;
        
        string staticClassName = LuauCore.PtrToStringUTF8(classNamePtr, classNameSize);
        
        LuauCore instance = LuauCore.CoreInstance;

        Type type = null;

        //Cast/marshal parameter data
        Marshal.Copy(firstParameterData, _parameterDataPtrs, 0, numParameters);
        Marshal.Copy(firstParameterSize, _parameterDataSizes, 0, numParameters);
        Marshal.Copy(firstParameterType, _parameterDataPODTypes, 0, numParameters);
        Marshal.Copy(firstParameterIsTable, _parameterIsTable, 0, numParameters);

        var parameterDataPtrs = new ArraySegment<IntPtr>(_parameterDataPtrs, 0, numParameters);
        var parameterDataSizes = new ArraySegment<int>(_parameterDataSizes, 0, numParameters);
        var parameterDataPODTypes = new ArraySegment<int>(_parameterDataPODTypes, 0, numParameters);
        var parameterIsTable = new ArraySegment<int>(_parameterIsTable, 0, numParameters);
        
        //This detects STATIC classobjects only - live objects do not report the className
        instance.unityAPIClasses.TryGetValue(staticClassName, out BaseLuaAPIClass staticClassApi);
        if (staticClassApi == null) {
            Debug.Log("Constructor on " + staticClassName + " failed. Types not found.");
            return 0;
        }
        
        
        type = staticClassApi.GetAPIType();
        // !!! This could be broken
        //This handles where we need to replace a method or implement a method directly in the c# side eg: GameObject.new 
        int retValue = staticClassApi.OverrideStaticMethod(context, thread, "new", numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
        if (retValue >= 0) {
            return retValue;
        }
        
        return RunConstructor(thread, type, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes, parameterIsTable);
    }

    private static int InvokeMethodAsync(LuauContext context, IntPtr thread, Type type, MethodInfo method, object obj, ArraySegment<object> parameters, out bool shouldYield) {
        try {
            var task = (Task)method.Invoke(obj, parameters.Array);
            var awaitingTask = new AwaitingTask {
#if UNITY_EDITOR
                DebugName = $"{method.Name} ({method.DeclaringType.FullName})",
#endif
                Thread = thread,
                ThreadRef = 0,
                Task = task,
                Method = method,
                Context = context,
                Type = type,
            };
            
            if (task.IsCompleted) {
                shouldYield = false;
                if (task.IsFaulted) {
                    return LuauError(thread, $"Error: Exception thrown in {type.Name} {method.Name}: {task.Exception.Message}");
                }
                ResumeAsyncTask(awaitingTask, true);
                return 0;
            }

            LuauPluginRaw.PushThread(thread);
            awaitingTask.ThreadRef = LuauPluginRaw.Ref(thread, -1);
            LuauPluginRaw.Pop(thread, 1);

            _awaitingTasks.Add(awaitingTask);
            // LuauState.FromContext(context).TryGetScriptBindingFromThread(thread, out var binding);
            //
            // if (binding != null) {
            //     binding.m_asyncYield = true;
            // } else {
            //     LuauPlugin.LuauPinThread(thread);
            // }
            // ThreadDataManager.SetThreadYielded(thread, true);

            shouldYield = true;
            return 0;
        } catch (Exception e) {
            shouldYield = false;
            return LuauError(thread, $"Error: Exception thrown in {type.Name} {method.Name}: {e.Message}");
        }
    }

    private static void ResumeAsyncTask(AwaitingTask awaitingTask, bool immediate = false) {
        var thread = awaitingTask.Thread;

        if (awaitingTask.ThreadRef != 0) {
            LuauPluginRaw.Unref(thread, awaitingTask.ThreadRef);
        }

        if (awaitingTask.Task.IsFaulted) {
            try {
                LuauPluginRaw.PushString(thread, $"Error: Exception thrown in {awaitingTask.Type.Name} {awaitingTask.Method.Name}: {awaitingTask.Task.Exception.Message}");
                ThreadDataManager.Error(thread);
                LuauPlugin.LuauResumeThreadError(thread);
            } catch (LuauException e) {
                Debug.LogException(e);
            }
            
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
            try {
                LuauPlugin.LuauResumeThread(thread, nArgs);
            } catch (LuauException e) {
                Debug.LogException(e);
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

    private struct FastCacheEntry {
        public bool exists;
        public Type ObjectType;
        public string PropName;
    }
    
    // This is faster frequently it seems, but could be slow if we keep overwriting the same entry
    // It only speeds up dictionary get time.
    private static int propGetFastCacheSize = 100;
    private static FastCacheEntry[] fastPropGetCacheKeys = new FastCacheEntry[propGetFastCacheSize];
    private static PropertyGetReflectionCache[] fastPropGetCacheValues = new PropertyGetReflectionCache[propGetFastCacheSize];

    private static PropertyGetReflectionCache? GetPropertyCacheValue(Type objectType, string propName) {
        var key = new PropertyCacheKey(objectType, propName);
        var l1Key = key.GetHashCode() % propGetFastCacheSize;
        if (l1Key < 0) l1Key += propGetFastCacheSize;
        
        var fastEntry = fastPropGetCacheKeys[l1Key];
        if (fastEntry.exists && fastEntry.ObjectType == objectType && fastEntry.PropName == propName) {
            return fastPropGetCacheValues[l1Key];
        }

        // Note: only caching on type full name + prop name. Possible collision on assemblies
        if (propertyGetCache.TryGetValue(key, out var data)) {
            fastPropGetCacheKeys[l1Key] = new FastCacheEntry() {
                ObjectType = objectType,
                PropName = propName,
                exists = true,
            };
            fastPropGetCacheValues[l1Key] = data;
            return data;
        }

        return null;
    }

    private static PropertyGetReflectionCache SetPropertyCacheValue(Type objectType, string propName, PropertyInfo propertyInfo) {
        var cacheData = new PropertyGetReflectionCache {
            t = propertyInfo.PropertyType,
            propertyInfo = propertyInfo,
            IsNativeClass = propertyInfo.DeclaringType.GetCustomAttributes(false)
                .Any(attr => attr.GetType().Name == "NativeClassAttribute")
        };
        LuauCore.propertyGetCache[new PropertyCacheKey(objectType, propName)] = cacheData;
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
