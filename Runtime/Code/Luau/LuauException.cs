using System;
using System.Runtime.InteropServices;

namespace Luau {
    [StructLayout(LayoutKind.Sequential)]
    public struct PluginException {
        internal IntPtr Data;
        internal int Len;

        public string Message => Marshal.PtrToStringUTF8(Data, Len);
    }
    
    public class LuauException : Exception {
        public LuauException(PluginException pluginException) : base(pluginException.Message) { }

        public LuauException(IntPtr pluginExceptionPtr) : base(
            Marshal.PtrToStructure<PluginException>(pluginExceptionPtr).Message
        ) { }
    }
}
