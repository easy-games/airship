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
        // Returns the ending distance from the target
        public void BumpForOcclusion(Vector3 targetPosition, Vector3 characterPosition, int mask) {
            GizmoUtils.DrawSphere(targetPosition, 0.05f, Color.white);
            var t = transform;
            var camPos = t.position;
            var mainDir = camPos - targetPosition;

            // Vector3 boxHalfExtents = this.GetCameraBoxExtents(this.targetCamera);
            // var boxHalfExtents = new Vector3(_projectionX * 0.02f, _projectionY * 0.02f, 0.01f);
            // var boxHalfExtents = new Vector3(0.25f, 0.15f, 0f);

            // If cam is too far above attach pos snap up
            // if (this.adjustToHead && targetPosition.y - camPos.y > this.adjustToHeadHeightThreshold) {
            //     distance /= (targetPosition.y - camPos.y) / this.adjustToHeadHeightThreshold;
            // }

            // Pre: Raycast from character to target position to prevent target being in a wall
            var preDir = targetPosition - characterPosition;
            Debug.DrawLine(characterPosition, characterPosition + preDir, Color.yellow);
            // bool preHit = Physics.BoxCast(characterPosition, boxHalfExtents, preDir.normalized, out RaycastHit preHitInfo, t.rotation, preDir.magnitude, mask,
            //     QueryTriggerInteraction.Ignore);
            bool preHit = Physics.Raycast(characterPosition, preDir.normalized, out RaycastHit preHitInfo, preDir.magnitude, mask,
                QueryTriggerInteraction.Ignore);
            if (preHit) {
                GizmoUtils.DrawSphere(preHitInfo.point, 0.03f, Color.yellow);
                targetPosition = preHitInfo.point - preDir.normalized * 0.1f;
            }

            Vector3 newCamPos;

            // Main: Raycast from target position backwards (away from character).
            Debug.DrawLine(targetPosition, targetPosition + mainDir, Color.blue);
            // bool mainHit = Physics.BoxCast(targetPosition - mainDir.normalized * 0.1f, boxHalfExtents, mainDir.normalized, out RaycastHit mainHitInfo,
            //     t.rotation, mainDir.magnitude + 0.05f, mask, QueryTriggerInteraction.Ignore);
            bool mainHit = Physics.Raycast(targetPosition - mainDir.normalized * 0.1f, mainDir.normalized, out RaycastHit mainHitInfo, mainDir.magnitude + 0.05f, mask, QueryTriggerInteraction.Ignore);
            if (mainHit) {
                GizmoUtils.DrawSphere(mainHitInfo.point, 0.03f, Color.blue);
                newCamPos = mainHitInfo.point - mainDir.normalized * 0.1f;
            } else {
                newCamPos = targetPosition + mainDir;
                GizmoUtils.DrawSphere(targetPosition + mainDir, 0.03f, Color.blue);
            }

            t.position = newCamPos;
        }
    }
}
