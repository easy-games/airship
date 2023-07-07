using UnityEditor;
using UnityEngine;

namespace Code.Player.Accessories.Editor {
	/// <summary>
	/// Tracks the virtual transform of a given accessory. This acts as a cache
	/// that represents an accessory while in the Accessory Editor window.
	/// </summary>
	internal class AccessoryTransform {
		internal readonly Accessory Accessory;
        
		internal Vector3 Position;
		internal Vector3 Rotation;
		internal Vector3 Scale;

		public Vector3 OriginalPosition { get; private set; }
		public Vector3 OriginalRotation { get; private set; }
		public Vector3 OriginalScale { get; private set; }

		internal AccessoryTransform(Accessory accessory) {
			Accessory = accessory;
			ResetFromAccessory();
		}
		
		internal void ResetFromAccessory() {
			Position = Accessory.Position;
			Rotation = Accessory.Rotation;
			Scale = Accessory.Scale;
			OriginalPosition = Accessory.Position;
			OriginalRotation = Accessory.Rotation;
			OriginalScale = Accessory.Scale;
		}

		/// <summary>
		/// Save the transform data to the actual Accessory object.
		/// </summary>
		internal void Save() {
			Undo.RecordObject(Accessory, "Save Accessory");
			Accessory.Position = Position;
			Accessory.Rotation = Rotation;
			Accessory.Scale = Scale;
			OriginalPosition = Position;
			OriginalRotation = Rotation;
			OriginalScale = Scale;
			PrefabUtility.RecordPrefabInstancePropertyModifications(Accessory);
		}
	}
}
