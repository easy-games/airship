#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Code.Luau.Editor {
#if UNITY_EDITOR
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
#endif
}
