using UnityEngine;

namespace Assets.Code.Alignment
{
	[LuauAPI]
	public interface IAlignmentManager
	{
		/// <summary>
		/// Translates the known vector info into a rotation info.
		/// </summary>
		/// <param name="sourceTransform">Transform we are calculating the rotation info for.</param>
		/// <param name="forward">Known vector type to represent what should be considered forward-facing.</param>
		/// <param name="up">Known vector type to represent what should be considered upward-facing.</param>
		/// <returns></returns>
		IRotationInfo GetRotationInfo(Transform sourceTransform, KnownVectorType forward, KnownVectorType up);

		/// <summary>
		/// Gets a direction vector based on the source transform and known vector type.
		/// </summary>
		/// <param name="sourceTransform">Source transform to get information off of (ie. local, world or camera vectors)</param>
		/// <param name="knownVectorType">Known vector type to use to calculate world vector.</param>
		/// <returns></returns>
		Vector3 GetWorldVectorFromVectorType(Transform sourceTransform, KnownVectorType knownVectorType);

		Quaternion InverseQuat(Quaternion rotation);
	}

	[LuauAPI]
	public class AlignmentManager : MonoBehaviour, IAlignmentManager
	{
		public static IAlignmentManager Instance;

		[SerializeField]
		private Transform cameraTransform;

		private void Awake()
		{
			Instance = this;
		}

		///<inheritdoc/>
		public IRotationInfo GetRotationInfo(Transform sourceTransform, KnownVectorType forward, KnownVectorType up)
		{
			var worldForward = this.GetWorldVectorFromVectorType(sourceTransform, forward);
			var worldUp = this.GetWorldVectorFromVectorType(sourceTransform, up);

			var knownRotation = Quaternion.LookRotation(worldForward, worldUp);

			// Find desired rotation relative to source transform.
			var rotationNeededFromKnownVectors = Quaternion.Inverse(knownRotation) * sourceTransform.rotation;

			return new RotationInfo(rotationNeededFromKnownVectors);
		}

		public Quaternion InverseQuat(Quaternion rotation)
		{
			return Quaternion.Inverse(rotation);
		}

		///<inheritdoc/>
		public Vector3 GetWorldVectorFromVectorType(Transform sourceTransform, KnownVectorType knownVectorType)
		{
			Vector3 worldVector;
			switch (knownVectorType)
			{
				case KnownVectorType.LocalForward:
					worldVector = sourceTransform.forward;
					break;
				case KnownVectorType.LocalBack:
					worldVector = -sourceTransform.forward;
					break;
				case KnownVectorType.LocalRight:
					worldVector = sourceTransform.right;
					break;
				case KnownVectorType.LocalLeft:
					worldVector = -sourceTransform.right;
					break;
				case KnownVectorType.LocalUp:
					worldVector = sourceTransform.up;
					break;
				case KnownVectorType.LocalDown:
					worldVector = -sourceTransform.up;
					break;
				case KnownVectorType.WorldForward:
					worldVector = Vector3.forward;
					break;
				case KnownVectorType.WorldBack:
					worldVector = Vector3.back;
					break;
				case KnownVectorType.WorldRight:
					worldVector = Vector3.right;
					break;
				case KnownVectorType.WorldLeft:
					worldVector = Vector3.left;
					break;
				case KnownVectorType.WorldUp:
					worldVector = Vector3.up;
					break;
				case KnownVectorType.WorldDown:
					worldVector = Vector3.down;
					break;
				case KnownVectorType.CameraForward:
					worldVector = this.cameraTransform.forward;
					break;
				case KnownVectorType.CameraBack:
					worldVector = -this.cameraTransform.forward;
					break;
				case KnownVectorType.CameraRight:
					worldVector = this.cameraTransform.right;
					break;
				case KnownVectorType.CameraLeft:
					worldVector = -this.cameraTransform.right;
					break;
				case KnownVectorType.CameraUp:
					worldVector = this.cameraTransform.up;
					break;
				case KnownVectorType.CameraDown:
					worldVector = -this.cameraTransform.up;
					break;
				default:
					Debug.LogError($"Unsupported {nameof(KnownVectorType)} encountered: " + knownVectorType);
					worldVector = Vector3.forward;
					break;
			}

			return worldVector;
		}
	}
}