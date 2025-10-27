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
using Code.Luau.LuauAssembly;
using Code.Luau.LuauAssembly.Protection;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Profiling;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using System.Text.RegularExpressions;
#endif

public partial class LuauCore : MonoBehaviour {
    private static LuauPluginNative.PrintCallback printCallback_holder = PrintCallback;

    private const int MaxParameters = 20;
    private const int MaxParsedObjects = 100;
    
    private LuauPluginNative.ComponentSetEnabledCallback componentSetEnabledCallback_holder;
    private LuauPluginNative.GetPropertyCallback getPropertyCallback_holder;
    private LuauPluginNative.SetPropertyCallback setPropertyCallback_holder;
    private LuauPluginNative.CallMethodCallback callMethodCallback_holder;
    private LuauPluginNative.ObjectGCCallback objectGCCallback_holder;
    private LuauPluginNative.RequireCallback requireCallback_holder;
    private LuauPluginNative.ConstructorCallback constructorCallback_holder;
    private LuauPluginNative.RequirePathCallback requirePathCallback_holder;
    private LuauPluginNative.ToStringCallback toStringCallback_holder;
    private LuauPluginNative.IsObjectDestroyedCallback isObjectDestroyedCallback_holder;
    private LuauPluginNative.GetUnityObjectName getUnityObjectNameCallback_holder;

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
        public string Traceback;
    }
    
    private struct MemberGetReflectionCache {
        /// <summary>
        /// This is true if the property info exists for this type / prop combo. This
        /// is to avoid trying to look up the property info constantly if it doesn't exist.
        /// </summary>
        public bool Exists;
        
        public Type memberType;
        public Type objectType;
        /// <summary>
        /// Can either be a FieldInfo or PropertyInfo based on the type of member.
        /// </summary>
        public MemberInfo memberInfo;
        public Delegate GetMember;
        public bool HasGetMemberFunc;
        public bool IsNativeClass;
        public bool IsStruct;
    }

    // Hopefully faster dictionary comparison / hash time
    private readonly struct MemberGetCacheKey : IEquatable<MemberGetCacheKey> {
        private readonly Type _type;
        private readonly string _propertyName;
        private readonly int _hashCode;

        public MemberGetCacheKey(Type type, string propertyName) {
            _type = type;
            _propertyName = propertyName;
            // Pre-compute hash code to avoid repeated calculations
            _hashCode = HashCode.Combine(type.GetHashCode(), propertyName.GetHashCode());
        }

        public override int GetHashCode() {
            return _hashCode;
        }

        public bool Equals(MemberGetCacheKey other) {
            return _type == other._type && string.Equals(_propertyName, other._propertyName);
        }
    }

    private struct ReverseObjectKeyUpdater : IDisposable {
        private object _instance;
        private int _instanceId;
        private bool _isValueType;
        public ReverseObjectKeyUpdater(object instance, int instanceId, Type instanceType) {
            this._instance = instance;
            this._instanceId = instanceId;
            this._isValueType = instanceType.IsValueType;

            if (this._isValueType) {
                ThreadDataManager.DeleteReverseObjectKey(instance);
            }
        }
        public void Dispose() {
            if (this._isValueType) {
                ThreadDataManager.UpdateReverseObjectKey(this._instanceId, this._instance);
            }
        }
    }
    
    public struct EventConnection {
        public int id;
        public object target;
        public System.Delegate handler;
        public EventInfo eventInfo;
        public CallbackWrapper callbackWrapper;
    }

    private static Dictionary<MemberGetCacheKey, MemberGetReflectionCache> memberGetCache = new();
    
    public static Dictionary<int, EventConnection> eventConnections = new();
    private static int eventIdCounter = 0;

    private static readonly Queue<AwaitingTask> _completedTasks = new();
    private static int _taskId = 0;

    public static GameObject luauModulesFolder;

    private void CreateCallbacks() {
        printCallback_holder = PrintCallback;
        getPropertyCallback_holder = GetPropertySafeCallback;
        setPropertyCallback_holder = SetPropertySafeCallback;
        callMethodCallback_holder = CallMethodSafeCallback;
        objectGCCallback_holder = ObjectGcCallback;
        requireCallback_holder = RequireCallback;
        constructorCallback_holder = ConstructorCallback;
        requirePathCallback_holder = RequirePathCallback;
        toStringCallback_holder = ToStringCallback;
        componentSetEnabledCallback_holder = SetComponentEnabledCallback;
        isObjectDestroyedCallback_holder = IsObjectDestroyedCallback;
        getUnityObjectNameCallback_holder = GetUnityObjectNameCallback;
    }

    public static int LuauError(IntPtr thread, string err) {
        LuauPlugin.PushCsError(err);
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

    [AOT.MonoPInvokeCallback(typeof(LuauPluginNative.PrintCallback))]
    private static void PrintCallback(LuauContext context, IntPtr thread, LuauLogLevel logLevel, int gameObjectId, IntPtr buffer, int length) {
        LuauProtection.CurrentContext = context;
        
        var res = Marshal.PtrToStringUTF8(buffer, length);
        UnityEngine.Object logContext = _coreInstance;

#if UNITY_EDITOR
        // Only do link injection and context fetching in-editor (expensive, and useless outside of editor)

        if (logLevel == LuauLogLevel.Warning || logLevel == LuauLogLevel.Error) {
            if (res.Contains(".lua:", StringComparison.OrdinalIgnoreCase)) {
                res = InjectAnchorLinkToLuaScript(res);
            }
        }

        if (gameObjectId >= 0) {
            var obj = ThreadDataManager.GetObjectReference(thread, gameObjectId, true);
            if (obj is UnityEngine.Object unityObj) {
                logContext = unityObj;
            }
        }
#endif

        switch (logLevel) {
            case LuauLogLevel.Log:
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, logContext, "{0}", res);
                break;
            case LuauLogLevel.Warning:
                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, logContext, "{0}", res);
                break;
            case LuauLogLevel.Error:
#if UNITY_STANDALONE && !UNITY_EDITOR
                // This is a temporary fix for a lag spike that happens in client builds. Errors cause a spike, but warnings do not.
                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, logContext, "{0}", "[ERROR] " + res);
#else
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, logContext, "{0}", res);
#endif
                ThreadDataManager.Error(thread);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, "Luau plugin provided unknown log level");
        }
    }
    
    [AOT.MonoPInvokeCallback(typeof(LuauPluginNative.ToStringCallback))]
    static void ToStringCallback(IntPtr thread, int instanceId, IntPtr str, int maxLen, out int len) {
        var obj = ThreadDataManager.GetObjectReference(thread, instanceId, true, true);
        
        var toString = obj != null ? obj.ToString() : "null";
        
        var bytes = Encoding.UTF8.GetBytes(toString);
        len = bytes.Length > maxLen ? maxLen : bytes.Length;

        Marshal.Copy(bytes, 0, str, len);
    }

    [AOT.MonoPInvokeCallback(typeof(LuauPluginNative.IsObjectDestroyedCallback))]
    static int IsObjectDestroyedCallback(int instanceId) {
        return ThreadDataManager.IsUnityObjectReferenceDestroyed(instanceId) ? 1 : 0;
    }

    [AOT.MonoPInvokeCallback(typeof(LuauPluginNative.GetUnityObjectName))]
    static void GetUnityObjectNameCallback(IntPtr thread, int instanceId, IntPtr str, int maxLen, out int len) {
        var obj = ThreadDataManager.GetObjectReference(thread, instanceId, true, true);
        if (obj is UnityEngine.Object unityObj) {
            var n = unityObj.name;
            var bytes = Encoding.UTF8.GetBytes(n);
            len = bytes.Length > maxLen ? maxLen : bytes.Length;
            Marshal.Copy(bytes, 0, str, len);
            
            return;
        }

        len = 0;
    }

    //when a lua thread gc releases an object, make sure our GC knows too
    [AOT.MonoPInvokeCallback(typeof(LuauPluginNative.ObjectGCCallback))]
    static unsafe int ObjectGcCallback(int instanceId, IntPtr objectDebugPointer) {
        ThreadDataManager.DeleteObjectReference(instanceId);
        // Debug.Log("GC " + instanceId + " ptr:" + objectDebugPointer);
        return 0;
    }

    // When a lua object wants to set a property
    [AOT.MonoPInvokeCallback(typeof(LuauPluginNative.SetPropertyCallback))]
    private static int SetPropertySafeCallback(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameLength, int propertyNameAtom, int type, IntPtr propertyData, ulong propertyDataSize, byte isTable) {
        var ret = 0;
        try {
            ret = SetProperty(context, thread, instanceId, classNamePtr, classNameSize, propertyName, propertyNameLength, propertyNameAtom, type, propertyData, propertyDataSize, isTable);
        } catch (Exception e) {
            ret = LuauError(thread, e.Message);
        }

        return ret;
    }
    
    private static int SetProperty(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameLength, int propertyNameAtom, int typeInt, IntPtr propertyData, ulong propertyDataSize, byte isTable) {
        LuauProtection.CurrentContext = context;
        
        var propName = LuauCore.PtrToStringUTF8(propertyName, propertyNameLength, out ulong propNameHash);
        var type = (PODTYPE)typeInt;

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
                    if (LuauProtection.IsAccessBlocked(context, target)) {
                        return target != null ?
                            LuauError(thread, "[Airship] Access denied when trying to set property " + target.name + "." + propName) :
                            LuauError(thread, "[Airship] Access denied when trying to set property (unknown)." + propName);
                    }
                } else if (sourceType.IsSubclassOf(typeof(Component)) || sourceType == typeof(Component)) {
                    var target = (Component) objectReference;
                    if (target != null && target.gameObject != null && LuauProtection.IsAccessBlocked(context, target.gameObject)) {
                        return LuauError(thread, "[Airship] Access denied when trying to set property " + target.name + "." + propName);
                    }
                }
            }

            _coreInstance.unityAPIClassesByType.TryGetValue(sourceType, out var valueTypeAPI);

            Type t = null;
            PropertyInfo property = null;
            FieldInfo field = null;
            
            MemberGetReflectionCache? cacheData;
            if (!(cacheData = LuauCore.GetMemberCacheValue(sourceType, propName)).HasValue) {
                MemberInfo memberInfo = null;
                
                // Try as property
                memberInfo = LuauCore.CoreInstance.GetPropertyInfoForType(sourceType, propName, propNameHash);
                // Try as field
                if (memberInfo == null) {
                    if (classNameSize != 0) {
                        memberInfo = sourceType.GetField(propName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    } else {
                        memberInfo = LuauCore.CoreInstance.GetFieldInfoForType(sourceType, propName, propNameHash);
                    }
                }
                
                
                cacheData = LuauCore.SetMemberCacheValue(sourceType, propName, memberInfo);
            }

            if (cacheData.Value.Exists) {
                if (cacheData.Value.memberInfo is PropertyInfo pi) property = pi;
                if (cacheData.Value.memberInfo is FieldInfo fi) field = fi;
                t = cacheData.Value.memberType;
            }

            if (t == null) {
                return LuauError(thread, "ERROR - (" + sourceType.Name + ")." + propName + " set property not found");
            }

            if (printReferenceAssemblies) {
                referencedAssemblies.Add(sourceType.Assembly.FullName);
            }

            using (new ReverseObjectKeyUpdater(objectReference, instanceId, sourceType)) {
                if (valueTypeAPI != null) {
                    var retValue = valueTypeAPI.OverrideMemberSetter(context, thread, objectReference, propName, type, propertyData,
                        (int)propertyDataSize);
                    if (retValue >= 0) {
                        return retValue;
                    }
                }
                
                if (isTable != 0) {
                    if (t.IsArray) {
                        if (!ParseTableParameter(thread, type, t, (int)propertyDataSize, -1, out var value)) {
                            return LuauError(thread, $"Luau table of type {type} cannot be parsed into C# array of type {t}");
                        }
                        if (field != null) {
                            field.SetValue(objectReference, value);
                        } else {
                            property.SetValue(objectReference, value);
                        }
                        return 0;
                    }
                    
                    if (t.IsClass || (t.IsValueType && !t.IsPrimitive)) {
                        return LuauError(thread, $"Parsing Luau table into C# {(t.IsClass ? "class" : "struct")} {t} is currently not supported");
                    }
                    
                    return LuauError(thread, $"Cannot parse Luau table into type {t}");
                }

                switch (type) {
                    case PODTYPE.POD_OBJECT: {
                        var propertyObjectRef = NewObjectFromPointer(propertyData, thread);

                        if (t.IsAssignableFrom(propertyObjectRef.GetType())) {
                            if (
                                propName == "parent"
                                && context != LuauContext.Protected
                                && objectReference.GetType() == typeof(Transform)
                                && propertyObjectRef.GetType() == typeof(Transform)
                            ) {
                                var targetTransform = (Transform)objectReference;
                                if (LuauProtection.IsProtectedScene(targetTransform.gameObject.scene)) {
                                    return LuauError(thread, "[Airship] Access denied when trying to set parent of protected object " + targetTransform.gameObject.name);
                                }

                                var valueTransform = (Transform)propertyObjectRef;
                                if (LuauProtection.IsProtectedScene(valueTransform.gameObject.scene)) {
                                    return LuauError(thread, "[Airship] Access denied when trying to set parent of " + targetTransform.gameObject.name + " to a child of scene " + valueTransform.gameObject.scene.name);
                                }
                            }
                            
                            if (field != null) {
                                field.SetValue(objectReference, propertyObjectRef);
                            } else {
                                SetPropertyValue<object>(objectReference, propertyObjectRef, property);
                            }
                            return 0;
                        }

                        break;
                    }

                    case PODTYPE.POD_VECTOR3: {
                        if (t.IsAssignableFrom(vector3Type)) {
                            var v = NewVector3FromPointer(propertyData);
                            if (field != null) {
                                SetFieldValue<Vector3>(objectReference, v, field);
                            } else {
                                SetPropertyValue<Vector3>(objectReference, v, property);
                            }
                            return 0;
                        }
                        if (t.IsAssignableFrom(vector3IntType)) {
                            var v = NewVector3FromPointer(propertyData);
                            if (field != null) {
                                SetFieldValue<Vector3Int>(objectReference, Vector3Int.FloorToInt(v), field);
                            } else {
                                SetPropertyValue<Vector3Int>(objectReference, Vector3Int.FloorToInt(v), property);
                            }
                            return 0;
                        }
                        break;
                    }
                    case PODTYPE.POD_BOOL: {
                        if (t.IsAssignableFrom(boolType)) {
                            var val = NewBooleanFromPointer(propertyData);
                            if (field != null) {
                                SetFieldValue<bool>(objectReference, val, field);
                            } else {
                                SetPropertyValue<bool>(objectReference, val, property);
                            }

                            return 0;
                        }

                        break;
                    }

                    case PODTYPE.POD_DOUBLE: { // Also integers
                        var doubleValue = NewDoubleFromPointer(propertyData);

                        if (t.IsAssignableFrom(doubleType)) {
                            if (field != null) {
                                SetFieldValue<double>(objectReference, doubleValue, field);
                            } else {
                                SetPropertyValue<double>(objectReference, doubleValue, property);
                            }

                            return 0;
                        } else if (t.IsAssignableFrom(ushortType)) {
                            if (field != null) {
                                SetFieldValue<ushort>(objectReference, (ushort)doubleValue, field);
                            } else {
                                SetPropertyValue<ushort>(objectReference, (ushort)doubleValue, property);
                            }

                            return 0;
                        } else if (t.IsAssignableFrom(floatType)) {
                            if (field != null) {
                                SetFieldValue<float>(objectReference, (float) doubleValue, field);
                            } else {
                                SetPropertyValue<float>(objectReference, (float) doubleValue, property);
                            }

                            return 0;
                        } else if (t.IsAssignableFrom(intType) || t.BaseType == enumType ||
                                   t.IsAssignableFrom(enumType) || t.IsAssignableFrom(byteType)) {
                            if (field != null) {
                                SetFieldValue<int>(objectReference, (int)doubleValue, field);
                            } else {
                                SetPropertyValue<int>(objectReference, (int)doubleValue, property);
                            }

                            return 0;
                        } else if (t.IsAssignableFrom(uIntType)) {
                            if (field != null) {
                                SetFieldValue<uint>(objectReference, unchecked((uint)doubleValue), field);
                            } else {
                                SetPropertyValue<uint>(objectReference, unchecked((uint)doubleValue), property);
                            }
                        } else if (t.IsAssignableFrom(longType)) {
                            if (field != null) {
                                SetFieldValue<long>(objectReference, (long)doubleValue, field);
                            } else {
                                SetPropertyValue<long>(objectReference, (long)doubleValue, property);
                            }

                            return 0;
                        } else if (t.IsAssignableFrom(uLongType)) {
                            if (field != null) {
                                SetFieldValue<ulong>(objectReference, (ulong)doubleValue, field);
                            } else {
                                SetPropertyValue<ulong>(objectReference, (ulong)doubleValue, property);
                            }

                            return 0;
                        }

                        break;
                    }

                    case PODTYPE.POD_STRING: {
                        if (t.IsAssignableFrom(stringType)) {
                            var dataStr = Marshal.PtrToStringUTF8(propertyData, (int)propertyDataSize);
                            if (field != null) {
                                field.SetValue(objectReference, dataStr);
                            } else {
                                SetPropertyValue<string>(objectReference, dataStr, property);
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
                                SetPropertyValue<object>(objectReference, null, property);
                            }
                            return 0;
                        }
                        break;
                    }

                    case PODTYPE.POD_RAY: {
                        if (t.IsAssignableFrom(rayType)) {
                            if (field != null) {
                                SetFieldValue<Ray>(objectReference, NewRayFromPointer(propertyData), field);
                            } else {
                                SetPropertyValue<Ray>(objectReference, NewRayFromPointer(propertyData), property);
                            }
                            return 0;
                        }
                        break;
                    }

                    case PODTYPE.POD_COLOR: {
                        if (t.IsAssignableFrom(colorType)) {
                            if (field != null) {
                                SetFieldValue<Color>(objectReference, NewColorFromPointer(propertyData), field);
                            } else {
                                SetPropertyValue<Color>(objectReference, NewColorFromPointer(propertyData), property);
                            }
                            return 0;
                        }
                        break;
                    }

                    case PODTYPE.POD_PLANE: {
                        if (t.IsAssignableFrom(planeType)) {
                            if (field != null) {
                                SetFieldValue<Plane>(objectReference, NewPlaneFromPointer(propertyData), field);
                            } else {
                                SetPropertyValue<Plane>(objectReference, NewPlaneFromPointer(propertyData), property);
                            }
                            return 0;
                        }
                        break;
                    }

                    case PODTYPE.POD_RECT: {
                        if (t.IsAssignableFrom(rectType)) {
                            if (field != null) {
                                SetFieldValue<Rect>(objectReference, NewRectFromPointer(propertyData), field);
                            } else {
                                SetPropertyValue<Rect>(objectReference, NewRectFromPointer(propertyData), property);
                            }
                            return 0;
                        }
                        break;
                    }

                    case PODTYPE.POD_QUATERNION: {
                        if (t.IsAssignableFrom(quaternionType)) {
                            if (field != null) {
                                SetFieldValue<Quaternion>(objectReference, NewQuaternionFromPointer(propertyData), field);
                            } else {
                                SetPropertyValue<Quaternion>(objectReference, NewQuaternionFromPointer(propertyData), property);
                            }
                            return 0;
                        }
                        break;
                    }

                    case PODTYPE.POD_VECTOR2: {
                        if (t.IsAssignableFrom(vector2Type)) {
                            if (field != null) {
                                SetFieldValue<Vector2>(objectReference, NewVector2FromPointer(propertyData), field);
                            } else {
                                SetPropertyValue<Vector2>(objectReference, NewVector2FromPointer(propertyData), property);
                            }
                            return 0;
                        }
                        break;
                    }

                    case PODTYPE.POD_VECTOR4: {
                        if (t.IsAssignableFrom(vector4Type)) {
                            if (field != null) {
                                SetFieldValue<Vector4>(objectReference, NewVector4FromPointer(propertyData), field);
                            } else {
                                SetPropertyValue<Vector4>(objectReference, NewVector4FromPointer(propertyData), property);
                            }
                            return 0;
                        }
                        break;
                    }

                    case PODTYPE.POD_MATRIX: {
                        if (t.IsAssignableFrom(matrixType)) {
                            if (field != null) {
                                SetFieldValue<Matrix4x4>(objectReference, NewMatrixFromPointer(propertyData), field);
                            } else {
                                SetPropertyValue<Matrix4x4>(objectReference, NewMatrixFromPointer(propertyData), property);
                            }

                            return 0;
                        }
                        break;
                    }

                    case PODTYPE.POD_BINARYBLOB: {
                        if (t.IsAssignableFrom(binaryBlobType)) {
                            if (field != null) {
                                field.SetValue(objectReference, NewBinaryBlobFromPointer(propertyData));
                            } else {
                                SetPropertyValue<BinaryBlob>(objectReference, NewBinaryBlobFromPointer(propertyData), property);
                            }

                            return 0;
                        }
                        break;
                    }

                    case PODTYPE.POD_BUFFER: {
                        if (t.IsAssignableFrom(luauBufferType)) {
                            if (field != null) {
                                field.SetValue(objectReference, NewLuauBufferFromPointer(propertyData, (int)propertyDataSize));
                            } else {
                                SetPropertyValue<LuauBuffer>(objectReference, NewLuauBufferFromPointer(propertyData, (int)propertyDataSize), property);
                            }

                            return 0;
                        }
                        break;
                    }
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

    private static unsafe T GetFieldValue<T>(object instance, FieldInfo fieldInfo) where T : unmanaged {
        var addr = UnsafeUtility.PinGCObjectAndGetAddress(instance, out ulong handle);
        try {
            var offset = UnsafeUtility.GetFieldOffset(fieldInfo);
            return *(T*)((byte*)addr + offset);
        } finally {
            UnsafeUtility.ReleaseGCObject(handle);
        }
    }
    
    private static unsafe void SetFieldValue<T>(object instance, T value, FieldInfo fieldInfo) where T : unmanaged {
        if (fieldInfo.IsStatic) {
            // Not sure how to do non-alloc static field sets, so just use reflection for now
            // (these are relatively rare anyways)
            fieldInfo.SetValue(null, value);
        } else {
            var addr = UnsafeUtility.PinGCObjectAndGetAddress(instance, out ulong handle);
            try {
                var offset = UnsafeUtility.GetFieldOffset(fieldInfo);
                *(T*)((byte*)addr + offset) = value;
            } finally {
                UnsafeUtility.ReleaseGCObject(handle);
            }
        }
    }

    private static T GetPropertyValue<T>(object instance, MemberGetReflectionCache cacheData) {
        if (cacheData.memberInfo is not PropertyInfo propertyInfo) throw new Exception("Failed to GetPropertyValue: MemberInfo must represent a property.");;
        
        if (typeof(T) == typeof(object) || cacheData.IsNativeClass || cacheData.IsStruct) {
            var res = (T) propertyInfo.GetValue(instance);
            return res;
        }
        
        if (!cacheData.HasGetMemberFunc) {
            MethodInfo getMethodInfo = propertyInfo.GetGetMethod();

            unsafe {
                delegate*<object, T> funcPtr = (delegate*<object, T>) getMethodInfo
                    .MethodHandle
                    .GetFunctionPointer()
                    .ToPointer();

                // Create a delegate that wraps the function pointer
                var getter = new Getter<T>(obj => {
                    unsafe {
                        return funcPtr(obj);
                    }
                });
            
                cacheData.HasGetMemberFunc = true;
                cacheData.GetMember = getter;

                var key = new MemberGetCacheKey(cacheData.objectType, cacheData.memberInfo.Name);
                UpdateMemberGetCache(key, cacheData);
            }
        }
        var finalRes = ((Getter<T>) cacheData.GetMember)(instance);
        return finalRes;
    }
    
    private static void SetPropertyValue<T>(object instance, T value, PropertyInfo pi) {
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
    [AOT.MonoPInvokeCallback(typeof(LuauPluginNative.GetPropertyCallback))]
    private static int GetPropertySafeCallback(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameLength, int propertyNameAtom) {
        var ret = 0;
        try {
            ret = GetProperty(context, thread, instanceId, classNamePtr, classNameSize, propertyName, propertyNameLength, propertyNameAtom);
        } catch (Exception e) {
            ret = LuauError(thread, $"{e.GetType()}: {e.Message}");
        }

        return ret;
    }

    private static int GetProperty(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr propertyName, int propertyNameLength, int propertyNameAtom) {
        LuauProtection.CurrentContext = context;

        string propName = LuauCore.PtrToStringUTF8(propertyName, propertyNameLength, out ulong propNameHash);
        LuauCore instance = LuauCore.CoreInstance;

        //This detects STATIC classobjects only - live objects do not report the className
        if (classNameSize != 0) {
            string staticClassName = LuauCore.PtrToStringUTF8(classNamePtr, classNameSize);
            instance.unityAPIClasses.TryGetValue(staticClassName, out BaseLuaAPIClass staticClassApi);
            if (staticClassApi == null) {
                return LuauError(thread, "ERROR - type of " + staticClassName + " class not found");
            }

            var classType = staticClassApi.GetAPIType();
            
            if (printReferenceAssemblies) {
                referencedAssemblies.Add(classType.Assembly.FullName);
            }

            // Get PropertyInfo from cache if possible -- otherwise put it in cache
            MemberGetReflectionCache? cacheData;
            if (!(cacheData = LuauCore.GetMemberCacheValue(classType, propName)).HasValue) {
                MemberInfo memberInfo = null;
                try {
                    memberInfo = classType.GetProperty(propName,
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                } catch (AmbiguousMatchException) {
                    // If we get an ambiguous match we should use the type declared explicitly in this static class
                    // (rather than whatever inherited static is causing the ambiguity)
                    memberInfo = classType.GetProperty(propName,
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                }

                if (memberInfo == null) {
                    memberInfo = classType.GetField(propName,
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                }

                cacheData = LuauCore.SetMemberCacheValue(classType, propName, memberInfo);
            }

            // Try access as PropertyInfo
            if (cacheData.Value.Exists && cacheData.Value.memberInfo is PropertyInfo propInfo) {
                if (!ReflectionList.IsMemberAllowed(classType, cacheData.Value.memberInfo, context)) {
                    return LuauError(thread, $"[Airship] Access denied when trying to read {staticClassName}.{propName}.");
                }

                // Fast, non-alloc write
                if (FastGetAndWriteValueProperty(thread, null, cacheData.Value)) {
                    return 1;
                }
                
                System.Object value = propInfo.GetValue(null);
                WritePropertyToThread(thread, value, cacheData.Value.memberType);
                return 1;
            }

            // Try access as C# event
            var eventInfo = classType.GetRuntimeEvent(propName);
            if (eventInfo != null) {
                if (!ReflectionList.IsMemberAllowed(classType, eventInfo, context)) {
                    return LuauError(thread, $"[Airship] Access denied when trying to read {staticClassName}.{propName}.");
                }
                
                return LuauSignalWrapper.HandleCsEvent(context, thread, staticClassApi, instanceId, propNameHash,
                    eventInfo, true);
            }

            // Try access as FieldInfo
            if (cacheData.Value.Exists && cacheData.Value.memberInfo is FieldInfo fieldInfo) {
                if (!ReflectionList.IsMemberAllowed(classType, fieldInfo, context)) {
                    return LuauError(thread, $"[Airship] Access denied when trying to read {staticClassName}.{propName}.");
                }
                
                Type t = fieldInfo.FieldType;
                System.Object value = fieldInfo.GetValue(null);
                WritePropertyToThread(thread, value, t);
                return 1;
            }

            return LuauError(thread, "ERROR - " + propName + " get property not found on class " + staticClassName);
        }
        else {
            // Not a static class object:
            
            System.Object objectReference = ThreadDataManager.GetObjectReference(thread, instanceId);
            if (objectReference == null) {
                return LuauError(thread,
                    "Error: InstanceId not currently available:" + instanceId + ". propName=" + propName);
            }

            Type sourceType = objectReference.GetType();

            // Scene Protection
            if (context != LuauContext.Protected) {
                if (objectReference is GameObject targetGo) {
                    // var target = (GameObject)objectReference;
                    if (LuauProtection.IsAccessBlocked(context, targetGo)) {
                        return LuauError(thread,
                            "[Airship] Access denied when trying to read " + targetGo.name + ".");
                    }
                }
                else if (sourceType.IsAssignableFrom(typeof(Component))) {
                    var target = (Component)objectReference;
                    if (target && LuauProtection.IsAccessBlocked(context, target.gameObject)) {
                        return LuauError(thread,
                            "[Airship] Access denied when trying to read " + target.name + ".");
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
            MemberGetReflectionCache? cacheData;
            if (!(cacheData = LuauCore.GetMemberCacheValue(sourceType, propName)).HasValue) {
                MemberInfo memberInfo = instance.GetPropertyInfoForType(sourceType, propName, propNameHash);
                if (memberInfo == null) {
                    memberInfo = instance.GetFieldInfoForType(sourceType, propName, propNameHash);
                }
                cacheData = LuauCore.SetMemberCacheValue(sourceType, propName, memberInfo);
            }

            // Handle as property
            if (cacheData.Value.Exists && cacheData.Value.memberInfo is PropertyInfo) {
                var propertyType = cacheData.Value.memberType;
                if (!ReflectionList.IsMemberAllowed(sourceType, cacheData.Value.memberInfo, context)) {
                    return LuauError(thread, $"[Airship] Access denied when trying to read {sourceType.Name}.{propName}.");
                }
                
                try {
                    // Try a fast write on value type (Vector3, int, etc. Not objects)
                    if (FastGetAndWriteValueProperty(thread, objectReference, cacheData.Value)) {
                        return 1;
                    }

                    var value = GetPropertyValue<object>(objectReference, cacheData.Value);
                    if (value != null) {
                        var valueType = value.GetType();
                        if (value is UnityEvent unityEvent0) {
                            return LuauSignalWrapper.HandleUnityEvent0(context, thread, objectReference, instanceId,
                                propNameHash, unityEvent0);
                        }
                        if (valueType.IsGenericType) {
                            var genericTypeDef = valueType.GetGenericTypeDefinition();
                            if (genericTypeDef == typeof(UnityEvent<>)) {
                                var unityEvent1 = (UnityEvent<object>)value;
                                return LuauSignalWrapper.HandleUnityEvent1(context, thread, objectReference,
                                    instanceId, propNameHash, unityEvent1);
                            }
                            if (genericTypeDef == typeof(UnityEvent<,>)) {
                                var unityEvent2 = (UnityEvent<object, object>)value;
                                return LuauSignalWrapper.HandleUnityEvent2(context, thread, objectReference,
                                    instanceId, propNameHash, unityEvent2);
                            }
                            if (genericTypeDef == typeof(UnityEvent<,,>)) {
                                var unityEvent3 = (UnityEvent<object, object, object>)value;
                                return LuauSignalWrapper.HandleUnityEvent3(context, thread, objectReference,
                                    instanceId, propNameHash, unityEvent3);
                            }
                            if (genericTypeDef == typeof(UnityEvent<,,,>)) {
                                var unityEvent4 = (UnityEvent<object, object, object, object>)value;
                                return LuauSignalWrapper.HandleUnityEvent4(context, thread, objectReference,
                                    instanceId, propNameHash, unityEvent4);
                            }
                        }
                        
                        if (valueType.BaseType != null && valueType.BaseType.BaseType == typeof(UnityEventBase)) {
                            // Handle special abstractions of UnityEvents from other libraries:
                            if (value is Slider.SliderEvent sliderEvent) {
                                return LuauSignalWrapper.HandleUnityEvent1(context, thread, objectReference,
                                    instanceId, propNameHash, sliderEvent);
                            }
                            if (value is TMPro.TMP_Dropdown.DropdownEvent dropdownEvent) {
                                return LuauSignalWrapper.HandleUnityEvent1(context, thread, objectReference,
                                    instanceId, propNameHash, dropdownEvent);
                            }
                            if (value is TMPro.TMP_InputField.OnChangeEvent onChangeEvent) {
                                return LuauSignalWrapper.HandleUnityEvent1(context, thread, objectReference,
                                    instanceId, propNameHash, onChangeEvent);
                            }
                            if (value is TMPro.TMP_InputField.SelectionEvent selectionEvent) {
                                return LuauSignalWrapper.HandleUnityEvent1(context, thread, objectReference,
                                    instanceId, propNameHash, selectionEvent);
                            }
                            if (value is TMPro.TMP_InputField.SubmitEvent submitEvent) {
                                return LuauSignalWrapper.HandleUnityEvent1(context, thread, objectReference,
                                    instanceId, propNameHash, submitEvent);
                            }
                            if (value is TMPro.TMP_InputField.TextSelectionEvent textSelectionEvent) {
                                return LuauSignalWrapper.HandleUnityEvent3(context, thread, objectReference,
                                    instanceId, propNameHash, textSelectionEvent);
                            }
                            if (value is TMPro.TMP_InputField.TouchScreenKeyboardEvent touchScreenKeyboardEvent) {
                                return LuauSignalWrapper.HandleUnityEvent1(context, thread, objectReference,
                                    instanceId, propNameHash, touchScreenKeyboardEvent);
                            }
                        }

                        WritePropertyToThread(thread, value, propertyType);
                        return 1;
                    }
                    else {
                        WritePropertyToThread(thread, null, null);
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
                    if (!cacheData.Value.memberType.IsPrimitive) {
                        WritePropertyToThread(thread, null, null);
                        return 1;
                    }
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
                    return 1;
                }

                if (dict.Contains(propName)) {
                    object value = dict[propName];
                    Type t = value.GetType();
                    WritePropertyToThread(thread, value, t);
                    return 1;
                }
                else {
                    // Debug.Log("[Luau]: Dictionary was found but key was not found. propName=" + propName +
                    //           ", sourceType=" + sourceType.Name);
                    WritePropertyToThread(thread, null, null);
                    return 1;
                }
            }
            
            // Get field:
            var isDelegate = false;
            if (cacheData.Value.Exists && cacheData.Value.memberInfo is FieldInfo fieldInfo) {
                isDelegate = fieldInfo.FieldType.IsSubclassOf(typeof(Delegate));
                if (!isDelegate) {
                    if (!ReflectionList.IsMemberAllowed(sourceType, fieldInfo, context)) {
                        return LuauError(thread,
                            $"[Airship] Access denied when trying to read {sourceType.Name}.{propName}.");
                    }
                    
                    // Fast, unsafe, and alloc-free field access (avoids boxing by accessing memory directly)
                    if (FastGetAndWriteValueField(thread, objectReference, cacheData.Value)) {
                        return 1;
                    }
                    
                    Type t = fieldInfo.FieldType;
                    System.Object value = fieldInfo.GetValue(objectReference);
                    WritePropertyToThread(thread, value, t);
                    return 1;
                }
            }

            // Get C# event:
            if (isDelegate) {
                var eventInfo = sourceType.GetRuntimeEvent(propName);
                if (eventInfo != null) {
                    if (!ReflectionList.IsMemberAllowed(sourceType, eventInfo, context)) {
                        return LuauError(thread,
                            $"[Airship] Access denied when trying to read {sourceType.Name}.{propName}.");
                    }

                    return LuauSignalWrapper.HandleCsEvent(context, thread, objectReference, instanceId, propNameHash,
                        eventInfo, false);
                }
            }

            return LuauError(thread, $"ERROR - ({sourceType.Name}).{propName} property/field not found");
        }
    }

    private static bool FastGetAndWriteValueField(IntPtr thread, object objectReference, MemberGetReflectionCache cacheData) {
        if (cacheData.memberInfo is not FieldInfo fieldInfo) throw new Exception("FastGetAndWriteValueField must be called with FieldInfo.");
        
        var fieldType = cacheData.memberType;
        
        if (fieldType == intType) {
            var intValue = GetFieldValue<int>(objectReference, fieldInfo);
            WritePropertyToThreadInt32(thread, intValue);
            return true;
        }
        if (fieldType == boolType) {
            var boolValue = GetFieldValue<bool>(objectReference, fieldInfo);
            WritePropertyToThreadBoolean(thread, boolValue);
            return true;
        }
        if (fieldType == doubleType) {
            var doubleValue = GetFieldValue<double>(objectReference, fieldInfo);
            WritePropertyToThreadDouble(thread, doubleValue);
            return true;
        }
        if (fieldType == floatType) {
            var floatValue = GetFieldValue<float>(objectReference, fieldInfo);
            WritePropertyToThreadSingle(thread, floatValue);
            return true;
        }
        if (fieldType == byteType) {
            var byteValue = GetFieldValue<byte>(objectReference, fieldInfo);
            WritePropertyToThreadByte(thread, byteValue);
            return true;
        }
        if (fieldType == vector3Type) {
            var vecValue = GetFieldValue<Vector3>(objectReference, fieldInfo);
            WritePropertyToThreadVector3(thread, vecValue);
            return true;
        }
        if (fieldType == vector2Type) {
            var vecValue = GetFieldValue<Vector2>(objectReference, fieldInfo);
            WritePropertyToThreadVector2(thread, vecValue);
            return true;
        }
        if (fieldType == quaternionType) {
            var quatValue = GetFieldValue<Quaternion>(objectReference, fieldInfo);
            WritePropertyToThreadQuaternion(thread, quatValue);
            return true;
        }
        if (fieldType == rectType) {
            var rectValue = GetFieldValue<Rect>(objectReference, fieldInfo);
            WritePropertyToThreadRect(thread, rectValue);
            return true;
        }
        return false;
    }

    /// <summary>
    /// If the property info is a value property (int/vec) this will speed up the get/write process
    /// because we avoid boxing (no heap allocations).
    /// </summary>
    /// <returns>True if successful, otherwise false if nothing was written.</returns>
    private static bool FastGetAndWriteValueProperty(IntPtr thread, object objectReference, MemberGetReflectionCache cacheData) {
        var propType = cacheData.memberType;
        
        if (IsOfType(propType, boolType)) {
            var boolValue = GetPropertyValue<bool>(objectReference, cacheData);
            WritePropertyToThreadBoolean(thread, boolValue);
            return true;
        }
        if (IsOfType(propType, intType)) {
            var intValue = GetPropertyValue<int>(objectReference, cacheData);
            WritePropertyToThreadInt32(thread, intValue);
            return true;
        }
        if (IsOfType(propType, doubleType)) {
            var doubleVal = GetPropertyValue<double>(objectReference, cacheData);
            WritePropertyToThreadDouble(thread, doubleVal);
            return true;
        }
        if (IsOfType(propType, floatType)) {
            var shortVal = GetPropertyValue<float>(objectReference, cacheData);
            WritePropertyToThreadSingle(thread, shortVal);
            return true;
        }
        if (IsOfType(propType, byteType)) {
            var byteVal = GetPropertyValue<byte>(objectReference, cacheData);
            WritePropertyToThreadByte(thread, byteVal);
            return true;
        }
        if (IsOfType(propType, vector3Type)) {
            var vecValue = GetPropertyValue<Vector3>(objectReference, cacheData);
            WritePropertyToThreadVector3(thread, vecValue);
            return true;
        }
        if (IsOfType(propType, vector2Type)) {
            var vecValue = GetPropertyValue<Vector2>(objectReference, cacheData);
            WritePropertyToThreadVector2(thread, vecValue);
            return true;
        }
        if (IsOfType(propType, quaternionType)) {
            var quatValue = GetPropertyValue<Quaternion>(objectReference, cacheData);
            WritePropertyToThreadQuaternion(thread, quatValue);
            return true;
        }
        if (IsOfType(propType, rectType)) {
            var rectValue = GetPropertyValue<Rect>(objectReference, cacheData);
            WritePropertyToThreadRect(thread, rectValue);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if type is of specified ofType including if it is a reference of the ofType. 
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOfType(Type type, Type ofType) {
        return type == ofType;
    }
    
    public static string GetRequirePath(string originalScriptPath, string fileNameStr) {
        Profiler.BeginSample("GetRequirePath");
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

        Profiler.EndSample();
        return fileNameStr;
    }

    //Take a random path name from a require and transform it into its path relative to /assets/.
    //The same file always gets the same path, so this is used as a key to return the same table every time from lua land
    [AOT.MonoPInvokeCallback(typeof(LuauPluginNative.RequireCallback))]
    private static void RequirePathCallback(LuauContext context, IntPtr thread, IntPtr scriptName, int scriptNameLen, IntPtr fileName, int fileNameLen) {
        LuauProtection.CurrentContext = context;
        
        var fileNameStr = LuauCore.PtrToStringUTF8(fileName, fileNameLen);
        var scriptNameStr = LuauCore.PtrToStringUTF8(scriptName, scriptNameLen);
        
        // LuauState.FromContext(context).TryGetScriptBindingFromThread(thread, out var binding);
        var fileRequirePath = GetRequirePath(scriptNameStr, fileNameStr);
        
        // LuauCore.WritePropertyToThread(thread, fileRequirePath, typeof(string));
        LuauPluginRaw.PushString(thread, fileRequirePath);
    }
    
    [AOT.MonoPInvokeCallback(typeof(LuauPluginNative.RequireCallback))]
    private static IntPtr RequireCallback(LuauContext context, IntPtr thread, IntPtr fileName, int fileNameSize) {
        LuauProtection.CurrentContext = context;

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
    [AOT.MonoPInvokeCallback(typeof(LuauPluginNative.ComponentSetEnabledCallback))]
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
    private static Dictionary<string, int> callCount = new Dictionary<string, int>();
    private static int numCalls = 0;

    [AOT.MonoPInvokeCallback(typeof(LuauPluginNative.CallMethodCallback))]
    static unsafe int CallMethodSafeCallback(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr,
        int classNameSize, IntPtr methodNamePtr, int methodNameLength, int methodNameAtom, int numParameters,
        IntPtr firstParameterType, IntPtr firstParameterData, IntPtr firstParameterSize, IntPtr firstParameterIsTable,
        IntPtr shouldYield) {
        int ret;
        try {
            ret = CallMethod(context, thread, instanceId, classNamePtr, classNameSize, methodNamePtr,
                methodNameLength, methodNameAtom, numParameters, firstParameterType, firstParameterData,
                firstParameterSize, firstParameterIsTable, shouldYield);
        } catch (Exception e) {
            ret = LuauError(thread, e.Message);
        }
        return ret;
    }

    // When a lua object wants to call a method
    static unsafe int CallMethod(LuauContext context, IntPtr thread, int instanceId, IntPtr classNamePtr, int classNameSize, IntPtr methodNamePtr, int methodNameLength, int methodNameAtom, int numParameters, IntPtr firstParameterType, IntPtr firstParameterData, IntPtr firstParameterSize, IntPtr firstParameterIsTable, IntPtr shouldYield) {
        LuauProtection.CurrentContext = context;
        Marshal.WriteInt32(shouldYield, 0);
        if (!IsReady) {
            return 0;
        }
        
        var methodName = LuauCore.PtrToStringUTF8(methodNamePtr, methodNameLength, out var methodNameHash);
        string staticClassName;
        ulong staticClassNameHash = 0;
        if (classNameSize > 0) {
            staticClassName = PtrToStringUTF8(classNamePtr, classNameSize, out staticClassNameHash);
        } else {
            staticClassName = "";
        }
        
        var instance = LuauCore.CoreInstance;

        object reflectionObject = null;
        Type type = null;

        var parameterDataPtrs = new Span<IntPtr>((void*) firstParameterData, numParameters);
        var parameterDataSizes = new Span<int>((int*) firstParameterSize, numParameters);
        var parameterDataPODTypes = new Span<int>((int*) firstParameterType, numParameters);
        var parameterIsTable = new Span<int>((int*) firstParameterIsTable, numParameters);
        
        //This detects STATIC classobjects only - live objects do not report the className
        instance.unityAPIClasses.TryGetValue(staticClassName, out BaseLuaAPIClass staticClassApi);
        if (staticClassApi != null) {
            type = staticClassApi.GetAPIType();
            //This handles where we need to replace a method or implement a method directly in the c# side eg: GameObject.new 
            int retValue = staticClassApi.OverrideStaticMethod(context, thread, methodName, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (retValue >= 0) {
                return retValue;
            }
        }
        
        if (type == null) {
            reflectionObject = ThreadDataManager.GetObjectReference(thread, instanceId);

            if (reflectionObject == null) {
                return LuauError(thread, $"Error: InstanceId not currently available for {instanceId} {methodName} {staticClassName} ({LuaThreadToString(thread)})");
            }
            type = reflectionObject.GetType();
        }
        
        // Optimization opportunity: hash extension + method name hash could be optimized to use Luau atoms. From rough
        // profiling this hashing seems to take about 30% of the method call overhead when we successfully use CallMethodDirectly
        
        var directCallbackHash = methodNameHash;
        // This could be done as a multiple step process to only full method hash if we know this is a fast method
        for (var i = 0; i < numParameters; i++) {
            directCallbackHash = ExtendByteHash(directCallbackHash, (byte) parameterDataPODTypes[i]);
        }
        // We use type.FullName because, although unnecessarily longer, it avoids the allocations involved in
        // type.Name (I think internally FullName is stored and then processed to produce Type.Name)
        foreach (var c in type.FullName) {
            directCallbackHash = ExtendByteHash(directCallbackHash, (byte) c);
        }
        
        try {
            if (CallMethodDirectly(directCallbackHash, context, reflectionObject, thread, firstParameterData, firstParameterSize,
                    firstParameterType, numParameters, out var methodReturn)) {
                return methodReturn; // (1?, we should probably return number of pushed values from CallMethodDirectly?)
            }
        } catch (Exception e) {
            return LuauError(thread,
                "Error: Exception thrown in method " + type.Name + "." + methodName + ": " + e);
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
                        if (LuauProtection.IsAccessBlocked(context, target)) {
                            return LuauError(thread, $"[Airship] Access denied when trying to call method {target.name}.{methodName}. Full type name: {type.FullName}");
                        }
                    } else if (type.IsSubclassOf(typeof(Component)) || type == typeof(Component)) {
                        var target = (Component) reflectionObject;
                        if (target.gameObject && LuauProtection.IsAccessBlocked(context, target.gameObject)) {
                            return LuauError(thread, $"[Airship] Access denied when trying to call method {target.name}.{methodName}. Full type name: {type.FullName}");
                        }
                    }
                }

                int retValue = valueTypeAPI.OverrideMemberMethod(context, thread, reflectionObject, methodName, numParameters,
                    parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
                if (retValue >= 0) {
                    return retValue;
                }
            }
        }
        
        
        // Check for IsA call:
        if (methodName == "IsA") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            
            var t = ReflectionList.AttemptGetTypeFromString(typeName);

            if (t == null) {
                return LuauError(thread, $"Error: Unknown type \"{typeName}\" when calling {type.Name}.IsA");
            }

            var isA = t.IsAssignableFrom(type);
            WritePropertyToThread(thread, isA, typeof(bool));

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
                    return LuauError(thread, $"Error: {methodName} takes 1 parameter (a function!)");
                }
                if (parameterDataPODTypes[0] != (int)PODTYPE.POD_LUAFUNCTION) {
                    return LuauError(thread, $"Error: {methodName} parameter must be a function");
                }

                int handle = GetParameterAsInt32(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
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

                // TODO: could be pooled to remove alloc
                if (eventInfoParams.Length > 0) {
                    var eventInfoParamTypes = new Type[eventInfoParams.Length];
                    for (var i = 0; i < eventInfoParams.Length; i++) {
                        eventInfoParamTypes[i] = eventInfoParams[i].ParameterType;
                    }

                    method = method.MakeGenericMethod(eventInfoParamTypes);
                }

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
                return 1;
            }
        }

        //Use reflection to try and find the method now
        bool countFound = false;
        bool nameFound = false;
        ParameterInfo[] finalParameters = null;
        MethodInfo finalMethod = null;

        var podObjects = UnrollPodObjects(thread, numParameters, parameterDataPODTypes, parameterDataPtrs);

        FindMethod(context, type, methodName, numParameters, parameterDataPODTypes, podObjects, parameterIsTable, out nameFound, out countFound, out finalParameters, out finalMethod, out var finalExtensionMethod, out var insufficientContext, out var attachContext);

        if (finalMethod == null) {
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

                if (targetTransform != null && LuauProtection.IsProtectedScene(targetTransform.gameObject.scene)) {
                    return LuauError(thread, $"[Airship] Access denied when trying call Object.Instantiate() with a parent transform inside a protected scene \"{targetTransform.gameObject.scene.name}\"");
                }
            } else if ((methodName == "Destroy" || methodName == "DestroyImmediate") && type == typeof(Object)) {
                if (finalParameters.Length >= 1 && parsedData[0] != null) {
                    var paramType = parsedData[0].GetType();
                    if (paramType == typeof(GameObject)) {
                        var param = parsedData[0] as GameObject;
                        if (param != null && LuauProtection.IsProtectedScene(param.scene)) {
                            return LuauError(thread, $"[Airship] Access denied when trying to destroy a protected GameObject \"{param.name}\"");
                        }
                    } else if (paramType == typeof(Component)) {
                        var param = parsedData[0] as Component;
                        if (param != null && LuauProtection.IsProtectedScene(param.gameObject.scene)) {
                            return LuauError(thread, $"[Airship] Access denied when trying to destroy a protected Component \"{param.gameObject.name}\"");
                        }
                    }
                }
            } else if (methodName == "SetParent" && type == typeof(Transform)) {
                var callingTransform = reflectionObject as Transform;
                if (callingTransform != null && LuauProtection.IsAccessBlocked(context, callingTransform.gameObject)) {
                    return LuauError(thread, $"[Airship] Access denied when trying set parent of a transform inside a protected scene \"{callingTransform.gameObject.scene.name}\"");
                }

                if (parsedData[0] != null && parsedData[0].GetType() == typeof(Transform)) {
                    var targetTransform = (Transform)parsedData[0];
                    if (targetTransform != null && LuauProtection.IsAccessBlocked(context, targetTransform.gameObject)) {
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
        }
        catch (TargetInvocationException e) {
            return LuauError(thread,
                "Error: Exception thrown in method " + type.Name + "." + finalMethod.Name + ": " + e.InnerException);
        }
        catch (Exception e) {
            return LuauError(thread,
                "Error: Exception thrown in method " + type.Name + "." + finalMethod.Name + ": " + e);
        }

        WriteMethodReturnValuesToThread(thread, type, finalMethod.ReturnType, finalParameters, returnValue, parsedData);
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
    
    [AOT.MonoPInvokeCallback(typeof(LuauPluginNative.ConstructorCallback))]
    static unsafe int ConstructorCallback(LuauContext context, IntPtr thread, IntPtr classNamePtr, int classNameSize, int classNameAtom, int numParameters, IntPtr firstParameterType, IntPtr firstParameterData, IntPtr firstParameterSize, IntPtr firstParameterIsTable) {
        LuauProtection.CurrentContext = context;
        
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
                    return LuauError(thread, GetAwaitingTaskExceptionMessage(awaitingTask));
                }
                ResumeAsyncTask(awaitingTask, true);
                return 0;
            }
            
            LuauPluginRaw.PushThread(thread);
            awaitingTask.ThreadRef = LuauPluginRaw.Ref(thread, -1);
            LuauPluginRaw.Pop(thread, 1);

            awaitingTask.Traceback = LuauPlugin.GetTraceback(thread);
            
            _ = RunTask(awaitingTask);
            shouldYield = true;
            
            return 0;
        } catch (Exception e) {
            shouldYield = false;
            return LuauError(thread, $"Error: Exception thrown in method {type.Name}.{method.Name}: {e.Message}");
        }
    }

    private static async Task RunTask(AwaitingTask awaitingTask) {
        var id = _taskId;
        try {
            await awaitingTask.Task;
        } catch (Exception e) {
            if (id != _taskId) {
                // This task completed after the Luau context was reset, so it's an orphaned task. Just log the exception:
                var trace = awaitingTask.Traceback;
                if (trace.StartsWith("[C] ", StringComparison.Ordinal)) {
                    var newline = trace.IndexOf('\n');
                    if (newline != -1) {
                        trace = trace.Substring(newline + 1);
                    }
                }
                Debug.LogError(GetAwaitingTaskExceptionMessage(awaitingTask) + "\n" + trace + "\n[This error occurred in an async task that completed after its originating Luau context reset].");
            }
            // Otherwise, the exception is handled by the task completion loop, which resumes the calling Luau thread
            // with the results of the task (or the error message from the exception).
        } finally {
            // Only mark task as ready for resumption if the original context is still alive (id == _taskId):
            if (id == _taskId) {
                _completedTasks.Enqueue(awaitingTask);
            }
        }
    }

    private static string GetAwaitingTaskExceptionMessage(AwaitingTask awaitingTask) {
        var aggregateException = awaitingTask.Task.Exception;
        
        if (aggregateException == null) {
            return $"Error: Unknown exception thrown in method {awaitingTask.Type.Name}.{awaitingTask.Method.Name}";
        }

        // Multiple exceptions:
        if (aggregateException.InnerExceptions.Count != 1) {
            return $"Error: Multiple exceptions thrown in method {awaitingTask.Type.Name}.{awaitingTask.Method.Name}: {aggregateException.Message}";
        }
        
        // Only one exception to log:
        var ex =  aggregateException.InnerExceptions[0];
        return $"Error: Exception thrown in method {awaitingTask.Type.Name}.{awaitingTask.Method.Name}: {ex.Message}";
    }
    
    private static void ResumeAsyncTask(AwaitingTask awaitingTask, bool immediate = false) {
        var thread = awaitingTask.Thread;

        if (awaitingTask.ThreadRef != 0) {
            LuauPluginRaw.Unref(thread, awaitingTask.ThreadRef);
        }

        if (awaitingTask.Task.IsFaulted) {
            try {
                LuauPluginRaw.PushString(thread, GetAwaitingTaskExceptionMessage(awaitingTask));
                ThreadDataManager.Error(thread);
                LuauPlugin.ResumeThreadError(thread);
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
                LuauPlugin.ResumeThread(thread, nArgs);
            } catch (LuauException e) {
                Debug.LogException(e);
            }
        }
    }

    private static void ResumeCompletedTasks() {
        while (_completedTasks.TryDequeue(out var awaitingTask)) {
            ResumeAsyncTask(awaitingTask);
        }
    }

    /// Get the string representation of a Lua thread in the same format that Lua would print a thread.
    public static string LuaThreadToString(IntPtr thread) {
        return $"thread: 0x{thread.ToInt64():x16}";
    }

    private struct FastCacheEntry {
        public bool exists;
        public Type ObjectType;
        public string MemberName;
    }
    
    // This is faster frequently it seems, but could be slow if we keep overwriting the same entry
    // It only speeds up dictionary get time.
    private static int fastMemberGetCacheSize = 1024;
    private static FastCacheEntry[] fastMemberGetCacheKeys = new FastCacheEntry[fastMemberGetCacheSize];
    private static MemberGetReflectionCache[] fastMemberGetCacheValues = new MemberGetReflectionCache[fastMemberGetCacheSize];

    private static MemberGetReflectionCache? GetMemberCacheValue(Type objectType, string propName) {
        // Avoid caching result of int field access of a dictionary (because this is a non-discrete
        // prop name and may flood look up table)
        if (typeof(IDictionary).IsAssignableFrom(objectType)) {
            if (int.TryParse(propName, out int keyInt)) {
                return new MemberGetReflectionCache {
                    Exists = false,
                };
            }
        }
        
        var key = new MemberGetCacheKey(objectType, propName);
        var l1Key = key.GetHashCode() % fastMemberGetCacheSize;
        if (l1Key < 0) l1Key += fastMemberGetCacheSize;
        
        var fastEntry = fastMemberGetCacheKeys[l1Key];
        if (fastEntry.exists && fastEntry.ObjectType == objectType && fastEntry.MemberName == propName) {
            return fastMemberGetCacheValues[l1Key];
        }

        // Note: only caching on type full name + prop name. Possible collision on assemblies
        if (memberGetCache.TryGetValue(key, out var data)) {
            fastMemberGetCacheKeys[l1Key] = new FastCacheEntry() {
                ObjectType = objectType,
                MemberName = propName,
                exists = true,
            };
            fastMemberGetCacheValues[l1Key] = data;
            return data;
        }

        return null;
    }

    /// <summary>
    /// Updates both the memberGetCache value and the fastMemberGetCache value
    /// </summary>
    private static void UpdateMemberGetCache(MemberGetCacheKey key, MemberGetReflectionCache value) {
        // If the method info exists add it to L1 cache (array index vs dictionary)
        if (value.Exists) {
            var l1Key = key.GetHashCode() % fastMemberGetCacheSize;
            if (l1Key < 0) l1Key += fastMemberGetCacheSize;

            fastMemberGetCacheKeys[l1Key] = new FastCacheEntry {
                ObjectType = value.objectType,
                MemberName = value.memberInfo.Name,
                exists = true,
            };
            fastMemberGetCacheValues[l1Key] = value;
        }
        
        memberGetCache[key] = value;
    }

    private static MemberGetReflectionCache SetMemberCacheValue(Type objectType, string propName, MemberInfo memberInfo) {
        MemberGetReflectionCache cacheData;
        Type memberType = null;
        if (memberInfo is PropertyInfo pi) memberType = pi.PropertyType;
        if (memberInfo is FieldInfo fi) memberType = fi.FieldType;
        
        if (memberInfo != null && memberType != null) {
            cacheData = new MemberGetReflectionCache {
                objectType = objectType,
                memberType = memberType.IsByRef ? memberType.GetElementType() : memberType,
                memberInfo = memberInfo,
                IsNativeClass = memberInfo.DeclaringType.GetCustomAttributes(false)
                    .Any(attr => attr.GetType().Name == "NativeClassAttribute"),
                IsStruct = memberInfo.DeclaringType.IsValueType && !memberInfo.DeclaringType.IsPrimitive,
                Exists = true,
            };
        } else {
            cacheData = new MemberGetReflectionCache {
                Exists = false,
            };
        }

        UpdateMemberGetCache(new MemberGetCacheKey(objectType, propName), cacheData);
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
