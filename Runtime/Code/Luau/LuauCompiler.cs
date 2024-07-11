using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Luau;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class LuauCompiler {
    public const string IconOk = "Packages/gg.easy.airship/Editor/LuauIcon.png";
    public const string IconFail = "Packages/gg.easy.airship/Editor/LuauErrorIcon.png";

    public struct CompilationResult {
        public IntPtr Data;
        public long DataSize;
        public bool Compiled;
    }

    public static void RuntimeCompile(string path, string data, AirshipScript airshipScript, bool airshipBehaviour) {
        // Read Lua source
        data += "\r\n" + "\r\n";

        IntPtr filenameStr = Marshal.StringToCoTaskMemUTF8(path); //Ok
        IntPtr dataStr = Marshal.StringToCoTaskMemUTF8(data); //Ok

        // Compile
        var len = Encoding.Unicode.GetByteCount(data);
        IntPtr res = LuauPlugin.LuauCompileCode(dataStr, len, filenameStr, path.Length, 1);

        Marshal.FreeCoTaskMem(dataStr);
        Marshal.FreeCoTaskMem(filenameStr);

        // Figure out what happened
        var resStruct = Marshal.PtrToStructure<CompilationResult>(res);
        // Debug.Log("Compilation of " + ctx.assetPath + ": " + resStruct.Compiled.ToString());

        bool compileSuccess = true;
        string compileErrMessage = "none";

        airshipScript.airshipBehaviour = airshipBehaviour;
        airshipScript.m_path = path;

        if (!resStruct.Compiled) {
            var resString = Marshal.PtrToStringUTF8(resStruct.Data, (int)resStruct.DataSize);
            compileSuccess = false;
            compileErrMessage = resString;
            UnityEngine.Debug.LogError($"Failed to compile {path}: {resString}");
        }

        airshipScript.m_compiled = compileSuccess;
        airshipScript.m_compilationError = compileErrMessage;

        var bytes = new byte[resStruct.DataSize];
        Marshal.Copy(resStruct.Data, bytes, 0, (int)resStruct.DataSize);

        airshipScript.m_bytes = bytes;

        var split = path.Split("/");
        if (split.Length > 0) {
            airshipScript.name = split[split.Length - 1];
        }
        // var iconPath = binaryFile.m_compiled ? IconOk : IconFail;
        // var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
    }
}