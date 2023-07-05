using System;
using UnityEngine;

namespace Assets.Code.Alignment
{
	[Serializable]
	public class CustomAlignmentOptions
	{
		[Tooltip("Defines what direction vector should be considered forward.")]
		[SerializeField]
		private KnownVectorType forwardVector = KnownVectorType.LocalForward;

		[Tooltip("Defines what direction vector should be considered up.")]
		[SerializeField]
		private KnownVectorType upVector = KnownVectorType.LocalUp;

		public int ForwardVectorInt => (int)this.forwardVector;
		public int UpVectorInt => (int)this.upVector;
	}
}