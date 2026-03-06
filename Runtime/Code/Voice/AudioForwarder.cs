using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Adrenak.BRW;
using Code.Platform.Shared;
using UnityEngine;

namespace Code.Voice {
    /// <summary>
    /// Forwards raw Opus audio frames over UDP to an external service.
    ///
    /// All packets: [0] u8 msg_type, [1] u8 version
    ///
    /// Init (0x01 v1) — sent on first audio per user, re-sent every ~2s:
    ///   [2..10]  u64    connection_id
    ///   [10..14] i32    frequency
    ///   [14..18] i32    channel_count
    ///   [18..]          null-terminated strings: user_id, org_id, game_id, server_id
    ///
    /// Audio (0x02 v1) — per frame:
    ///   [2..10]  u64    connection_id
    ///   [10..14] u32    sequence
    ///   [14..22] i64    timestamp_ms (unix epoch)
    ///   [22..]          payload (raw Opus frame bytes)
    /// </summary>
    public class AudioForwarder : IDisposable {
        const byte MSG_INIT = 0x01;
        const byte MSG_AUDIO = 0x02;
        const byte VERSION = 1;
        const int AUDIO_HEADER_LEN = 22;
        const int MAX_STRING_LEN = 128;
        const float INIT_RESEND_SEC = 2f;
        private readonly UdpClient udp;
        private readonly string host;
        private readonly int port;
        private readonly Func<int, string> resolveUserId;
        private readonly string orgId;
        private readonly string gameId;
        private readonly string serverId;

        struct Session {
            public ulong connId;
            public float lastInitTime;
            public uint seq;
            public bool initFailed;
        }

        readonly Dictionary<int, Session> sessions = new();
        static readonly System.Random rng = new();

        AudioForwarder(string host, int port, Func<int, string> resolveUserId, string orgId, string gameId, string serverId) {
            udp = new UdpClient();
            this.host = host;
            this.port = port;
            this.resolveUserId = resolveUserId;
            this.orgId = orgId ?? "";
            this.gameId = gameId ?? "";
            this.serverId = serverId ?? "";
        }

        /// <summary>
        /// Creates a forwarder if voice moderation is enabled (see AirshipPlatformUrl).
        /// Returns null when disabled.
        /// </summary>
        public static AudioForwarder Create(Func<int, string> resolveUserId, string orgId, string gameId, string serverId) {
            if (!AirshipPlatformUrl.voiceModerationEnabled) {
                Debug.Log("[AudioForwarder] Voice moderation disabled");
                return null;
            }

            var host = AirshipPlatformUrl.moderationStreamHost;
            var port = AirshipPlatformUrl.moderationStreamPort;

            Debug.Log($"[AudioForwarder] Forwarding to {host}:{port}");
            return new AudioForwarder(host, port, resolveUserId, orgId, gameId, serverId);
        }

        public void Send(int mirrorConnId, byte[] messageData) {
            var userId = resolveUserId?.Invoke(mirrorConnId);
            if (string.IsNullOrEmpty(userId)) return;

            // Parse UniVoice BRW wire format
            var reader = new BytesReader(messageData);
            reader.ReadString();   // skip "AUDIO_FRAME" tag
            reader.ReadInt();      // skip sender peer ID
            reader.ReadLong();     // skip UniVoice timestamp
            var frequency = reader.ReadInt();
            var channelCount = reader.ReadInt();
            var samples = reader.ReadByteArray();
            if (samples == null || samples.Length == 0) return;

            // Get or create session
            if (!this.sessions.TryGetValue(mirrorConnId, out var session)) {
                var buf = new byte[8];
                rng.NextBytes(buf);
                session = new Session { connId = BitConverter.ToUInt64(buf, 0) };
                this.sessions[mirrorConnId] = session;
            }

            // Re-send init periodically so receiver always has context
            if (Time.unscaledTime - session.lastInitTime >= INIT_RESEND_SEC) {
                var wasOk = !session.initFailed;
                var error = SendInit(session.connId, userId, frequency, channelCount);
                session.initFailed = error != null;
                if (session.initFailed && wasOk)
                    Debug.LogError($"[AudioForwarder] init failed for connection {mirrorConnId}: {error}");
                session.lastInitTime = Time.unscaledTime;
                this.sessions[mirrorConnId] = session;
            }

            // Skip audio if init couldn't be sent
            if (session.initFailed) return;

            // Audio frame
            var packet = new byte[AUDIO_HEADER_LEN + samples.Length];
            using (var ms = new MemoryStream(packet))
            using (var w = new BinaryWriter(ms)) {
                w.Write(MSG_AUDIO);
                w.Write(VERSION);
                w.Write(session.connId);
                w.Write(session.seq++);
                w.Write(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
            Buffer.BlockCopy(samples, 0, packet, AUDIO_HEADER_LEN, samples.Length);
            this.sessions[mirrorConnId] = session;
            UdpSend(packet);
        }

        /// <returns>null on success, error message on failure</returns>
        string SendInit(ulong connId, string userId, int frequency, int channelCount) {
            var strings = new[] {
                ("userId", userId),
                ("orgId", orgId),
                ("gameId", gameId),
                ("serverId", serverId),
            };
            foreach (var (name, value) in strings) {
                var len = Encoding.UTF8.GetByteCount(value ?? "");
                if (len > MAX_STRING_LEN)
                    return $"{name} is {len} bytes (max {MAX_STRING_LEN})";
            }

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms)) {
                w.Write(MSG_INIT);
                w.Write(VERSION);
                w.Write(connId);
                w.Write(frequency);
                w.Write(channelCount);
                foreach (var (_, value) in strings)
                    WriteNullTerminatedString(w, value);
                UdpSend(ms.ToArray());
            }
            return null;
        }

        void UdpSend(byte[] packet) {
            try {
                udp.SendAsync(packet, packet.Length, host, port);
            } catch (Exception e) {
                Debug.LogError($"[AudioForwarder] {e.Message}");
            }
        }

        static void WriteNullTerminatedString(BinaryWriter w, string value) {
            var bytes = Encoding.UTF8.GetBytes(value ?? "");
            w.Write(bytes);
            w.Write((byte)0);
        }

        public void Dispose() {
            udp?.Dispose();
        }
    }
}
