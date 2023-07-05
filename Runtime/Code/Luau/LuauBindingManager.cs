using UnityEngine;

namespace Assets.Code.Luau
{
	public class LuauBindingManager : MonoBehaviour
	{
		public static LuauBindingManager Instance;

		private void Start()
		{
			Instance = this;

			var bindings = this.GetComponentsInChildren<LuauBinding>();

			for (var i = 0; i < bindings.Length; i++)
			{
				bindings[i].Init();
			}
		}
	}
}