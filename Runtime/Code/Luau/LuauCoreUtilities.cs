using System;
using System.IO;
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
        binding.CreateThreadFromPath(path, LuauSecurityContext.Normal);  // "Resources/Editor/TestEditorScript.lua"

        GameObject.DestroyImmediate(obj);

        if (Application.isPlaying == false)
        {
            LuauCore.ShutdownInstance();
        }
    }

    public int ResumeScript(ScriptBinding binding)
    {

        int retValue = LuauPlugin.LuauRunThread(binding.m_thread);

        return retValue;
    }

    public void AddThread(IntPtr thread, ScriptBinding binding)
    {
        m_threads.TryAdd(thread, binding);
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
