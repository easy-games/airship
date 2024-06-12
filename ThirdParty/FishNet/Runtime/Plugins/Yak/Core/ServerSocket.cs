using FishNet.Connection;
using FishNet.Transporting.Yak.Client;
using System;
using System.Collections.Generic;

namespace FishNet.Transporting.Yak.Server
{
    /// <summary>
    /// Creates a fake socket acting as server.
    /// </summary>
    public class ServerSocket : CommonSocket
    {
        #region Public.
        /// <summary>
        /// Gets the current ConnectionState of a remote client on the server.
        /// </summary>
        /// <param name="connectionId">ConnectionId to get ConnectionState for.</param>
        internal RemoteConnectionState GetConnectionState(int connectionId)
        {
            if (connectionId != NetworkConnection.SIMULATED_CLIENTID_VALUE)
                return RemoteConnectionState.Stopped;

            LocalConnectionState state = _client.GetLocalConnectionState();
            return (state == LocalConnectionState.Started) ? RemoteConnectionState.Started :
                RemoteConnectionState.Stopped;
        }
        #endregion

        #region Private.
        /// <summary>
        /// Packets received from local client.
        /// </summary>
        private Queue<LocalPacket> _incoming = new Queue<LocalPacket>();
        /// <summary>
        /// Socket for client.
        /// </summary>
        private ClientSocket _client;
        #endregion

        //PROSTART
        /// <summary>
        /// Initializes this for use.
        /// </summary>
        internal override void Initialize(Transport t, CommonSocket socket)
        {
            base.Initialize(t, socket);
            _client = (ClientSocket)socket;
        }
        //PROEND

        /// <summary>
        /// Starts the server.
        /// </summary>
        internal bool StartConnection()
        {
            //PROSTART
            SetLocalConnectionState(LocalConnectionState.Starting, true);
            SetLocalConnectionState(LocalConnectionState.Started, true);
            //PROEND
            return true;
        }


        //PROstart
        /// <summary>
        /// Sets a new connection state.
        /// </summary>
        protected override void SetLocalConnectionState(LocalConnectionState connectionState, bool server)
        {
            base.SetLocalConnectionState(connectionState, server);
            _client.OnLocalServerConnectionState(connectionState);
        }
        //PROEND

        /// <summary>
        /// Stops the local socket.
        /// </summary>
        internal bool StopConnection()
        {
            //PROSTART
            if (base.GetLocalConnectionState() == LocalConnectionState.Stopped)
                return false;

            base.ClearQueue(ref _incoming);
            SetLocalConnectionState(LocalConnectionState.Stopping, true);
            SetLocalConnectionState(LocalConnectionState.Stopped, true);
            //PROEND
            return true;
        }

        /// <summary>
        /// Stops a remote client from the server, disconnecting the client.
        /// </summary>
        /// <param name="connectionId">ConnectionId of the client to disconnect.</param>
        internal bool StopConnection(int connectionId)
        {
            //PROSTART
            if (connectionId != NetworkConnection.SIMULATED_CLIENTID_VALUE)
                return false;

            _client.StopConnection();
            //PROEND
            return true;
        }

        //PROSTART
        /// <summary>
        /// Iterates the Incoming queue.
        /// </summary>
        /// <param name="transport"></param>
        internal void IterateIncoming()
        {
            if (base.GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            //Iterate local client packets first.
            while (_incoming.Count > 0)
            {
                LocalPacket packet = _incoming.Dequeue();
                ArraySegment<byte> segment = new ArraySegment<byte>(packet.Data, 0, packet.Length);
                ServerReceivedDataArgs args = new ServerReceivedDataArgs(segment, (Channel)packet.Channel, NetworkConnection.SIMULATED_CLIENTID_VALUE, base.Transport.Index);
                base.Transport.HandleServerReceivedDataArgs(args);
            }
        }
        //PROEND

        //PROSTART
        /// <summary>
        /// Sends data to a client.
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="segment"></param>
        /// <param name="connectionId"></param>
        internal void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            if (base.GetLocalConnectionState() != LocalConnectionState.Started)
                return;
            if (connectionId != NetworkConnection.SIMULATED_CLIENTID_VALUE)
                return;

            LocalPacket packet = new LocalPacket(segment, channelId);
            _client.ReceivedFromLocalServer(packet);
        }
        //PROEND

        #region Local client.
        //PROSTART
        /// <summary>
        /// Called when the local client starts or stops.
        /// </summary>
        internal void OnLocalClientConnectionState(LocalConnectionState state)
        {
            //If not started flush incoming from local client.
            if (state != LocalConnectionState.Started)
            {
                base.ClearQueue(ref _incoming);
                //If stopped then send stopped event as well.
                if (state == LocalConnectionState.Stopped)
                    base.Transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(RemoteConnectionState.Stopped, NetworkConnection.SIMULATED_CLIENTID_VALUE, base.Transport.Index));
            }
            else
            {
                base.Transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(RemoteConnectionState.Started, NetworkConnection.SIMULATED_CLIENTID_VALUE, base.Transport.Index));
            }
        }
        //PROEND

        //PROSTART
        /// <summary>
        /// Queues a received packet from the local client.
        /// </summary>
        internal void ReceivedFromLocalClient(LocalPacket packet)
        {
            if (_client.GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            _incoming.Enqueue(packet);
        }
        //PROEND
        #endregion
    }
}