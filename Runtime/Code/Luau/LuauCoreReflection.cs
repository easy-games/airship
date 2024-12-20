using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Airship.DevConsole;
using Assets.Luau;
using Code.Luau;
using UnityEngine;
using Luau;
using NUnit.Framework;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

public partial class LuauCore : MonoBehaviour
{
    public static bool didReflectionSetup = false;
    private static Luau.StringPool s_stringPool;
    private static Dictionary<Type, List<MethodInfo>> extensionMethods;

    private static Dictionary<Type, Dictionary<string, List<MethodInfo>>> typeMethodInfos = new();
    private static Type extensionAttributeType = typeof(ExtensionAttribute);
    private static Dictionary<MethodInfo, ParameterInfo[]> methodParameters = new ();

    private static HashSet<string> referencedAssemblies = new();
    public static bool printReferenceAssemblies = false;

    public static event Action onSetupReflection;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void OnLoad() {
        didReflectionSetup = false;
        referencedAssemblies.Clear();
    }

    public static IEnumerator PrintReferenceAssemblies() {
        while (true) {
            yield return new WaitForSecondsRealtime(3);
            if (!printReferenceAssemblies) continue;
            print("-------------");
            print("Assemblies:");
            foreach (var fullName in referencedAssemblies)
            {
                print("    - " + fullName);
            }
            print("-------------");
        }
    }

    private static Dictionary<string, List<MethodInfo>> GetCachedMethods(Type type) {
        Dictionary<string, List<MethodInfo>> dict;
        if (typeMethodInfos.TryGetValue(type, out dict)) {
            return dict;
        }

        dict = new();
        var methodInfos = type.GetMethods( BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        foreach (var info in methodInfos) {
            if (dict.TryGetValue(info.Name, out var methodList)) {
                methodList.Add(info);
            } else {
                var list = new List<MethodInfo>();
                list.Add(info);
                dict.Add(info.Name, list);
            }
        }

        typeMethodInfos.Add(type, dict);
        referencedAssemblies.Add(type.Assembly.FullName);
        return dict;
    }

    public static ParameterInfo[] GetCachedParameters(MethodInfo methodInfo)
    {
        if (methodParameters.TryGetValue(methodInfo, out var existing))
        {
            return existing;
        }

        var parameters = methodInfo.GetParameters();
        methodParameters.Add(methodInfo, parameters);
        return parameters;
    }

    public static List<MethodInfo> GetCachedExtensionMethods(Type type)
    {
        if (extensionMethods.TryGetValue(type, out List<MethodInfo> existing))
        {
            return existing;
        }

        List<MethodInfo> methods = new();
        if (typeof(Component).IsAssignableFrom(type))
        {
            if (extensionMethods.TryGetValue(typeof(Component), out var f))
            {
                methods.AddRange(f);
            }
        }

        if (type != typeof(Component))
        {
            if (extensionMethods.TryGetValue(type, out var foundExtensionMethods))
            {
                methods.AddRange(foundExtensionMethods);
            }
        }

        extensionMethods.Add(type, methods);
        return methods;
    }

    private static void SetupReflection() {
#if UNITY_EDITOR
        DevConsole.AddCommand(Command.Create(
            "assemblies",
            "",
            "Toggles tracking and printing of used assemblies. Only works in editor.",
            () => {
                printReferenceAssemblies = !printReferenceAssemblies;
                if (printReferenceAssemblies) {
                    DevConsole.Log("Enabled assembly tracking.");
                } else {
                    DevConsole.Log("Disabled assembly tracking.");
                }
            }
        ));
#endif

        if (didReflectionSetup) return;
        didReflectionSetup = true;

        typeMethodInfos.Clear();
        s_stringPool = new Luau.StringPool(1024 * 1024 * 5); //5mb
        extensionMethods = new();

        var stopwatch = Stopwatch.StartNew();
        Profiler.BeginSample("SetupReflection");
        onSetupReflection?.Invoke();
        Profiler.EndSample();
        // print("Finished reflection setup in " + stopwatch.ElapsedMilliseconds + "ms");
    }

    public static void AddTypeExtensionMethodsFromClass(Type type, Type classToSearch)
    {
        var methods = classToSearch.GetMethods();
        methods = Array.FindAll(methods, info =>
        {
            if (!info.IsDefined(extensionAttributeType, true))
            {
                return false;
            }

            var parameters = info.GetParameters();
            if (parameters.Length == 0) return false;
            var paramType = parameters[0].ParameterType;
            if (!paramType.IsAssignableFrom(type))
            {
                return false;
            }

            return true;
        });
        // print("Found " + methods.Length + " extension methods for " + type.Name + " in class " + classToSearch.Name);

        if (extensionMethods.TryGetValue(type, out var existing))
        {
            existing.AddRange(methods);
        } else
        {
            extensionMethods.Add(type, new List<MethodInfo>(methods));
        }
    }

    public static void AddExtensionMethodsFromNamespace(Type type, string assemblyName, string namespaceName)
    {
        List<MethodInfo> methods = new();
        List<Type> types = new();
        types.AddRange(GetTypesInNamespace(assemblyName, namespaceName));
        foreach (var t in types)
        {
            var values = GetCachedMethods(t).Values;
            foreach (var list in values)
            {
                var tMethods = list.FindAll(info =>
                {
                    return info.IsDefined(extensionAttributeType, true);
                });
                methods.AddRange(tMethods);
            }

        }

        // print("Found " + methods.Count + " extension methods for " + type.Name + " in namespace " + namespaceName);

        if (extensionMethods.TryGetValue(type, out var existing))
        {
            existing.AddRange(methods);
        } else
        {
            extensionMethods.Add(type, methods);
        }
    }
    
    private static Type[] GetTypesInNamespace(string assemblyName, string nameSpace)
    {
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name == assemblyName)
            {
                var types = assembly.GetTypes();
                types = types
                    .Where(t => String.Equals(t.Namespace, nameSpace, StringComparison.Ordinal))
                    .ToArray();
                if (types.Length > 0)
                {
                    return types;
                }   
            }
        }

        return new Type[] { };
    }

    PropertyInfo GetPropertyInfoForType(Type sourceType, string propName, ulong propNameHash)
    {
        unityPropertyAlias.TryGetValue(sourceType, out Dictionary<ulong, PropertyInfo> propDict);
        //if its null create it
        if (propDict == null)
        {
            propDict = new Dictionary<ulong, PropertyInfo>();
            unityPropertyAlias.Add(sourceType, propDict);
        }

        propDict.TryGetValue(propNameHash, out PropertyInfo property);
        if (property == null)
        {
            property = sourceType.GetProperty(propName);

            //Still null?
            if (property == null)
            {
                var list = sourceType.GetRuntimeProperties();
                foreach (var prop in list)
                {
                    string name = prop.Name;
                    var parts = name.Split('.');
                    string possibleName = parts[parts.Length - 1];
                    if (possibleName == propName)
                    {
                        property = prop;
                        //Store it for next time
                        break;
                    }
                }
            }

            //we (finally) found it, write it for next time
            if (property != null)
            {
                propDict.Add(propNameHash, property);
            }
        }
        return property;
    }

    FieldInfo GetFieldInfoForType(Type sourceType, string propName, ulong propNameHash)
    {
        unityFieldAlias.TryGetValue(sourceType, out Dictionary<ulong, FieldInfo> fieldDict);
        //if its null create it
        if (fieldDict == null)
        {
            fieldDict = new Dictionary<ulong, FieldInfo>();
            unityFieldAlias.Add(sourceType, fieldDict);
        }
        fieldDict.TryGetValue(propNameHash, out FieldInfo field);
        if (field == null)
        {
            field = sourceType.GetField(propName);
            
            //Still null?
            if (field == null)
            {
                var list = sourceType.GetRuntimeFields();
                foreach (var listField in list)
                {
                    string name = listField.Name;
                    var parts = name.Split('.');
                    string possibleName = parts[parts.Length - 1];
                    if (possibleName == propName)
                    {
                        field = listField;
                        //Store it for next time
                        break;
                    }
                }
            }

            //we (finally) found it, write it for next time
            if (field != null)
            {
                fieldDict.Add(propNameHash, field);
            }
        }
        return field;
    }

    EventInfo GetEventInfoForType(Type sourceType, string propName, ulong propNameHash) {
        var eventType = sourceType.GetRuntimeEvent(propName);
        return eventType;
    }

    private static readonly object[] UnrolledPodObjects = new object[MaxParameters];
    private static readonly int[] UnrolledPodTypeData = new int[1];
    private static ArraySegment<object> UnrollPodObjects(IntPtr thread, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs) {
        // var podObjects = new object[numParameters];
        for (var j = 0; j < numParameters; j++) {
            if (parameterDataPODTypes[j] == (int)PODTYPE.POD_OBJECT) {
                // var intData = new int[1];
                Marshal.Copy(parameterDataPtrs[j], UnrolledPodTypeData, 0, 1);
                var instanceId = UnrolledPodTypeData[0];
                UnrolledPodObjects[j] = ThreadDataManager.GetObjectReference(thread, instanceId);
            } else if (parameterDataPODTypes[j] == (int)PODTYPE.POD_AIRSHIP_COMPONENT) {
                var ptr = parameterDataPtrs[j];
                var componentRef = Marshal.PtrToStructure<AirshipComponentRef>(ptr);
                UnrolledPodObjects[j] = componentRef.AsUnityComponent();
            } else {
                UnrolledPodObjects[j] = null;
            }
        }
        return new ArraySegment<object>(UnrolledPodObjects, 0, numParameters);
    }

    private static int RunConstructor(IntPtr thread, Type type, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> paramaterDataSizes) {

        ConstructorInfo[] constructors = type.GetConstructors();

        if (constructors.Length == 0)
        {
            System.Object retStruct = Activator.CreateInstance(type);

            //Push this onto the stack
            WritePropertyToThread(thread, retStruct, type);
            return 1;
        }

        var podObjects = UnrollPodObjects(thread, numParameters, parameterDataPODTypes, parameterDataPtrs);
        FindConstructor(type, constructors, numParameters, parameterDataPODTypes, podObjects, out bool countFound, out ParameterInfo[] finalParameters, out ConstructorInfo finalConstructor);

        if (finalConstructor == null)
        {
            if (countFound == false)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: No version of New on " + type.Name + " takes " + numParameters + " parameters.");
            }
            else
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: No matching New found for " + type.Name);
            }

            return 0;
        }
        
        var success = ParseParameterData(thread, numParameters, parameterDataPtrs, parameterDataPODTypes, finalParameters, paramaterDataSizes, podObjects, false, out var parsedData);
        if (success == false) {
            ThreadDataManager.Error(thread);
            Debug.LogError("Error: Unable to parse parameters for " + type.Name + " constructor.");
            return 0;
        }

        //We have parameters
        var returnValue = finalConstructor.Invoke(parsedData.Array);

        //Push this onto the stack
        WritePropertyToThread(thread, returnValue, type);
        return 1;
    }
    
    public static Dictionary<Type, Delegate> writeMethodFunctions = new ();

    /// <summary>
    /// This will not work if passed in type is not a value type
    /// </summary>
    public static unsafe bool FastWriteValuePropertyToThread<T>(IntPtr thread, T value) {
        var genericType = typeof(T);
        if (writeMethodFunctions.TryGetValue(genericType, out var f)) {
            ((Action<IntPtr, T>) f)(thread, value);
            return true;
        }
        
        var genericName = genericType.Name;
        // Find write property function for this type
        var methodName = $"WritePropertyToThread{genericName}";
        Debug.Log("Checking for " + methodName);
        var mi = typeof(LuauCore).GetMethod(methodName); // ex: WritePropertyToThreadVector3

        if (mi == null) {
            // If this error throws it likely means:
            // 1. If type is a value type (int / Vector3, not an object) then we should probably implement
            // the linked function name in this file.
            // 2. If the type is an object whatever is calling FastWriteValuePropertyToThread should instead be calling
            // WritePropertyToThread.
            throw new ArgumentException($"No implemented function '{methodName}' type={genericName}");
        }
        
        var method = new DynamicMethod(
            name: $"Write{typeof(T).Name}Property",
            returnType: typeof(void),
            parameterTypes: new[] { typeof(IntPtr), typeof(T) },
            restrictedSkipVisibility: true);

        ILGenerator il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.EmitCall(OpCodes.Call, mi, null);
        il.Emit(OpCodes.Ret);

        var funcType = typeof(Action<IntPtr, T>);
        var writePropertyDelegate = (Action<IntPtr, T>)method.CreateDelegate(funcType);
        writeMethodFunctions[genericType] = writePropertyDelegate;
        writePropertyDelegate(thread, value);
        return true;
    }
    
    // Called from WriteProperty
    public static unsafe void WritePropertyToThreadVector3(IntPtr thread, Vector3 value) {
        LuauPlugin.LuauPushVector3ToThread(thread, value.x, value.y, value.z);
    }
    
    // Called from WriteProperty
    public static unsafe void WritePropertyToThreadQuaternion(IntPtr thread, Quaternion quat) {
        float* quatData = stackalloc float[4];
        quatData[0] = quat.x;
        quatData[1] = quat.y;
        quatData[2] = quat.z;
        quatData[3] = quat.w;
        
        LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_QUATERNION, new IntPtr(quatData),
            0); // 0, because we know how big an intPtr is
    }
    
    // Called from WriteProperty
    public static unsafe void WritePropertyToThreadInt32(IntPtr thread, int value) {
        LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_INT32, new IntPtr(value: &value),
            0); // 0, because we know how big an intPtr is
    }
    
    // Called from WriteProperty
    public static unsafe void WritePropertyToThreadSingle(IntPtr thread, float value) {
        double number = value;
        LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_DOUBLE, new IntPtr(value: &number),
            0); // 0, because we know how big an intPtr is
    }
    
    // Called from WriteProperty
    public static unsafe void WritePropertyToThreadDouble(IntPtr thread, double value) {
        LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_DOUBLE, new IntPtr(value: &value),
            0); // 0, because we know how big an intPtr is
    }

     public static unsafe bool WritePropertyToThread(IntPtr thread, System.Object value, Type t) {
         if (value == null) {
             LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_NULL, IntPtr.Zero, 0);
             return true;
         }

         if (t == stringType) {
             byte[] str = System.Text.Encoding.UTF8.GetBytes((string)value);
             var allocation = GCHandle.Alloc(str, GCHandleType.Pinned); //Ok
             LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_STRING, allocation.AddrOfPinnedObject(),
                 str.Length);
             allocation.Free();
             return true;
         }

         if (t == intType) {
             WritePropertyToThreadInt32(thread, (int) value);
             return true;
         }

         if (t.IsEnum) {
             System.Int32 integer = (System.Int32)value;
             LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_INT32, new IntPtr(value: &integer),
                 0); // 0, because we know how big an intPtr is
             return true;
         }

         if (t == uIntType) {
             UInt32 uintVal = (UInt32)value;
             System.Int32 integer = unchecked((int)uintVal);
             LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_INT32, new IntPtr(value: &integer),
                 0); // 0, because we know how big an intPtr is
             return true;
         }
         if (t == longType) {
             Int64 intVal = (Int64)value;
             System.Int32 integer = unchecked((int)intVal);
             LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_INT32, new IntPtr(value: &integer),
                 0); // 0, because we know how big an intPtr is
             return true;
         }

         if (t == vector3Type) {
             WritePropertyToThreadVector3(thread, (Vector3) value);
             return true;
         }

         if (t == vector3IntType) {
             Vector3 vec = Vector3Int.FloorToInt((Vector3Int)value);
             LuauPlugin.LuauPushVector3ToThread(thread, vec.x, vec.y, vec.z);
             return true;
         }

         if (t == boolType) {
             if ((bool)value == true) {

                 int fixedValue = 1;
                 LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_BOOL, new IntPtr(&fixedValue),
                     0); // 0, because we know how big an intPtr is
                 return true;
             }
             else {
                 int fixedValue = 0;
                 LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_BOOL, new IntPtr(&fixedValue),
                     0); // 0, because we know how big an intPtr is
                 return true;
             }

         }

         if (t == doubleType) {
             WritePropertyToThreadDouble(thread, (double) value);
             return true;
         }

         if (t == floatType) {
             WritePropertyToThreadSingle(thread, (float) value);
             return true;
         }

         if (t == ushortType) {
             double number = (ushort)value;
             LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_DOUBLE, new IntPtr(value: &number),
                 0); // 0, because we know how big an intPtr is
             return true;
         }

         if (t == rayType) {
             Ray ray = (Ray)value;
             float[] rayData = new float[6];
             rayData[0] = ray.origin.x;
             rayData[1] = ray.origin.y;
             rayData[2] = ray.origin.z;
             rayData[3] = ray.direction.x;
             rayData[4] = ray.direction.y;
             rayData[5] = ray.direction.z;

             //woof. maybe make something that can eat 6 parameters akin to LuauPushVector3ToThread
             var gch = GCHandle.Alloc(rayData, GCHandleType.Pinned); //Ok
             LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_RAY, gch.AddrOfPinnedObject(),
                 0); // 0, because we know how big an intPtr is
             gch.Free();

             return true;
         }

         if (t == colorType) {
             Color color = (Color)value;
             float[] colorData = new float[4];
             colorData[0] = color.r;
             colorData[1] = color.g;
             colorData[2] = color.b;
             colorData[3] = color.a;

             var gch = GCHandle.Alloc(colorData, GCHandleType.Pinned); //Ok
             LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_COLOR, gch.AddrOfPinnedObject(),
                 0); // 0, because we know how big an intPtr is
             gch.Free();
             return true;
         }

         if (t == binaryBlobType) {
             Assets.Luau.BinaryBlob blob = (Assets.Luau.BinaryBlob)value;

             var gch = GCHandle.Alloc(blob.m_data, GCHandleType.Pinned); //Ok
             LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_BINARYBLOB, gch.AddrOfPinnedObject(),
                 (int)blob.m_dataSize); // 0, because we know how big an intPtr is
             gch.Free();

             return true;
         }

         if (t == quaternionType) {
             WritePropertyToThreadQuaternion(thread, (Quaternion) value);
             return true;
         }

         if (t == vector2Type) {
             var vec = (Vector2)value;
             var vecData = new float[2];
             vecData[0] = vec.x;
             vecData[1] = vec.y;

             var gch = GCHandle.Alloc(vecData, GCHandleType.Pinned); //Ok
             LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_VECTOR2, gch.AddrOfPinnedObject(),
                 0); // 0, because we know how big an intPtr is
             gch.Free();

             return true;
         }

         if (t == vector2IntType) {
             Vector2 vec = Vector2Int.FloorToInt((Vector2Int)value);
             var vecData = new float[2];
             vecData[0] = vec.x;
             vecData[1] = vec.y;

             var gch = GCHandle.Alloc(vecData, GCHandleType.Pinned); //Ok
             LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_VECTOR2, gch.AddrOfPinnedObject(),
                 0); // 0, because we know how big an intPtr is
             gch.Free();

             return true;
         }

         if (t == vector4Type) {
             var vec = (Vector4)value;
             var vecData = new float[4];
             vecData[0] = vec.x;
             vecData[1] = vec.y;
             vecData[2] = vec.z;
             vecData[3] = vec.w;

             var gch = GCHandle.Alloc(vecData, GCHandleType.Pinned); //Ok
             LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_VECTOR4, gch.AddrOfPinnedObject(),
                 0); // 0, because we know how big an intPtr is
             gch.Free();

             return true;
         }

         if (t == planeType) {
             Plane plane = (Plane)value;
             float[] planeData = new float[4];
             planeData[0] = plane.normal.x;
             planeData[1] = plane.normal.y;
             planeData[2] = plane.normal.z;
             planeData[3] = plane.distance;

             var gch = GCHandle.Alloc(planeData, GCHandleType.Pinned); //Ok
             LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_PLANE, gch.AddrOfPinnedObject(),
                 0); // 0, because we know how big an intPtr is
             gch.Free();

             return true;
         }

         if (t == matrixType) {
             Matrix4x4 mat = (Matrix4x4)value;
             float[] matData = new float[16];
             matData[0] = mat.m00;
             matData[1] = mat.m01;
             matData[2] = mat.m02;
             matData[3] = mat.m03;
             matData[4] = mat.m10;
             matData[5] = mat.m11;
             matData[6] = mat.m12;
             matData[7] = mat.m13;
             matData[8] = mat.m20;
             matData[9] = mat.m21;
             matData[10] = mat.m22;
             matData[11] = mat.m23;
             matData[12] = mat.m30;
             matData[13] = mat.m31;
             matData[14] = mat.m32;
             matData[15] = mat.m33;

             var gch = GCHandle.Alloc(matData, GCHandleType.Pinned); //Ok
             LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_MATRIX, gch.AddrOfPinnedObject(),
                 0); // 0, because we know how big an intPtr is
             gch.Free();

             return true;
         }

         //This has to go dead last ////////////////////////////////////////
         if (t == systemObjectType || t.IsSubclassOf(systemObjectType)) {
             /*
              * Unity sometimes returns a dummy object instead of "null" for nice console prints.
              * We need to manually cast to a UnityEngine.Object and check for null.
              */
             if (value is UnityEngine.Object) {
                 UnityEngine.Object go = (UnityEngine.Object)value;
                 if (go == null) {
                     LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_NULL, IntPtr.Zero, 0);
                     return true;
                 }
             }

             int objectInstanceId = ThreadDataManager.AddObjectReference(thread, value);
             LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_OBJECT, new IntPtr(value: &objectInstanceId),
                 0); //size == 0, intptr size known.
             return true;
         } //NO! Dont add anything here ///////////////////////

         ThreadDataManager.Error(thread);
         Debug.LogError("Attempted to write parameter of type " + t.ToString() + " and can't currently handle it.");
         return false;
     }

    private static readonly object[] ParsedObjectsData = new object[MaxParameters];
    private static bool ParseParameterData(IntPtr thread, int numParameters, ArraySegment<IntPtr> intPtrs, ArraySegment<int> podTypes, ParameterInfo[] methodParameters, ArraySegment<int> sizes, ArraySegment<object> podObjects, bool usingAttachedContext, out ArraySegment<object> parsedData) {
        var numParametersIncludingContext = numParameters;
        if (usingAttachedContext) numParametersIncludingContext += 1;
        parsedData = new object[numParametersIncludingContext];

        for (int i = 0; i < numParameters; i++) {
            var paramIndex = i;
            if (usingAttachedContext) paramIndex += 1;
            
            PODTYPE paramType = (PODTYPE)podTypes[i];
            Type sourceParamType = methodParameters[paramIndex].ParameterType;
            switch (paramType)
            {
                case PODTYPE.POD_OBJECT:
                    {
                        System.Object objectRef = podObjects[i];
                        parsedData[paramIndex] = objectRef;
                        continue;
                    }
                case PODTYPE.POD_AIRSHIP_COMPONENT: {
                    var objectRef = podObjects[paramIndex] as AirshipComponent;
                    parsedData[paramIndex] = objectRef;
                    continue;
                }
                case PODTYPE.POD_DOUBLE:
                    {
                        double[] doubleData = new double[1];
                        Marshal.Copy(intPtrs[i], doubleData, 0, 1);
                        if (sourceParamType.IsAssignableFrom(doubleType))
                        {
                            parsedData[paramIndex] = doubleData[0];
                            continue;
                        }
                        if (sourceParamType.IsAssignableFrom(floatType))
                        {
                            parsedData[paramIndex] = (System.Single)doubleData[0];
                            continue;
                        }
                        if (sourceParamType.IsAssignableFrom(byteType))
                        {
                            parsedData[paramIndex] = (System.Byte)doubleData[0];
                            continue;
                        }

                        if (sourceParamType.BaseType == enumType)
                        {
                            if (Enum.GetUnderlyingType(sourceParamType) == byteType)
                            {
                                parsedData[paramIndex] = (System.Byte)doubleData[0];
                            } else
                            {
                                parsedData[paramIndex] = (System.Int32)doubleData[0];
                            }
                            continue;
                        }
                        if (sourceParamType.IsAssignableFrom(intType))
                        {
                            parsedData[paramIndex] = (System.Int32)doubleData[0];
                            continue;
                        }
                        if (sourceParamType.IsAssignableFrom(uIntType))
                        {
                            parsedData[paramIndex] = (System.UInt32)doubleData[0];
                            continue;
                        }
                        if (sourceParamType.IsAssignableFrom(ushortType)) {
                            parsedData[paramIndex] = (System.UInt16)doubleData[0];
                            continue;
                        }
                        if (sourceParamType.IsAssignableFrom(longType))
                        {
                            parsedData[paramIndex] = (System.Int64)doubleData[0];
                            continue;
                        }
                        if (sourceParamType.IsAssignableFrom(uLongType))
                        {
                            parsedData[paramIndex] = (System.UInt64)doubleData[0];
                            continue;
                        }

                        break;
                    }
                case PODTYPE.POD_BOOL:
                    {
                        double[] doubleData = new double[1];
                        Marshal.Copy(intPtrs[i], doubleData, 0, 1);
                        if (doubleData[0] == 0)
                        {
                            parsedData[paramIndex] = false;
                        }
                        else
                        {
                            parsedData[paramIndex] = true;
                        }

                        continue;
                    }


                case PODTYPE.POD_VECTOR3:
                    {
                        parsedData[paramIndex] = NewVector3FromPointer(intPtrs[i]);
                        continue;
                    }

                case PODTYPE.POD_STRING:
                    {
                        string dataStr = LuauCore.PtrToStringUTF8(intPtrs[i], sizes[i]);
                        parsedData[paramIndex] = dataStr;

                        continue;
                    }

                case PODTYPE.POD_RAY:
                    {
                        parsedData[paramIndex] = NewRayFromPointer(intPtrs[i]);
                        continue;
                    }

                case PODTYPE.POD_BINARYBLOB:
                    {
                        parsedData[paramIndex] = NewBinaryBlobFromPointer(intPtrs[i], sizes[i]);
                        continue;
                    }

                case PODTYPE.POD_PLANE:
                    {
                        parsedData[paramIndex] = NewPlaneFromPointer(intPtrs[i]);
                        continue;
                    }
                case PODTYPE.POD_QUATERNION:
                    {
                        parsedData[paramIndex] = NewQuaternionFromPointer(intPtrs[i]);
                        continue;
                    }
                case PODTYPE.POD_VECTOR2:
                    {
                        parsedData[paramIndex] = NewVector2FromPointer(intPtrs[i]);
                        continue;
                    }
                case PODTYPE.POD_VECTOR4:
                    {
                        parsedData[paramIndex] = NewVector4FromPointer(intPtrs[i]);
                        continue;
                    }
                case PODTYPE.POD_COLOR:
                    {
                        parsedData[paramIndex] = NewColorFromPointer(intPtrs[i]);
                        continue;
                    }
                case PODTYPE.POD_MATRIX:
                    {
                        parsedData[paramIndex] = NewMatrixFromPointer(intPtrs[i]);
                        continue;
                    }
            }

            Debug.LogError("Param " + paramIndex + " " + podTypes[i] + " not valid type for this parameter/unhandled so far.");
            return false;
        }
        return true;
    }
    
    public static LuauCore.PODTYPE GetParamPodType(Type sourceParamType) {
        if (sourceParamType == null) {
            return PODTYPE.POD_NULL;
        }
        if (sourceParamType == typeof(object)) {
            return PODTYPE.POD_OBJECT;
        }
        
        foreach (var podType in Enum.GetValues(typeof(LuauCore.PODTYPE))) {
            switch (podType) {
                case LuauCore.PODTYPE.POD_BOOL:
                    if (sourceParamType.IsAssignableFrom(boolType) == true) {
                        return PODTYPE.POD_BOOL;
                    }
                    break;
                case LuauCore.PODTYPE.POD_DOUBLE:
                    if (sourceParamType.IsAssignableFrom(doubleType) == true) {
                        return PODTYPE.POD_DOUBLE;
                    }

                    if (sourceParamType.IsAssignableFrom(floatType) == true) {
                        return PODTYPE.POD_DOUBLE;
                    }

                    if (sourceParamType.IsAssignableFrom(ushortType) == true) {
                        return PODTYPE.POD_DOUBLE;
                    }

                    if (sourceParamType.IsAssignableFrom(byteType) == true) {
                        return PODTYPE.POD_DOUBLE;
                    }
                    if (sourceParamType.IsAssignableFrom(intType) == true || sourceParamType.BaseType == enumType) {
                        return PODTYPE.POD_DOUBLE;
                    }
                    if (sourceParamType.IsAssignableFrom(uIntType) == true) {
                        return PODTYPE.POD_DOUBLE;
                    }
                    if (sourceParamType.IsAssignableFrom(longType) == true) {
                        return PODTYPE.POD_DOUBLE;
                    }
                    if (sourceParamType.IsAssignableFrom(uLongType) == true) {
                        return PODTYPE.POD_DOUBLE;
                    }

                    break;
                case LuauCore.PODTYPE.POD_VECTOR3:
                    if (sourceParamType.IsAssignableFrom(vector3Type) ||
                        sourceParamType.IsAssignableFrom(vector3IntType)) {
                        return PODTYPE.POD_VECTOR3;
                    }

                    break;
                case LuauCore.PODTYPE.POD_STRING:
                    if (sourceParamType.IsAssignableFrom(stringType) == true) {
                        return PODTYPE.POD_STRING;
                    }

                    break;
                case LuauCore.PODTYPE.POD_RAY:
                    if (sourceParamType.IsAssignableFrom(rayType) == true) {
                        return PODTYPE.POD_RAY;
                    }

                    break;
                case LuauCore.PODTYPE.POD_BINARYBLOB:
                    if (sourceParamType.IsAssignableFrom(binaryBlobType) == true) {
                        return PODTYPE.POD_BINARYBLOB;
                    }

                    break;
                case LuauCore.PODTYPE.POD_COLOR:
                    if (sourceParamType.IsAssignableFrom(colorType) == true) {
                        return PODTYPE.POD_COLOR;
                    }

                    break;
                case LuauCore.PODTYPE.POD_MATRIX:
                    if (sourceParamType.IsAssignableFrom(matrixType) == true) {
                        return PODTYPE.POD_MATRIX;
                    }

                    break;
                case LuauCore.PODTYPE.POD_PLANE:
                    if (sourceParamType.IsAssignableFrom(planeType) == true) {
                        return PODTYPE.POD_PLANE;
                    }

                    break;
                case LuauCore.PODTYPE.POD_QUATERNION:
                    if (sourceParamType.IsAssignableFrom(quaternionType) == true) {
                        return PODTYPE.POD_QUATERNION;
                    }

                    break;
                case LuauCore.PODTYPE.POD_VECTOR2:
                    if (sourceParamType.IsAssignableFrom(vector2Type) ||
                        sourceParamType.IsAssignableFrom(vector2IntType)) {
                        return PODTYPE.POD_VECTOR2;
                    }

                    break;
                case LuauCore.PODTYPE.POD_VECTOR4:
                    if (sourceParamType.IsAssignableFrom(vector4Type)) {
                        return PODTYPE.POD_VECTOR4;
                    }
                    break;
            }
        }
        return PODTYPE.POD_OBJECT;
    }


    // private static HashSet<MethodInfo> _methodsUsedTest = new();
    private static void FindMethod(LuauContext context, Type type, string methodName, int numParameters, ArraySegment<int> podTypes, ArraySegment<object> podObjects, out bool nameFound, out bool countFound, out ParameterInfo[] finalParameters, out MethodInfo finalMethod, out bool finalExtensionMethod, out bool insufficientContext, out bool attachContext)
    {
        nameFound = false;
        countFound = false;
        finalParameters = null;
        finalMethod = null;
        finalExtensionMethod = false;
        insufficientContext = false;
        attachContext = false;
        
        var methodDict = GetCachedMethods(type);
        if (methodDict.TryGetValue(methodName, out var methods))
        {
            nameFound = true;
            foreach (var info in methods)
            {
                ParameterInfo[] parameters = GetCachedParameters(info);

                var contextAttached = false;
                //match parameters
                if (parameters.Length != numParameters)
                {
                    // Check for context pass through (c# function would have 1 more param then Luau call)
                    if (parameters.Length != (numParameters + 1)) {
                        continue;
                    }
                    // Faster than GetCustomAttribute (I think) to quickly eliminate this as an option
                    if (parameters[0].ParameterType != typeof(LuauContext)) {
                        continue;
                    }
                    
                    contextAttached = info.GetCustomAttribute<AttachContext>() != null;
                    if (!contextAttached) continue;
                }
                countFound = true;

                bool match = MatchParameters(numParameters, parameters, podTypes, podObjects, contextAttached);
                if (match)
                {
                    if (!type.IsArray) {
                        if (!ReflectionList.IsMethodAllowed(type, info, context)) {
                            insufficientContext = true;
                            return;
                        }
                    }

                    finalMethod = info;
                    finalParameters = parameters;
                    finalExtensionMethod = false;
                    attachContext = contextAttached;

                    // if (_methodsUsedTest.Add(finalMethod)) {
                    //     Debug.Log($"METHOD: {type} {finalMethod}");
                    // }
                    
                    return;
                }
            }
        }

        // reset
        // nameFound = false;
        // countFound = false;
        // finalParameters = null;
        // finalMethod = null;
        // finalExtensionMethod = false;

        var extensions = GetCachedExtensionMethods(type);
        foreach (MethodInfo info in extensions)
        {
            if (info.Name != methodName)
            {
                continue;
            }

            nameFound = true;
            ParameterInfo[] parameters = GetCachedParameters(info);
            if (parameters.Length - 1 != numParameters)
            {
                continue;
            }
            // first param is a reference to "this". Remember, extension methods are really static methods.
            parameters = parameters.Skip(1).ToArray();
            countFound = true;

            bool match = MatchParameters(numParameters, parameters, podTypes, podObjects, false);

            if (match)
            {
                if (!ReflectionList.IsMethodAllowed(type, info, context)) {
                    insufficientContext = true;
                    return;
                }
                
                finalMethod = info;
                finalParameters = parameters;
                finalExtensionMethod = true;
                return;
            }
        }
    }

    static public void FindConstructor(Type type, ConstructorInfo[] constructors, int numParameters, ArraySegment<int> podTypes, ArraySegment<object> podObjects, out bool countFound, out ParameterInfo[] finalParameters, out ConstructorInfo finalConstructor)
    {
        countFound = false;
        finalParameters = null;
        finalConstructor = null;

        //Check our method signature
        foreach (ConstructorInfo info in constructors)
        {
            ParameterInfo[] parameters = info.GetParameters();
            if (parameters.Length != numParameters)
            {
                // Debug.Log("Length mismatch: " + numParameters + " " + parameters.Length);
                continue;
            }
            countFound = true;

            bool match = MatchParameters(numParameters, parameters, podTypes, podObjects, false) ;
            if (match == true)
            {
                finalConstructor = info;
                finalParameters = parameters;
                break;
            }
        }
    }

    static bool MatchParameters(int numParameters, ParameterInfo[] parameters, ArraySegment<int> podTypes, ArraySegment<object> podObjects, bool contextAttached) {
        for (int i = 0; i < numParameters; i++) {
            var paramIndex = i;
            if (contextAttached) paramIndex += 1; // Because 0'th param should be context
            
            PODTYPE paramType = (PODTYPE)podTypes[i];
            Type sourceParamType = parameters[paramIndex].ParameterType;
            if (parameters[paramIndex].IsOut == true || parameters[paramIndex].IsIn == true)
            {
                sourceParamType = sourceParamType.GetElementType();
            }
            
            switch (paramType)
            {
                case PODTYPE.POD_NULL:
                    continue;
                case PODTYPE.POD_AIRSHIP_COMPONENT:
                    if (sourceParamType.IsAssignableFrom(componentType)) {
                        continue;
                    }
                    break;
                case PODTYPE.POD_OBJECT:
                    var obj = podObjects[i];
                    if (obj == null || sourceParamType.IsAssignableFrom(obj.GetType()))
                    {
                        continue;
                    }
                    break;
                case PODTYPE.POD_BOOL:
                    if (sourceParamType.IsAssignableFrom(boolType) == true)
                    {
                        continue;
                    }
                    break;
                case PODTYPE.POD_DOUBLE:
                    if (sourceParamType.IsAssignableFrom(doubleType) == true)
                    {
                        continue;
                    }
                    if (sourceParamType.IsAssignableFrom(floatType) == true)
                    {
                        continue;
                    }
                    if (sourceParamType.IsAssignableFrom(ushortType) == true)
                    {
                        continue;
                    }
                    if (sourceParamType.IsAssignableFrom(byteType) == true)
                    {
                        continue;
                    }
                    if (sourceParamType.IsAssignableFrom(intType) == true || sourceParamType.BaseType == enumType)
                    {
                        continue;
                    }
                    if (sourceParamType.IsAssignableFrom(uIntType) == true)
                    {
                        continue;
                    }
                    if (sourceParamType.IsAssignableFrom(longType) == true)
                    {
                        continue;
                    }
                    if (sourceParamType.IsAssignableFrom(uLongType) == true)
                    {
                        continue;
                    }
                    break;
                case PODTYPE.POD_VECTOR3:
                    if (sourceParamType.IsAssignableFrom(vector3Type) || sourceParamType.IsAssignableFrom(vector3IntType))
                    {
                        continue;
                    }
                    break;
                case PODTYPE.POD_STRING:
                    if (sourceParamType.IsAssignableFrom(stringType) == true)
                    {
                        continue;
                    }
                    break;
                case PODTYPE.POD_RAY:
                    if (sourceParamType.IsAssignableFrom(rayType) == true)
                    {
                        continue;
                    }
                    break;
                case PODTYPE.POD_BINARYBLOB:
                    if (sourceParamType.IsAssignableFrom(binaryBlobType) == true)
                    {
                        continue;
                    }
                    break;
                case PODTYPE.POD_COLOR:
                    if (sourceParamType.IsAssignableFrom(colorType) == true)
                    {
                        continue;
                    }
                    break;
                case PODTYPE.POD_MATRIX:
                    if (sourceParamType.IsAssignableFrom(matrixType) == true)
                    {
                        continue;
                    }
                    break;
                case PODTYPE.POD_PLANE:
                    if (sourceParamType.IsAssignableFrom(planeType) == true)
                    {
                        continue;
                    }
                    break;
                case PODTYPE.POD_QUATERNION:
                    if (sourceParamType.IsAssignableFrom(quaternionType) == true)
                    {
                        continue;
                    }
                    break;
                case PODTYPE.POD_VECTOR2:
                    if (sourceParamType.IsAssignableFrom(vector2Type) || sourceParamType.IsAssignableFrom(vector2IntType))
                    {
                        continue;
                    }
                    break;
                case PODTYPE.POD_VECTOR4:
                    if (sourceParamType.IsAssignableFrom(vector4Type))
                    {
                        continue;
                    }
                    break;
            }
            return false;
        }
        return true;
    }

    //Generalized utility version - move these!
    public static string GetParameterAsString(int paramIndex, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        if (paramIndex >= numParameters) {
            return null;
        }
        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_STRING) {
            return null;
        }
        return LuauCore.PtrToStringUTF8(parameterDataPtrs[paramIndex], parameterDataSizes[paramIndex]);
    }

    // public static AirshipComponent GetParameterAsAirshipComponent(int paramIndex, int numParameters, int[] parameterDataPODTypes,
    //     IntPtr[] parameterDataPtrs, int[] parameterDataSizes) {
    //     if (paramIndex >= numParameters)
    //     {
    //         return null;
    //     }
    //     if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_STRING)
    //     {
    //         return null;
    //     }
    // }

    public static string GetPropertyAsString(PODTYPE dataPodType, IntPtr dataPtr) {
        return dataPodType == PODTYPE.POD_STRING ? PtrToStringUTF8NullTerminated(dataPtr) : null;
    }

    public static bool GetParameterAsBool(int paramIndex, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes, out bool exists) {
        if (paramIndex >= numParameters) {
            exists = false;
            return false;
        }

        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_BOOL) {
            exists = false;
            return false;
        }

        exists = true;
        return NewBoolFromPointer(parameterDataPtrs[paramIndex]);
    }

    public static Vector3 GetParameterAsVector3(int paramIndex, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        if (paramIndex >= numParameters) {
            return Vector3.zero;
        }
        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_VECTOR3) {
            return Vector3.zero;
        }
        return NewVector3FromPointer(parameterDataPtrs[paramIndex]);
    }
    
    public static Ray GetParameterAsRay(int paramIndex, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        if (paramIndex >= numParameters) {
            return new Ray();
        }
        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_RAY) {
            return new Ray();
        }
        return NewRayFromPointer(parameterDataPtrs[paramIndex]);
    }

    public static Color GetParameterAsColor(int paramIndex, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        if (paramIndex >= numParameters) {
            return Color.white;
        }
        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_COLOR) {
            return Color.white;
        }
        return NewColorFromPointer(parameterDataPtrs[paramIndex]);
    }
    public static Quaternion GetParameterAsQuaternion(int paramIndex, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        if (paramIndex >= numParameters) {
            return Quaternion.identity;
        }
        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_QUATERNION) {
            return Quaternion.identity;
        }
        return NewQuaternionFromPointer(parameterDataPtrs[paramIndex]);
    }

    public static Vector2 GetParameterAsVector2(int paramIndex, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        if (paramIndex >= numParameters || parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_VECTOR2) {
            return Vector2.zero;
        }
        return NewVector2FromPointer(parameterDataPtrs[paramIndex]);
    }
    public static float GetParameterAsFloat(int paramIndex, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        if (paramIndex >= numParameters) {
            return 0;
        }
        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_DOUBLE) {
            return 0;
        }
        return NewFloatFromPointer(parameterDataPtrs[paramIndex]);
    }
    public static int GetParameterAsInt(int paramIndex, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        if (paramIndex >= numParameters) {
            return 0;
        }
        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_DOUBLE
            && parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_INT32
            && parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_LUAFUNCTION) {
            return 0;
        }

        return NewIntFromPointer(parameterDataPtrs[paramIndex]);
    }

    private static readonly int[] ObjectParamIntData = new int[1];
    public static object GetParameterAsObject(int paramIndex,  int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes, IntPtr thread) {
        if (paramIndex >= numParameters) {
            return null;
        }
        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_OBJECT) {
            return null;
        }
        // var intData = new int[1];
        Marshal.Copy(parameterDataPtrs[paramIndex], ObjectParamIntData, 0, 1);
        var propertyInstanceId = ObjectParamIntData[0];
        
        //int instanceId = NewIntFromPointer(parameterDataPtrs[paramIndex]);
        return ThreadDataManager.GetObjectReference(thread, propertyInstanceId);
    }

    private static readonly double[] DoubleData = new double[1];
    private static float NewFloatFromPointer(IntPtr data) {
        // double[] doubles = new double[1];
        Marshal.Copy(data, DoubleData, 0, 1);
        return (float)DoubleData[0];
    }

    private static int NewIntFromPointer(IntPtr data) {
        // double[] doubles = new double[1];
        Marshal.Copy(data, DoubleData, 0, 1);
        return (int)DoubleData[0];
    }

    private static bool NewBoolFromPointer(IntPtr data) {
        // double[] doubles = new double[1];
        Marshal.Copy(data, DoubleData, 0, 1);
        return DoubleData[0] != 0;
    }

    private static readonly float[] VectorData = new float[4];
    private static Vector3 NewVector3FromPointer(IntPtr data) {
        // float[] floats = new float[3];
        Marshal.Copy(data, VectorData, 0, 3);
        return new Vector3(VectorData[0], VectorData[1], VectorData[2]);
    }
    public static int Vector3Size() {
        return 4 * 3;
    }

    private static Assets.Luau.BinaryBlob NewBinaryBlobFromPointer(IntPtr data, int size) {
        var bytes = new byte[size];
        Marshal.Copy(data, bytes, 0, size);
        return new Assets.Luau.BinaryBlob(bytes);
    }

    private static readonly float[] RayData = new float[6]; 
    public static Ray NewRayFromPointer(IntPtr data) {
        // float[] floats = new float[6];
        Marshal.Copy(data, RayData, 0, 6);
        var origin = new Vector3(RayData[0], RayData[1], RayData[2]);
        var direction = new Vector3(RayData[3], RayData[4], RayData[5]);
        return new Ray(origin, direction);
    }
    public static int RaySize() {
        return 6 * sizeof(float);
    }

    public static Color NewColorFromPointer(IntPtr data) {
        // float[] floats = new float[4];
        Marshal.Copy(data, VectorData, 0, 4);
        return new Color(VectorData[0], VectorData[1], VectorData[2], VectorData[3]);
    }
    public static int ColorSize() {
        return 4 * sizeof(float);
    }

    private static readonly float[] MatrixData = new float[16];
    public static Matrix4x4 NewMatrixFromPointer(IntPtr data) {
        // float[] floats = new float[16];
        Marshal.Copy(data, MatrixData, 0, 16);
        return new Matrix4x4(
            new Vector4(MatrixData[0], MatrixData[1], MatrixData[2], MatrixData[3]),
            new Vector4(MatrixData[4], MatrixData[5], MatrixData[6], MatrixData[7]),
            new Vector4(MatrixData[8], MatrixData[9], MatrixData[10], MatrixData[11]),
            new Vector4(MatrixData[12], MatrixData[13], MatrixData[14], MatrixData[15])
        );
    }
    public static int MatrixSize() {
        return 16 * sizeof(float);
    }

    public static Plane NewPlaneFromPointer(IntPtr data) {
        // float[] floats = new float[4];
        Marshal.Copy(data, VectorData, 0, 4);
        return new Plane(new Vector3(VectorData[0], VectorData[1], VectorData[2]), VectorData[3]);
    }
    public static int PlaneSize() {
        return 4 * sizeof(float);
    }

    public static Quaternion NewQuaternionFromPointer(IntPtr data) {
        // float[] floats = new float[4];
        Marshal.Copy(data, VectorData, 0, 4);
        return new Quaternion(VectorData[0], VectorData[1], VectorData[2], VectorData[3]);
    }
    public static int QuaternionSize() {
        return 4 * sizeof(float);
    }

    public static Vector2 NewVector2FromPointer(IntPtr data) {
        // var floats = new float[2];
        Marshal.Copy(data, VectorData, 0, 2);
        return new Vector2(VectorData[0], VectorData[1]);
    }
    public static int Vector2Size() {
        return 2 * sizeof(float);
    }
    
    public static Vector4 NewVector4FromPointer(IntPtr data) {
        // var floats = new float[4];
        Marshal.Copy(data, VectorData, 0, 4);
        return new Vector4(VectorData[0], VectorData[1], VectorData[2], VectorData[3]);
    }
    public static int Vector4Size() {
        return 4 * sizeof(float);
    }

    private static string PtrToStringUTF8(IntPtr nativePtr, int size) {
        unsafe {
            return s_stringPool.GetString((byte*)nativePtr.ToPointer(), size, out var hash);
        }
    }

    private static string PtrToStringUTF8(IntPtr nativePtr, int size, out ulong hash) {
        unsafe {
            return s_stringPool.GetString((byte*)nativePtr.ToPointer(), size, out hash);
        }
    }

    private static string PtrToStringUTF8NullTerminated(IntPtr nativePtr) {
        unsafe {
            var size = 0;
            var b = (byte*)nativePtr.ToPointer();
            while (size < 1024*1024) { //Caps at 1mb just in case
                if (b[size] == 0) {
                    break;
                }
                size++;
            }
            return s_stringPool.GetString((byte*)nativePtr.ToPointer(), size, out var hash);
        }
    }
}
 