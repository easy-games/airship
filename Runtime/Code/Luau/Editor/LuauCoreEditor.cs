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
		private const string LuauCorePrefabPath = "Packages/gg.easy.airship/Runtime/Prefabs/LuauCore.prefab";
		
		static LuauCoreEditor() {
			AddLuauCoreIfMissing();
			EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
		}

		private static void OnActiveSceneChanged(Scene current, Scene next) {
			AddLuauCoreIfMissing();
		}
	
		private static void AddLuauCoreIfMissing() {
			var existingLuauCore = Object.FindAnyObjectByType<LuauCore>();
			if (existingLuauCore != null) {
				if (!existingLuauCore.gameObject.hideFlags.HasFlag(HideFlags.HideInHierarchy)) {
					existingLuauCore.gameObject.hideFlags |= HideFlags.HideInHierarchy;
				}
				return;
			}

			var luauCorePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LuauCorePrefabPath);
			var core = (GameObject)PrefabUtility.InstantiatePrefab(luauCorePrefab, SceneManager.GetActiveScene());
			core.hideFlags |= HideFlags.HideInHierarchy;
		}
	}
#endif
}
