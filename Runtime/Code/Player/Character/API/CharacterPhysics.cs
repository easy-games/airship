using UnityEngine;

namespace Code.Player.Character.API {
	public class CharacterPhysics {
		private const float offsetMargin = .05f;
		private const float gizmoDuration = 2f;

		private CharacterMovement movement;

		public CharacterPhysics(CharacterMovement movement){
			this.movement = movement;
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

		public bool IsPointVerticallyInCharacter(Vector3 worldPosition){
			Vector3 localPoint = movement.transform.InverseTransformPoint(worldPosition);
				//var distance = Vector3.Distance(Vector3.zero, localHit);
				//var inCylinder =  distance <= standingCharacterRadius+.01f && localHit.y >= movement.moveData.maxSlopeDelta;
			return localPoint.y >= movement.moveData.maxSlopeDelta && localPoint.y < movement.currentCharacterHeight;
		}

		private bool VoxelIsSolid(ushort voxel) {
			return movement.voxelWorld.GetCollisionType(voxel) != VoxelBlocks.CollisionType.None;
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
			//Use a little less then the actual colliders to avoid getting stuck in walls
			var groundCheckRadius = movement.characterRadius-offsetMargin;

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
			var distance = movement.moveData.maxStepUpHeight+movement.characterRadius+.01f;
			var castStartPos = currentPos;
			//Move the start position up
			castStartPos.y += distance;
			//Extend the ray further if you are falling faster
			distance -= Mathf.Min(0, vel.y);// Mathf.Min(0, movement.transform.InverseTransformVector(vel).y); //Need this part of we change gravity dir
			
			var gravityDir = -movement.transform.up;
			var gravityDirOffset = gravityDir.normalized * .1f;
			
			if(movement.drawDebugGizmos){
				GizmoUtils.DrawSphere(castStartPos, groundCheckRadius, Color.magenta, 4, gizmoDuration);
				GizmoUtils.DrawSphere(castStartPos+gravityDir*distance, groundCheckRadius, Color.magenta, 4, gizmoDuration);
				GizmoUtils.DrawLine(castStartPos, castStartPos+gravityDir*distance, Color.magenta, gizmoDuration);
			}

			if (Physics.SphereCast(castStartPos, groundCheckRadius, gravityDir, out var hitInfo, distance, movement.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)) {
				
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
			
				var isKindaUpwards = (1-Vector3.Dot(hitInfo.normal, movement.transform.up)) < movement.moveData.maxSlopeDelta;
				Debug.Log("isKindaUpwards: " + isKindaUpwards + " dot: " + (1-Vector3.Dot(hitInfo.normal, movement.transform.up)));
				var inCollider = IsPointVerticallyInCharacter(hitInfo.point);
				return (isGrounded: isKindaUpwards, blockId: 0, Vector3Int.zero, hitInfo, true);
			}

			return (isGrounded: false, blockId: 0, Vector3Int.zero, default, false);
		}

		public (bool didHit, RaycastHit hitInfo) CheckForwardHit(Vector3 forwardVector, Collider currentGround){
			//Not moving
			if(forwardVector.sqrMagnitude < .1f){
				return (false, default);
			}

			RaycastHit hitInfo;
			//CAPSULE CASTING
			Vector3 normalizedForward = forwardVector.normalized;
			Vector3 pointA = movement.mainCollider.transform.position - normalizedForward * offsetMargin;
			Vector3 pointB = pointA + new Vector3(0, movement.currentCharacterHeight - movement.moveData.maxStepUpHeight, 0);
			//this.mainCollider.GetCapsuleCastParams(out pointA, out pointB, out standingCharacterRadius);
			if(movement.drawDebugGizmos){
				GizmoUtils.DrawSphere(pointB+(normalizedForward * (forwardVector.magnitude-movement.characterRadius)), movement.characterRadius, Color.green, 4, gizmoDuration);
				GizmoUtils.DrawSphere(pointA+(normalizedForward * (forwardVector.magnitude-movement.characterRadius)), movement.characterRadius, Color.green, 4, gizmoDuration);
			}
			if(Physics.CapsuleCast(pointA,pointB, movement.characterRadius, forwardVector, out hitInfo, forwardVector.magnitude-movement.characterRadius+offsetMargin, movement.groundCollisionLayerMask)){
				//bool sameCollider = currentGround != null && hitInfo.collider.GetInstanceID() == currentGround.GetInstanceID();
				var inCollider = IsPointVerticallyInCharacter(hitInfo.point);
                var isVerticalWall = 1-Mathf.Max(0, Vector3.Dot(hitInfo.normal, Vector3.up)) >= movement.moveData.maxSlopeDelta;
				//localHit.y = 0;
				var newDir = hitInfo.point-pointA;
				hitInfo.normal = CalculateRealNormal(hitInfo.normal, pointA, newDir, newDir.magnitude, movement.groundCollisionLayerMask);

				if(movement.drawDebugGizmos){
					GizmoUtils.DrawSphere(hitInfo.point, .05f, Color.green, 12, gizmoDuration);
					GizmoUtils.DrawSphere(pointA, .05f, Color.green, 4, gizmoDuration);
					GizmoUtils.DrawSphere(pointA + newDir, .05f, Color.green, 4, gizmoDuration);
					GizmoUtils.DrawLine(hitInfo.point, hitInfo.point + hitInfo.normal, Color.green, gizmoDuration);
				}

				return (isVerticalWall && inCollider, hitInfo);
			}

			//BOX CASTING
			// Vector3 center = this.mainCollider.transform.position + new Vector3(0, standingCharacterRadius, 0);
			// center -=  forwardVector.normalized * standingCharacterRadius;
			// Vector3 halfExtents = new Vector3(standingCharacterRadius, standingCharacterRadius, standingCharacterRadius);
			// Quaternion rotation = Quaternion.LookRotation(forwardVector, Vector3.up);
			// float magnitude = forwardVector.magnitude-.01f;
			// //this.mainCollider.GetCapsuleCastParams(out pointA, out pointB, out standingCharacterRadius);
			// if(drawDebugGizmos){
			// 	GizmoUtils.DrawBox(center + (forwardVector.normalized * (magnitude/2f)), rotation, new Vector3(halfExtents.x, halfExtents.y, halfExtents.z + magnitude/2f), Color.green, .1f);
			// }
			// if(Physics.BoxCast(center, halfExtents, forwardVector, out hitInfo, rotation, magnitude, groundCollisionLayerMask)){
			// 	var isVerticalWall = 1-Mathf.Max(0, Vector3.Dot(hitInfo.normal, Vector3.up)) >= movement.moveData.maxSlopeDelta;
			// 	//bool sameCollider = currentGround != null && hitInfo.collider.GetInstanceID() == currentGround.GetInstanceID();
			// var snappedHitPoint = hitInfo.point;
			// snappedHitPoint.y = transform.position.y;
			// var distance = Vector3.Distance(transform.position, snappedHitPoint);
			// var inCylinder =  distance<= standingCharacterRadius+.01f;
			// 
        	// Debug.Log("inCylinder: " + inCylinder + " distance: " + distance);
			// if(drawDebugGizmos){
			// 	GizmoUtils.DrawSphere(snappedHitPoint, .05f, Color.green, 12, .1f);
			// }
			// 	return (isVerticalWall && inCylinder, hitInfo);
			// }

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
