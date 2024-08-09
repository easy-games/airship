using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Airship.DevConsole;
using UnityEngine;
using Luau;
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

    static private object[] UnrollPodObjects(IntPtr thread, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs)
    {
        object[] podObjects = new object[numParameters];
        for (int j = 0; j < numParameters; j++)
        {
            if (parameterDataPODTypes[j] == (int)PODTYPE.POD_OBJECT)
            {
                int[] intData = new int[1];
                Marshal.Copy(parameterDataPtrs[j], intData, 0, 1);
                int instanceId = intData[0];
                podObjects[j] = ThreadDataManager.GetObjectReference(thread, instanceId);
            }
            else
            {
                podObjects[j] = null;
            }
        }
        return podObjects;
    }

    static private int RunConstructor(IntPtr thread, Type type, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {

        ConstructorInfo[] constructors = type.GetConstructors();

        if (constructors.Length == 0)
        {
            System.Object retStruct = Activator.CreateInstance(type);

            //Push this onto the stack
            WritePropertyToThread(thread, retStruct, type);
            return 1;
        }

        object[] podObjects = UnrollPodObjects(thread, numParameters, parameterDataPODTypes, parameterDataPtrs);
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

        object[] parsedData = null;
        bool success = ParseParameterData(thread, numParameters, parameterDataPtrs, parameterDataPODTypes, finalParameters, paramaterDataSizes, podObjects, out parsedData);
        if (success == false)
        {
            ThreadDataManager.Error(thread);
            Debug.LogError("Error: Unable to parse parameters for " + type.Name + " constructor.");
            return 0;
        }

        //We have parameters
        System.Object returnValue = finalConstructor.Invoke(parsedData);

        //Push this onto the stack
        WritePropertyToThread(thread, returnValue, type);
        return 1;
    }

    static public unsafe bool WritePropertyToThread(IntPtr thread, System.Object value, Type t) {
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

        if (t == intType || t.IsEnum) {
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
            Vector3 vec = (Vector3)value;
            LuauPlugin.LuauPushVector3ToThread(thread, vec.x, vec.y, vec.z);
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
            double number = (double)value;
            LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_DOUBLE, new IntPtr(value: &number),
                0); // 0, because we know how big an intPtr is
            return true;
        }

        if (t == floatType) {
            double number = (float)value;
            LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_DOUBLE, new IntPtr(value: &number),
                0); // 0, because we know how big an intPtr is
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
            Quaternion quat = (Quaternion)value;
            float[] quatData = new float[4];
            quatData[0] = quat.x;
            quatData[1] = quat.y;
            quatData[2] = quat.z;
            quatData[3] = quat.w;

            var gch = GCHandle.Alloc(quatData, GCHandleType.Pinned); //Ok
            LuauPlugin.LuauPushValueToThread(thread, (int)PODTYPE.POD_QUATERNION, gch.AddrOfPinnedObject(),
                0); // 0, because we know how big an intPtr is
            gch.Free();

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

    private static bool ParseParameterData(IntPtr thread, int numParameters, IntPtr[] intPtrs, int[] podTypes, ParameterInfo[] methodParameters, int[] sizes, object[] podObjects, out object[] parsedData)
    {
        parsedData = new object[numParameters];

        for (int paramIndex = 0; paramIndex < numParameters; paramIndex++)
        {
            PODTYPE paramType = (PODTYPE)podTypes[paramIndex];
            Type sourceParamType = methodParameters[paramIndex].ParameterType;
            switch (paramType)
            {
                case PODTYPE.POD_OBJECT:
                    {
                        System.Object objectRef = podObjects[paramIndex];
                        parsedData[paramIndex] = objectRef;
                        continue;
                    }


                case PODTYPE.POD_DOUBLE:
                    {
                        double[] doubleData = new double[1];
                        Marshal.Copy(intPtrs[paramIndex], doubleData, 0, 1);
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
                        Marshal.Copy(intPtrs[paramIndex], doubleData, 0, 1);
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
                        parsedData[paramIndex] = NewVector3FromPointer(intPtrs[paramIndex]);
                        continue;
                    }

                case PODTYPE.POD_STRING:
                    {
                        string dataStr = LuauCore.PtrToStringUTF8(intPtrs[paramIndex], sizes[paramIndex]);
                        parsedData[paramIndex] = dataStr;

                        continue;
                    }

                case PODTYPE.POD_RAY:
                    {
                        parsedData[paramIndex] = NewRayFromPointer(intPtrs[paramIndex]);
                        continue;
                    }

                case PODTYPE.POD_BINARYBLOB:
                    {
                        parsedData[paramIndex] = NewBinaryBlobFromPointer(intPtrs[paramIndex], sizes[paramIndex]);
                        continue;
                    }

                case PODTYPE.POD_PLANE:
                    {
                        parsedData[paramIndex] = NewPlaneFromPointer(intPtrs[paramIndex]);
                        continue;
                    }
                case PODTYPE.POD_QUATERNION:
                    {
                        parsedData[paramIndex] = NewQuaternionFromPointer(intPtrs[paramIndex]);
                        continue;
                    }
                case PODTYPE.POD_VECTOR2:
                    {
                        parsedData[paramIndex] = NewVector2FromPointer(intPtrs[paramIndex]);
                        continue;
                    }
                case PODTYPE.POD_VECTOR4:
                    {
                        parsedData[paramIndex] = NewVector4FromPointer(intPtrs[paramIndex]);
                        continue;
                    }
                case PODTYPE.POD_COLOR:
                    {
                        parsedData[paramIndex] = NewColorFromPointer(intPtrs[paramIndex]);
                        continue;
                    }
                case PODTYPE.POD_MATRIX:
                    {
                        parsedData[paramIndex] = NewMatrixFromPointer(intPtrs[paramIndex]);
                        continue;
                    }
            }

            Debug.LogError("Param " + paramIndex + " " + podTypes[paramIndex] + " not valid type for this parameter/unhandled so far.");
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


    private static HashSet<MethodInfo> _methodsUsedTest = new();
    private static void FindMethod(LuauContext context, Type type, string methodName, int numParameters, int[] podTypes, object[] podObjects, out bool nameFound, out bool countFound, out ParameterInfo[] finalParameters, out MethodInfo finalMethod, out bool finalExtensionMethod, out bool insufficientContext)
    {
        nameFound = false;
        countFound = false;
        finalParameters = null;
        finalMethod = null;
        finalExtensionMethod = false;
        insufficientContext = false;
        
        var methodDict = GetCachedMethods(type);
        if (methodDict.TryGetValue(methodName, out var methods))
        {
            nameFound = true;
            foreach (var info in methods)
            {
                ParameterInfo[] parameters = GetCachedParameters(info);

                //match parameters
                if (parameters.Length != numParameters)
                {
                    continue;
                }
                countFound = true;

                bool match = MatchParameters(numParameters, parameters, podTypes, podObjects);
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

            bool match = MatchParameters(numParameters, parameters, podTypes, podObjects);

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

    static public void FindConstructor(Type type, ConstructorInfo[] constructors, int numParameters, int[] podTypes, object[] podObjects, out bool countFound, out ParameterInfo[] finalParameters, out ConstructorInfo finalConstructor)
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

            bool match = MatchParameters(numParameters, parameters, podTypes, podObjects);
            if (match == true)
            {
                finalConstructor = info;
                finalParameters = parameters;
                break;
            }
        }
    }

    static bool MatchParameters(int numParameters, ParameterInfo[] parameters, int[] podTypes, object[] podObjects)
    {
        for (int paramIndex = 0; paramIndex < numParameters; paramIndex++)
        {
            PODTYPE paramType = (PODTYPE)podTypes[paramIndex];
            Type sourceParamType = parameters[paramIndex].ParameterType;
            if (parameters[paramIndex].IsOut == true || parameters[paramIndex].IsIn == true)
            {
                sourceParamType = sourceParamType.GetElementType();
            }
            
            switch (paramType)
            {
                case PODTYPE.POD_NULL:
                    continue;
                case PODTYPE.POD_OBJECT:
                    var obj = podObjects[paramIndex];
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
    static public string GetParameterAsString(int paramIndex, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {
        if (paramIndex >= numParameters)
        {
            return null;
        }
        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_STRING)
        {
            return null;
        }
        return LuauCore.PtrToStringUTF8(parameterDataPtrs[paramIndex], paramaterDataSizes[paramIndex]);
    }

    static public bool GetParameterAsBool(int paramIndex, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] parameterDataSizes, out bool exists) {
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

    static public Vector3 GetParameterAsVector3(int paramIndex, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes = null)
    {
        if (paramIndex >= numParameters)
        {
            return Vector3.zero;
        }
        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_VECTOR3)
        {
            return Vector3.zero;
        }
        return NewVector3FromPointer(parameterDataPtrs[paramIndex]);
    }

    static public Ray GetParameterAsRay(int paramIndex, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes = null)
    {
        if (paramIndex >= numParameters)
        {
            return new Ray();
        }
        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_RAY) {
            return new Ray();
        }
        return NewRayFromPointer(parameterDataPtrs[paramIndex]);
    }

    public static Color GetParameterAsColor(int paramIndex, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes = null)
    {
        if (paramIndex >= numParameters)
        {
            return Color.white;
        }
        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_COLOR)
        {
            return Color.white;
        }
        return NewColorFromPointer(parameterDataPtrs[paramIndex]);
    }
    public static Quaternion GetParameterAsQuaternion(int paramIndex, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes = null)
    {
        if (paramIndex >= numParameters)
        {
            return Quaternion.identity;
        }
        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_QUATERNION)
        {
            return Quaternion.identity;
        }
        return NewQuaternionFromPointer(parameterDataPtrs[paramIndex]);
    }

    public static Vector2 GetParameterAsVector2(int paramIndex, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes = null) {
        if (paramIndex >= numParameters || parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_VECTOR2) {
            return Vector2.zero;
        }
        return NewVector2FromPointer(parameterDataPtrs[paramIndex]);
    }
    static public float GetParameterAsFloat(int paramIndex, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes = null)
    {
        if (paramIndex >= numParameters)
        {
            return 0;
        }
        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_DOUBLE)
        {
            return 0;
        }
        return NewFloatFromPointer(parameterDataPtrs[paramIndex]);
    }
    static public int GetParameterAsInt(int paramIndex, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes = null)
    {
        if (paramIndex >= numParameters)
        {
            return 0;
        }
        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_DOUBLE
            && parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_INT32
            && parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_LUAFUNCTION)
        {
            return 0;
        }

        return NewIntFromPointer(parameterDataPtrs[paramIndex]);
    }
 
    static public object GetParameterAsObject(int paramIndex,  int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes, IntPtr thread)
    {
        if (paramIndex >= numParameters)
        {
            return null;
        }
        if (parameterDataPODTypes[paramIndex] != (int)PODTYPE.POD_OBJECT)
        {
            return null;
        }
        int[] intData = new int[1];
        Marshal.Copy(parameterDataPtrs[paramIndex], intData, 0, 1);
        int propertyInstanceId = intData[0];
        
        //int instanceId = NewIntFromPointer(parameterDataPtrs[paramIndex]);
        return ThreadDataManager.GetObjectReference(thread, propertyInstanceId);
    }
 
    static float NewFloatFromPointer(IntPtr data)
    {
        double[] doubles = new double[1];
        Marshal.Copy(data, doubles, 0, 1);
        return (float)doubles[0];
    }

    static int NewIntFromPointer(IntPtr data)
    {
        double[] doubles = new double[1];
        Marshal.Copy(data, doubles, 0, 1);
        return (int)doubles[0];
    }

    static bool NewBoolFromPointer(IntPtr data) {
        double[] doubles = new double[1];
        Marshal.Copy(data, doubles, 0, 1);
        return doubles[0] != 0;
    }

    static Vector3 NewVector3FromPointer(IntPtr data)
    {
        float[] floats = new float[3];
        Marshal.Copy(data, floats, 0, 3);
        return new Vector3(floats[0], floats[1], floats[2]);
    }
    public static int Vector3Size()
    {
        return 4 * 3;
    }

    static Assets.Luau.BinaryBlob NewBinaryBlobFromPointer(IntPtr data, int size)
    {
        byte[] bytes = new byte[size];
        Marshal.Copy(data, bytes, 0, size);
        return new Assets.Luau.BinaryBlob(bytes);
    }

    public static Ray NewRayFromPointer(IntPtr data)
    {
        float[] floats = new float[6];
        Marshal.Copy(data, floats, 0, 6);
        Vector3 origin = new Vector3(floats[0], floats[1], floats[2]);
        Vector3 direction = new Vector3(floats[3], floats[4], floats[5]);
        return new Ray(origin, direction);
    }
    public static int RaySize()
    {
        return 6 * sizeof(float);
    }

    public static Color NewColorFromPointer(IntPtr data)
    {
        float[] floats = new float[4];
        Marshal.Copy(data, floats, 0, 4);
        return new Color(floats[0], floats[1], floats[2], floats[3]);
    }
    public static int ColorSize()
    {
        return 4 * sizeof(float);
    }

    public static Matrix4x4 NewMatrixFromPointer(IntPtr data)
    {
        float[] floats = new float[16];
        Marshal.Copy(data, floats, 0, 16);
        return new Matrix4x4(new Vector4(floats[0], floats[1], floats[2], floats[3]), new Vector4(floats[4], floats[5], floats[6], floats[7]), new Vector4(floats[8], floats[9], floats[10], floats[11]), new Vector4(floats[12], floats[13], floats[14], floats[15]));
    }
    public static int MatrixSize()
    {
        return 16 * sizeof(float);
    }

    public static Plane NewPlaneFromPointer(IntPtr data)
    {
        float[] floats = new float[4];
        Marshal.Copy(data, floats, 0, 4);
        return new Plane(new Vector3(floats[0], floats[1], floats[2]), floats[3]);
    }
    public static int PlaneSize()
    {
        return 4 * sizeof(float);
    }

    public static Quaternion NewQuaternionFromPointer(IntPtr data)
    {
        float[] floats = new float[4];
        Marshal.Copy(data, floats, 0, 4);
        return new Quaternion(floats[0], floats[1], floats[2], floats[3]);
    }
    public static int QuaternionSize()
    {
        return 4 * sizeof(float);
    }

    public static Vector2 NewVector2FromPointer(IntPtr data) {
        var floats = new float[2];
        Marshal.Copy(data, floats, 0, 2);
        return new Vector2(floats[0], floats[1]);
    }
    public static int Vector2Size()
    {
        return 2 * sizeof(float);
    }
    
    public static Vector4 NewVector4FromPointer(IntPtr data) {
        var floats = new float[4];
        Marshal.Copy(data, floats, 0, 4);
        return new Vector4(floats[0], floats[1], floats[2], floats[3]);
    }
    public static int Vector4Size()
    {
        return 4 * sizeof(float);
    }

    public static string PtrToStringUTF8(IntPtr nativePtr, int size)
    {
        unsafe
        {
            return s_stringPool.GetString((byte*)nativePtr.ToPointer(), size, out ulong hash);
        }
    }

    public static string PtrToStringUTF8(IntPtr nativePtr, int size, out ulong hash)
    {
        unsafe
        {
            return s_stringPool.GetString((byte*)nativePtr.ToPointer(), size, out hash);
        }
    }

    public static string PtrToStringUTF8NullTerminated(IntPtr nativePtr)
    {
        unsafe
        {
            int size = 0;
            var b = (byte*)nativePtr.ToPointer();
            while (size < 1024*1024) //Caps at 1mb just in case
            {
                if (b[size] == 0)
                {
                    break;
                }
                size++;
            }
            return s_stringPool.GetString((byte*)nativePtr.ToPointer(), size, out ulong hash);
        }
    }
}
 