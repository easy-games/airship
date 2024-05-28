using UnityEngine;

namespace Code.Player.Character.API {
	public class CharacterPhysics {
		private const float offsetMargin = .05f;
		private const float gizmoDuration = 2f;

		private CharacterMovement movement;
		private Vector3 uniformHalfExtents;

		public CharacterPhysics(CharacterMovement movement){
			this.movement = movement;
			uniformHalfExtents = new Vector3(movement.characterRadius,movement.characterRadius,movement.characterRadius);
		}

		public Vector2 RotateV2(Vector2 v, float angle) {
			angle *= Mathf.Deg2Rad;
			return new Vector2(
				v.x * Mathf.Cos(angle) - v.y * Mathf.Sin(angle),
				v.x * Mathf.Sin(angle) + v.y * Mathf.Cos(angle)
			);
		}

		public Vector3 CalculateDrag(Vector3 velocity) {
			var drag = 0.5f * movement.moveData.airDensity * Vector3.Dot(velocity, velocity) * movement.characterRadius * movement.moveData.drag;
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
			
			if(movement.drawDebugGizmos){
				GizmoUtils.DrawBox(castStartPos, Quaternion.identity, uniformHalfExtents, Color.magenta, gizmoDuration);
				GizmoUtils.DrawBox(castStartPos+gravityDir*distance, Quaternion.identity, uniformHalfExtents, Color.magenta, gizmoDuration);
			}


			//Check directly below character as an early out and for comparison information
			// if(Physics.Raycast(castStartPos, gravityDir, out var rayHitInfo, distance + .01f, movement.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)){
			// 	if(movement.drawDebugGizmos){
			// 		GizmoUtils.DrawLine(castStartPos, castStartPos+gravityDir*(distance + .01f), Color.gray, gizmoDuration);
			// 		GizmoUtils.DrawSphere(rayHitInfo.point, .05f, Color.red, 4, gizmoDuration);
			// 	}

			// 	return (isGrounded: IsWalkableSurface(rayHitInfo.normal), blockId: 0, Vector3Int.zero, rayHitInfo, true);
			// }

			//Check down around the entire character
			if (Physics.BoxCast(castStartPos, uniformHalfExtents, gravityDir, out var hitInfo, Quaternion.identity, distance, movement.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)) {
			//if (Physics.BoxCast(castStartPos, new Vector3(groundCheckRadius, groundCheckRadius, groundCheckRadius), gravityDir, out var hitInfo, Quaternion.identity, distance, movement.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)) {	
				if(movement.drawDebugGizmos){
					GizmoUtils.DrawSphere(hitInfo.point + gravityDirOffset, .05f, Color.red, 4, gizmoDuration);
				}
				if(!movement.grounded){
					if(movement.drawDebugGizmos){
						GizmoUtils.DrawSphere(hitInfo.point, .1f, Color.red, 8, gizmoDuration);
						
					}
					if(movement.useExtraLogging){
        				Debug.Log("hitInfo GROUND. UpDot: " +  Vector3.Dot(hitInfo.normal, movement.transform.up) + " Start: " + castStartPos + " distance: " + distance + " hitInfo point: " + hitInfo.collider.gameObject.name + " at: " + hitInfo.point);
					}
				}

				//Physics Casts give you interpolated normals. This uses a ray to find an exact normal
				hitInfo.normal = CalculateRealNormal(hitInfo.normal, hitInfo.point + gravityDirOffset + moveDir.normalized*.01f, gravityDir, .11f, movement.groundCollisionLayerMask);
			
				if(movement.drawDebugGizmos){
					GizmoUtils.DrawLine(hitInfo.point, hitInfo.point + hitInfo.normal, Color.red, gizmoDuration);
				}

				//var inCollider = IsPointInCharacter...(hitInfo.point);
				return (isGrounded: IsWalkableSurface(hitInfo.normal), blockId: 0, Vector3Int.zero, hitInfo, true);
			}

			return (isGrounded: false, blockId: 0, Vector3Int.zero, default, false);
		}

		public (bool didHit, RaycastHit hitInfo) CheckForwardHit(Vector3 forwardVector, Collider currentGround){
			//Not moving
			if(forwardVector.sqrMagnitude < .1f){
				return (false, default);
			}

			RaycastHit hitInfo;
			//BOX CASTING
			Vector3 normalizedForward = forwardVector.normalized;
			Vector3 startPoint = movement.mainCollider.transform.position;// - normalizedForward * offsetMargin;
			float distance = forwardVector.magnitude-movement.characterRadius;
			if(movement.drawDebugGizmos){
				GizmoUtils.DrawBox(startPoint, Quaternion.identity, movement.characterHalfExtents, Color.green, gizmoDuration);
				GizmoUtils.DrawBox(startPoint+normalizedForward * distance, Quaternion.identity, movement.characterHalfExtents, Color.green, gizmoDuration);
			}
			if(Physics.BoxCast(startPoint, movement.characterHalfExtents, forwardVector, out hitInfo, Quaternion.identity, distance, movement.groundCollisionLayerMask)){
				//bool sameCollider = currentGround != null && hitInfo.collider.GetInstanceID() == currentGround.GetInstanceID();
				//var inCollider = IsPointVerticallyInCharacter(hitInfo.point);
                var isVerticalWall = 1-Mathf.Max(0, Vector3.Dot(hitInfo.normal, Vector3.up)) >= movement.moveData.maxSlopeDelta;
				//localHit.y = 0;
				var newDir = hitInfo.point-startPoint;
				hitInfo.normal = CalculateRealNormal(hitInfo.normal, startPoint, newDir, newDir.magnitude, movement.groundCollisionLayerMask);

				if(movement.drawDebugGizmos){
					GizmoUtils.DrawSphere(hitInfo.point, .05f, Color.black, 4, gizmoDuration);
					GizmoUtils.DrawLine(hitInfo.point, hitInfo.point + hitInfo.normal, Color.black, gizmoDuration);
				}

				return (isVerticalWall, hitInfo);
			}

			//Hit nothing
			return (false, hitInfo);
		}

		public (bool didHit, RaycastHit hitInfo, float stepHeight) CheckStepHit(Vector3 startPos, float maxDepth, Collider currentGround){
			if(currentGround){
				if(movement.drawDebugGizmos){
					GizmoUtils.DrawSphere(startPos, .05f, Color.yellow, 4, gizmoDuration);
					GizmoUtils.DrawSphere(startPos+new Vector3(0,-maxDepth,0), .05f, Color.yellow, 4, gizmoDuration);
					GizmoUtils.DrawLine(startPos, startPos+new Vector3(0,-maxDepth,0), Color.yellow, gizmoDuration);
				}
				
				RaycastHit hitInfo;
				if(Physics.Raycast(startPos, new Vector3(0,-maxDepth,0).normalized, out hitInfo, maxDepth, movement.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)){
					//Don't step up onto the same collider you are already standing on
					if(hitInfo.collider.GetInstanceID() != currentGround.GetInstanceID() 
						&& hitInfo.point.y > movement.transform.position.y //Don't step up to something below you
						&& hitInfo.rigidbody == null) { //Don't step up onto physics objects
						//
        Debug.Log("groundID: " + currentGround.GetInstanceID() + " stepColliderID: " + hitInfo.collider.GetInstanceID());
						
					if(movement.drawDebugGizmos){
						GizmoUtils.DrawSphere(hitInfo.point, .1f, Color.yellow, 8, gizmoDuration);
					}
					return (true, hitInfo, maxDepth - hitInfo.distance);
					}
				}
			}
			return (false, new RaycastHit(), 0);
		}
#endregion
	}
}
