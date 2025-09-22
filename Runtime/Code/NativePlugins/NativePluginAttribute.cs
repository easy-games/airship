using System;

namespace NativePlugins {
#if UNITY_EDITOR
	[AttributeUsage(AttributeTargets.Field)]
	public class NativePluginAttribute : Attribute {
		public readonly string LibPath;
		
		/// <param name="libPath">Library path, relative to the Unity project directory.</param>
		public NativePluginAttribute(string libPath) {
			LibPath = libPath;
		}
	}
#endif
}
