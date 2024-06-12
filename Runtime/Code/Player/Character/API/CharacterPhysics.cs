using UnityEngine;

namespace Code.Player.Character.API {
	public class CharacterPhysics {
		private const float offsetMargin = .02f;
		private const float gizmoDuration = 1f;
		private const bool renderGroundGizmos = false;

		private CharacterMovement movement;
		private Vector3 uniformHalfExtents;
		private Vector3 uniformFullExtents;

		public CharacterPhysics(CharacterMovement movement){
			this.movement = movement;
			uniformHalfExtents = new Vector3(movement.characterRadius,movement.characterRadius,movement.characterRadius);
			uniformFullExtents = uniformHalfExtents*2f;
		}

		public Vector2 RotateV2(Vector2 v, float angle) {
			angle *= Mathf.Deg2Rad;
			return new Vector2(
				v.x * Mathf.Cos(angle) - v.y * Mathf.Sin(angle),
				v.x * Mathf.Sin(angle) + v.y * Mathf.Cos(angle)
			);
		}

		public Vector3 CalculateDrag(Vector3 velocity) {
			var drag = Vector3.Dot(velocity, velocity) * movement.characterRadius * movement.moveData.drag;
			return -velocity.normalized * drag;
		}

		public Vector3 CalculateFriction(Vector3 velocity, float gravity, float mass, float frictionCoefficient) {
			var flatVelocity = new Vector3(velocity.x, 0, velocity.z);
			var normalForce = mass *gravity;
			var friction = frictionCoefficient * normalForce;
			return -flatVelocity.normalized * friction;
		}

		public bool IsPointVerticallyInCharacter(Vector3 worldPosition, bool avoidStepHeight = false){
			Vector3 localPoint = movement.transform.InverseTransformPoint(worldPosition);
			float minHeight = avoidStepHeight ? movement.moveData.maxStepUpHeight : 0;
				//var distance = Vector3.Distance(Vector3.zero, localHit);
				//var inCylinder =  distance <= standingCharacterRadius+.01f && localHit.y >= movement.moveData.maxSlopeDelta;
			return localPoint.y >= minHeight && localPoint.y < movement.currentCharacterHeight;
		}

		public bool IsPointInCharacterRadius(Vector3 worldPosition){
			return GetFlatDistance(worldPosition, movement.transform.position) < movement.characterRadius;
		}

		public float GetFlatDistance(Vector3 A, Vector3 B){
			A.y = 0;
			B.y = 0;
			return Vector3.Distance(A, B);
		}

		private bool VoxelIsSolid(ushort voxel) {
			return movement.voxelWorld.GetCollisionType(voxel) != VoxelBlocks.CollisionType.None;
		}

		public bool IsWalkableSurface(Vector3 normal){
			return (1-Vector3.Dot(normal, movement.transform.up)) < movement.moveData.maxSlopeDelta;
		}

		public static Vector3 CalculateRealNormal(Vector3 currentNormal, Vector3 origin, Vector3 direction, float magnitude, int layermask) {
			//Ray ray = new Ray(origin, direction);
			RaycastHit hit;
			if (Physics.Raycast(origin, direction, out hit, magnitude+.01f, layermask, QueryTriggerInteraction.Ignore)) {
				//Debug.Log("Did Hit");
				return hit.normal;
			}
			//Debug.LogWarning("we are not suppose to miss that one...");
			return currentNormal;
		}
		
	#region RAYCASTS
		public (bool isGrounded, ushort blockId, Vector3Int blockPos, RaycastHit hit, bool detectedGround) CheckIfGrounded(Vector3 currentPos, Vector3 vel, Vector3 moveDir) {
			const float tolerance = 0.03f;
			var offset = new Vector3(-0.5f, -0.5f - tolerance, -0.5f);
			var groundCheckRadius = movement.characterRadius;

			// Check four corners to see if there's a block beneath player:
			if (movement.voxelWorld) {
				var pos00 = Vector3Int.RoundToInt(currentPos + offset + new Vector3(-groundCheckRadius, 0, -groundCheckRadius));
				ushort voxel00 = movement.voxelWorld.ReadVoxelAt(pos00);
				if (
					VoxelIsSolid(voxel00) &&
					!VoxelIsSolid(movement.voxelWorld.ReadVoxelAt(pos00 + new Vector3Int(0, 1, 0)))
				)
				{
					return (isGrounded: true, blockId: VoxelWorld.VoxelDataToBlockId(voxel00), blockPos: pos00, default, true);
				}

				var pos10 = Vector3Int.RoundToInt(currentPos + offset + new Vector3(groundCheckRadius, 0, -groundCheckRadius));
				ushort voxel10 = movement.voxelWorld.ReadVoxelAt(pos10);
				if (
					VoxelIsSolid(voxel10) &&
					!VoxelIsSolid(movement.voxelWorld.ReadVoxelAt(pos10 + new Vector3Int(0, 1, 0)))
				)
				{
					return (isGrounded: true, blockId: VoxelWorld.VoxelDataToBlockId(voxel10), pos10, default, true);
				}

				var pos01 = Vector3Int.RoundToInt(currentPos + offset + new Vector3(-groundCheckRadius, 0, groundCheckRadius));
				ushort voxel01 = movement.voxelWorld.ReadVoxelAt(pos01);
				if (
					VoxelIsSolid(voxel01) &&
					!VoxelIsSolid(movement.voxelWorld.ReadVoxelAt(pos01 + new Vector3Int(0, 1, 0)))
				)
				{
					return (isGrounded: true, blockId: VoxelWorld.VoxelDataToBlockId(voxel01), pos01, default, true);
				}

				var pos11 = Vector3Int.RoundToInt(currentPos + offset + new Vector3(groundCheckRadius, 0, groundCheckRadius));
				ushort voxel11 = movement.voxelWorld.ReadVoxelAt(pos11);
				if (
					VoxelIsSolid(voxel11) &&
					!VoxelIsSolid(movement.voxelWorld.ReadVoxelAt(pos11 + new Vector3Int(0, 1, 0)))
				)
				{
					return (isGrounded: true, blockId: VoxelWorld.VoxelDataToBlockId(voxel11), pos11, default, true);
				}
			}


			// Fallthrough - do raycast to check for PrefabBlock object below:
			var distance = movement.moveData.maxStepUpHeight+offsetMargin;
			var castStartPos = currentPos;
			//Move the start position up
			castStartPos.y += distance+movement.characterRadius;
			//Extend the ray further if you are falling faster
			distance -= Mathf.Min(0, vel.y);// Mathf.Min(0, movement.transform.InverseTransformVector(vel).y); //Need this part of we change gravity dir
			
			var gravityDir = -movement.transform.up;
			var gravityDirOffset = gravityDir.normalized * .1f;

			//Check directly below character as an early out and for comparison information
			if(Physics.Raycast(castStartPos, gravityDir, out var rayHitInfo, distance + movement.characterRadius+.01f, movement.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)){
				if(movement.drawDebugGizmos && renderGroundGizmos){
					GizmoUtils.DrawLine(castStartPos, castStartPos+gravityDir*(distance + .01f), Color.gray, gizmoDuration);
					GizmoUtils.DrawSphere(rayHitInfo.point, .05f, Color.red, 4, gizmoDuration);
				}

				return (isGrounded: IsWalkableSurface(rayHitInfo.normal), blockId: 0, Vector3Int.zero, rayHitInfo, true);
			}
			
			//Slightly less so you don't hit walls and think they are ground
			var extents = uniformHalfExtents *.98f;
			if(movement.drawDebugGizmos && renderGroundGizmos){
				GizmoUtils.DrawBox(castStartPos, Quaternion.identity, extents, Color.magenta, gizmoDuration);
				GizmoUtils.DrawBox(castStartPos+gravityDir*distance, Quaternion.identity, extents, Color.magenta, gizmoDuration);
			}

			//Check down around the entire character
			if (Physics.BoxCast(castStartPos, extents, gravityDir, out var hitInfo, Quaternion.identity, distance+offsetMargin, movement.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)) {
			//if (Physics.BoxCast(castStartPos, new Vector3(groundCheckRadius, groundCheckRadius, groundCheckRadius), gravityDir, out var hitInfo, Quaternion.identity, distance, movement.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)) {	
				if(movement.drawDebugGizmos && renderGroundGizmos){
					GizmoUtils.DrawSphere(hitInfo.point + gravityDirOffset, .05f, Color.red, 4, gizmoDuration);
				}
				if(!movement.grounded){
					if(movement.drawDebugGizmos && renderGroundGizmos){
						GizmoUtils.DrawSphere(hitInfo.point, .1f, Color.red, 8, gizmoDuration);
						
					}
					if(movement.useExtraLogging){
        				Debug.Log("hitInfo GROUND. UpDot: " +  Vector3.Dot(hitInfo.normal, movement.transform.up) + " Start: " + castStartPos + " distance: " + distance + " hitInfo point: " + hitInfo.collider.gameObject.name + " at: " + hitInfo.point);
					}
				}

				//Physics Casts give you interpolated normals. This uses a ray to find an exact normal
				hitInfo.normal = CalculateRealNormal(hitInfo.normal, hitInfo.point + gravityDirOffset + moveDir.normalized*.01f, gravityDir, .11f, movement.groundCollisionLayerMask);
			
				if(movement.drawDebugGizmos && renderGroundGizmos){
					GizmoUtils.DrawLine(hitInfo.point, hitInfo.point + hitInfo.normal, Color.red, gizmoDuration);
				}

				//var inCollider = IsPointInCharacter...(hitInfo.point);
				return (isGrounded: IsWalkableSurface(hitInfo.normal), blockId: 0, Vector3Int.zero, hitInfo, true);
			}

			return (isGrounded: false, blockId: 0, Vector3Int.zero, default, false);
		}

		public (bool didHit, RaycastHit hitInfo) CheckForwardHit(Vector3 rootPos, Vector3 forwardVector, bool ignoreStepUp = false){
			//Not moving
			if(forwardVector.sqrMagnitude < .1f){
				return (false, default);
			}

			RaycastHit hitInfo;
			//BOX CASTING
			Vector3 normalizedForward = forwardVector.normalized;
			float distance = forwardVector.magnitude-movement.characterRadius;
			float centerHeight = ignoreStepUp ? (movement.moveData.maxStepUpHeight + movement.currentCharacterHeight)/2f : movement.currentCharacterHeight/2f; 
			//Move from root to center of collider
			var startPos = rootPos + new Vector3(0,centerHeight,0);
			var extents = ignoreStepUp ? new Vector3(movement.characterHalfExtents.x, movement.characterHalfExtents.y - movement.moveData.maxStepUpHeight/2f, movement.characterHalfExtents.z) : movement.characterHalfExtents; 
			if(movement.drawDebugGizmos){
				// GizmoUtils.DrawBox(startPos, Quaternion.identity, extents, Color.green, gizmoDuration);
				// GizmoUtils.DrawBox(startPos+normalizedForward * distance, Quaternion.identity, extents, Color.green, gizmoDuration);
			}
			if(Physics.BoxCast(startPos, extents, forwardVector, out hitInfo, Quaternion.identity, distance, movement.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)){
				//bool sameCollider = currentGround != null && hitInfo.collider.GetInstanceID() == currentGround.GetInstanceID();
				//var inCollider = IsPointVerticallyInCharacter(hitInfo.point);
                var isVerticalWall = 1-Mathf.Max(0, Vector3.Dot(hitInfo.normal, Vector3.up)) >= movement.moveData.maxSlopeDelta;
				//localHit.y = 0;
				hitInfo.normal = CalculateRealNormal(hitInfo.normal, hitInfo.point-forwardVector, forwardVector, forwardVector.magnitude, movement.groundCollisionLayerMask);

				if(movement.drawDebugGizmos){
					GizmoUtils.DrawSphere(hitInfo.point, .05f, Color.black, 4, gizmoDuration);
					GizmoUtils.DrawLine(hitInfo.point, hitInfo.point + hitInfo.normal, Color.black, gizmoDuration);
				}

				return (true, hitInfo);
			}

			//Hit nothing
			return (false, hitInfo);
		}

		public (bool didHit, bool onRamp, Vector3 pointOnRamp, Vector3 newVel) StepUp(Vector3 startPos, Vector3 vel, float deltaTime, Vector3 currentUpNormal){
			var flatVel = vel;
			flatVel.y = 0;
			if(flatVel.sqrMagnitude < .1f){
				return (false, false, vel, vel);
			}

			//Check if there is an obstruction
			var velDir = flatVel.normalized;
			var velFrame = vel/deltaTime;
			float stepUpRampDistance = .75f;
			(bool didHitForward, RaycastHit forwardHitInfo) = CheckForwardHit(startPos - velDir*(offsetMargin), velDir * (stepUpRampDistance+offsetMargin));

			if(didHitForward && movement.useExtraLogging){
				Debug.Log("currentUpNormal: " + currentUpNormal + " forwardHitInfo: " + forwardHitInfo.normal + " EQUAL: "+ (currentUpNormal == forwardHitInfo.normal));
			}

			var heightDiff = Mathf.Abs(forwardHitInfo.point.y - startPos.y);
			var flatDistance = GetFlatDistance(movement.rootTransform.position, forwardHitInfo.point);
			//If we hit an obstruction 
			if(didHitForward &&  
				(movement.grounded || movement.assistedLedgeJump) &&
				//lower than the step up height
				heightDiff < movement.moveData.maxStepUpHeight &&
				//Thats not the same surface we are standing on
				(heightDiff < offsetMargin || currentUpNormal != forwardHitInfo.normal) &&
				//The hit wall isn't a walkable surface
				!IsWalkableSurface(forwardHitInfo.normal)){
				//See if there is a surface to step up onto
				var stepUpRayStart = forwardHitInfo.point + velDir * (forwardHitInfo.distance + offsetMargin);
				stepUpRayStart.y =  startPos.y + movement.moveData.maxStepUpHeight+movement.characterRadius;
				
				if(movement.drawDebugGizmos){
					GizmoUtils.DrawSphere(stepUpRayStart, .05f, Color.yellow, 4, gizmoDuration);
					GizmoUtils.DrawSphere(startPos, .04f, Color.blue, 4, gizmoDuration);
				}
				
				if(Physics.BoxCast(stepUpRayStart, new Vector3(movement.characterRadius,movement.characterRadius,movement.characterRadius), 
					Vector3.down, out RaycastHit stepUpRayHitInfo, Quaternion.identity, movement.moveData.characterHeight, movement.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)){
				//if(Physics.Raycast(stepUpRayStart, new Vector3(0,-1,0), out RaycastHit stepUpRayHitInfo, movement.moveData.characterHeight, movement.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)){
					//Hit a surface that is in range
					if(movement.drawDebugGizmos){
						GizmoUtils.DrawLine(stepUpRayStart, stepUpRayHitInfo.point, Color.yellow, gizmoDuration);
					}

					//Make sure the surface is valid
					if(stepUpRayHitInfo.point.y > startPos.y + Mathf.Min(0,velFrame.y)
						&& IsWalkableSurface(stepUpRayHitInfo.normal)
						&& !Physics.Raycast(stepUpRayHitInfo.point, Vector3.up, movement.currentCharacterHeight, movement.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)){						//CAN STEP UP HERE
						//Find the slope direction that the character needs to walk up to the step
						var topPoint = new Vector3(forwardHitInfo.point.x, stepUpRayHitInfo.point.y + offsetMargin, forwardHitInfo.point.z)- velDir * offsetMargin;//(movement.characterRadius+offsetMargin);
						var bottompoint = topPoint - velDir * stepUpRampDistance;
						bottompoint.y = startPos.y;

						var rampVec = topPoint - bottompoint;
						var rampNormal = Vector3.Cross(Vector3.right, rampVec).normalized;

						var rawDelta = GetFlatDistance(startPos + velDir * movement.characterRadius, topPoint) / stepUpRampDistance;
						var pointOnRampDelta = 1-Mathf.Clamp01(rawDelta);
						var pointOnRamp = Vector3.Lerp(bottompoint, topPoint, pointOnRampDelta);
						if(movement.drawDebugGizmos){
							GizmoUtils.DrawSphere(topPoint, .02f, Color.cyan, 4, gizmoDuration);
							GizmoUtils.DrawLine(topPoint, topPoint+rampNormal, Color.yellow, gizmoDuration);
							GizmoUtils.DrawSphere(pointOnRamp, .02f, Color.green, 4, gizmoDuration);
							GizmoUtils.DrawSphere(bottompoint, .02f, Color.black, 4, gizmoDuration);
							GizmoUtils.DrawLine(bottompoint, bottompoint+rampVec, Color.black, gizmoDuration);
						}

						//Manipulate velocity so that it moves up a ramp instead of hitting the step
						return (true, rawDelta >= 0 && rawDelta <= 1, pointOnRamp, Vector3.ProjectOnPlane(vel, rampNormal));
					}else if(movement.useExtraLogging){
						Debug.Log("Can't step up here. hitPoint: " + stepUpRayHitInfo.point + " startPos: " + startPos + " isWalkable: "+ IsWalkableSurface(stepUpRayHitInfo.normal));
						
					}

				}
			}
			if(movement.alwaysStepUp || 
				(didHitForward && movement.grounded && flatDistance < velFrame.magnitude+movement.characterRadius)){
				//We hit something but don't qualify for the advanced ramp step up
				//Instead just jump to the new height of the surface
				var startPoint = new Vector3(forwardHitInfo.point.x, startPos.y + movement.moveData.maxStepUpHeight, forwardHitInfo.point.z);
				startPoint += vel * offsetMargin;
				//Cast a ray down from where the character will be next frame
				if(Physics.Raycast(startPoint, Vector3.down, out RaycastHit quickStepHitInfo, movement.moveData.maxStepUpHeight,  movement.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)){
					//make sure there isn't an obstruction above us
					if(!Physics.Raycast(quickStepHitInfo.point, Vector3.up, movement.standingCharacterHeight+offsetMargin, movement.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)
						&& IsWalkableSurface(quickStepHitInfo.normal)){
						var hitPoint = quickStepHitInfo.point + new Vector3(0,offsetMargin, 0);
						if(movement.drawDebugGizmos){
							GizmoUtils.DrawSphere(hitPoint, .05f, Color.white, 4, gizmoDuration);
						}
						return (true, false, hitPoint, vel);
					}
				}
			}
			return (false, false, vel, vel);
		}

		public bool CanStand(){
			return !Physics.BoxCast(
				movement.rootTransform.position + new Vector3(0,movement.characterRadius,0), 
				new Vector3(movement.characterRadius,movement.characterRadius,movement.characterRadius), 
				Vector3.up, Quaternion.identity, movement.standingCharacterHeight - movement.characterRadius, 
				movement.groundCollisionLayerMask, QueryTriggerInteraction.Ignore);
		}
#endregion
	}
}
