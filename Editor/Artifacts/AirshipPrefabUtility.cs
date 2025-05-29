using Mirror;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    internal static class AirshipPrefabUtility {
        internal static bool FindReconcilablePrefabComponent(AirshipComponent component, out AirshipComponent prefabComponent) {
            var isPrefab = PrefabUtility.IsPartOfAnyPrefab(component);
            prefabComponent = PrefabUtility.GetCorrespondingObjectFromOriginalSource(component);

            if (!isPrefab) {
                prefabComponent = null;
                return false;
            }
            
            if (prefabComponent.script == null) {
                prefabComponent = null;
                return false;
            }
            
            var networkIdentity = prefabComponent.gameObject.GetComponentInParent<NetworkIdentity>();
            if (networkIdentity != null) {
                prefabComponent = null;
                return false;
            }

            var meshFilter = prefabComponent.GetComponentsInChildren<MeshFilter>(); // because of 'SendMessage' we can't force reconcile these
            if (meshFilter.Length > 0) {
                prefabComponent = null;
                return false;
            }

            return true;
        }
    }
}