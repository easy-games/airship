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
