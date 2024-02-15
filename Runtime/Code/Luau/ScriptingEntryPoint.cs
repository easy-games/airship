using Luau;
using UnityEngine;

namespace Assets.Code.Luau {
	public class ScriptingEntryPoint : MonoBehaviour {
		private const string CoreEntryScript = "@easy/core/shared/resources/ts/corebootstrap.lua";
		
		private void Start() {
			var gameBindings = GetComponentsInChildren<ScriptBinding>();

			var coreBindingGo = new GameObject("@easy/core");
			coreBindingGo.transform.parent = transform;
			var coreBinding = coreBindingGo.AddComponent<ScriptBinding>();
			
			coreBinding.SetScriptFromPath(CoreEntryScript, LuauSecurityContext.Core);
			coreBinding.Init();

			foreach (var binding in gameBindings) {
				binding.Init();
			}
		}
	}
}
