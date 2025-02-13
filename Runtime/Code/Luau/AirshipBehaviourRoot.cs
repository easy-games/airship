using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Luau {
    public static class AirshipBehaviourRootV2 {
        private static int _idGen;

        private static readonly Dictionary<GameObject, int> Ids = new();
        private static readonly Dictionary<int, GameObject> IdToGameObject = new();
        private static readonly Dictionary<int, HashSet<int>> GameObjectComponentIds = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() {
            _idGen = 0;
            Ids.Clear();
            IdToGameObject.Clear();
        }

        public static int GetId(GameObject gameObject) {
            if (Ids.TryGetValue(gameObject, out var id)) return id;

            id = ++_idGen;
            Ids.Add(gameObject, id);
            IdToGameObject.Add(id, gameObject);

            return id;
        }

        public static int GetId(Component component) {
            return GetId(component.gameObject);
        }

        /// <summary>
        /// This will ensure that the parent GameObject has a reference to the AirshipComponent
        /// </summary>
        internal static void LinkComponentToGameObject(AirshipComponent component, out int gameObjectId) {
            gameObjectId = GetId(component.gameObject);
            var componentId = component.GetAirshipComponentId();

            if (!GameObjectComponentIds.TryGetValue(gameObjectId, out var componentIds)) {
                componentIds = new HashSet<int>();
                GameObjectComponentIds.Add(gameObjectId, componentIds);
            }

            componentIds.Add(componentId);
        }

        internal static void CleanIdOnDestroy(GameObject gameObject, AirshipComponent component) {
            if (!Ids.TryGetValue(gameObject, out var id)) return;
            
            var components = gameObject.GetComponents<AirshipComponent>().Where(c => c != component);
            if (components.Any() && gameObject.activeInHierarchy) return;

            var componentIds =  GameObjectComponentIds[id];
            
            componentIds.Remove(component.GetAirshipComponentId());

            if (componentIds.Count != 0) return;
            
            // If no more components, we'll remove Id <-> GameObject mappings, tyvm
            Ids.Remove(gameObject);
            IdToGameObject.Remove(id);
            GameObjectComponentIds.Remove(id);
        }

        public static bool HasId(GameObject gameObject) {
            return Ids.ContainsKey(gameObject);
        }

        public static GameObject GetGameObject(int objectId) {
            return IdToGameObject.GetValueOrDefault(objectId);
        }

        public static AirshipComponent GetComponent(GameObject gameObject, int componentId) {
            return gameObject != null
                ? gameObject.GetComponents<AirshipComponent>()
                    .FirstOrDefault(f => f.GetAirshipComponentId() == componentId)
                : null;
        }

        public static AirshipComponent GetComponent(int unityInstanceId, int componentId) {
            return GetComponent(GetGameObject(unityInstanceId), componentId);
        }
    }

    [DisallowMultipleComponent]
    public class AirshipBehaviourRoot : MonoBehaviour {
        private static int _idGen = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() {
            _idGen = 0;
        }

        public int Id { get; } = _idGen++;
    }

    // Matches same enum order in AirshipComponent.h plugin file
    public enum AirshipComponentUpdateType {
        AirshipUpdate,
        AirshipEnabled,
        AirshipDisabled,
        AirshipLateUpdate,
        AirshipFixedUpdate,
        AirshipStart,
        AirshipAwake,
        AirshipDestroy,
        AirshipCollisionEnter,
        AirshipCollisionStay,
        AirshipCollisionExit,
        AirshipCollisionEnter2D,
        AirshipCollisionStay2D,
        AirshipCollisionExit2D,
        AirshipTriggerEnter,
        AirshipTriggerStay,
        AirshipTriggerExit,
        AirshipTriggerEnter2D,
        AirshipTriggerStay2D,
        AirshipTriggerExit2D,
    }
}