using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Player {
    [LuauAPI]
    public class OcclusionCam : MonoBehaviour
    {
        [Header("References")]
        public Camera targetCamera;

        [FormerlySerializedAs("clampYToHeadHeight")] [Header("Variables")]
        public bool adjustToHead = false;

        [FormerlySerializedAs("clampYToHeadHeightThreshold")]
        public float adjustToHeadHeightThreshold = 0.45f;


        private float _fov;
        private Vector2Int _res;
        private float _projectionX;
        private float _projectionY;

        private RaycastHit[] raycast2Hits;

        public void Init(Camera camera)
        {
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

        Vector3 GetCameraBoxExtents(Camera cam) {
            float near = cam.nearClipPlane;
            float halfHeight = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * near;
            float halfWidth = halfHeight * cam.aspect;
            return new Vector3(halfWidth, halfHeight, 0.05f); // Slight depth to make it 3D
        }

        // Called from TS/Lua side
        // Returns the ending distance from the character
        public float BumpForOcclusion(Vector3 targetPosition, Vector3 characterPosition, int mask)
        {
            var t = transform;
            var camPos = t.position;
            var mainDir = camPos - targetPosition;

            // If cam is too far above attach pos snap up
            if (this.adjustToHead)
            {
                float pitch = t.eulerAngles.x;
                if (pitch > 90f)
                {
                    pitch -= 360;
                }
                if (pitch <= -45f)
                {
                    float alpha = Mathf.Clamp01(1 - (pitch + 45f) / -40f);
                    targetPosition = Vector3.Lerp(characterPosition, targetPosition, alpha);
                }
            }

            Vector3 newCamPos;
            float distance;

            // Step 1: Raycast from character to the ideal non-occluded position
            // If nothing hits, then we skip all other checks and immediately update camera position.
            bool step1Hit = Physics.Raycast(characterPosition, mainDir.normalized, out RaycastHit step1HitInfo,
                mainDir.magnitude, mask, QueryTriggerInteraction.Ignore);
            if (!step1Hit)
            {
                newCamPos = targetPosition + mainDir;
                t.position = newCamPos;
                distance = Vector3.Distance(newCamPos, characterPosition);
            }
            else
            {
                newCamPos = step1HitInfo.point - mainDir.normalized * 0.1f;
                t.position = newCamPos;
                distance = Vector3.Distance(newCamPos, characterPosition);
            }

            return distance;
        }
    }
}
