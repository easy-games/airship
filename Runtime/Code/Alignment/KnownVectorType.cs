namespace Assets.Code.Alignment
{
	/// <summary>
	/// Represents a specific direction to use as a world-space vector.
	/// </summary>
	public enum KnownVectorType
	{
		/// <summary>
		/// Self forward vector.
		/// </summary>
		LocalForward = 0,

		/// <summary>
		/// Self back vector.
		/// </summary>
		LocalBack = 1,

		/// <summary>
		/// Self right vector.
		/// </summary>
		LocalRight = 2,

		/// <summary>
		/// Self left vector.
		/// </summary>
		LocalLeft = 3,

		/// <summary>
		/// Self up vector.
		/// </summary>
		LocalUp = 4,

		/// <summary>
		/// Self down vector.
		/// </summary>
		LocalDown = 5,

		/// <summary>
		/// World forward vector.
		/// </summary>
		WorldForward = 6,

		/// <summary>
		/// World back vector.
		/// </summary>
		WorldBack = 7,

		/// <summary>
		/// World right vector.
		/// </summary>
		WorldRight = 8,

		/// <summary>
		/// World left vector.
		/// </summary>
		WorldLeft = 9,

		/// <summary>
		/// World up vector.
		/// </summary>
		WorldUp = 10,

		/// <summary>
		/// World down vector.
		/// </summary>
		WorldDown = 11,

		/// <summary>
		/// Main camera forward vector.
		/// </summary>
		CameraForward = 12,

		/// <summary>
		/// Main camera back vector.
		/// </summary>
		CameraBack = 13,

		/// <summary>
		/// Main camera right vector.
		/// </summary>
		CameraRight = 14,

		/// <summary>
		/// Main camera left vector.
		/// </summary>
		CameraLeft = 15,

		/// <summary>
		/// Main camera up vector.
		/// </summary>
		CameraUp = 16,

		/// <summary>
		/// Main camera down vector.
		/// </summary>
		CameraDown = 17,
	}
}