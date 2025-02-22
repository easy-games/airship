using Code.Bootstrap;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Accessories.Clothing {
    [CreateAssetMenu(menuName = "Airship/Gear Bundle Manifest", fileName = "Gear Bundle Manifest")]
    public class PlatformGearBundleManifest : ScriptableObject {
        [FormerlySerializedAs("clothingList")] public PlatformGear[] gearList;
        public string airId;
    }
}