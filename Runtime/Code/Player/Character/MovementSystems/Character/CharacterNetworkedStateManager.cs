using Code.Network.StateSystem;
using Mirror;

namespace Code.Player.Character.MovementSystems.Character
{
    [LuauAPI]
    public class CharacterNetworkedStateManager: AirshipNetworkedStateManager<CharacterMovement, CharacterSnapshotData, CharacterInputData>
    {
        public override void SendClientInputToServer(CharacterInputData input)
        {
            this.RpcClientInputToServer(input);
        }

        public override void SendClientSnapshotToServer(CharacterSnapshotData snapshot)
        {
            this.RpcClientSnapshotToServer(snapshot);
        }

        public override void SendServerSnapshotToClients(CharacterSnapshotData snapshot)
        {
            this.RpcServerSnapshotToClients(snapshot);
        }


        [ClientRpc(channel = Channels.Unreliable)]
        private void RpcServerSnapshotToClients(CharacterSnapshotData snapshot)
        {
            this.OnClientReceiveSnapshot?.Invoke(snapshot);
        }

        [Command(channel = Channels.Unreliable)]
        private void RpcClientInputToServer(CharacterInputData input)
        {
            this.OnServerReceiveInput?.Invoke(input);
        }

        [Command(channel = Channels.Unreliable)]
        private void RpcClientSnapshotToServer(CharacterSnapshotData snapshot)
        {
            this.OnServerReceiveSnapshot?.Invoke(snapshot);
        }
    }
}