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
            LuauPlugin.ErrorThread(thread, new IntPtr(ptr), str.Length);
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
        // Add .lua to the end
        if (!FastEndsWithIgnoreCase(fileNameStr, ".lua")) {
            fileNameStr += ".lua";
        }

        var fileNameStrSlice = new StringSlice(fileNameStr);

        // Make sure assets is properly capitalized for GetRelativePath call
        if (fileNameStrSlice.StartsWithIgnoreCase("assets")) {
            fileNameStrSlice = fileNameStrSlice.Substring(6);
        }
        
        // Remove the ../ off the front
        while (fileNameStrSlice.StartsWith("..\\") || fileNameStrSlice.StartsWith("../")) {
            fileNameStrSlice = fileNameStrSlice.Substring(3);
        }
        // Remove all /'s
        while (fileNameStrSlice.Length > 0 && fileNameStrSlice[0] == '/') {
            fileNameStrSlice = fileNameStrSlice.Substring(1);
        }
        
        // Make sure assets is properly capitalized for GetRelativePath call
        // if (FastStartsWithIgnoreCase(fileNameStr, "assets")) {
        //     fileNameStr = fileNameStr.Substring(6);
        // }

        // Remove the ../ off the front
        // while (FastStartsWith(fileNameStr, "..\\") || FastStartsWith(fileNameStr, "../")) {
        //     fileNameStr = fileNameStr.Substring(3);
        // }
        // Remove all /'s
        // while (fileNameStr.Length > 0 && fileNameStr[0] == '/') {
        //     // while (FastStartsWith(fileNameStr, "/")) {
        //     fileNameStr = fileNameStr.Substring(1);
        // }

        // Replace backslashes
        fileNameStr = fileNameStrSlice.ToString().ToLowerInvariant();

        if (fileNameStr.Contains('\\')) {
            fileNameStr = fileNameStr.Replace('\\', '/');
        }

        return fileNameStr;
    }

    // Source: https://docs.unity3d.com/2022.3/Documentation/Manual/UnderstandingPerformanceStringsAndText.html
    private static bool FastStartsWith(string a, string b) {
        var aLen = a.Length;
        var bLen = b.Length;

        var ap = 0;
        var bp = 0;

        while (ap < aLen && bp < bLen && a[ap] == b[bp]) {
            ap++;
            bp++;
        }

        return bp == bLen;
    }

    private static bool FastStartsWithIgnoreCase(string a, string b) {
        var aLen = a.Length;
        var bLen = b.Length;

        var ap = 0;
        var bp = 0;

        // while (ap < aLen && bp < bLen && a[ap] == b[bp]) {
        while (ap < aLen && bp < bLen && char.ToUpperInvariant(a[ap]) == char.ToUpperInvariant(b[bp])) {
            ap++;
            bp++;
        }

        return bp == bLen;
    }

    private static bool FastEndsWith(string a, string b) {
        var ap = a.Length - 1;
        var bp = b.Length - 1;

        while (ap >= 0 && bp >= 0 && a[ap] == b[bp]) {
            ap--;
            bp--;
        }

        return bp < 0;
    }

    private static bool FastEndsWithIgnoreCase(string a, string b) {
        var ap = a.Length - 1;
        var bp = b.Length - 1;

        while (ap >= 0 && bp >= 0 && char.ToUpperInvariant(a[ap]) == char.ToUpperInvariant(b[bp])) {
            ap--;
            bp--;
        }

        return bp < 0;
    }
}
