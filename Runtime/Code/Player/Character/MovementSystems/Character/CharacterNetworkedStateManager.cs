using Code.Network.StateSystem;
using Mirror;

namespace Code.Player.Character.MovementSystems.Character
{
    public class CharacterNetworkedStateManager: AirshipNetworkedStateManager<CharacterMovement, CharacterMovementState, CharacterInputData>
    {
        public override void SendClientInputToServer(CharacterInputData input)
        {
            this.RpcClientInputToServer(input);
        }

        public override void SendClientSnapshotToServer(CharacterMovementState snapshot)
        {
            this.RpcClientSnapshotToServer(snapshot);
        }

        public override void SendServerSnapshotToClients(CharacterMovementState snapshot)
        {
            this.RpcServerSnapshotToClients(snapshot);
        }


        [ClientRpc(channel = Channels.Unreliable)]
        private void RpcServerSnapshotToClients(CharacterMovementState state)
        {
            this.OnClientReceiveSnapshot?.Invoke(state);
        }

        [Command(channel = Channels.Unreliable)]
        private void RpcClientInputToServer(CharacterInputData input)
        {
            this.OnServerReceiveInput?.Invoke(input);
        }

        [Command(channel = Channels.Unreliable)]
        private void RpcClientSnapshotToServer(CharacterMovementState state)
        {
            this.OnServerReceiveSnapshot?.Invoke(state);
        }
    }
}