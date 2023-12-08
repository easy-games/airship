using UnityEngine;

namespace Assets.Code.Luau {
	public class ScriptingEntryPoint : MonoBehaviour {
		private const string EntryScript = "@easy/core/shared/resources/ts/main.lua";
		
		private void Start() {
			var gameBindings = GetComponentsInChildren<ScriptBinding>();

			var coreBindingGo = new GameObject("@easy/core");
			var coreBinding = coreBindingGo.AddComponent<ScriptBinding>();
			
			coreBinding.SetScriptFromPath(EntryScript);
			coreBinding.Init();

			foreach (var binding in gameBindings) {
				binding.Init();
			}
		}
	}
}
