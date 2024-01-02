using UnityEngine;
using UnityEditor;
using Unity.EditorCoroutines.Editor;

using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Luau;
using Newtonsoft.Json;
using Debug = UnityEngine.Debug;

[UnityEditor.AssetImporters.ScriptedImporter(1, "lua")]
public class LuauImporter : UnityEditor.AssetImporters.ScriptedImporter
{
    private const string IconOk = "Packages/gg.easy.airship/Editor/LuauIcon.png";
    private const string IconFail = "Packages/gg.easy.airship/Editor/LuauErrorIcon.png";

    private static EditorCoroutine _stopOfCompilationCoroutine;
    private static bool _isCompiling = false;
    private static long _elapsed;

    private static readonly List<Luau.BinaryFile> CompiledFiles = new();
    private static readonly Stopwatch Stopwatch = new();
    private static readonly Stopwatch StopwatchCompile = new();

    private struct CompilationResult
    {
        public IntPtr Data;
        public long DataSize;
        public bool Compiled;
    }

    [MenuItem("Airship/Misc/Reimport Luau Files")]
    public static void ReimportAll() {
        AssetDatabase.Refresh();

        AssetDatabase.StartAssetEditing();
        foreach (var file in Directory.EnumerateFiles("Assets", "*.lua", SearchOption.AllDirectories)) {
            AssetDatabase.ImportAsset(file, ImportAssetOptions.Default);
        }
        AssetDatabase.StopAssetEditing();
    }

    public override unsafe void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
    {
        ClearStopOfCompilationCoroutine();

        if (!_isCompiling)
        {
            _isCompiling = true;
            StopwatchCompile.Reset();
            Stopwatch.Restart();
        }

        // Read Lua source
        var data = File.ReadAllText(ctx.assetPath) + "\r\n" + "\r\n";

        IntPtr filenameStr = Marshal.StringToCoTaskMemUTF8(ctx.assetPath); //Ok
        IntPtr dataStr = Marshal.StringToCoTaskMemUTF8(data); //Ok

        // Compile
        StopwatchCompile.Start();
        IntPtr res = LuauPlugin.LuauCompileCode(dataStr, data.Length, filenameStr, ctx.assetPath.Length, 1);
        StopwatchCompile.Stop();

        Marshal.FreeCoTaskMem(dataStr);
        Marshal.FreeCoTaskMem(filenameStr);

        // Figure out what happened
        var resStruct = Marshal.PtrToStructure<CompilationResult>(res);
        // Debug.Log("Compilation of " + ctx.assetPath + ": " + resStruct.Compiled.ToString());

        var ext = Path.GetExtension(ctx.assetPath);
        var fileName = ctx.assetPath.Substring(0, ctx.assetPath.Length - ext.Length) + ".bytes";

        var subAsset = ScriptableObject.CreateInstance<Luau.BinaryFile>();

        // Get metadata from JSON file (if it's found):
        var metadataFilepath = $"{ctx.assetPath}.json~";
        if (File.Exists(metadataFilepath))
        {
            var json = File.ReadAllText(metadataFilepath);
            var metadata = LuauMetadata.FromJson(json);
            subAsset.m_metadata = metadata;
        }

        subAsset.m_path = ctx.assetPath;

        if (!resStruct.Compiled)
        {
            var resString = Marshal.PtrToStringUTF8(resStruct.Data, (int)resStruct.DataSize);
            subAsset.m_compiled = false;
            subAsset.m_compilationError = resString;
            ctx.LogImportError($"Failed to compile {ctx.assetPath}: {resString}");
        }
        else
        {
            subAsset.m_compiled = true;
            subAsset.m_compilationError = "none";
        }

        var bytes = new byte[resStruct.DataSize];
        Marshal.Copy(resStruct.Data, bytes, 0, (int)resStruct.DataSize);

        subAsset.m_bytes = bytes;

        var iconPath = subAsset.m_compiled ? IconOk : IconFail;
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);

        ctx.AddObjectToAsset(fileName, subAsset, icon);
        ctx.SetMainObject(subAsset);

        CompiledFiles.Add(subAsset);
        _elapsed = Stopwatch.ElapsedMilliseconds;

        ClearStopOfCompilationCoroutine();
        _stopOfCompilationCoroutine = EditorCoroutineUtility.StartCoroutineOwnerless(ScheduleStopOfCompilation());
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

        Debug.LogFormat(
            $"{numCompiled} lua file{(numCompiled == 1 ? "" : "s")} imported in {elapsedTime} ({successFormat}, {failureFormat})", numSuccess, numFailure);
    }
}
