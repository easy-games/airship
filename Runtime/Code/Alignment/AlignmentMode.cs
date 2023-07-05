namespace Assets.Code.Alignment
{
	/// <summary>
	/// Defines how the visual clone should be intially rotated when presenting to the user.
	/// </summary>
	public enum AlignmentMode
	{
		/// <summary>
		/// Don't align the visual clone.
		/// </summary>
		None = 0,

		/// <summary>
		/// Allows you to define the world forward and up vectors from well known vectors.
		/// </summary>
		CustomKnownVectors = 1,

		/// <summary>
		/// Use local forward and up for the world forward and up vectors.
		/// </summary>
		DefaultLocal = 2,

		/// <summary>
		/// Use local back and up for the world forward and up vectors.
		/// </summary>
		ReversedLocal = 3,

		/// <summary>
		/// Use player area's forward and up for the world forward and up vectors.
		/// </summary>
		DefaultPlayerArea = 4,

		/// <summary>
		/// Use player area's back and up for the world forward and up vectors.
		/// </summary>
		ReversedPlayerArea = 5,

		/// <summary>
		/// Use common area's forward and up for the world forward and up vectors.
		/// </summary>
		DefaultCommonArea = 6,

		/// <summary>
		/// Use common area's back and up for the world forward and up vectors.
		/// </summary>
		ReversedCommonArea = 7,

		/// <summary>
		/// Use camera's forward and up for the world forward and up vectors.
		/// </summary>
		DefaultCamera = 8,

		/// <summary>
		/// Use camera's back and up for the world forward and up vectors.
		/// </summary>
		ReversedCamera = 9,
	}
}