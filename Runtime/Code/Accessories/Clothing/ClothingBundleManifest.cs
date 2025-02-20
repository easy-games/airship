using Code.Bootstrap;
using UnityEngine;

namespace Code.Accessories.Clothing {
    [CreateAssetMenu(menuName = "Airship/Clothing Bundle Manifest", fileName = "Clothing Bundle Manifest")]
    public class ClothingBundleManifest : ScriptableObject {
        public Clothing[] clothingList;
        public string airId;
    }
}