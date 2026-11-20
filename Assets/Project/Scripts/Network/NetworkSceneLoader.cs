using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkManager))]
public sealed class NetworkSceneLoader : MonoBehaviour
{
    [SerializeField] private LoadingScreenView loadingScreen;

    private const string GameScene = "3Game";
    private const double StartDelay = 1.0;

    private NetworkManager networkManager;
    private NetworkSceneManager sceneManager;

    private readonly HashSet<ulong> expectedClients = new();
    private readonly HashSet<ulong> loadedClients = new();

    private bool registered;
    private bool loadingGame;
    private bool sceneEventFinished;
    private bool localGameReady;

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
    }

    public void RegisterSession()
    {
        if (registered)
            return;

        sceneManager = networkManager.SceneManager;
        sceneManager.OnSceneEvent += OnSceneEvent;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;

        registered = true;
    }

    public void ResetSession()
    {
        if (registered)
        {
            sceneManager.OnSceneEvent -= OnSceneEvent;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        registered = false;
        loadingGame = false;
        sceneEventFinished = false;
        localGameReady = false;

        expectedClients.Clear();
        loadedClients.Clear();

        loadingScreen.Hide();
    }

    public void StartGame()
    {
        if (!networkManager.IsHost || loadingGame)
            return;

        NetworkSessionState session = NetworkSessionState.Instance;

        if (session == null || !session.BeginLoading())
            return;

        expectedClients.Clear();
        loadedClients.Clear();

        foreach (ulong clientId in networkManager.ConnectedClientsIds)
            expectedClients.Add(clientId);

        loadingGame = true;
        sceneEventFinished = false;
        localGameReady = false;

        loadingScreen.Show("Loading game...");
        UpdateProgress();

        var status = sceneManager.LoadScene(GameScene, LoadSceneMode.Single);
        if (status == SceneEventProgressStatus.Started)
            return;

        Debug.LogError($"Scene load failed: {status}");
        loadingGame = false;
        expectedClients.Clear();
        loadedClients.Clear();
        session.ReturnToLobby();
        loadingScreen.Hide();
    }

    public bool CanPlay
    {
        get
        {
            if (!registered || !localGameReady)
                return false;

            NetworkSessionState session = NetworkSessionState.Instance;

            return session != null &&
                   session.Phase.Value == SessionPhase.Playing &&
                   session.GameStartTime.Value > 0 &&
                   networkManager.ServerTime.Time >= session.GameStartTime.Value;
        }
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        bool isGameScene = sceneEvent.SceneName == GameScene;
        switch (sceneEvent.SceneEventType)
        {
            case SceneEventType.Synchronize when !networkManager.IsServer:
                loadingScreen.Show("Joining session...");
                break;

            case SceneEventType.SynchronizeComplete when !networkManager.IsServer:
                OnLocalSynchronizationComplete();
                break;

            case SceneEventType.Load when isGameScene:
                localGameReady = false;
                loadingScreen.Show("Loading game...");
                break;

            case SceneEventType.LoadComplete when isGameScene:
                OnLoadComplete(sceneEvent.ClientId);
                break;

            case SceneEventType.LoadEventCompleted when isGameScene:
                OnLoadEventCompleted(sceneEvent);
                break;
        }
    }

    private void OnLocalSynchronizationComplete()
    {
        if (SceneManager.GetActiveScene().name != GameScene)
        {
            loadingScreen.Hide();
            return;
        }

        localGameReady = true;
        loadingScreen.Show("Waiting for game...");
    }

    private void OnLoadComplete(ulong clientId)
    {
        if (clientId == networkManager.LocalClientId)
        {
            localGameReady = true;
            loadingScreen.Show("Waiting for other players...");
        }

        if (!networkManager.IsServer || !loadingGame)
            return;

        if (expectedClients.Contains(clientId))
        {
            loadedClients.Add(clientId);
            UpdateProgress();
        }
    }

    private void OnLoadEventCompleted(SceneEvent sceneEvent)
    {
        if (!networkManager.IsServer || !loadingGame)
            return;

        foreach (ulong clientId in sceneEvent.ClientsThatCompleted)
        {
            if (expectedClients.Contains(clientId))
                loadedClients.Add(clientId);
        }

        foreach (ulong clientId in sceneEvent.ClientsThatTimedOut)
        {
            expectedClients.Remove(clientId);
            loadedClients.Remove(clientId);

            if (clientId != NetworkManager.ServerClientId &&
                networkManager.ConnectedClientsIds.Contains(clientId))
            {
                networkManager.DisconnectClient(
                    clientId,
                    "Scene loading timed out"
                );
            }
        }

        sceneEventFinished = true;

        UpdateProgress();
        TryFinishLoading();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!networkManager.IsServer || !loadingGame)
            return;

        expectedClients.Remove(clientId);
        loadedClients.Remove(clientId);

        UpdateProgress();
        TryFinishLoading();
    }

    private void TryFinishLoading()
    {
        if (!loadingGame || !sceneEventFinished)
            return;

        if (!expectedClients.IsSubsetOf(loadedClients))
            return;

        NetworkSessionState session = NetworkSessionState.Instance;

        if (session == null)
            return;

        double startTime = networkManager.ServerTime.Time + StartDelay;

        session.BeginPlaying(startTime);
        loadingGame = false;

        Debug.Log("Game scene is ready");
    }

    private void UpdateProgress()
    {
        if (!networkManager.IsServer || !loadingGame)
            return;

        loadingScreen.SetProgress(
            loadedClients.Count,
            expectedClients.Count
        );
    }

    private void Update()
    {
        if (CanPlay && loadingScreen.IsVisible)
            loadingScreen.Hide();
    }

    private void OnDestroy()
    {
        ResetSession();
    }
}
