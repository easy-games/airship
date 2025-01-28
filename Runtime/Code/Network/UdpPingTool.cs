using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Assets.Luau.Network
{
    [LuauAPI]
    public class UdpPingTool
    {
        private const int BufferSize = 32; // Size of the UDP packet.
        private const int TimeoutMilliseconds = 5000; // Timeout for the ping response.

        public static async Task<long> GetPing(string serverUrl)
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
                udpClient.Client.ReceiveTimeout = TimeoutMilliseconds;
                var endpoint = new IPEndPoint(ipAddress, port);

                var uniqueId = Guid.NewGuid().ToString(); // Generate a unique ID for this ping.
                var buffer = Encoding.ASCII.GetBytes(uniqueId); // Send the unique ID as the ping message.
                var stopwatch = new Stopwatch();

                try
                {
                    var receiveTask = udpClient.ReceiveAsync(); // start waiting before we send the packet to avoid race condition
                    await udpClient.SendAsync(buffer, buffer.Length, endpoint);
                    
                    stopwatch.Start();
                    if (await Task.WhenAny(receiveTask, Task.Delay(TimeoutMilliseconds)) == receiveTask)
                    {
                        var response = receiveTask.Result;

                        stopwatch.Stop();

                        // Check if the response matches the sent unique ID.
                        if (Encoding.ASCII.GetString(response.Buffer) == uniqueId)
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