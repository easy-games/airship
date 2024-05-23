using System;
using UnityEngine;
using UnityEngine.Serialization;

[LuauAPI]
public class OcclusionCam : MonoBehaviour {
	public Camera targetCamera;
	private float _fov;
	private Vector2Int _res;
	private float _projectionX;
	private float _projectionY;

	public void Init(Camera camera) {
		targetCamera = camera;
		OnScreenSizeChanged();
	}

	private void Update() {
		if (!targetCamera) {
			return;
		}
		if (Screen.width != _res.x || Screen.height != _res.y || Math.Abs(_fov - targetCamera.fieldOfView) > 0.001f) {
			OnScreenSizeChanged();
		}
	}

	private void OnScreenSizeChanged() {
		_res.x = Screen.width;
		_res.y = Screen.height;
		_fov = targetCamera.fieldOfView;
		var fov = _fov * Mathf.Deg2Rad;
		var aspectRatio = _res.x / _res.y;
		_projectionY = Mathf.Tan(fov / 2f) * 2f;
		_projectionX = _projectionY * aspectRatio;
	}

	// Called from TS/Lua side
	public void BumpForOcclusion(Vector3 attachToPos, int mask) {
		var t = transform;
		var camPos = t.position;
		var nearClip = targetCamera.nearClipPlane;

		var distance = Vector3.Distance(camPos, attachToPos);
		var newDistance = distance;
		var adjusted = false;

		// Scan corners of viewport:
		for (var x = 0; x <= 1; x++) {
			var wx = t.right * ((x - 0.5f) * _projectionX);
			for (var y = 0; y <= 1; y++) {
				var wy = t.up * ((y - 0.5f) * _projectionY);

				var origin = attachToPos + (wx + wy) * nearClip;
				var viewportPos = targetCamera.ViewportPointToRay(new Vector3(x, y, 0)).origin;
				var diff = viewportPos - origin;

				// Raycast outward from origin toward the camera:
				if (!Physics.Raycast(origin, diff.normalized, out var hit, diff.magnitude, mask, QueryTriggerInteraction.Ignore)) continue;
				adjusted = true;

				// Set new distance if closer than previous:
				var distFromOrigin = Vector3.Distance(hit.point, origin) - nearClip;
				if (distFromOrigin < newDistance) {
					newDistance = distFromOrigin;
				}
			}
		}
		if (!adjusted) return;

		// Bump camera forward to be in front of the occluding object:
		t.position += t.forward * (distance - newDistance);
	}
}
