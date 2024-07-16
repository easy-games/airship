using System;
using UnityEngine;
using UnityEditor;

namespace Airship.Editor {
    [Serializable]
    public class EditorIcons : ScriptableObject {
        [SerializeField]
        public byte[] signedInIcon;

        private void OnEnable() {
            hideFlags = HideFlags.HideAndDontSave;
        }

        private static EditorIcons instance;
        public static EditorIcons Instance {
            get {
                if (instance == null) {
                    instance = AssetDatabase.LoadAssetAtPath<EditorIcons>("Packages/gg.easy.airship/Editor/EditorIcons.asset");
                    // if (instance == null) {
                    //     instance = CreateInstance<EditorIcons>();
                    //     AssetDatabase.CreateAsset(instance, "Packages/gg.easy.airship/Editor/EditorIcons.asset");
                    //     AssetDatabase.SaveAssets();
                    // }
                }
                return instance;
            }
        }
    }
}