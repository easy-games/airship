using FishNet;
using FishNet.Managing.Timing;
using UnityEngine;

namespace Player.Entity {
	public class EntityLerp {
		private readonly float _startY;
		private readonly float _goalY;
		private readonly PreciseTick _tickStart;
		private readonly float _duration;

		public EntityLerp(float startY, float goalY, PreciseTick tickStart, float duration) {
			_startY = startY;
			_goalY = goalY;
			_tickStart = tickStart;
			_duration = duration;
		}

		public bool Update(out float y) {
			var elapsed = (float) InstanceFinder.TimeManager.TimePassed(_tickStart);

			var done = elapsed >= _duration;
			if (done) {
				elapsed = _duration;
			}

			var alpha = elapsed / _duration;
			y = Mathf.Lerp(_startY, _goalY, alpha);

			return done;
		}
	}
}
