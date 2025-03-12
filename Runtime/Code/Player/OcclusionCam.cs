using System;
using System.Windows.Forms;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using Screen = UnityEngine.Screen;

namespace Code.Player {
    [LuauAPI]
    public class OcclusionCam : MonoBehaviour {
        [Header("References")]
        public Camera targetCamera;

        [Header("Obstructions")]
        [Tooltip(("Allow the camera to remain behind obstructions to the character"))]
        public bool allowSmallObstructions = true;
        [Tooltip("How far must an obstruction be from the camera without making the camera jump closer to the character")]
        public float allowedObstructionForwardDistance = .5f;
        [Tooltip("How far behind the camera can we bump to get out of an obstruction?")]
        public float allowedObstructionBackwardDistance = 2;
        
        [Header(("Positioning"))]
        [Tooltip("As the camera gets closer to the player adjust to a first person perspective")]
        public bool adjustToFirstPerson = false;
        [Tooltip("How far can the camera move under the target before transitioning")]
        public float firstPersonHeightThreshold = 0.45f;
        [Tooltip("How far does transitioning last")]
        public float firstPersonHeightTransition = 1.5f;


        private float _fov;
        private Vector2Int _res;
        private float _projectionX;
        private float _projectionY;

        public void Init(Camera camera) {
            targetCamera = camera;
            OnScreenSizeChanged();
        }

        private void Update() {
            if (!targetCamera)
            {
                return;
            }
            if (Screen.width != _res.x || Screen.height != _res.y || Math.Abs(_fov - targetCamera.fieldOfView) > 0.001f)
            {
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

        /// <summary>
        /// Called from TS/Lua side to bump the camera based on obstructions
        /// </summary>
        /// <param name="truePos"> The true center of the target (Head of character)</param>
        /// <param name="attachToPos"> The attached position of the viewpoint, can be offset from truePos</param>
        /// <param name="mask">A layer mask that defines what counts as an obstruction</param>
        /// <returns>the resulting distance from the target</returns>
        public float BumpForOcclusion(Vector3 truePos, Vector3 attachToPos, int mask) {
            const float marginOffset = .02f;
            var thisTransform = transform;
            var camPos = thisTransform.position;
            //Camera point

            var originalDistance = Vector3.Distance(camPos, attachToPos);
            var modifiedDistance = originalDistance;

            var diffDir = (camPos - attachToPos).normalized;
            var boxHalfExtents = new Vector3(_projectionX * 0.05f, _projectionY * 0.05f, 0f);

            // If cam is too far above attach pos snap up
            if (adjustToFirstPerson && attachToPos.y - camPos.y > firstPersonHeightThreshold) {
                var delta = (attachToPos.y - camPos.y - firstPersonHeightThreshold) / firstPersonHeightTransition;
                //print("camera is below attached pos. Y: " + attachToPos.y + ", " + camPos.y + " Delta: " + delta);
                delta = 1 - Mathf.Clamp01(delta);
                modifiedDistance *= delta;
                attachToPos = Vector3.Lerp(truePos, attachToPos, delta);
                camPos = attachToPos + diffDir * modifiedDistance;
                diffDir = (camPos - attachToPos).normalized;
            }
            
            //Check if target point is inside an obstacle
            if (truePos != attachToPos) {
                if (Physics.OverlapSphere(attachToPos, .1f, mask, QueryTriggerInteraction.Ignore).Length > 0) {
                    //Move out from of obstruction
                    //Find the closest point to our desired attachment position
                    var dir = (attachToPos - truePos).normalized;
                    if (Physics.Raycast(truePos, dir, out RaycastHit hitInfo, Vector3.Distance(attachToPos, truePos) + 1, mask,
                            QueryTriggerInteraction.Ignore)) {
                        attachToPos = hitInfo.point - dir * marginOffset;
                        GizmoUtils.DrawSphere(attachToPos, .04f, Color.red);
                    } else {
                        attachToPos = truePos;
                    }

                    //Adjust attachToPos to match new attachment
                    camPos = attachToPos + diffDir * modifiedDistance;
                }
            }
            
            //Cast from target attached position to camera position 
            if (Physics.BoxCast(attachToPos + diffDir * marginOffset,
                    boxHalfExtents, diffDir, out var attachedHitInfo, thisTransform.rotation, modifiedDistance + marginOffset,
                    mask, QueryTriggerInteraction.Ignore))
            {
                //There is an obstruction
                var usingCameraPosition = false;
                if (allowSmallObstructions) {
                    var canCheckCameraPosition = true;
                    //See if the camera is directly inside of a collider
                    if (Physics.OverlapSphere(camPos, .1f, mask, QueryTriggerInteraction.Ignore).Length > 0) {
                        //Move out from of obstruction
                        camPos -= this.allowedObstructionBackwardDistance * diffDir;
                        modifiedDistance += this.allowedObstructionBackwardDistance;
                        
                        //Are we still in something?
                        if (Physics.OverlapSphere(camPos, .1f, mask, QueryTriggerInteraction.Ignore).Length > 0) {
                            //Camera is not in a valid position
                            canCheckCameraPosition = false;
                            modifiedDistance = originalDistance;
                        }
                    }
                    
                    
                    //Cast from camera to target attached pos
                    if (canCheckCameraPosition && Physics.BoxCast(camPos,
                            boxHalfExtents, diffDir * -1, out var cameraHitInfo, thisTransform.rotation, modifiedDistance,
                            mask, QueryTriggerInteraction.Ignore))
                    {
                        //Determine if we should use the adjusted position or look from the outside of it
                        //Use distance to the hit to determine which side of the obstruction we should be at 
                        if (cameraHitInfo.distance > attachedHitInfo.distance &&
                            cameraHitInfo.distance > allowedObstructionForwardDistance)
                        {
                            //The obstruction is closer to the player and camera position is in a valid
                            usingCameraPosition = true;
                        }
                    }

                } 
                //print("cameraHitDistance: " + cameraHitInfo.distance + " attachedHitDistance: " + attachedHitInfo.distance);

                if (!usingCameraPosition) {
                    //Don't use camera position, use found position from character
                    modifiedDistance = attachedHitInfo.distance;
                    camPos = attachedHitInfo.point;
                }
            }
            
            //Draw gizmos
            //Target Point
            GizmoUtils.DrawSphere(attachToPos, .05f, Color.blue);
            GizmoUtils.DrawLine(attachToPos,  diffDir * Mathf.Max(marginOffset, modifiedDistance-marginOffset), Color.blue);
            GizmoUtils.DrawSphere(camPos, .06f, Color.cyan);
            
            //Move in from of obstruction
            thisTransform.position = attachToPos + diffDir * Mathf.Max(marginOffset, modifiedDistance-marginOffset);
            return modifiedDistance;
        }
    }
}
