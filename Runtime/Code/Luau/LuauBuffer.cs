namespace Luau {
	/// <summary>
	/// Represents a Luau buffer value. Any C# method that takes LuauBuffer as a parameter
	/// will automatically accept "buffer" types from Luau. Similarly, any C# method that
	/// returns a LuauBuffer will return a buffer type to Luau.
	/// </summary>
	public struct LuauBuffer {
		public readonly byte[] Data;

		public LuauBuffer(byte[] data) {
			Data = data;
		}
		
		public static implicit operator byte[](LuauBuffer buffer) => buffer.Data;
		public static implicit operator LuauBuffer(byte[] data) => new (data);
	}
}
