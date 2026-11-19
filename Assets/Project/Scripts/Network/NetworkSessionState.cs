
using System;
using Unity.Netcode;
using UnityEngine;

    public enum SessionPhase : byte
    {
        Lobby,
        Loading,
        Playing
    }

public sealed class NetworkSessionState : NetworkBehaviour
{
    public static NetworkSessionState Instance { get; private set; }

    public event Action PlayersChanged;

    private NetworkList<PlayerInfo> players;


    public readonly NetworkVariable<SessionPhase> Phase = new(SessionPhase.Lobby);
    public readonly NetworkVariable<double> GameStartTime = new(0);

    public int PlayerCount => players.Count;

    public bool BeginLoading()
    {
        if (!IsServer || Phase.Value != SessionPhase.Lobby)
            return false;

        GameStartTime.Value = 0;
        Phase.Value = SessionPhase.Loading;

        return true;
    }

    public void BeginPlaying(double startTime)
    {
        if (!IsServer || Phase.Value != SessionPhase.Loading)
            return;

        GameStartTime.Value = startTime;
        Phase.Value = SessionPhase.Playing;
    }

    public void ReturnToLobby()
    {
        if (!IsServer)
            return;

        GameStartTime.Value = 0;
        Phase.Value = SessionPhase.Lobby;
    }

    private void Awake()
    {
        players = new NetworkList<PlayerInfo>();
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;

        players.OnListChanged += OnPlayersChanged;
        PlayersChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        players.OnListChanged -= OnPlayersChanged;

        if (Instance == this)
            Instance = null;
    }

    public PlayerInfo GetPlayer(int index)
    {
        return players[index];
    }

    public bool ContainsPlayerId(string playerId)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].PlayerId.ToString() == playerId)
                return true;
        }

        return false;
    }

    public void AddPlayer(PlayerInfo player)
    {
        if (!IsServer)
            return;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].ClientId == player.ClientId)
            {
                players[i] = player;
                return;
            }
        }

        players.Add(player);
    }

    public void RemovePlayer(ulong clientId)
    {
        if (!IsServer)
            return;

        for (int i = players.Count - 1; i >= 0; i--)
        {
            if (players[i].ClientId == clientId)
                players.RemoveAt(i);
        }
    }

    private void OnPlayersChanged(NetworkListEvent<PlayerInfo> change)
    {
        PlayersChanged?.Invoke();
    }
}
