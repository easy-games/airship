using UnityEngine;

namespace Luau
{
    [DisallowMultipleComponent]
    public class LuauAirshipComponent : MonoBehaviour
    {
        private static int _idGen = 0;

        public int Id { get; } = _idGen++;
    }

    // Matches same enum in AirshipComponent.h plugin file
    public enum AirshipComponentUpdateType
    {
        AirshipUpdate,
        AirshipLateUpdate,
        AirshipFixedUpdate,
        AirshipStart,
        AirshipDestroy,
    }
}
