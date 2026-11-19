using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    private const int MaxPlayers = 4;
    private const string MenuScene = "1MainMenu";
    private const string LobbyScene = "2Lobby";
    private const string GameScene = "3Game";

    private NetworkManager networkManager;
    private UnityTransport transport;

    private readonly HashSet<ulong> pendingApprovals = new();

    private bool returningToMenu;

    public string LastMessage { get; private set; } = "Not connected";

    public bool IsHost => networkManager != null &&
                          networkManager.IsHost;

    public bool IsConnected => networkManager != null &&
                               networkManager.IsConnectedClient;

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

    public bool StartHost(ushort port)
    {
        if (networkManager.IsListening || returningToMenu || port == 0)
            return false;

        pendingApprovals.Clear();

        transport.SetConnectionData(
            "127.0.0.1",
            port,
            "0.0.0.0"
        );

        SetStatus("Starting host...");

        if (!networkManager.StartHost())
        {
            SetStatus("Failed to start host");
            return false;
        }

        var result = networkManager.SceneManager.LoadScene(
            LobbyScene,
            LoadSceneMode.Single
        );

        if (result != SceneEventProgressStatus.Started)
        {
            SetStatus($"Lobby loading failed: {result}");
            StartCoroutine(ReturnToMenuRoutine());
            return false;
        }

        SetStatus("Host started");
        return true;
    }

    public bool Join(string address, ushort port)
    {
        if (networkManager.IsListening ||
            returningToMenu ||
            port == 0 ||
            string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        transport.SetConnectionData(address.Trim(), port);

        SetStatus("Connecting...");

        if (!networkManager.StartClient())
        {
            SetStatus("Failed to start client");
            return false;
        }

        return true;
    }

    public void StartGame()
    {
        if (!networkManager.IsHost)
            return;

        var result = networkManager.SceneManager.LoadScene(
            GameScene,
            LoadSceneMode.Single
        );

        if (result != SceneEventProgressStatus.Started)
            SetStatus($"Game loading failed: {result}");
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
        bool isAlreadyConnected =
            networkManager.ConnectedClientsIds.Contains(request.ClientNetworkId);

        int occupiedSlots =
            networkManager.ConnectedClientsIds.Count +
            pendingApprovals.Count;

        bool hasSpace =
            isAlreadyConnected || occupiedSlots < MaxPlayers;

        response.Approved = hasSpace;
        response.CreatePlayerObject = false;
        response.Pending = false;

        if (!hasSpace)
        {
            response.Reason = "Session is full";
            return;
        }

        if (!isAlreadyConnected)
            pendingApprovals.Add(request.ClientNetworkId);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (networkManager.IsServer)
            pendingApprovals.Remove(clientId);

        if (clientId == networkManager.LocalClientId)
            SetStatus("Connected");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        pendingApprovals.Remove(clientId);

        // The host should not leave when a remote client disconnects.
        if (networkManager.IsServer)
            return;

        if (returningToMenu)
            return;

        string reason = networkManager.DisconnectReason;

        SetStatus(string.IsNullOrEmpty(reason)
            ? "Connection to host lost"
            : reason);

        StartCoroutine(ReturnToMenuRoutine());
    }

    private IEnumerator ReturnToMenuRoutine()
    {
        if (returningToMenu)
            yield break;

        returningToMenu = true;

        if (networkManager.IsListening)
            networkManager.Shutdown();

        // Let NGO finish its shutdown before loading a local scene.
        yield return null;

        pendingApprovals.Clear();

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
