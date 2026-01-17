using FishNet.Managing;
using FishNet.Transporting.FishySteamworks;
using Steamworks;
using UnityEngine;
using TMPro;

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

    private void OnEnable()
    {
        _lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
        _lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        _gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        _gameRichPresenceJoinRequestedCallback = Callback<GameRichPresenceJoinRequested_t>.Create(OnGameRichPresenceJoinRequested);
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
}