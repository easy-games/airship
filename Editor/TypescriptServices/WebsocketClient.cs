using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Airship.Editor {
    public delegate void WebsocketDataReceivedEvent(string data);
    
    public class WebsocketClient {
        private ClientWebSocket webSocket;
        public event WebsocketDataReceivedEvent WebsocketDataReceived;
        
        private const int sendChunkSize = 256;
        private const int receiveChunkSize = 64;
        
        public Uri Uri { get; private set; }
        
        public WebsocketClient(Uri uri) {
            webSocket = new ClientWebSocket();
            Uri = uri;
        }

        public async Task ConnectAsync() {
            try {
                await webSocket.ConnectAsync(Uri, CancellationToken.None);
                await Task.WhenAll(Receive());
            }
            catch (Exception ex) {
                Debug.LogException(ex);
            }
            finally {
                webSocket.Dispose();
            }
        }

        private async Task Receive() {
            byte[] buffer = new byte[receiveChunkSize];
            while (webSocket.State == WebSocketState.Open)
            {                
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                else {
                    var data = Encoding.UTF8.GetString(buffer);
                    WebsocketDataReceived?.Invoke(data);
                }
            }
        }
    }
}