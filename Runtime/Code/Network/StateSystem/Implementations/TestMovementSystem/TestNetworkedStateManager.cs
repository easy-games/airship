using Code.Player.Character.Net;
using Mirror;

namespace Code.Network.StateSystem.Implementations.TestMovementSystem
{
    public class TestNetworkedStateManager: AirshipNetworkedStateManager<TestMovement, TestMovementState, TestMovementDiff, TestMovementInput>
    {
        
        public override void SendClientInputToServer(TestMovementInput input)
        {
            this.RpcClientInputToServer(input);
        }

        public override void SendClientSnapshotToServer(TestMovementState snapshot)
        {
            this.RpcClientSnapshotToServer(snapshot);
        }
        
        public override void SendRequestFullSnapshotToServer() {
            this.CmdClientRequestFullSnapshot();
        }
        
        public override void SendServerSnapshotToClient(NetworkConnection client, TestMovementState snapshot)
        {
            this.RpcServerSnapshotToClient(snapshot);
        }

        public override void SendServerDiffToClient(NetworkConnection client, TestMovementDiff diff) {
            this.RpcServerDiffToClient(diff);
        }
        
        [TargetRpc(channel = Channels.Unreliable)]
        private void RpcServerSnapshotToClient(TestMovementState state)
        {
            this.OnClientReceiveSnapshot?.Invoke(state);
        }
        
        [TargetRpc(channel = Channels.Unreliable)]
        private void RpcServerDiffToClient(TestMovementDiff diff) {
            this.OnClientReceiveDiff?.Invoke(diff);
        }

        [Command(channel = Channels.Unreliable)]
        private void RpcClientInputToServer(TestMovementInput input)
        {
            this.OnServerReceiveInput?.Invoke(input);
        }

        [Command(channel = Channels.Unreliable)]
        private void RpcClientSnapshotToServer(TestMovementState state)
        {
            this.OnServerReceiveSnapshot?.Invoke(state);
        }
        
        [Command(channel = Channels.Reliable, requiresAuthority = false)]
        private void CmdClientRequestFullSnapshot(NetworkConnectionToClient sender = null) {
            if (sender == null) return;
            this.OnServerReceiveFullSnapshotRequest?.Invoke(sender.connectionId);
        }

    }
}