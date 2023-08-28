using System.Collections.Generic;
using Code.Auth;
using SocketIOClient;

public class SocketManager : Singleton<SocketManager> {
    private List<SocketPacket> queuedPackets;
    public SocketIO socketIO;
    private bool isScriptListening = false;

    public void SetScriptListening(bool val) {
        this.isScriptListening = val;
    }
}