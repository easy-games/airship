using UnityEngine;

namespace Code.Player.Human.API {
	public static class HumanPhysics {
		public static Vector2 RotateV2(Vector2 v, float angle) {
			angle *= Mathf.Deg2Rad;
			return new Vector2(
				v.x * Mathf.Cos(angle) - v.y * Mathf.Sin(angle),
				v.x * Mathf.Sin(angle) + v.y * Mathf.Cos(angle)
			);
		}

		public static Vector3 CalculateDrag(Vector3 velocity, float airDensity, float dragCoefficient, float frontalArea) {
			var drag = 0.5f * airDensity * Vector3.Dot(velocity, velocity) * frontalArea * dragCoefficient;
			return -velocity.normalized * drag;
		}

		public static Vector3 CalculateFriction(Vector3 velocity, float gravity, float mass, float frictionCoefficient) {
			var flatVelocity = new Vector3(velocity.x, 0, velocity.z);
			var normalForce = mass * gravity;
			var friction = frictionCoefficient * normalForce;
			return -flatVelocity.normalized * friction;
		}
	}
}
