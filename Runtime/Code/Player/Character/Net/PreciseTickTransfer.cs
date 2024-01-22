using System;
using FishNet.Managing.Timing;

namespace Code.Player.Character.Net {
	/// <summary>
	/// A serializable version of FishNet's PreciseTick.
	/// </summary>
	[Serializable]
	public struct PreciseTickTransfer {
		public readonly uint Tick;
		public readonly double Percent;

		public PreciseTickTransfer(uint tick, double percent) {
			Tick = tick;
			Percent = percent;
		}

		public static explicit operator PreciseTickTransfer(PreciseTick preciseTick) {
			return new PreciseTickTransfer(preciseTick.Tick, preciseTick.Percent);
		}

		public static implicit operator PreciseTick(PreciseTickTransfer transfer) {
			return new PreciseTick(transfer.Tick, transfer.Percent);
		}
	}
}
