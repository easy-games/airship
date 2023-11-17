using System;
using UnityEngine;

namespace Code.Player.Accessories.Editor {
	/// <summary>
	/// Handle camera movement and positioning for the Accessory Editor window.
	/// </summary>
	[Obsolete]
	internal class AccessoryEditorCamera {
		private readonly float _initialCamDistance;
		private readonly float _initialCamTilt;
		private readonly float _initialCamRot;
		
		private float _camDistance;
		private float _camTilt;
		private float _camRot;
		
		private Vector3 _camCenter;
		private Vector3 _camMove;

		internal Vector3 CameraCenter {
			set => _camCenter = value;
		}

		internal float CameraDistance => _camDistance;

		internal AccessoryEditorCamera(float initialDistance, float initialTilt, float initialRot) {
			_initialCamDistance = initialDistance;
			_initialCamTilt = initialTilt;
			_initialCamRot = initialRot;
			ResetPosition();
		}

		/// <summary>
		/// Reset position.
		/// </summary>
		internal void ResetPosition() {
			_camDistance = _initialCamDistance;
			_camTilt = _initialCamTilt;
			_camRot = _initialCamRot;
			_camMove = Vector3.zero;
		}
		
		/// <summary>
		/// Calculate and set the actual position for the given <c>camera</c>. The camera will
		/// also be rendered by calling <c>camera.Render()</c>.
		/// </summary>
		internal void SetCameraPosition(Camera camera) {
			var x = _camDistance * Mathf.Sin(_camTilt * Mathf.Deg2Rad) * Mathf.Cos(_camRot * Mathf.Deg2Rad);
			var z = _camDistance * Mathf.Sin(_camTilt * Mathf.Deg2Rad) * Mathf.Sin(_camRot * Mathf.Deg2Rad);
			var y = _camDistance * Mathf.Cos(_camTilt * Mathf.Deg2Rad);
			var camPosition = new Vector3(x, y, z);

			var position = _camCenter + _camMove;
            
			camera.transform.position = position + camPosition;
			camera.transform.LookAt(position);

			camera.Render();
		}

		/// <summary>
		/// Increment the rotation and tilt of the camera. Bound checks will be done automatically.
		/// </summary>
		internal void Increment(float deltaTilt, float deltaRot) {
			_camTilt = Mathf.Clamp(_camTilt - deltaTilt, 1f, 179f);
			_camRot = (_camRot - deltaRot) % 360f;
		}

		/// <summary>
		/// Pan the camera.>
		/// </summary>
		internal void Pan(Camera camera, Vector2 pan) {
			var t = camera.transform;
			_camMove += t.right * -pan.x + t.up * pan.y;
		}

		/// <summary>
		/// Zoom the camera. Bound checks are automatically calculated.
		/// </summary>
		internal void Zoom(float deltaZoom) {
			_camDistance = Mathf.Clamp(_camDistance + deltaZoom, 1f, 15f);
		}
	}
}
