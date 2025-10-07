#if UNITY_EDITOR
using System;

namespace NativePlugins {
	/// <summary>
	/// Binds a delegate to a native library plugin.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class NativeDelegateAttribute : Attribute {
		public readonly string SymbolName;
		public NativeDelegateAttribute() {
			SymbolName = null;
		}
		/// <param name="symbolName">Specified symbol name for lookup.</param>
		public NativeDelegateAttribute(string symbolName) {
			SymbolName = symbolName;
		}
	}
}

#endif
