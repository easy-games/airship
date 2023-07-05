using UnityEngine;

namespace Assets.Code.Alignment
{
	public interface IRotationInfo
	{
		/// <summary>
		/// Gets world rotation for looking at a specific forward/up vector.
		/// </summary>
		/// <param name="worldForward">World forward vector to look at.</param>
		/// <param name="worldUp">World up vector to look at.</param>
		/// <returns></returns>
		Quaternion GetWorldRotationForLookingAt(Vector3 worldForward, Vector3 worldUp);
	}

	public class RotationInfo : IRotationInfo
	{
		///<inheritdoc/>
		public Quaternion GetWorldRotationForLookingAt(Vector3 worldForward, Vector3 worldUp)
		{
			// Find the world rotation which we want to be facing.
			var desiredWorldRotation = Quaternion.LookRotation(worldForward, worldUp);

			// Applied our rotation adjustment to the desired world rotation
			// to make the proper side of the object face that direction.
			var finalRotation = desiredWorldRotation * this.rotationAdjustment;

			return finalRotation;
		}

		private readonly Quaternion rotationAdjustment;
		public RotationInfo(Quaternion rotationAdjustment)
		{
			this.rotationAdjustment = rotationAdjustment;
		}
	}
}