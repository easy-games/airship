using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Luau;
using UnityEngine;
using Debug = UnityEngine.Debug;
using System.IO;

public class LuauCompiler {
    // Any globals in Luau that have values that change need to be added to this list (e.g. "Time" because "Time.time" changes):
    public static readonly string[] MutableGlobals = {"Time", "NetworkTime", "Physics", "Screen", "Input"};

    public struct CompilationResult {
        public IntPtr Data;
        public long DataSize;
        public bool Compiled;
    }

    private static bool _mutableGlobalsSet = false;

#if UNITY_EDITOR
    private static bool _isCompiling = false;
    private static readonly List<AirshipScript> CompiledFiles = new();
    private static readonly Stopwatch Stopwatch = new();
    private static readonly Stopwatch StopwatchCompile = new();
    public static long byteCounter = 0;
    private static long _elapsed;
    private static bool previouslyCompiledWithErrors = false;
    
    private static Dictionary<string, string> ParseLuauDirectives(string source) {
        Dictionary<string, string> directives = null;
        
        var len = source.Length;
        for (var i = 0; i < len - 3; i++) {
            // Ensure line starts with "--!"
            if (source[i] != '-' || source[i + 1] != '-' || source[i + 2] != '!') {
                break;
            }
            i += 3;

            var start = i;
            
            // Find end of line:
            for (; i < len; i++) {
                if (source[i] == '\n' || (i < len - 1 && source[i] == '\r' && source[i + 1] == '\n')) {
                    break;
                }
            }

            var directive = source.Substring(start, i - start);
            var directiveData = directive.Split(' ', 2);
            if (directiveData.Length == 0 || directiveData[0] == string.Empty) {
                Debug.LogWarning($"Unknown Luau directive: {directive}");
                continue;
            }

            directives ??= new Dictionary<string, string>();
            if (directiveData.Length == 1) {
                directives.Add(directiveData[0], string.Empty);
            } else {
                directives.Add(directiveData[0], directiveData[1].Trim());
            }
        }

        return directives;
    }
    
    public static CompilationResult EditorCompile(AirshipScript asset, string assetPath) {
        //ClearStopOfCompilationCoroutine();

        if (!_mutableGlobalsSet) {
            try {
                LuauPlugin.LuauSetMutableGlobals(LuauCompiler.MutableGlobals);
                _mutableGlobalsSet = true;
            } catch (LuauException e) {
                Debug.LogError(e);
            }
        }
        
        if (!_isCompiling)
        {
            _isCompiling = true;
            StopwatchCompile.Reset();
            Stopwatch.Restart();
        }
        
        // Read Lua source
        var data = File.ReadAllText(assetPath);

        var filenameStr = Marshal.StringToCoTaskMemUTF8(assetPath);
        var dataStr = Marshal.StringToCoTaskMemUTF8(data);

        // Parse and store Luau directives:
        var directives = ParseLuauDirectives(data);
        if (directives != null) {
            asset.m_directives = new string[directives.Count];
            asset.m_directiveValues = new string[directives.Count];
            var directiveIdx = 0;
            foreach (var (k, v) in directives) {
                asset.m_directives[directiveIdx] = k;
                asset.m_directiveValues[directiveIdx] = v;
                directiveIdx++;
            }
        }
        
        // Compile
        StopwatchCompile.Start();
        var len = Encoding.UTF8.GetByteCount(data);
        var res = LuauPlugin.LuauCompileCode(dataStr, len, filenameStr, assetPath.Length, LuauPlugin.LuauOptimizationLevel.Baseline);
        StopwatchCompile.Stop();
        
        Marshal.FreeCoTaskMem(dataStr);
        Marshal.FreeCoTaskMem(filenameStr);
        
        // Figure out what happened
        var compilationResult = Marshal.PtrToStructure<CompilationResult>(res);

        var ext = Path.GetExtension(assetPath);
        var fileName = assetPath.Substring(0, assetPath.Length - ext.Length) + ".bytes";

        var compileSuccess = true;
        var compileErrMessage = "none";
        
        // Get metadata from JSON file (if it's found):
        var metadataFilepath = $"{assetPath}.json~";
        if (File.Exists(metadataFilepath)) {
            var json = File.ReadAllText(metadataFilepath);
            var (metadata, err) = LuauMetadata.FromJson(json);

            if (metadata != null) {
                asset.m_metadata = metadata;
                asset.airshipBehaviour = true;
            }

            if (err != null) {
                compileSuccess = false;
                compileErrMessage = err;
            }
        }

        if (!compilationResult.Compiled) {
            var resString = Marshal.PtrToStringUTF8(compilationResult.Data, (int)compilationResult.DataSize);
            compileSuccess = false;
            compileErrMessage = resString;
        }
        else {
            Debug.Log($"[LuauCompiler] Compiled {assetPath} to {fileName}");
        }
        
        asset.m_compiled = compileSuccess;
        asset.m_compilationError = compileErrMessage;

        var bytes = new byte[compilationResult.DataSize];
        Marshal.Copy(compilationResult.Data, bytes, 0, (int)compilationResult.DataSize);
        
        asset.m_bytes = bytes;
        byteCounter += bytes.Length;

        CompiledFiles.Add(asset);
        _elapsed = Stopwatch.ElapsedMilliseconds;

        //ClearStopOfCompilationCoroutine();
        //_stopOfCompilationCoroutine = EditorCoroutineUtility.StartCoroutineOwnerless(ScheduleStopOfCompilation());
        
        return compilationResult;
    }
#endif
    
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
