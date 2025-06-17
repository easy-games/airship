using Code.Network.StateSystem;
using Mirror;

namespace Code.Player.Character.MovementSystems.Character
{
    [LuauAPI]
    public class CharacterNetworkedStateManager: AirshipNetworkedStateManager<CharacterMovement, CharacterSnapshotData, CharacterStateDiff, CharacterInputData>
    {
        public override void SendClientInputToServer(CharacterInputData input)
        {
            this.CmdClientInputToServer(input);
        }

        public override void SendClientSnapshotToServer(CharacterSnapshotData snapshot)
        {
            this.CmdClientSnapshotToServer(snapshot);
        }

        public override void SendRequestFullSnapshotToServer() {
            this.CmdClientRequestFullSnapshot();
        }

        public override void SendAckSnapshotToServer(uint tick) {
            this.CmdClientAckSnapshot(tick);
        }

        public override void SendServerSnapshotToClient(NetworkConnection client, CharacterSnapshotData snapshot)
        {
            this.RpcServerSnapshotToClients(client, snapshot);
        }

        public override void SendServerDiffToClient(NetworkConnection client, CharacterStateDiff diff) {
            this.RpcSendServerDiffToClients(client, diff);
        }
        
        
        [TargetRpc(channel = Channels.Unreliable)]
        private void RpcServerSnapshotToClients(NetworkConnection client, CharacterSnapshotData snapshot)
        {
            this.OnClientReceiveSnapshot?.Invoke(snapshot);
        }
        
        [TargetRpc(channel = Channels.Unreliable)]
        private void RpcSendServerDiffToClients(NetworkConnection client, CharacterStateDiff diff)
        {
            this.OnClientReceiveDiff?.Invoke(diff);
        }

        [Command(channel = Channels.Unreliable)]
        private void CmdClientInputToServer(CharacterInputData input)
        {
            this.OnServerReceiveInput?.Invoke(input);
        }

        [Command(channel = Channels.Unreliable)]
        private void CmdClientSnapshotToServer(CharacterSnapshotData snapshot)
        {
            this.OnServerReceiveSnapshot?.Invoke(snapshot);
        }
        
        [Command(channel = Channels.Reliable, requiresAuthority = false)]
        private void CmdClientRequestFullSnapshot(NetworkConnectionToClient sender = null) {
            if (sender == null) return;
            this.OnServerReceiveFullSnapshotRequest?.Invoke(sender.connectionId);
        }

        [Command(channel = Channels.Reliable, requiresAuthority = false)]
        private void CmdClientAckSnapshot(uint tick, NetworkConnectionToClient sender = null) {
            if (sender == null) return;
            this.OnServerReceiveSnapshotAck?.Invoke(sender.connectionId, tick);
        }
    }
}