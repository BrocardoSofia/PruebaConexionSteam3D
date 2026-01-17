using FishNet.Managing;
using FishNet.Transporting;
using Steamworks;
using System;
using UnityEngine;

namespace FishNet.Transporting.FishySteamworks
{
    public class FishySteamworks : Transport
    {
        private bool _isInitialized = false;
        private bool _serverStarted = false;

        #region Events requeridos por Transport
        public override event Action<ClientConnectionStateArgs> OnClientConnectionState;
        public override event Action<ServerConnectionStateArgs> OnServerConnectionState;
        public override event Action<RemoteConnectionStateArgs> OnRemoteConnectionState;
        public override event Action<ClientReceivedDataArgs> OnClientReceivedData;
        public override event Action<ServerReceivedDataArgs> OnServerReceivedData;
        #endregion

        public override void Initialize(NetworkManager networkManager, int transportIndex)
        {
            base.Initialize(networkManager, transportIndex);

            if (!SteamAPI.Init())
            {
                Debug.LogError("Steam API failed to initialize!");
                return;
            }

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

        public override LocalConnectionState GetConnectionState(bool server)
        {
            if (server && _serverStarted)
                return LocalConnectionState.Started;

            return LocalConnectionState.Stopped;
        }

        public override RemoteConnectionState GetConnectionState(int connectionId)
        {
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

        public override void IterateIncoming(bool server) { }

        public override void IterateOutgoing(bool server) { }

        public override void SendToServer(byte channelId, ArraySegment<byte> segment) { }

        public override void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId) { }

        public override bool StartConnection(bool server)
        {
            if (server && _isInitialized)
            {
                Debug.Log("Starting as server...");
                _serverStarted = true;

                // Invocar evento de conexión del servidor
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
            }
            return true;
        }

        public override bool StopConnection(int connectionId, bool server)
        {
            return true;
        }

        public override string GetConnectionAddress(int connectionId)
        {
            return string.Empty;
        }

        public override void SetMaximumClients(int value) { }

        public override int GetMTU(byte channel)
        {
            return 1200;
        }
    }
}