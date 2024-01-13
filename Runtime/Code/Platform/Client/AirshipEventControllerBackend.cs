using System;

namespace Code.Platform.Client
{
    [LuauAPI]
    public class AirshipEventControllerBackend : Singleton<SocketManager>
    {
        public event Action<string> OnTeleport;
    }
}