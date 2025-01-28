using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine.Scripting;

namespace Code.Network
{
    [LuauAPI][Preserve]
    public class UdpPingTool
    {
        public static async Task<long> GetPing(string serverUrl, int timeoutMilliseconds)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                throw new ArgumentException("Server URL cannot be null or empty.", nameof(serverUrl));
            }
            var parts = serverUrl.Split(':');
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var ipAddress) || !int.TryParse(parts[1], out var port))
            {
                throw new ArgumentException("Invalid server URL format. Expected format: IP:Port", nameof(serverUrl));
            }

            using (var udpClient = new UdpClient())
            {
                udpClient.Client.ReceiveTimeout = timeoutMilliseconds;
                var endpoint = new IPEndPoint(ipAddress, port);

                var uniqueId = Guid.NewGuid().ToByteArray();
                var stopwatch = new Stopwatch();

                try
                {
                    var receiveTask = udpClient.ReceiveAsync();
                    await udpClient.SendAsync(uniqueId, uniqueId.Length, endpoint);
                    
                    stopwatch.Start();
                    if (await Task.WhenAny(receiveTask, Task.Delay(timeoutMilliseconds)) == receiveTask)
                    {
                        var response = receiveTask.Result;
                        stopwatch.Stop();
                        
                        if (response.Buffer.SequenceEqual(uniqueId))
                        {
                            return stopwatch.ElapsedMilliseconds;
                        }
                        else
                        {
                            throw new Exception("Invalid response received from server.");
                        }
                    }
                    else
                    {
                        throw new TimeoutException("Ping request timed out.");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error during ping: {ex.Message}", ex);
                }
            }
        }
    }
}