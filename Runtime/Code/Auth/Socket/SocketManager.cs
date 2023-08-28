using System.Collections.Generic;
using Code.Auth;
using SocketIOClient;

public class SocketManager : Singleton<SocketManager> {
    private bool isScriptListening = false;
    private List<SocketPacket> queuedPackets;
    public SocketIO socketIO;

    public void SetScriptListening(bool val) {
        this.isScriptListening = val;
    }
}