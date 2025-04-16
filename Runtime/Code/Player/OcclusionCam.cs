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

        private void Update()
        {
            if (!targetCamera)
            {
                return;
            }
            if (Screen.width != _res.x || Screen.height != _res.y || Math.Abs(_fov - targetCamera.fieldOfView) > 0.001f)
            {
                OnScreenSizeChanged();
            }
        }

        private void OnScreenSizeChanged()
        {
            _res.x = Screen.width;
            _res.y = Screen.height;
            _fov = targetCamera.fieldOfView;
            var fov = _fov * Mathf.Deg2Rad;
            var aspectRatio = _res.x / _res.y;
            _projectionY = Mathf.Tan(fov / 2f) * 2f;
            _projectionX = _projectionY * aspectRatio;
        }

        // Called from TS/Lua side
        // Returns the ending distance from the target
        public void BumpForOcclusion(Vector3 targetPosition, Vector3 characterPosition, int mask) {
            GizmoUtils.DrawSphere(targetPosition, 0.05f, Color.white);
            var t = transform;
            var camPos = t.position;
            var distance = Vector3.Distance(camPos, targetPosition);
            // If cam is too far above attach pos snap up
            if (this.adjustToHead && targetPosition.y - camPos.y > this.adjustToHeadHeightThreshold) {
                distance /= (targetPosition.y - camPos.y) / this.adjustToHeadHeightThreshold;
            }
            var diff = camPos - targetPosition;
            var boxHalfExtents = new Vector3(_projectionX * 0.02f, _projectionY * 0.02f, 0f);
            RaycastHit[] hits;
            hits = Physics.RaycastAll(targetPosition - diff.normalized * 0.05f, diff, distance + 0.1f, mask, QueryTriggerInteraction.Ignore);
            Debug.DrawLine(targetPosition - diff.normalized * 0.05f, targetPosition + diff.normalized * distance, Color.blue);
            if (hits.Length == 0) {
                print($"path1 distance: {distance}");
                t.position = targetPosition + diff.normalized * distance;
                return;
            }

            RaycastHit hitClosestToCharacter = hits[0];
            float closestDist = -1f;
            foreach (var hit in hits) {
                var dist = Vector3.Distance(hit.point, characterPosition);
                if (closestDist < 0 || dist < closestDist) {
                    hitClosestToCharacter = hit;
                    closestDist = dist;
                }
            }
            GizmoUtils.DrawSphere(hitClosestToCharacter.point, 0.05f, Color.blue);

            Vector3 newCamPosition = hitClosestToCharacter.point - diff.normalized * 0.05f;
            // Debug.DrawLine(camPos, newCamPosition, Color.blue);

            // Adjust back towards character to prevent going into blocks
            {
                var origin = characterPosition;
                var dir = newCamPosition - origin;
                // Debug.DrawLine(origin, origin + dir, Color.red, 0.01f);
                var hit = Physics.Raycast(origin, dir.normalized, out var raycastHit, dir.magnitude, mask, QueryTriggerInteraction.Ignore);
                Debug.DrawLine(origin, origin + dir, Color.white);
                Debug.Log($"hit: {hit}, dir: {dir}, firstCollider: {hitClosestToCharacter.collider.GetInstanceID()} ({hitClosestToCharacter.collider.gameObject.name})", hitClosestToCharacter.collider.gameObject);
                if (hit) {
                    GizmoUtils.DrawSphere(raycastHit.point, 0.05f, Color.yellow);
                    var n = raycastHit.point - dir.normalized * 0.05f;
                    Debug.DrawLine(origin, n, Color.yellow);
                    newCamPosition = n;
                    // return;
                } else {
                    var n = newCamPosition - dir.normalized * 0.05f;
                    Debug.DrawLine(origin, n, Color.magenta);
                    newCamPosition = n;
                }
            }


            // print($"path2 dir: {diff.normalized}, distance: {hitInfo.distance}");
            t.position = newCamPosition;
        }
    }
}
