using UnityEngine;

namespace Assets.Code.Luau
{
	public class ScriptingEntryPoint : MonoBehaviour
	{
		private void Start() {
			var gameBindings = this.GetComponentsInChildren<ScriptBinding>();

			var coreBindingGo = new GameObject("@easy/core");
			var coreBinding = coreBindingGo.AddComponent<ScriptBinding>();
			coreBinding.m_fileFullPath = "@easy/core/shared/resources/ts/main.lua";
			coreBinding.Init();

			for (var i = 0; i < gameBindings.Length; i++)
			{
				gameBindings[i].Init();
			}
		}
	}
}