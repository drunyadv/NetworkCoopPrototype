using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkManager))]
[RequireComponent(typeof(UnityTransport))]
public sealed class ConnectionService : MonoBehaviour
{
    public static ConnectionService Instance { get; private set; }

    public event Action<string> StatusChanged;

    [SerializeField] private NetworkSessionState sessionPrefab;
    [SerializeField] private NetworkSceneLoader sceneLoader;

    private const int MaxPlayers = 4;
    private const string MenuScene = "1MainMenu";
    private const string LobbyScene = "2Lobby";
    private const string GameScene = "3Game";

    private NetworkManager networkManager;
    private UnityTransport transport;

    private readonly Dictionary<ulong, PlayerCredentials> pendingPlayers = new();

    private PlayerCredentials hostCredentials;
    private bool returningToMenu;

    public string LastMessage { get; private set; } = "Not connected";

    public bool IsHost => networkManager != null && networkManager.IsHost;
    public bool IsConnected => networkManager != null && networkManager.IsConnectedClient;

    [Serializable]
    private sealed class PlayerCredentials
    {
        public string PlayerId;
        public string Name;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        networkManager = GetComponent<NetworkManager>();
        transport = GetComponent<UnityTransport>();

        networkManager.NetworkConfig.EnableSceneManagement = true;
        networkManager.NetworkConfig.ConnectionApproval = true;

        networkManager.ConnectionApprovalCallback += ApproveConnection;
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public bool StartHost(string playerName, ushort port)
    {
        if (networkManager.IsListening || returningToMenu || port == 0)
            return false;

        if (sessionPrefab == null)
        {
            SetStatus("Session prefab is missing");
            return false;
        }

        if (!TryCreateCredentials(playerName, out hostCredentials))
            return false;

        pendingPlayers.Clear();
        SetConnectionPayload(hostCredentials);

        transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");

        SetStatus("Starting host...");

        if (!networkManager.StartHost())
        {
            SetStatus("Failed to start host");
            return false;
        }
        sceneLoader.RegisterSession();

        NetworkSessionState session = Instantiate(sessionPrefab);
        session.GetComponent<NetworkObject>().Spawn(false);

        session.AddPlayer(new PlayerInfo(
            NetworkManager.ServerClientId,
            hostCredentials.PlayerId,
            hostCredentials.Name
        ));

        var result = networkManager.SceneManager.LoadScene(
            LobbyScene,
            LoadSceneMode.Single
        );

        if (result != SceneEventProgressStatus.Started)
        {
            SetStatus($"Failed to load lobby: {result}");
            StartCoroutine(ReturnToMenuRoutine());
            return false;
        }

        SetStatus("Host started");
        return true;
    }

    public bool Join(string playerName, string address, ushort port)
    {
        if (networkManager.IsListening || returningToMenu || port == 0)
            return false;

        if (string.IsNullOrWhiteSpace(address))
        {
            SetStatus("Enter a server address");
            return false;
        }

        if (!TryCreateCredentials(playerName, out var credentials))
            return false;

        SetConnectionPayload(credentials);
        transport.SetConnectionData(address.Trim(), port);

        SetStatus("Connecting...");

        if (!networkManager.StartClient())
        {
            SetStatus("Failed to start client");
            return false;
        }
        sceneLoader.RegisterSession();

        return true;
    }

    public void StartGame()
    {
        sceneLoader.StartGame();
    }

    public void Leave()
    {
        if (returningToMenu)
            return;

        SetStatus("Disconnected");
        StartCoroutine(ReturnToMenuRoutine());
    }

    private void ApproveConnection(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        response.CreatePlayerObject = false;
        response.Pending = false;

        if (request.ClientNetworkId == NetworkManager.ServerClientId)
        {
            response.Approved = true;
            return;
        }


    NetworkSessionState session = NetworkSessionState.Instance;

    if (session != null && session.Phase.Value == SessionPhase.Loading)
    {
        Reject(response, "Match is loading. Try again shortly.");
        return;
    }


        if (networkManager.ConnectedClientsIds.Count + pendingPlayers.Count >= MaxPlayers)
        {
            Reject(response, "Session is full");
            return;
        }

        if (!TryReadCredentials(request.Payload, out var credentials))
        {
            Reject(response, "Invalid player data");
            return;
        }

        if (NetworkSessionState.Instance != null &&
            NetworkSessionState.Instance.ContainsPlayerId(credentials.PlayerId))
        {
            Reject(response, "Player is already connected");
            return;
        }

        foreach (var player in pendingPlayers.Values)
        {
            if (player.PlayerId == credentials.PlayerId)
            {
                Reject(response, "Player is already connecting");
                return;
            }
        }

        pendingPlayers[request.ClientNetworkId] = credentials;
        response.Approved = true;
    }

    private static void Reject(
        NetworkManager.ConnectionApprovalResponse response,
        string reason)
    {
        response.Approved = false;
        response.Reason = reason;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (networkManager.IsServer)
        {
            if (clientId == NetworkManager.ServerClientId)
                return;

            if (pendingPlayers.TryGetValue(clientId, out var credentials))
            {
                pendingPlayers.Remove(clientId);

                NetworkSessionState.Instance?.AddPlayer(new PlayerInfo(
                    clientId,
                    credentials.PlayerId,
                    credentials.Name
                ));
            }

            return;
        }

        if (clientId == networkManager.LocalClientId)
            SetStatus("Connected");
    }

    
    private void OnClientDisconnected(ulong clientId)
    {
        if (returningToMenu)
            return;

        if (networkManager.IsServer)
        {
            pendingPlayers.Remove(clientId);
            NetworkSessionState.Instance?.RemovePlayer(clientId);
            return;
        }

        string reason = networkManager.DisconnectReason;

        SetStatus(string.IsNullOrEmpty(reason)
            ? "Connection to host lost"
            : reason);

        StartCoroutine(ReturnToMenuRoutine());
    }


    private bool TryCreateCredentials(
        string playerName,
        out PlayerCredentials credentials)
    {
        credentials = null;

        string name = playerName?.Trim() ?? string.Empty;

        if (name.Length == 0 ||
            name.Length > 24 ||
            Encoding.UTF8.GetByteCount(name) > 60)
        {
            SetStatus("Name must be 1-24 characters");
            return false;
        }

        string key = GetPlayerIdKey();
        string id = PlayerPrefs.GetString(key, "");

        if (!Guid.TryParseExact(id, "N", out _))
        {
            id = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(key, id);
            PlayerPrefs.Save();
        }

        credentials = new PlayerCredentials
        {
            PlayerId = id,
            Name = name
        };

        return true;
    }

    private static string GetPlayerIdKey()
    {
        foreach (string arg in Environment.GetCommandLineArgs())
        {
            if (arg.StartsWith("--profile=", StringComparison.OrdinalIgnoreCase))
                return "NetworkCoop.PlayerId." + arg.Substring(10);
        }

        return "NetworkCoop.PlayerId.default";
    }

    private void SetConnectionPayload(PlayerCredentials credentials)
    {
        string json = JsonUtility.ToJson(credentials);
        networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(json);
    }

    private static bool TryReadCredentials(
        byte[] payload,
        out PlayerCredentials credentials)
    {
        credentials = null;

        if (payload == null || payload.Length == 0 || payload.Length > 512)
            return false;

        try
        {
            string json = Encoding.UTF8.GetString(payload);
            credentials = JsonUtility.FromJson<PlayerCredentials>(json);
        }
        catch (Exception)
        {
            return false;
        }

        if (credentials == null ||
            !Guid.TryParseExact(credentials.PlayerId, "N", out _) ||
            string.IsNullOrWhiteSpace(credentials.Name))
        {
            return false;
        }

        string name = credentials.Name;

        return name == name.Trim() &&
               name.Length <= 24 &&
               Encoding.UTF8.GetByteCount(name) <= 60;
    }

    private IEnumerator ReturnToMenuRoutine()
    {
        if (returningToMenu)
            yield break;

        returningToMenu = true;
        sceneLoader.ResetSession();

        if (networkManager.IsListening)
            networkManager.Shutdown();

        while (networkManager.ShutdownInProgress)
            yield return null;

        pendingPlayers.Clear();
        SceneManager.LoadScene(MenuScene);

        returningToMenu = false;
    }

    private void SetStatus(string message)
    {
        LastMessage = message;
        Debug.Log($"[Connection] {message}");
        StatusChanged?.Invoke(message);
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        if (networkManager != null)
        {
            networkManager.ConnectionApprovalCallback -= ApproveConnection;
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        Instance = null;
    }
}