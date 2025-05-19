using System;
using System.Runtime.InteropServices;
using System.Text;
using Luau;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class LuauCompiler {
    // Any globals in Luau that have values that change need to be added to this list (e.g. "Time" because "Time.time" changes):
    public static readonly string[] MutableGlobals = {"Time", "NetworkTime", "Physics", "Screen", "Input"};

    public struct CompilationResult {
        public IntPtr Data;
        public long DataSize;
        public bool Compiled;
    }

    private static bool _mutableGlobalsSet = false;

    public static void RuntimeCompile(string path, string data, AirshipScript airshipScript, bool airshipBehaviour) {
        // Read Lua source
        if (!_mutableGlobalsSet) {
            try {
                LuauPlugin.LuauSetMutableGlobals(MutableGlobals);
                _mutableGlobalsSet = true;
            } catch (LuauException e) {
                Debug.LogError(e);
            }
        }

        var filenameStr = Marshal.StringToCoTaskMemUTF8(path);
        var dataStr = Marshal.StringToCoTaskMemUTF8(data);

        // Compile
        var len = Encoding.UTF8.GetByteCount(data);
        var res = LuauPlugin.LuauCompileCode(dataStr, len, filenameStr, path.Length, LuauPlugin.LuauOptimizationLevel.Max);

        Marshal.FreeCoTaskMem(dataStr);
        Marshal.FreeCoTaskMem(filenameStr);

        // Figure out what happened
        var compilationResult = Marshal.PtrToStructure<CompilationResult>(res);

        var compileSuccess = true;
        var compileErrMessage = "none";

        airshipScript.airshipBehaviour = airshipBehaviour;
        airshipScript.m_path = path;

        if (!compilationResult.Compiled) {
            var resString = Marshal.PtrToStringUTF8(compilationResult.Data, (int)compilationResult.DataSize);
            compileSuccess = false;
            compileErrMessage = resString;
            Debug.LogError($"Failed to compile {path}: {resString}");
        }

        airshipScript.m_compiled = compileSuccess;
        airshipScript.m_compilationError = compileErrMessage;

        var bytes = new byte[compilationResult.DataSize];
        Marshal.Copy(compilationResult.Data, bytes, 0, (int)compilationResult.DataSize);

        airshipScript.m_bytes = bytes;

        var split = path.Split("/");
        if (split.Length > 0) {
            airshipScript.name = split[split.Length - 1];
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnSubsystemRegistration() {
        _mutableGlobalsSet = false;
    }
}
