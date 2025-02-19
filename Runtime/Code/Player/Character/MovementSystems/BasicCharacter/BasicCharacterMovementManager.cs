using Mirror;

namespace Code.Player.Character.NetworkedMovement
{
    public class BasicCharacterMovementManager: AirshipMovementManager<BasicCharacterMovement, BasicCharacterMovementState, BasicCharacterInputData>
    {
        public override void SendClientInputToServer(BasicCharacterInputData[] input)
        {
            this.RpcClientInputToServer(input);
        }

        public override void SendClientSnapshotToServer(BasicCharacterMovementState snapshot)
        {
            this.RpcClientSnapshotToServer(snapshot);
        }

        public override void SendServerSnapshotToClients(BasicCharacterMovementState snapshot)
        {
            this.RpcServerSnapshotToClients(snapshot);
        }


        [ClientRpc(channel = Channels.Unreliable)]
        private void RpcServerSnapshotToClients(BasicCharacterMovementState state)
        {
            this.OnClientReceiveSnapshot?.Invoke(state);
        }

        [Command(channel = Channels.Unreliable)]
        private void RpcClientInputToServer(BasicCharacterInputData[] input)
        {
            this.OnServerReceiveInput?.Invoke(input);
        }

        [Command(channel = Channels.Unreliable)]
        private void RpcClientSnapshotToServer(BasicCharacterMovementState state)
        {
            this.OnServerReceiveSnapshot?.Invoke(state);
        }
    }
}