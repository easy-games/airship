using UnityEngine;

namespace Luau
{
    [DisallowMultipleComponent]
    public class LuauAirshipComponent : MonoBehaviour
    {
        private static int _idGen = 0;

        public int Id { get; } = _idGen++;
    }
}
