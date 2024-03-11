using System;
using System.IO;
using System.Runtime.InteropServices;
using Luau;
using UnityEngine;

public partial class LuauCore : MonoBehaviour
{
    //Utilities
    public static void OneshotScript(string path)
    {
        GameObject obj = new GameObject();
        obj.name = "ScriptRunner";
        ScriptBinding binding = obj.AddComponent<ScriptBinding>();
        binding.CreateThreadFromPath(path, LuauContext.Game);  // "Resources/Editor/TestEditorScript.lua"

        GameObject.DestroyImmediate(obj);

        if (Application.isPlaying == false)
        {
            LuauCore.ShutdownInstance();
        }
    }

    public int ResumeScript(LuauContext context, ScriptBinding binding) {
        var retValue = LuauState.FromContext(context).ResumeScript(binding);

        return retValue;
    }

    public void AddThread(LuauContext context, IntPtr thread, ScriptBinding binding) {
        LuauState.FromContext(context).AddThread(thread, binding);
    }

    public static void ErrorThread(IntPtr thread, string errorMsg) {
        byte[] str = System.Text.Encoding.UTF8.GetBytes((string) errorMsg);
        var allocation = GCHandle.Alloc(str, GCHandleType.Pinned); //Ok
        LuauPlugin.LuauErrorThread(thread, allocation.AddrOfPinnedObject(), str.Length);
        allocation.Free();
    }

    private static string GetTidyPathName(string fileNameStr)
    {
        //Fully qualify it
        fileNameStr = Path.GetFullPath(fileNameStr);
        fileNameStr = Path.GetRelativePath(Application.dataPath, fileNameStr);

        //Remove the ../ off the front
        while (fileNameStr.StartsWith("..\\") || fileNameStr.StartsWith("../"))
        {
            fileNameStr = fileNameStr.Substring(3);
        }

        //Replace backslashes
        fileNameStr = fileNameStr.Replace("\\", "/");
        return fileNameStr;
    }

}
