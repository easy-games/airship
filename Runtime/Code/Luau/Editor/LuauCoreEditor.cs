using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Code.Luau.Editor {
	[InitializeOnLoad]
	public class LuauCoreEditor {
		static LuauCoreEditor() {
			AddLuauCoreIfMissing();
			EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
		}

		private static void OnActiveSceneChanged(Scene current, Scene next) {
			AddLuauCoreIfMissing();
		}
	
		private static void AddLuauCoreIfMissing() {
			var existingLuauCore = Object.FindAnyObjectByType<LuauCore>();
			if (existingLuauCore != null) return;

			var luauCorePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/gg.easy.airship/Runtime/Prefabs/LuauCore.prefab");
			PrefabUtility.InstantiatePrefab(luauCorePrefab, SceneManager.GetActiveScene());
		}
	}
}
