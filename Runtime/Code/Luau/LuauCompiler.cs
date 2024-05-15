using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Luau;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

public static class LuauCompiler {
    public const string IconOk = "Packages/gg.easy.airship/Editor/LuauIcon.png";
    public const string IconFail = "Packages/gg.easy.airship/Editor/LuauErrorIcon.png";

    private static unsafe T[] ArrayFromPointer<T>(void* source, int length) where T : unmanaged {
        var type = typeof(T);
        var sizeInBytes = sizeof(T);
        
        var output = new T[length];

        if (type.IsPrimitive) {
            var handle = GCHandle.Alloc(output, GCHandleType.Pinned);
            var dest = (byte*)handle.AddrOfPinnedObject().ToPointer();
            var byteLen = length * sizeInBytes;
            for (var i = 0; i < byteLen; i++) {
                dest[i] = ((byte*)source)[i];
            }
            handle.Free();
        } else if (type.IsValueType) {
            if (!type.IsLayoutSequential && !type.IsExplicitLayout) {
                throw new InvalidOperationException($"{type} does not define a StructLayout attribute");
            }
            for (var i = 0; i < length; i++) {
                var p = new IntPtr((byte*)source + i * sizeInBytes);
                output[i] = (T)Marshal.PtrToStructure(p, type);
            }
        } else {
            throw new InvalidOperationException($"{type} is not supported");
        }

        return output;
    }

    private static unsafe T[] ArrayFromPointer<T>(UnsafeList<T> list) where T : unmanaged {
        return ArrayFromPointer<T>(list.Ptr, list.Length);
    }

    public struct CompilationBatchItem {
        public string Path;
        public string Data;
        public BinaryFile BinaryFile;
        public bool AirshipBehaviour;
    }
    
    public struct NativeCompilationResult : IDisposable {
        public UnsafeList<char> Name;
        public UnsafeList<byte> Data;
        public UnsafeList<char> ErrorMessage;
        public bool Compiled;
        public double CompileTimeMillis;

        public unsafe NativeCompilationResult(LuauPlugin.CompilationResult result, Allocator allocator) {
            Name = new UnsafeList<char>(result.Name.Length, allocator);
            Data = new UnsafeList<byte>(result.Data.Length, allocator);
            ErrorMessage = new UnsafeList<char>(result.ErrorMessage.Length, allocator);
            Compiled = result.Compiled;
            CompileTimeMillis = result.CompileTimeMillis;

            var nameArray = result.Name.ToCharArray();
            fixed (char* ptr = nameArray) {
                Name.AddRange(ptr, nameArray.Length);
            }

            fixed (byte* ptr = result.Data) {
                Data.AddRange(ptr, result.Data.Length);
            }

            var errorArray = result.ErrorMessage.ToCharArray();
            fixed (char* ptr = errorArray) {
                ErrorMessage.AddRange(ptr, errorArray.Length);
            }
        }

        public LuauPlugin.CompilationResult ToCompilationResult() {
            var res = new LuauPlugin.CompilationResult {
                Name = new string(ArrayFromPointer(Name)),
                Data = ArrayFromPointer(Data),
                ErrorMessage = new string(ArrayFromPointer(ErrorMessage)),
                Compiled = Compiled,
                CompileTimeMillis = CompileTimeMillis,
            };

            return res;
        }

        public void Dispose() {
            Name.Dispose();
            Data.Dispose();
            ErrorMessage.Dispose();
        }
    }

    public struct CompileJob : IJob, IDisposable {
        [ReadOnly] public NativeArray<IntPtr> DataPtr;
        [ReadOnly] public NativeArray<IntPtr> PathPtr;
        [ReadOnly] public int DataLen;
        [ReadOnly] public int PathLen;
        public NativeArray<NativeCompilationResult> Result;

        public CompileJob(string data, string path, Allocator allocator) {
            DataPtr = new NativeArray<IntPtr>(1, allocator);
            PathPtr = new NativeArray<IntPtr>(1, allocator);
            DataPtr[0] = Marshal.StringToCoTaskMemUTF8(data);
            PathPtr[0] = Marshal.StringToCoTaskMemUTF8(path);
            DataLen = Encoding.Unicode.GetByteCount(data);
            PathLen = Encoding.Unicode.GetByteCount(path);
            Result = new NativeArray<NativeCompilationResult>(1, allocator);
        }
        
        public void Execute() {
            var name = Marshal.PtrToStringUTF8(PathPtr[0]);
            
            var res = LuauPlugin.LuauCompileCode(name, DataPtr[0], DataLen, PathPtr[0], PathLen, 1);
            Result[0] = new NativeCompilationResult(res, Allocator.Temp);
        }

        public void Dispose() {
            Marshal.FreeCoTaskMem(DataPtr[0]);
            Marshal.FreeCoTaskMem(PathPtr[0]);
            DataPtr.Dispose();
            PathPtr.Dispose();
            Result.Dispose();
        }
    }

    private struct CompileAllJob : IJobParallelFor {
        [ReadOnly] public NativeArray<IntPtr> DataPtrs;
        [ReadOnly] public NativeArray<IntPtr> PathPtrs;
        [ReadOnly] public NativeArray<int> DataLens;
        [ReadOnly] public NativeArray<int> PathLens;
        
        public NativeArray<NativeCompilationResult> Results;
        
        public void Execute(int i) {
            var dataStr = DataPtrs[i];
            var pathStr = PathPtrs[i];
            
            var dataLen = DataLens[i];
            var pathLen = PathLens[i];

            var name = Marshal.PtrToStringUTF8(pathStr);

            var stopwatch = Stopwatch.StartNew();
            var res = LuauPlugin.LuauCompileCode(name, dataStr, dataLen, pathStr, pathLen, 1);
            stopwatch.Stop();
            Results[i] = new NativeCompilationResult(res, Allocator.Temp);
        }
    }

    public static LuauPlugin.CompilationResult[] RuntimeCompileBatch(List<CompilationBatchItem> items) {
        // var actions = new Action[items.Count];
        // var results = new LuauPlugin.CompilationResult[items.Count];
        var mem = new List<IntPtr>();

        var size = items.Count;
        var nativeDataPtrs = new NativeArray<IntPtr>(size, Allocator.TempJob);
        var nativePathPtrs = new NativeArray<IntPtr>(size, Allocator.TempJob);
        var nativeDataLens = new NativeArray<int>(size, Allocator.TempJob);
        var nativePathLens = new NativeArray<int>(size, Allocator.TempJob);
        var nativeResults = new NativeArray<NativeCompilationResult>(size, Allocator.TempJob);

        // Populate native array data:
        for (var i = 0; i < size; i++) {
            var item = items[i];
            
            var pathStr = Marshal.StringToCoTaskMemUTF8(item.Path);
            var dataStr = Marshal.StringToCoTaskMemUTF8(item.Data);
            
            var pathLen = Encoding.Unicode.GetByteCount(item.Path);
            var dataLen = Encoding.Unicode.GetByteCount(item.Data);
            
            mem.Add(pathStr);
            mem.Add(dataStr);

            nativeDataPtrs[i] = dataStr;
            nativePathPtrs[i] = pathStr;
            nativeDataLens[i] = dataLen;
            nativePathLens[i] = pathLen;
        }
        
        var job = new CompileAllJob {
            DataPtrs = nativeDataPtrs,
            PathPtrs = nativePathPtrs,
            DataLens = nativeDataLens,
            PathLens = nativePathLens,
            Results = nativeResults,
        };
        
        Profiler.BeginSample("ParallelJobInvoke");
        var handle = job.Schedule(size, 1);
        handle.Complete();
        Profiler.EndSample();

        foreach (var ptr in mem) {
            Marshal.FreeCoTaskMem(ptr);
        }

        var results = new LuauPlugin.CompilationResult[size];

        for (var i = 0; i < size; i++) {
            var nativeResult = nativeResults[i];
            var result = nativeResult.ToCompilationResult();
            results[i] = result;
            nativeResult.Dispose();
            
            var item = items[i];
            
            item.BinaryFile.m_compiled = result.Compiled;
            item.BinaryFile.m_path = item.Path;
            item.BinaryFile.m_compilationError = result.ErrorMessage;
            item.BinaryFile.m_bytes = result.Data;
            item.BinaryFile.airshipBehaviour = item.AirshipBehaviour;
            
            var split = item.Path.Split("/");
            if (split.Length > 0) {
                item.BinaryFile.name = split[split.Length - 1];
            }
        }

        nativeDataPtrs.Dispose();
        nativePathPtrs.Dispose();
        nativeDataLens.Dispose();
        nativePathLens.Dispose();
        nativeResults.Dispose();

        return results;
    }

    public static (CompileJob, JobHandle) ScheduleRuntimeCompile(string path, string data) {
        var job = new CompileJob(data, path, Allocator.TempJob);
        return (job, job.Schedule());
    }

    public static void RuntimeCompile(string path, string data, BinaryFile binaryFile, bool airshipBehaviour) {
        // Read Lua source
        // data += "\r\n" + "\r\n";

        IntPtr filenameStr = Marshal.StringToCoTaskMemUTF8(path); //Ok
        IntPtr dataStr = Marshal.StringToCoTaskMemUTF8(data); //Ok

        // Compile
        var len = Encoding.Unicode.GetByteCount(data);
        var pathLen = Encoding.Unicode.GetByteCount(path);
        Profiler.BeginSample("LuauCompileCode");
        var resStruct = LuauPlugin.LuauCompileCode(path, dataStr, len, filenameStr, pathLen, 1);
        Profiler.EndSample();

        Marshal.FreeCoTaskMem(dataStr);
        Marshal.FreeCoTaskMem(filenameStr);

        // Figure out what happened
        // Debug.Log("Compilation of " + ctx.assetPath + ": " + resStruct.Compiled.ToString());

        var ext = Path.GetExtension(path);
        var fileName = path.Substring(0, path.Length - ext.Length) + ".bytes";

        bool compileSuccess = resStruct.Compiled;
        string compileErrMessage = "none";

        binaryFile.airshipBehaviour = airshipBehaviour;
        binaryFile.m_path = path;

        if (!compileSuccess) {
            compileErrMessage = resStruct.ErrorMessage;
            UnityEngine.Debug.LogError($"Failed to compile {path}: {resStruct.ErrorMessage}");
        }

        binaryFile.m_compiled = compileSuccess;
        binaryFile.m_compilationError = compileErrMessage;

        binaryFile.m_bytes = resStruct.Data;

        var split = path.Split("/");
        if (split.Length > 0) {
            binaryFile.name = split[split.Length - 1];
        }
        // var iconPath = binaryFile.m_compiled ? IconOk : IconFail;
        // var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
    }
}