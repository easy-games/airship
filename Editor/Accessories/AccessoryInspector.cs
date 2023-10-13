using System.Collections.Generic;
using System.Linq;
using ParrelSync;
using UnityEditor;
using UnityEngine;

namespace Code.Player.Accessories.Editor {
	/// <summary>
	/// Adds an "Open Editor" button to Accessory items, which will open the
	/// Accessory Editor window when clicked.
	/// </summary>
	[CustomEditor(typeof(Accessory))]
	public class AccessoryInspector : UnityEditor.Editor {
		public override void OnInspectorGUI() {
			DrawDefaultInspector();

			// Add the Open Editor button:
			EditorGUILayout.Space();
			if (ClonesManager.IsClone()) {
				GUILayout.Label("Accessory Editor disabled in clone window.");
			} else {
				if (GUILayout.Button("Open Editor")) {
					var accessory = targets?.First((obj) => obj is Accessory) as Accessory;
					if (accessory != null) {
						AccessoryEditor.OpenWithAccessory(accessory);
					}
				}
			}
		}


		[MenuItem("Airship/👕 Prefab Tools/Accessory Editor", priority = 203)]
		public static void OpenAccessoryEditor() {
			AccessoryEditor.OpenOrCreateWindow();
		}
	}
}
