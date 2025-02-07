using Code.Player.Character.Net;
using Mirror;

namespace Code.Player.Character.NetworkedMovement.BasicTest
{
    public class BasicTestMovementManager: AirshipMovementManager<BasicTestMovement, BasicMovementState, BasicMovementInput>
    {
        
        public override void SendClientInputToServer(BasicMovementInput[] input)
        {
            this.RpcClientInputToServer(input);
        }

        public override void SendClientSnapshotToServer(BasicMovementState snapshot)
        {
            this.RpcClientSnapshotToServer(snapshot);
        }

        public override void SendServerSnapshotToClients(BasicMovementState snapshot)
        {
            this.RpcServerSnapshotToClients(snapshot);
        }


        [ClientRpc(channel = Channels.Unreliable)]
        private void RpcServerSnapshotToClients(BasicMovementState state)
        {
            this.OnClientReceiveSnapshot?.Invoke(state);
        }

        [Command(channel = Channels.Unreliable)]
        private void RpcClientInputToServer(BasicMovementInput[] input)
        {
            this.OnServerReceiveInput?.Invoke(input);
        }

        [Command(channel = Channels.Unreliable)]
        private void RpcClientSnapshotToServer(BasicMovementState state)
        {
            this.OnServerReceiveSnapshot?.Invoke(state);
        }
    }
}