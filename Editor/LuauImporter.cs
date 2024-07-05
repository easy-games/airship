using UnityEngine;
using UnityEditor;
using Unity.EditorCoroutines.Editor;

using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Airship.Editor;
using Luau;
using Newtonsoft.Json;
using Debug = UnityEngine.Debug;

[UnityEditor.AssetImporters.ScriptedImporter(2, "lua")]
public class LuauImporter : UnityEditor.AssetImporters.ScriptedImporter
{
    private const string IconOk = "Packages/gg.easy.airship/Editor/LuauAssetIcon.png";
    private const string IconFail = "Packages/gg.easy.airship/Editor/LuauAssetIconError.png";

    private static EditorCoroutine _stopOfCompilationCoroutine;
    private static bool _isCompiling = false;
    private static long _elapsed;

    private static readonly List<Luau.AirshipScript> CompiledFiles = new();
    private static readonly Stopwatch Stopwatch = new();
    private static readonly Stopwatch StopwatchCompile = new();

    public static long byteCounter = 0;

    protected struct CompilationResult
    {
        public IntPtr Data;
        public long DataSize;
        public bool Compiled;
    }
    
    public static void ReimportAllLuau() {
        AssetDatabase.Refresh();
    
        byteCounter = 0;
        AssetDatabase.StartAssetEditing();
        foreach (var file in Directory.EnumerateFiles("Assets", "*.lua", SearchOption.AllDirectories)) {
            AssetDatabase.ImportAsset(file, ImportAssetOptions.Default);
        }
        AssetDatabase.StopAssetEditing();
        Debug.Log("Byte count: " + byteCounter);
    }

    protected (string fileName, CompilationResult? result) CompileLuauAsset(UnityEditor.AssetImporters.AssetImportContext ctx, AirshipScript subAsset, string assetPath) {
        ClearStopOfCompilationCoroutine();

        if (!_isCompiling)
        {
            _isCompiling = true;
            StopwatchCompile.Reset();
            Stopwatch.Restart();
        }

        // Read Lua source
        var data = File.ReadAllText(assetPath);

        IntPtr filenameStr = Marshal.StringToCoTaskMemUTF8(assetPath); //Ok
        IntPtr dataStr = Marshal.StringToCoTaskMemUTF8(data); //Ok

        // Compile
        StopwatchCompile.Start();
        var len = Encoding.Unicode.GetByteCount(data);
        IntPtr res = LuauPlugin.LuauCompileCode(dataStr, len, filenameStr, ctx.assetPath.Length, 1);
        StopwatchCompile.Stop();

        Marshal.FreeCoTaskMem(dataStr);
        Marshal.FreeCoTaskMem(filenameStr);

        // Figure out what happened
        var resStruct = Marshal.PtrToStructure<CompilationResult>(res);
        // Debug.Log("Compilation of " + ctx.assetPath + ": " + resStruct.Compiled.ToString());

        var ext = Path.GetExtension(ctx.assetPath);
        var fileName = assetPath.Substring(0, assetPath.Length - ext.Length) + ".bytes";

        // var subAsset = ScriptableObject.CreateInstance<Luau.BinaryFile>();

        bool compileSuccess = true;
        string compileErrMessage = "none";
        
        // Get metadata from JSON file (if it's found):
        var metadataFilepath = $"{assetPath}.json~";
        if (File.Exists(metadataFilepath))
        {
            var json = File.ReadAllText(metadataFilepath);
            var (metadata, err) = LuauMetadata.FromJson(json);

            if (metadata != null) {
                subAsset.m_metadata = metadata;
                subAsset.airshipBehaviour = true;
            }

            if (err != null) {
                compileSuccess = false;
                compileErrMessage = err;
                ctx.LogImportError($"Failed to compile {ctx.assetPath}: {err}");
            }
        }

        subAsset.m_path = FileExtensions.Transform( ctx.assetPath, FileExtensions.Typescript, FileExtensions.Lua);

        if (!resStruct.Compiled)
        {
            var resString = Marshal.PtrToStringUTF8(resStruct.Data, (int)resStruct.DataSize);
            compileSuccess = false;
            compileErrMessage = resString;
            ctx.LogImportError($"Failed to compile {ctx.assetPath}: {resString}");
        }

        subAsset.m_compiled = compileSuccess;
        subAsset.m_compilationError = compileErrMessage;

        var bytes = new byte[resStruct.DataSize];
        Marshal.Copy(resStruct.Data, bytes, 0, (int)resStruct.DataSize);

        subAsset.m_bytes = bytes;
        byteCounter += bytes.Length;

        CompiledFiles.Add(subAsset);
        _elapsed = Stopwatch.ElapsedMilliseconds;
        
       

        ClearStopOfCompilationCoroutine();
        _stopOfCompilationCoroutine = EditorCoroutineUtility.StartCoroutineOwnerless(ScheduleStopOfCompilation());

        return (fileName, resStruct);
    }

    public override unsafe void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx) {
        var luauScript = ScriptableObject.CreateInstance<Luau.AirshipScript>();
        luauScript.scriptLanguage = AirshipScriptLanguage.Luau;
        luauScript.assetPath = ctx.assetPath;
        var (fileName, _) = CompileLuauAsset(ctx, luauScript, ctx.assetPath);
        
        var iconPath = luauScript.m_compiled ? IconOk : IconFail;
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        ctx.AddObjectToAsset(fileName, luauScript, icon);
        ctx.SetMainObject(luauScript);
    }

    private static void ClearStopOfCompilationCoroutine()
    {
        if (_stopOfCompilationCoroutine == null) return;

        EditorCoroutineUtility.StopCoroutine(_stopOfCompilationCoroutine);
        _stopOfCompilationCoroutine = null;
    }

    private static IEnumerator ScheduleStopOfCompilation()
    {
        // Wait 1 frame
        yield return null;

        StopwatchCompile.Stop();
        Stopwatch.Stop();
        LogResults();
        CompiledFiles.Clear();

        _stopOfCompilationCoroutine = null;
        _isCompiling = false;
    }

    private static bool previouslyCompiledWithErrors = false;
    private static void LogResults()
    {
        // Count success & failure compilations
        var numCompiled = CompiledFiles.Count;
        var numSuccess = 0;
        var numFailure = 0;
        foreach (var binaryFile in CompiledFiles)
        {
            if (binaryFile.m_compiled) numSuccess++;
            else numFailure++;
        }

        // Show elapsed time in seconds or milliseconds
        var elapsedCompileTime = StopwatchCompile.ElapsedMilliseconds;

        var elapsedTimeAll = _elapsed >= 1000 ? $"{_elapsed / 1000f:N2}s" : $"{_elapsed}ms";
        var elapsedTime = $"{elapsedTimeAll} (compile time: {elapsedCompileTime}ms)";

        // Show color formatting only if number is above 0
        var successFormat = numSuccess > 0 ? "<color=#77f777>{0} succeeded</color>" : "{0} succeeded";
        var failureFormat = numFailure > 0 ? "<color=#ff534a>{1} failed</color>" : "{1} failed";

        

        if (EditorIntegrationsConfig.instance.typescriptVerbose || numFailure > 0 || previouslyCompiledWithErrors) {
            Debug.LogFormat(
                $"{numCompiled} lua source{(numCompiled == 1 ? "" : "s")} compiled in {elapsedTime} ({successFormat}, {failureFormat})", numSuccess, numFailure);
        }
        
        previouslyCompiledWithErrors = numFailure > 0;
    }
}
