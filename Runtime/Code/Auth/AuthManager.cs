using System.Collections.Generic;
using Code.Auth;
using SocketIOClient;

[LuauAPI]
public class AuthManager : Singleton<AuthManager> {
    private bool isScriptListening = false;
    public string token;
    private List<SocketPacket> queuedPackets;
    public SocketIO socketIO;

    public void Login() {

    }

    public void SetScriptListening(bool val) {
        this.isScriptListening = val;
    }
}