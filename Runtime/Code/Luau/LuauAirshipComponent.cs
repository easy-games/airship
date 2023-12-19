using UnityEngine;

namespace Luau {
    [DisallowMultipleComponent]
    public class LuauAirshipComponent : MonoBehaviour {
        private static int _idGen = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() {
            _idGen = 0;
        }

        public int Id { get; } = _idGen++;
    }

    // Matches same enum in AirshipComponent.h plugin file
    public enum AirshipComponentUpdateType {
        AirshipUpdate,
        AirshipEnabled,
        AirshipDisabled,
        AirshipLateUpdate,
        AirshipFixedUpdate,
        AirshipStart,
        AirshipAwake,
        AirshipDestroy,
    }
}
