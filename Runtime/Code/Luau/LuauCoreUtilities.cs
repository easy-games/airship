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
}
