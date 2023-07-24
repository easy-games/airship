using UnityEngine;
using UnityEditor;
using Unity.EditorCoroutines.Editor;

using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Debug = UnityEngine.Debug;

[UnityEditor.AssetImporters.ScriptedImporter(1, "lua")]
public class LuauImporter : UnityEditor.AssetImporters.ScriptedImporter
{
    private const string IconOk = "Packages/gg.easy.airship/Editor/scriptOK.png";
    private const string IconFail = "Packages/gg.easy.airship/Editor/scriptFAIL.png";

    private static EditorCoroutine _shutdownCoroutine;
    private static List<Luau.BinaryFile> _compiledFiles = new();
    private static Stopwatch _stopwatch;
    private static Stopwatch _stopwatchCompile = new();
    private static long _elapsed;

    private struct CompilationResult
    {
        public IntPtr Data;
        public long DataSize;
        public bool Compiled;
    };

    public unsafe override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
    {
        ClearShutdownCoroutine();

        // Read lua source
        var data = File.ReadAllText(ctx.assetPath) + "\r\n" + "\r\n";

        // Startup or reset lua core
        LuauCore.s_shutdown = false;
        var didSetupLuauCore = LuauCore.Instance.CheckSetup();
        if (didSetupLuauCore)
        {
            _compiledFiles.Clear();
            _stopwatch = Stopwatch.StartNew();
            _stopwatchCompile.Reset();
        }
        else
        {
            LuauCore.ResetInstance();
        }

        IntPtr filenameStr = Marshal.StringToCoTaskMemUTF8(ctx.assetPath);
        IntPtr dataStr = Marshal.StringToCoTaskMemUTF8(data);

        // Compile
        _stopwatchCompile.Start();
        IntPtr res = LuauPlugin.LuauCompileCode(dataStr, data.Length, filenameStr, ctx.assetPath.Length, 2);
        _stopwatchCompile.Stop();

        Marshal.FreeCoTaskMem(dataStr);
        Marshal.FreeCoTaskMem(filenameStr);

        // Figure out what happened
        var resStruct = Marshal.PtrToStructure<CompilationResult>(res);
        // Debug.Log("Compilation of " + ctx.assetPath + ":" + resStruct.compiled.ToString());

        var ext = Path.GetExtension(ctx.assetPath);
        var fileName = ctx.assetPath.Substring(0, ctx.assetPath.Length - ext.Length) + ".bytes";

        var subAsset = ScriptableObject.CreateInstance<Luau.BinaryFile>();

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

        _compiledFiles.Add(subAsset);
        _elapsed = _stopwatch.ElapsedMilliseconds;

        ClearShutdownCoroutine();
        _shutdownCoroutine = EditorCoroutineUtility.StartCoroutineOwnerless(ScheduleShutdown());
    }

    private static void ClearShutdownCoroutine()
    {
        if (_shutdownCoroutine == null) return;

        EditorCoroutineUtility.StopCoroutine(_shutdownCoroutine);
        _shutdownCoroutine = null;
    }

    private static IEnumerator ScheduleShutdown()
    {
        // Wait 1 frame
        yield return null;

        _stopwatch.Stop();
        LogResults();
        _compiledFiles.Clear();

        // Shutdown LuauCore instance
        _shutdownCoroutine = null;
        LuauCore.ShutdownInstance();
    }

    private static void LogResults()
    {
        // Count success & failure compilations
        var numCompiled = _compiledFiles.Count;
        var numSuccess = 0;
        var numFailure = 0;
        foreach (var binaryFile in _compiledFiles)
        {
            if (binaryFile.m_compiled) numSuccess++;
            else numFailure++;
        }

        // Show elapsed time in seconds or milliseconds
        var elapsedCompileTime = _stopwatchCompile.ElapsedMilliseconds;

        var elapsedTimeAll = _elapsed >= 1000 ? $"{_elapsed / 1000f:N2}s" : $"{_elapsed}ms";
        var elapsedTime = $"{elapsedTimeAll} (compile time: {elapsedCompileTime}ms)";

        // Show color formatting only if number is above 0
        var successFormat = numSuccess > 0 ? "<color=#77f777>{0} succeeded</color>" : "{0} succeeded";
        var failureFormat = numFailure > 0 ? "<color=#ff534a>{1} failed</color>" : "{1} failed";

        Debug.LogFormat(
            $"{numCompiled} lua file{(numCompiled == 1 ? "" : "s")} imported in {elapsedTime} ({successFormat}, {failureFormat})", numSuccess, numFailure);
    }
}
