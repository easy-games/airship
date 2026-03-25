using Mirror;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    internal static class AirshipPrefabUtility {
        internal static bool FindReconcilablePrefabComponent(AirshipComponent component, out AirshipComponent prefabComponent) {
            var isPrefab = PrefabUtility.IsPartOfAnyPrefab(component);
            if (!isPrefab) {
                prefabComponent = null;
                return false;
            }
            
            var networkIdentity = component.gameObject.GetComponentInParent<NetworkIdentity>();
            if (networkIdentity != null) {
                prefabComponent = null;
                return false;
            }

            var meshFilter = component.GetComponentsInChildren<MeshFilter>(true); // because of 'SendMessage' we can't force reconcile these
            if (meshFilter.Length > 0) {
                prefabComponent = null;
                return false;
            }
            
            prefabComponent = PrefabUtility.GetCorrespondingObjectFromOriginalSource(component);
            if (prefabComponent == null || prefabComponent.script == null) {
                prefabComponent = null;
                return false;
            }
            
            return true;
        }
    }
}