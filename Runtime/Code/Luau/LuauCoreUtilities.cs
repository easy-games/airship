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
        AirshipComponent binding = obj.AddComponent<AirshipComponent>();
        binding.CreateThreadFromPath(path, LuauContext.Game);  // "Resources/Editor/TestEditorScript.lua"

        GameObject.DestroyImmediate(obj);

        if (Application.isPlaying == false)
        {
            LuauCore.ShutdownInstance();
        }
    }

    public int ResumeScript(LuauContext context, AirshipRuntimeScript binding) {
        var retValue = LuauState.FromContext(context).ResumeScript(binding);

        return retValue;
    }

    public void AddThread(LuauContext context, IntPtr thread, AirshipComponent binding) {
        LuauState.FromContext(context).AddThread(thread, binding);
    }

    public static void ErrorThread(IntPtr thread, string errorMsg) {
        byte[] str = System.Text.Encoding.UTF8.GetBytes((string) errorMsg);
        var allocation = GCHandle.Alloc(str, GCHandleType.Pinned); //Ok
        LuauPlugin.LuauErrorThread(thread, allocation.AddrOfPinnedObject(), str.Length);
        allocation.Free();
    }

    /// <summary>
    /// Returns a file path relative to Assets/. Example output:
    /// <code>airshippackages/@easy/core/server/protectedservices/airship/platforminventory/platforminventoryservice.lua</code>
    ///
    /// Will add ".lua" to the end and lowercase the result. Intention is that two different paths
    /// pointing at the same file will result in the same output.
    /// </summary>
    private static string GetTidyPathNameForLuaFile(string fileNameStr)
    {
        // Make sure assets is properly capitalized for GetRelativePath call
        if (fileNameStr.ToLower().StartsWith("assets")) {
            fileNameStr = fileNameStr.Substring("assets".Length);
        }
        fileNameStr = "Assets/" + fileNameStr;
        
        // Add .lua to the end
        if (!fileNameStr.EndsWith(".lua")) {
            fileNameStr += ".lua";
        }
        
        
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
        return fileNameStr.ToLower();
    }

}
