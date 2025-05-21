using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Player.Character.MovementSystems.Character {
    public class CharacterPhysics {
        private const float offsetMargin = .02f;
        private const float gizmoDuration = 4f;

        private CharacterMovement _movement;
        private Vector3 uniformHalfExtents;
        private Vector3 uniformFullExtents;

        public Dictionary<int, Collider> ignoredColliders = new();

        public CharacterPhysics(CharacterMovement movement) {
            _movement = movement;
            uniformHalfExtents =
                new Vector3(movement.characterRadius, movement.characterRadius, movement.characterRadius);
            uniformFullExtents = uniformHalfExtents * 2f;
        }

        public Vector2 RotateV2(Vector2 v, float angle) {
            angle *= Mathf.Deg2Rad;
            return new Vector2(
                v.x * Mathf.Cos(angle) - v.y * Mathf.Sin(angle),
                v.x * Mathf.Sin(angle) + v.y * Mathf.Cos(angle)
            );
        }

        public Vector3 CalculateDrag(Vector3 velocity, float dragConstant) {
            var drag = 1 * dragConstant +
                       velocity.magnitude / _movement.movementSettings.terminalVelocity * dragConstant;
            return -velocity.normalized * drag;
        }

        public Vector3 CalculateFriction(Vector3 velocity, float gravity, float mass, float frictionCoefficient) {
            var flatVelocity = new Vector3(velocity.x, 0, velocity.z);
            var normalForce = mass * gravity;
            var friction = frictionCoefficient * normalForce;
            return -flatVelocity.normalized * friction;
        }

        public bool IsPointVerticallyInCharacter(Vector3 worldPosition, bool avoidStepHeight = false) {
            var localPoint = _movement.transform.InverseTransformPoint(worldPosition);
            var minHeight = avoidStepHeight ? _movement.movementSettings.maxStepUpHeight : 0;
            //var distance = Vector3.Distance(Vector3.zero, localHit);
            //var inCylinder =  distance <= standingCharacterRadius+.01f && localHit.y >= movement.moveData.maxSlopeDelta;
            return localPoint.y >= minHeight && localPoint.y < _movement.currentCharacterHeight;
        }

        public bool IsPointInCharacterRadius(Vector3 worldPosition) {
            return GetFlatDistance(worldPosition, _movement.transform.position) < _movement.characterRadius;
        }

        public float GetFlatDistance(Vector3 A, Vector3 B) {
            A.y = 0;
            B.y = 0;
            return Vector3.Distance(A, B);
        }

        public bool IsWalkableSurface(Vector3 normal) {
            return 1 - Vector3.Dot(normal, _movement.transform.up) < _movement.movementSettings.maxSlopeDelta;
        }

        public static Vector3 CalculateRealNormal(
            Vector3 currentNormal,
            Vector3 origin,
            Vector3 direction,
            float magnitude,
            int layermask) {
            //Ray ray = new Ray(origin, direction);
            RaycastHit hit;
            if (Physics.Raycast(origin, direction, out hit, magnitude + .01f, layermask,
                    QueryTriggerInteraction.Ignore)) {
                //Debug.Log("Did Hit");
                return hit.normal;
            }

            //Debug.LogWarning("we are not suppose to miss that one...");
            return currentNormal;
        }

#region GROUNDED

        public (bool isGrounded, RaycastHit hit, bool detectedGround) CheckIfGrounded(
            Vector3 currentPos,
            Vector3 vel,
            Vector3 moveDir) {
            var intersectionMargin = .075f;
            var castDistance = .2f;
            var castStartPos = currentPos;
            //Move the start position up
            castStartPos.y += castDistance;
            //Extend the ray further if you are falling faster
            castDistance +=
                Mathf.Max(0, -vel.y) +
                offsetMargin; // Mathf.Min(0, movement.transform.InverseTransformVector(vel).y); //Need this part of we change gravity dir

            var gravityDir = -_movement.transform.up;
            var gravityDirOffset = gravityDir.normalized * .1f;

            //Check directly below character as an early out and for comparison information
            if (Physics.Raycast(castStartPos, gravityDir, out var rayHitInfo, castDistance,
                    _movement.movementSettings.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)) {
                if (_movement.drawDebugGizmos_GROUND) {
                    Debug.DrawLine(castStartPos, castStartPos + gravityDir * castDistance, Color.gray,
                        gizmoDuration);
                    GizmoUtils.DrawSphere(rayHitInfo.point, .05f, Color.red, 4, gizmoDuration);
                }

                if (!ignoredColliders.ContainsKey(rayHitInfo.collider.GetInstanceID())) {
                    return (isGrounded: IsWalkableSurface(rayHitInfo.normal), rayHitInfo, true);
                }
            }

            //Extend the casting for the box
            var verticalExtents = .05f;
            var extents = uniformHalfExtents * .98f;
            extents.y = verticalExtents;
            castStartPos.y += verticalExtents;

            if (_movement.drawDebugGizmos_GROUND) {
                GizmoUtils.DrawBox(castStartPos, Quaternion.identity, extents, Color.magenta, gizmoDuration);
                GizmoUtils.DrawBox(castStartPos + gravityDir * castDistance, Quaternion.identity, extents,
                    Color.magenta, gizmoDuration);
            }

            //Check down around the entire character
            if (Physics.BoxCast(castStartPos, extents, gravityDir, out var hitInfo, Quaternion.identity, castDistance,
                    _movement.movementSettings.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)) {
                if (_movement.drawDebugGizmos_GROUND) {
                    GizmoUtils.DrawSphere(hitInfo.point + gravityDirOffset, .05f, Color.red, 4, gizmoDuration);
                }

                if (!_movement.currentMoveSnapshot.isGrounded) {
                    if (_movement.drawDebugGizmos_GROUND) {
                        GizmoUtils.DrawSphere(hitInfo.point, .1f, Color.red, 8, gizmoDuration);
                    }

                    if (_movement.useExtraLogging) {
                        Debug.Log("hitInfo GROUND. UpDot: " + Vector3.Dot(hitInfo.normal, _movement.transform.up) +
                                  " Start: " + castStartPos + " distance: " + castDistance + " hitInfo point: " +
                                  hitInfo.collider.gameObject.name + " at: " + hitInfo.point);
                    }
                }

                if (!ignoredColliders.ContainsKey(hitInfo.collider.GetInstanceID())) {
                    //Physics Casts give you interpolated normals. This uses a ray to find an exact normal
                    hitInfo.normal = CalculateRealNormal(hitInfo.normal,
                        hitInfo.point + gravityDirOffset + moveDir.normalized * .01f, gravityDir, .11f,
                        _movement.movementSettings.groundCollisionLayerMask);

                    if (_movement.drawDebugGizmos_GROUND) {
                        Debug.DrawLine(hitInfo.point, hitInfo.point + hitInfo.normal, Color.red, gizmoDuration);
                    }

                    //var inCollider = IsPointInCharacter...(hitInfo.point);
                    return (isGrounded: IsWalkableSurface(hitInfo.normal), hitInfo, true);
                }
            }

            return (isGrounded: false, default, false);
        }

#endregion

#region FORWARD

        public (bool didHit, RaycastHit hitInfo) CheckForwardHit(
            Vector3 rootPos,
            Vector3 forwardVector,
            bool ignoreStepUp = false,
            bool drawGizmo = false) {
            //Not moving
            if (forwardVector.sqrMagnitude < .1f) {
                return (false, default);
            }

            RaycastHit hitInfo;
            //BOX CASTING
            var normalizedForward = forwardVector.normalized;
            var distance = forwardVector.magnitude - _movement.characterRadius;
            var centerHeight = ignoreStepUp
                ? (_movement.movementSettings.maxStepUpHeight + _movement.currentCharacterHeight) / 2f
                : _movement.currentCharacterHeight / 2f;
            //Move from root to center of collider
            var startPos = rootPos + new Vector3(0, centerHeight, 0);
            var extents = ignoreStepUp
                ? new Vector3(_movement.characterHalfExtents.x,
                    _movement.characterHalfExtents.y - _movement.movementSettings.maxStepUpHeight / 2f,
                    _movement.characterHalfExtents.z)
                : _movement.characterHalfExtents;
            if (drawGizmo && _movement.drawDebugGizmos_FORWARD) {
                GizmoUtils.DrawBox(startPos, Quaternion.identity, extents, Color.blue, gizmoDuration);
                GizmoUtils.DrawBox(startPos + normalizedForward * distance, Quaternion.identity, extents, Color.green,
                    gizmoDuration);
            }

            if (Physics.BoxCast(startPos, extents, forwardVector, out hitInfo, Quaternion.identity, distance,
                    _movement.movementSettings.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)) {
                if (!ignoredColliders.ContainsKey(hitInfo.collider.GetInstanceID())) {
                    //bool sameCollider = currentGround != null && hitInfo.collider.GetInstanceID() == currentGround.GetInstanceID();
                    //var inCollider = IsPointVerticallyInCharacter(hitInfo.point);
                    // var isVerticalWall = 1 - Mathf.Max(0, Vector3.Dot(hitInfo.normal, Vector3.up)) >=
                    //                      _movement.movementSettings.maxSlopeDelta;
                    //localHit.y = 0;
                    hitInfo.normal = CalculateRealNormal(hitInfo.normal, hitInfo.point - forwardVector, forwardVector,
                        forwardVector.magnitude, _movement.movementSettings.groundCollisionLayerMask);

                    if (drawGizmo && _movement.drawDebugGizmos_FORWARD) {
                        GizmoUtils.DrawSphere(hitInfo.point, .05f, Color.black, 4, gizmoDuration);
                        Debug.DrawLine(hitInfo.point, hitInfo.point + hitInfo.normal, Color.black, gizmoDuration);
                    }

                    return (true, hitInfo);
                }
            }

            //Hit nothing
            return (false, hitInfo);
        }

        public RaycastHit[] CheckAllForwardHits(
            Vector3 rootPos,
            Vector3 forwardVector,
            bool ignoreStepUp = false,
            bool drawGizmo = false) {
            //Not moving
            if (forwardVector.sqrMagnitude < .1f) {
                return Array.Empty<RaycastHit>();
            }

            //BOX CASTING
            var normalizedForward = forwardVector.normalized;
            var distance = forwardVector.magnitude - _movement.characterRadius;
            var centerHeight = ignoreStepUp
                ? (_movement.movementSettings.maxStepUpHeight + _movement.currentCharacterHeight) / 2f
                : _movement.currentCharacterHeight / 2f;
            //Move from root to center of collider
            var startPos = rootPos + new Vector3(0, centerHeight, 0);
            var extents = ignoreStepUp
                ? new Vector3(_movement.characterHalfExtents.x,
                    _movement.characterHalfExtents.y - _movement.movementSettings.maxStepUpHeight / 2f,
                    _movement.characterHalfExtents.z)
                : _movement.characterHalfExtents;
            if (drawGizmo && _movement.drawDebugGizmos_FORWARD) {
                GizmoUtils.DrawBox(startPos, Quaternion.identity, extents, Color.blue, gizmoDuration);
                GizmoUtils.DrawBox(startPos + normalizedForward * distance, Quaternion.identity, extents, Color.green,
                    gizmoDuration);
            }

            return Physics.BoxCastAll(startPos, extents, forwardVector, Quaternion.identity, distance,
                _movement.movementSettings.groundCollisionLayerMask, QueryTriggerInteraction.Ignore);
        }

#endregion

#region STEP UP

        public (bool didHit, bool onRamp, Vector3 pointOnRamp, Vector3 newVel) StepUp(
            Vector3 startPos,
            Vector3 vel,
            float deltaTime,
            Vector3 currentUpNormal) {
            //Check if there is an obstruction
            var velDir = vel.normalized;
            var flatDir = new Vector3(velDir.x, 0, velDir.z).normalized;
            var velFrame = vel / deltaTime;
            var stepUpRampDistance = _movement.movementSettings.stepUpRampDistance;
            var (didHitForward, forwardHitInfo) = CheckForwardHit(
                startPos + new Vector3(0, offsetMargin, 0) - velDir * offsetMargin,
                flatDir * (stepUpRampDistance + offsetMargin), false, false);

            if (didHitForward && _movement.useExtraLogging) {
                Debug.Log("currentUpNormal: " + currentUpNormal + " forwardHitInfo: " + forwardHitInfo.normal +
                          " EQUAL: " + (currentUpNormal == forwardHitInfo.normal));
            }

            if (didHitForward && _movement.drawDebugGizmos_STEPUP) {
                GizmoUtils.DrawSphere(forwardHitInfo.point, .025f, Color.cyan, 4, gizmoDuration);
            }

            var heightDiff = Mathf.Abs(forwardHitInfo.point.y - startPos.y);
            var flatDistance = GetFlatDistance(_movement.rootTransform.position, forwardHitInfo.point);
            //See if we can do ramp based step up
            if (didHitForward &&
                //lower than the step up height
                heightDiff <= _movement.movementSettings.maxStepUpHeight &&
                //Thats not the same surface we are standing on
                (heightDiff < offsetMargin || currentUpNormal != forwardHitInfo.normal) &&
                //The hit wall isn't a walkable surface
                !IsWalkableSurface(forwardHitInfo.normal)) {
                //See if there is a surface to step up onto
                var stepUpRayStart = forwardHitInfo.point + velDir * (forwardHitInfo.distance + offsetMargin);
                stepUpRayStart.y = startPos.y + _movement.movementSettings.maxStepUpHeight + _movement.characterRadius;

                if (_movement.drawDebugGizmos_STEPUP) {
                    //Where the character is in this moment
                    GizmoUtils.DrawSphere(startPos, .04f, Color.blue, 4, gizmoDuration);
                }

                //if(Physics.BoxCast(stepUpRayStart, new Vector3(movement.characterRadius,movement.characterRadius,movement.characterRadius), 
                //Vector3.down, out RaycastHit stepUpRayHitInfo, Quaternion.identity, movement.moveData.characterHeight, movement.moveData.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)){
                if (Physics.Raycast(stepUpRayStart, new Vector3(0, -1, 0), out var stepUpRayHitInfo,
                        _movement.movementSettings.characterHeight, _movement.movementSettings.groundCollisionLayerMask,
                        QueryTriggerInteraction.Ignore)) {
                    //Hit a surface that is in range
                    if (_movement.drawDebugGizmos_STEPUP) {
                        //A line to where the step up was hit
                        GizmoUtils.DrawSphere(stepUpRayStart, .05f, Color.yellow, 4, gizmoDuration);
                        Debug.DrawLine(stepUpRayStart, stepUpRayHitInfo.point, Color.yellow, gizmoDuration);
                    }

                    //Make sure the surface is valid
                    if (stepUpRayHitInfo.point.y > startPos.y + Mathf.Min(0, velFrame.y)
                        && IsWalkableSurface(stepUpRayHitInfo.normal)
                        && !Physics.Raycast(stepUpRayHitInfo.point, Vector3.up, _movement.currentCharacterHeight,
                            _movement.movementSettings.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)) {
                        //CAN STEP UP HERE
                        //Find the slope direction that the character needs to walk up to the step
                        var cornerPoint = new Vector3(forwardHitInfo.point.x, stepUpRayHitInfo.point.y + offsetMargin,
                            forwardHitInfo.point.z);
                        var topPoint = cornerPoint - flatDir * (offsetMargin + _movement.characterRadius);
                        topPoint.y += offsetMargin + offsetMargin;

                        var bottompoint = topPoint - flatDir * stepUpRampDistance;
                        bottompoint.y = topPoint.y - _movement.movementSettings.maxStepUpHeight;
                        if (Physics.Raycast(startPos + Vector3.up, new Vector3(0, -1, 0), out var originalFloorHitInfo,
                                1 + _movement.movementSettings.maxStepUpHeight,
                                _movement.movementSettings.groundCollisionLayerMask,
                                QueryTriggerInteraction.Ignore)) {
                            bottompoint.y = originalFloorHitInfo.point.y;
                        }

                        var rampVec = (topPoint - bottompoint).normalized;
                        var perpendicularRampVec = Vector3.Cross(rampVec, Vector3.up);
                        var rampNormal = Vector3.Cross(rampVec, perpendicularRampVec);

                        //raw delta breaks when the distance is now beyond the top point! Need to calculate this differently
                        var rawDelta = GetFlatDistance(startPos + vel * deltaTime, bottompoint) / stepUpRampDistance;
                        var pointOnRampDelta = Mathf.Clamp01(rawDelta);
                        var pointOnRamp =
                            Vector3.Lerp(bottompoint, topPoint, pointOnRampDelta); // + new Vector3(0,offsetMargin,0);
                        if (_movement.drawDebugGizmos_STEPUP) {
                            //Where we consider the corner of the ledge
                            GizmoUtils.DrawBox(cornerPoint, Quaternion.identity, Vector3.one * .02f, Color.red, 4);

                            //Draw the calculated ramp
                            GizmoUtils.DrawSphere(topPoint, .02f, Color.cyan, 4, gizmoDuration);
                            GizmoUtils.DrawSphere(pointOnRamp, .02f, Color.green, 4, gizmoDuration);
                            GizmoUtils.DrawSphere(bottompoint, .02f, Color.black, 4, gizmoDuration);
                            Debug.DrawLine(topPoint, topPoint + rampNormal, Color.green, gizmoDuration);
                            Debug.DrawLine(topPoint, topPoint + rampVec, Color.red, gizmoDuration);
                            Debug.DrawLine(topPoint, topPoint + perpendicularRampVec, Color.blue, gizmoDuration);
                            Debug.DrawLine(bottompoint, bottompoint + rampVec, Color.black, gizmoDuration);
                        }

                        //Manipulate velocity so that it moves up a ramp instead of hitting the step
                        return (true, rawDelta >= 0 && rawDelta <= 1, pointOnRamp,
                            Vector3.ProjectOnPlane(vel, rampNormal));
                    } else if (_movement.useExtraLogging) {
                        Debug.Log("Can't step up here. hitPoint: " + stepUpRayHitInfo.point + " startPos: " + startPos +
                                  " isWalkable: " + IsWalkableSurface(stepUpRayHitInfo.normal));
                    }
                }
            }

            var (didHitExactForward, forwardExactHitInfo) = CheckForwardHit(
                startPos - velDir * offsetMargin, velDir * (stepUpRampDistance + offsetMargin), false, false);

            //See if we should fallback to simplified stepup
            if (_movement.movementSettings.alwaysStepUp ||
                (didHitExactForward && _movement.currentMoveSnapshot.isGrounded &&
                 flatDistance < velFrame.magnitude + _movement.characterRadius
                 && (Equals(currentUpNormal, Vector3.up) || !IsWalkableSurface(forwardExactHitInfo.normal)))) {
                //We hit something but don't qualify for the advanced ramp step up
                Vector3 startPoint;
                if (!didHitExactForward) {
                    startPoint = startPos;
                    startPoint.y += _movement.movementSettings.maxStepUpHeight + _movement.standingCharacterHeight;
                } else {
                    startPoint = new Vector3(forwardExactHitInfo.point.x,
                        startPos.y + _movement.movementSettings.maxStepUpHeight + _movement.standingCharacterHeight,
                        forwardExactHitInfo.point.z);
                }

                startPoint += vel * offsetMargin;

                //Cast a ray down from where the character will be next frame
                if (Physics.Raycast(startPoint, Vector3.down, out var quickStepHitInfo,
                        _movement.movementSettings.maxStepUpHeight + _movement.standingCharacterHeight,
                        _movement.movementSettings.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)) {
                    //make sure there isn't an obstruction above us
                    if (!Physics.Raycast(quickStepHitInfo.point, Vector3.up,
                            _movement.standingCharacterHeight + offsetMargin,
                            _movement.movementSettings.groundCollisionLayerMask,
                            QueryTriggerInteraction.Ignore)
                        && IsWalkableSurface(quickStepHitInfo.normal)
                        && Mathf.Abs(quickStepHitInfo.point.y - _movement.transform.position.y) <
                        _movement.movementSettings.maxStepUpHeight) {
                        var hitPoint = quickStepHitInfo.point + new Vector3(0, offsetMargin + offsetMargin, 0);
                        if (_movement.drawDebugGizmos_STEPUP) {
                            GizmoUtils.DrawSphere(startPoint, .1f, Color.white, 4, gizmoDuration);
                            GizmoUtils.DrawSphere(hitPoint, .05f, Color.white, 4, gizmoDuration);
                        }

                        return (true, false, hitPoint, vel);
                    }
                }
            }

            return (false, false, vel, vel);
        }

#endregion

#region CAN STAND

        public bool CanStand() {
            if (Physics.BoxCast(
                    _movement.rootTransform.position + new Vector3(0, _movement.characterRadius + offsetMargin, 0),
                    new Vector3(_movement.characterRadius, _movement.characterRadius, _movement.characterRadius),
                    Vector3.up, out var hitInfo, Quaternion.identity,
                    _movement.standingCharacterHeight - _movement.characterRadius - _movement.characterRadius -
                    offsetMargin,
                    _movement.movementSettings.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)) {
                if (!ignoredColliders.ContainsKey(hitInfo.collider.GetInstanceID())) {
                    return false;
                }
            }

            return true;
        }

#endregion
    }
}