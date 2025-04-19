using System;
using System.Net.WebSockets;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Airship.Editor {
    public delegate void WebsocketDataReceivedEvent(string data);
    public delegate void WebsocketReadyEvent();

    public class WebsocketClient {
        private ClientWebSocket webSocket;
        public event WebsocketDataReceivedEvent WebsocketDataReceived;
        public event WebsocketReadyEvent WebsocketReady;
        
        private const int sendChunkSize = 2048;
        private const int receiveChunkSize = 2048;
        
        public Uri Uri { get; private set; }
        
        public WebsocketClient(Uri uri) {
            webSocket = new ClientWebSocket();
            Uri = uri;
        }

        public async Task ConnectAsync() {
            try {
                await webSocket.ConnectAsync(Uri, CancellationToken.None);
                WebsocketReady?.Invoke();
                await Task.WhenAll(Receive());
            }
            catch (Exception ex) {
                Debug.LogException(ex);
            }
            finally {
                webSocket.Dispose();
                webSocket = null;
            }
        }

        public async Task CloseAsync() {
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }

        private async Task Receive() {
            var buffer = new byte[receiveChunkSize];
            while (webSocket.State == WebSocketState.Open) {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (webSocket.State == WebSocketState.Closed) {
                    break;
                } else if (result.MessageType == WebSocketMessageType.Close) {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty,
                        CancellationToken.None);
                }
                else {
                    var data = Encoding.UTF8.GetString(buffer);
                    WebsocketDataReceived?.Invoke(data);
                }
            }
        }
        
        private void SendJson(object data) {
            var encoded = JsonConvert.SerializeObject(data);
            webSocket.SendAsync(Encoding.UTF8.GetBytes(encoded), WebSocketMessageType.Text, true,
                new CancellationToken());
        }

        public void CompileAllFiles() {
            SendJson(new {
                eventName = "requestCompile",
            });
        }
    }
}