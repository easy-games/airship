using Code.Player.Character.Net;
using Mirror;

namespace Code.Network.StateSystem.Implementations.TestMovementSystem
{
    public class TestNetworkedStateManager: AirshipNetworkedStateManager<TestMovement, TestMovementState, TestMovementInput>
    {
        
        public override void SendClientInputToServer(TestMovementInput[] input)
        {
            this.RpcClientInputToServer(input);
        }

        public override void SendClientSnapshotToServer(TestMovementState[] snapshot)
        {
            this.RpcClientSnapshotToServer(snapshot);
        }

        public override void SendServerSnapshotToClients(TestMovementState snapshot)
        {
            this.RpcServerSnapshotToClients(snapshot);
        }


        [ClientRpc(channel = Channels.Unreliable)]
        private void RpcServerSnapshotToClients(TestMovementState state)
        {
            this.OnClientReceiveSnapshot?.Invoke(state);
        }

        [Command(channel = Channels.Unreliable)]
        private void RpcClientInputToServer(TestMovementInput[] input)
        {
            this.OnServerReceiveInput?.Invoke(input);
        }

        [Command(channel = Channels.Unreliable)]
        private void RpcClientSnapshotToServer(TestMovementState[] state)
        {
            this.OnServerReceiveSnapshot?.Invoke(state);
        }
    }
}