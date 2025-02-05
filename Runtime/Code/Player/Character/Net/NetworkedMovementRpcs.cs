using System;
using Mirror;

namespace Code.Player.Character.Net
{
    public class NetworkedMovementRpcs : NetworkBehaviour
    {
        public Action<InputCommand> OnServerReceivedInput;
        public Action<StateSnapshot> OnServerReceivedSnapshot;
        public Action<StateSnapshot> OnClientReceivedSnapshot;

        [ClientRpc(channel = Channels.Unreliable)]
        private void RpcOnClientReceivedSnapshot(StateSnapshot state)
        {
            this.OnClientReceivedSnapshot?.Invoke(state);
        }
    }
}