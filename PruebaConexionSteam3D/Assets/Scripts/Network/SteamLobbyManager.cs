using FishNet.Managing;
using FishNet.Transporting.FishySteamworks;
using Steamworks;
using UnityEngine;

public class SteamLobbyManager : MonoBehaviour
{
    private NetworkManager _networkManager;

    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();

        if (_networkManager == null)
        {
            Debug.LogError("NetworkManager no encontrado en el mismo GameObject!");
        }
    }

    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam no está inicializado!");
            return;
        }

        // Obtener nombre del usuario de Steam
        string steamUsername = SteamFriends.GetPersonaName();

        // Iniciar el servidor automáticamente
        _networkManager.ServerManager.StartConnection();

        // Log de confirmación
        Debug.Log($"{steamUsername} se conectó como host");
    }
}