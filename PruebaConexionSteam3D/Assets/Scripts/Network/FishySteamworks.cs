using FishNet.Managing;
using FishNet.Transporting;
using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Transporting.FishySteamworks
{
    public class FishySteamworks : Transport
    {
        private bool _isInitialized = false;
        private bool _serverStarted = false;
        private bool _clientStarted = false;

        private CSteamID _hostSteamID;
        private Dictionary<int, CSteamID> _connectedClients = new Dictionary<int, CSteamID>();
        private int _nextConnectionId = 0;

        #region Events requeridos por Transport
        public override event Action<ClientConnectionStateArgs> OnClientConnectionState;
        public override event Action<ServerConnectionStateArgs> OnServerConnectionState;
        public override event Action<RemoteConnectionStateArgs> OnRemoteConnectionState;
        public override event Action<ClientReceivedDataArgs> OnClientReceivedData;
        public override event Action<ServerReceivedDataArgs> OnServerReceivedData;
        #endregion

        private Callback<P2PSessionRequest_t> _p2pSessionRequestCallback;
        private Callback<P2PSessionConnectFail_t> _p2pSessionConnectFailCallback;

        public override void Initialize(NetworkManager networkManager, int transportIndex)
        {
            base.Initialize(networkManager, transportIndex);

            if (!SteamAPI.Init())
            {
                Debug.LogError("Steam API failed to initialize!");
                return;
            }

            _p2pSessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
            _p2pSessionConnectFailCallback = Callback<P2PSessionConnectFail_t>.Create(OnP2PSessionConnectFail);

            _isInitialized = true;
            Debug.Log("FishySteamworks initialized successfully");
        }

        public override void Shutdown()
        {
            if (_isInitialized)
            {
                SteamAPI.Shutdown();
                _isInitialized = false;
            }
        }

        private void OnP2PSessionRequest(P2PSessionRequest_t param)
        {
            Debug.Log($"P2P Session request from {param.m_steamIDRemote}");
            SteamNetworking.AcceptP2PSessionWithUser(param.m_steamIDRemote);
        }

        private void OnP2PSessionConnectFail(P2PSessionConnectFail_t param)
        {
            Debug.LogError($"P2P connection failed with {param.m_steamIDRemote}: {param.m_eP2PSessionError}");
        }

        public override LocalConnectionState GetConnectionState(bool server)
        {
            if (server && _serverStarted)
                return LocalConnectionState.Started;
            if (!server && _clientStarted)
                return LocalConnectionState.Started;

            return LocalConnectionState.Stopped;
        }

        public override RemoteConnectionState GetConnectionState(int connectionId)
        {
            if (_connectedClients.ContainsKey(connectionId))
                return RemoteConnectionState.Started;
            return RemoteConnectionState.Stopped;
        }

        private void Update()
        {
            if (_isInitialized)
            {
                SteamAPI.RunCallbacks();
            }
        }

        public override void HandleClientReceivedDataArgs(ClientReceivedDataArgs args)
        {
            OnClientReceivedData?.Invoke(args);
        }

        public override void HandleServerReceivedDataArgs(ServerReceivedDataArgs args)
        {
            OnServerReceivedData?.Invoke(args);
        }

        public override void HandleClientConnectionState(ClientConnectionStateArgs args)
        {
            OnClientConnectionState?.Invoke(args);
        }

        public override void HandleServerConnectionState(ServerConnectionStateArgs args)
        {
            OnServerConnectionState?.Invoke(args);
        }

        public override void HandleRemoteConnectionState(RemoteConnectionStateArgs args)
        {
            OnRemoteConnectionState?.Invoke(args);
        }

        public override void IterateIncoming(bool server)
        {
            if (!_isInitialized) return;

            uint packetSize;
            while (SteamNetworking.IsP2PPacketAvailable(out packetSize))
            {
                byte[] buffer = new byte[packetSize];
                CSteamID senderId;

                if (SteamNetworking.ReadP2PPacket(buffer, packetSize, out _, out senderId))
                {
                    if (server)
                    {
                        // Servidor recibe datos
                        int connectionId = GetConnectionId(senderId);
                        if (connectionId == -1)
                        {
                            // Nueva conexión
                            connectionId = _nextConnectionId++;
                            _connectedClients[connectionId] = senderId;

                            RemoteConnectionStateArgs connArgs = new RemoteConnectionStateArgs(
                                RemoteConnectionState.Started,
                                connectionId,
                                Index
                            );
                            OnRemoteConnectionState?.Invoke(connArgs);

                            Debug.Log($"Client {senderId} connected with ID {connectionId}");
                        }

                        ServerReceivedDataArgs dataArgs = new ServerReceivedDataArgs(
                            new ArraySegment<byte>(buffer),
                            0,
                            connectionId,
                            Index
                        );
                        OnServerReceivedData?.Invoke(dataArgs);
                    }
                    else
                    {
                        // Cliente recibe datos
                        ClientReceivedDataArgs dataArgs = new ClientReceivedDataArgs(
                            new ArraySegment<byte>(buffer),
                            0,
                            Index
                        );
                        OnClientReceivedData?.Invoke(dataArgs);
                    }
                }
            }
        }

        public override void IterateOutgoing(bool server) { }

        public override void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            if (!_clientStarted || !_isInitialized) return;

            byte[] data = new byte[segment.Count];
            Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);

            SteamNetworking.SendP2PPacket(_hostSteamID, data, (uint)data.Length,
                EP2PSend.k_EP2PSendReliable);
        }

        public override void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            if (!_serverStarted || !_isInitialized) return;

            if (_connectedClients.TryGetValue(connectionId, out CSteamID clientId))
            {
                byte[] data = new byte[segment.Count];
                Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);

                SteamNetworking.SendP2PPacket(clientId, data, (uint)data.Length,
                    EP2PSend.k_EP2PSendReliable);
            }
        }

        public void ConnectToHost(CSteamID hostId)
        {
            _hostSteamID = hostId;
            _clientStarted = true;

            // Enviar primer paquete para iniciar conexión P2P
            byte[] connectPacket = new byte[1] { 0 };
            SteamNetworking.SendP2PPacket(hostId, connectPacket, 1, EP2PSend.k_EP2PSendReliable);

            ClientConnectionStateArgs args = new ClientConnectionStateArgs(
                LocalConnectionState.Started,
                Index
            );
            OnClientConnectionState?.Invoke(args);

            Debug.Log($"Connecting to host {hostId}");
        }

        public override bool StartConnection(bool server)
        {
            if (server && _isInitialized)
            {
                Debug.Log("Starting as server...");
                _serverStarted = true;

                ServerConnectionStateArgs args = new ServerConnectionStateArgs(
                    LocalConnectionState.Started,
                    Index
                );
                OnServerConnectionState?.Invoke(args);

                return true;
            }
            return false;
        }

        public override bool StopConnection(bool server)
        {
            if (server)
            {
                _serverStarted = false;
                foreach (var client in _connectedClients.Values)
                {
                    SteamNetworking.CloseP2PSessionWithUser(client);
                }
                _connectedClients.Clear();
            }
            else
            {
                _clientStarted = false;
                if (_hostSteamID.IsValid())
                {
                    SteamNetworking.CloseP2PSessionWithUser(_hostSteamID);
                }
            }
            return true;
        }

        public override bool StopConnection(int connectionId, bool server)
        {
            if (_connectedClients.TryGetValue(connectionId, out CSteamID clientId))
            {
                SteamNetworking.CloseP2PSessionWithUser(clientId);
                _connectedClients.Remove(connectionId);
                return true;
            }
            return false;
        }

        private int GetConnectionId(CSteamID steamId)
        {
            foreach (var kvp in _connectedClients)
            {
                if (kvp.Value == steamId)
                    return kvp.Key;
            }
            return -1;
        }

        public override string GetConnectionAddress(int connectionId)
        {
            if (_connectedClients.TryGetValue(connectionId, out CSteamID steamId))
                return steamId.ToString();
            return string.Empty;
        }

        public override void SetMaximumClients(int value) { }

        public override int GetMTU(byte channel)
        {
            return 1200;
        }
    }
}