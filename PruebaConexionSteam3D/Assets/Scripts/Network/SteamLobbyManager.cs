using FishNet.Managing;
using FishNet.Transporting.FishySteamworks;
using Steamworks;
using TMPro;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;
using static UnityEngine.UI.Image;

public class SteamLobbyManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI hostNameText;
    public TextMeshProUGUI playerNameText;

    private NetworkManager _networkManager;
    private FishySteamworks _transport;
    private CSteamID _lobbyId;
    private const int MAX_PLAYERS = 2;
    private bool _isHost = false;

    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();
        _transport = GetComponent<FishySteamworks>();

        if (_networkManager == null)
        {
            Debug.LogError("NetworkManager no encontrado!");
        }
        if (_transport == null)
        {
            Debug.LogError("FishySteamworks no encontrado!");
        }
    }

    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam no esta inicializado!");
            return;
        }

        // Suscribirse a eventos de conexión del servidor
        if (_networkManager != null && _networkManager.ServerManager != null)
        {
            _networkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
        }

        Invoke("StartAsHost", 0.5f);
    }

    private void StartAsHost()
    {
        _isHost = true;
        string steamUsername = SteamFriends.GetPersonaName();

        if (hostNameText != null)
            hostNameText.text = "Host: " + steamUsername;

        _networkManager.ServerManager.StartConnection();

        CreateSteamLobby();

        Debug.Log(steamUsername + " se conecto como host");
    }

    private void CreateSteamLobby()
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MAX_PLAYERS);
        Debug.Log("Creando lobby de Steam...");
    }

    public void OpenInviteDialog()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam no esta inicializado!");
            return;
        }

        if (_lobbyId.m_SteamID == 0)
        {
            Debug.LogError("No hay lobby creado todavia!");
            return;
        }

        SteamFriends.SetRichPresence("connect", "+connect_lobby " + _lobbyId.m_SteamID);
        SteamFriends.SetRichPresence("steam_display", "#StatusWithConnect");

        SteamFriends.ActivateGameOverlay("friends");

        Debug.Log("Overlay abierto. Tu amigo puede:");
        Debug.Log("1. Ver tu perfil - Click derecho - Unirse a la partida");
        Debug.Log("2. O hacer click en Unirse desde el chat");
    }

    private Callback<LobbyCreated_t> _lobbyCreatedCallback;
    private Callback<LobbyEnter_t> _lobbyEnterCallback;
    private Callback<LobbyChatUpdate_t> _lobbyChatUpdateCallback;
    private Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequestedCallback;
    private Callback<GameRichPresenceJoinRequested_t> _gameRichPresenceJoinRequestedCallback;
    private Callback<LobbyDataUpdate_t> _lobbyDataUpdateCallback;

    private void OnEnable()
    {
        _lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
        _lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        _gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        _gameRichPresenceJoinRequestedCallback = Callback<GameRichPresenceJoinRequested_t>.Create(OnGameRichPresenceJoinRequested);
        _lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
    }

    private void OnDisable()
    {
        if (_networkManager != null && _networkManager.ServerManager != null)
        {
            _networkManager.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;
        }
    }

    private void ServerManager_OnRemoteConnectionState(FishNet.Connection.NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped)
        {
            Debug.Log("Cliente desconectado: " + conn.ClientId);

            // Limpiar UI
            if (playerNameText != null)
            {
                playerNameText.text = "Player: ---";
            }

            // Reiniciar todo el lobby
            Debug.Log("Reiniciando lobby...");
            ReiniciarLobby();
        }
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("Error al crear el lobby de Steam!");
            return;
        }

        _lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        Debug.Log("Lobby de Steam creado exitosamente! ID: " + _lobbyId);

        SteamMatchmaking.SetLobbyData(_lobbyId, "HostAddress", SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(_lobbyId, "name", "Partida de " + SteamFriends.GetPersonaName());
    }

    private void OnLobbyEnter(LobbyEnter_t callback)
    {
        _lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        Debug.Log("Entraste al lobby: " + _lobbyId);

        CSteamID ownerID = SteamMatchmaking.GetLobbyOwner(_lobbyId);
        bool isOwner = (ownerID == SteamUser.GetSteamID());

        Debug.Log("Eres el owner del lobby? " + isOwner);

        if (!isOwner)
        {
            _isHost = false;

            string hostAddress = SteamMatchmaking.GetLobbyData(_lobbyId, "HostAddress");
            ulong hostId;
            if (ulong.TryParse(hostAddress, out hostId))
            {
                CSteamID hostSteamID = new CSteamID(hostId);
                Debug.Log("Conectando al host: " + hostSteamID);

                string playerName = SteamFriends.GetPersonaName();

                if (hostNameText != null)
                {
                    string hostName = SteamFriends.GetFriendPersonaName(hostSteamID);
                    hostNameText.text = "Host: " + hostName;
                }

                if (playerNameText != null)
                    playerNameText.text = "Player: " + playerName;

                _transport.ConnectToHost(hostSteamID);
                _networkManager.ClientManager.StartConnection();

                Debug.Log("CONECTADO COMO CLIENTE!");
            }
        }
        else
        {
            Debug.Log("Eres el host de este lobby");
            _isHost = true;
        }
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("INVITACION ACEPTADA! Uniendose al lobby: " + callback.m_steamIDLobby);

        CancelInvoke("StartAsHost");

        _isHost = false;

        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnGameRichPresenceJoinRequested(GameRichPresenceJoinRequested_t callback)
    {
        Debug.Log("Rich Presence Join! Connect string: " + callback.m_rgchConnect);

        if (_isHost)
        {
            _networkManager.ServerManager.StopConnection(true);
            _isHost = false;
            Debug.Log("Deteniendo servidor para convertirse en cliente...");
        }

        string connectStr = callback.m_rgchConnect;
        if (connectStr.Contains("+connect_lobby"))
        {
            string[] parts = connectStr.Split(' ');
            foreach (string part in parts)
            {
                ulong lobbyId;
                if (ulong.TryParse(part, out lobbyId))
                {
                    Debug.Log("Uniendose al lobby: " + lobbyId);
                    SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
                    return;
                }
            }
        }
    }

    private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
    {
        if (callback.m_ulSteamIDLobby != _lobbyId.m_SteamID)
            return;

        CSteamID userID = new CSteamID(callback.m_ulSteamIDUserChanged);
        string userName = SteamFriends.GetFriendPersonaName(userID);

        EChatMemberStateChange stateChange = (EChatMemberStateChange)callback.m_rgfChatMemberStateChange;

        if (stateChange == EChatMemberStateChange.k_EChatMemberStateChangeEntered)
        {
            if (userID != SteamUser.GetSteamID())
            {
                Debug.Log(userName + " se unio al lobby!");

                if (_isHost && playerNameText != null)
                {
                    playerNameText.text = "Player: " + userName;
                }

                int numPlayers = SteamMatchmaking.GetNumLobbyMembers(_lobbyId);
                Debug.Log("Jugadores en el lobby: " + numPlayers + "/" + MAX_PLAYERS);

                if (numPlayers >= MAX_PLAYERS)
                {
                    SteamMatchmaking.SetLobbyJoinable(_lobbyId, false);
                    Debug.Log("Lobby lleno.");
                }
            }
        }
        else if (stateChange == EChatMemberStateChange.k_EChatMemberStateChangeLeft ||
                 stateChange == EChatMemberStateChange.k_EChatMemberStateChangeDisconnected)
        {
            if (userID != SteamUser.GetSteamID())
            {
                Debug.Log(userName + " salio del lobby.");

                if (_isHost && playerNameText != null)
                {
                    playerNameText.text = "Player: ---";
                }

                int numPlayers = SteamMatchmaking.GetNumLobbyMembers(_lobbyId);
                if (numPlayers < MAX_PLAYERS)
                {
                    SteamMatchmaking.SetLobbyJoinable(_lobbyId, true);
                }
            }
        }
    }

    private void ReiniciarLobby()
    {
        // Cerrar sesiones P2P
        SteamNetworking.CloseP2PChannelWithUser(new CSteamID(0), 0);

        // Salir del lobby actual si existe
        if (_lobbyId.m_SteamID != 0)
        {
            SteamMatchmaking.LeaveLobby(_lobbyId);
            _lobbyId = CSteamID.Nil;
        }

        // Detener servidor
        if (_networkManager.ServerManager.Started)
        {
            _networkManager.ServerManager.StopConnection(true);
        }

        // Detener cliente por si acaso
        if (_networkManager.ClientManager.Started)
        {
            _networkManager.ClientManager.StopConnection();
        }

        // Limpiar UI
        if (playerNameText != null)
        {
            playerNameText.text = "Player: ---";
        }

        // Esperar un poco y reiniciar como host
        Invoke("StartAsHost", 1f);
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
    {
        if (callback.m_ulSteamIDLobby != _lobbyId.m_SteamID)
            return;

        CSteamID newOwner = SteamMatchmaking.GetLobbyOwner(_lobbyId);

        // Si ahora somos el owner y antes no lo eramos
        if (newOwner == SteamUser.GetSteamID() && !_isHost)
        {
            Debug.Log("El host se desconecto. Convirtiendose en el nuevo host...");
            ConvertirseEnHost();
        }
    }

    private void ConvertirseEnHost()
    {
        // Detener cliente
        if (_networkManager.ClientManager.Started)
        {
            _networkManager.ClientManager.StopConnection();
        }

        // Salir del lobby actual
        if (_lobbyId.m_SteamID != 0)
        {
            SteamMatchmaking.LeaveLobby(_lobbyId);
            _lobbyId = CSteamID.Nil;
        }

        // Limpiar UI
        if (playerNameText != null)
        {
            playerNameText.text = "Player: ";
        }

        // Reiniciar como host
        Debug.Log("Reiniciando como nuevo host...");
        Invoke("StartAsHost", 0.5f);
    }
}