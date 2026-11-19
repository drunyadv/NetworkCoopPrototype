
using System;
using Unity.Netcode;
using UnityEngine;

public sealed class NetworkSessionState : NetworkBehaviour
{
    public static NetworkSessionState Instance { get; private set; }

    public event Action PlayersChanged;

    private NetworkList<PlayerInfo> players;

    public int PlayerCount => players.Count;

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
