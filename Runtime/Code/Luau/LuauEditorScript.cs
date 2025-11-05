using Luau;
using UnityEngine;

[ExecuteAlways]
public class LuauEditorScript : LuauScript {
	private AirshipScript _prevScript;

	// This will serialize, but that's fine for now. Eventually we'll add a button.
	public bool reload;

	private void Awake() {
		if (!Application.isPlaying && script != null) {
			LoadAndExecuteInternal();
		}
	}
	
	private void OnValidate() {
		// Don't choose to run the script if it changes here, because that will trigger while previewing the
		// scripts from the popup list. In other words, it would run each script you highlight before selecting it.
		
		if (reload) {
			reload = false;
			if (script != null) {
				LoadAndExecuteInternal();
			}
		}
	}
}
