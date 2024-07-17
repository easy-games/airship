using System;
using UnityEngine;
using UnityEditor;

namespace Airship.Editor {
    [Serializable]
    public class EditorIcons : ScriptableObject {
        [SerializeField]
        public byte[] signedInIcon;

        private static EditorIcons instance;
        public static EditorIcons Instance {
            get {
                if (instance == null) {
                    instance = AssetDatabase.LoadAssetAtPath<EditorIcons>("Assets/Editor/EditorIcons.asset");
                }
                return instance;
            }
        }

        public static void Setup() {
            if (instance == null) {
                instance = CreateInstance<EditorIcons>();
                AssetDatabase.CreateAsset(instance, "Assets/Editor/EditorIcons.asset");
                instance.hideFlags = HideFlags.HideAndDontSave;
                AssetDatabase.SaveAssets();
            }
        }
    }
}