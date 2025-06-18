using System;
using System.IO;
using System.Runtime.InteropServices;
using Luau;
using UnityEngine;

public partial class LuauCore : MonoBehaviour {
    public void AddThread(LuauContext context, IntPtr thread, AirshipComponent binding) {
        LuauState.FromContext(context).AddThread(thread, binding);
    }

    public static unsafe void ErrorThread(IntPtr thread, string errorMsg) {
        byte[] str = System.Text.Encoding.UTF8.GetBytes(errorMsg);
        fixed (byte* ptr = str) {
            LuauPlugin.LuauErrorThread(thread, new IntPtr(ptr), str.Length);
        }
    }

    /// <summary>
    /// Returns a file path relative to Assets/. Example output:
    /// <code>airshippackages/@easy/core/server/protectedservices/airship/platforminventory/platforminventoryservice.lua</code>
    ///
    /// Will add ".lua" to the end and lowercase the result. Intention is that two different paths
    /// pointing at the same file will result in the same output.
    /// </summary>
    private static string GetTidyPathNameForLuaFile(string fileNameStr) {
        var init = fileNameStr;
        // Make sure assets is properly capitalized for GetRelativePath call
        if (fileNameStr.StartsWith("assets", StringComparison.OrdinalIgnoreCase)) {
            fileNameStr = fileNameStr.Substring("assets".Length);
        }
        
        // Add .lua to the end
        if (!fileNameStr.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)) {
            fileNameStr += ".lua";
        }

        //Remove the ../ off the front
        while (fileNameStr.StartsWith("..\\") || fileNameStr.StartsWith("../"))
        {
            fileNameStr = fileNameStr.Substring(3);
        }
        // Remove all /'s
        while (fileNameStr.StartsWith("/")) {
            fileNameStr = fileNameStr.Substring(1);
        }

        //Replace backslashes
        fileNameStr = fileNameStr.Replace("\\", "/");
        return fileNameStr.ToLower();
    }

}
