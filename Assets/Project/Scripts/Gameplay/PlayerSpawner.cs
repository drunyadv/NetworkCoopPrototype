using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkManager))]
public sealed class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private NetworkObject playerPrefab;

    private const string GameScene = "3Game";

    private NetworkManager networkManager;
    private NetworkSceneManager sceneManager;
    private bool registered;

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
    }

    public void RegisterSession()
    {
        if (registered)
            return;

        if (!networkManager.IsServer)
        {
            Debug.LogWarning("[PlayerSpawner] Registration requires a server.");
            return;
        }

        sceneManager = networkManager.SceneManager;

        if (sceneManager == null)
        {
            Debug.LogError("[PlayerSpawner] NetworkSceneManager is missing.");
            return;
        }

        sceneManager.OnSceneEvent += OnSceneEvent;
        networkManager.OnClientConnectedCallback += OnClientConnected;

        registered = true;
    }

    public void ResetSession()
    {
        if (!registered)
            return;

        registered = false;

        if (sceneManager != null)
            sceneManager.OnSceneEvent -= OnSceneEvent;

        if (networkManager != null)
            networkManager.OnClientConnectedCallback -= OnClientConnected;

        sceneManager = null;
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        if (!networkManager.IsServer || sceneEvent.SceneName != GameScene ||
            sceneEvent.SceneEventType != SceneEventType.LoadEventCompleted)
        {
            return;
        }

        foreach (ulong clientId in networkManager.ConnectedClientsIds)
            SpawnPlayer(clientId);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!networkManager.IsServer ||
            SceneManager.GetActiveScene().name != GameScene)
        {
            return;
        }

        NetworkSessionState session = NetworkSessionState.Instance;

        if (session == null || session.Phase.Value != SessionPhase.Playing)
            return;

        SpawnPlayer(clientId);
    }

    private void SpawnPlayer(ulong clientId)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawner] Player prefab is not assigned.");
            return;
        }

        if (!networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            Debug.LogError($"[PlayerSpawner] Client {clientId} not found.");
            return;
        }

        if (client.PlayerObject != null)
        {
            Debug.Log($"[PlayerSpawner] Client {clientId} already has a player.");
            return;
        }

        NetworkSessionState session = NetworkSessionState.Instance;

        if (session == null)
        {
            Debug.LogError("[PlayerSpawner] Session state not found.");
            return;
        }

        int spawnIndex = session.IndexOfClient(clientId);
        if (spawnIndex < 0)
        {
            Debug.LogError($"[PlayerSpawner] Client {clientId} is missing from the roster.");
            return;
        }

        GameSpawnPoints spawnPoints = FindFirstObjectByType<GameSpawnPoints>();

        if (spawnPoints == null)
        {
            Debug.LogError("[PlayerSpawner] GameSpawnPoints not found.");
            return;
        }

        Transform point = spawnPoints.GetPoint(spawnIndex);

        if (point == null)
        {
            Debug.LogError("[PlayerSpawner] Spawn point is not configured.");
            return;
        }

        NetworkObject player = Instantiate(playerPrefab, point.position, point.rotation);

        player.SpawnAsPlayerObject(clientId, true);

        Debug.Log($"[PlayerSpawner] Spawned player {clientId} at {point.position}");
    }

    private void OnDestroy()
    {
        ResetSession();
    }
}
