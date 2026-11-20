using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;

internal static class PlayerListView
{
    public static void Rebuild(NetworkSessionState session, Transform content,
        PlayerListEntryView entryPrefab, TMP_Text countText, Action<ulong> onKick = null)
    {
        for (int i = content.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(content.GetChild(i).gameObject);

        for (int i = 0; i < session.PlayerCount; i++)
        {
            PlayerInfo player = session.GetPlayer(i);
            bool isHost = player.ClientId == NetworkManager.ServerClientId;
            var entry = UnityEngine.Object.Instantiate(entryPrefab, content);
            entry.SetPlayer(player, isHost, onKick);
        }

        if (countText != null)
            countText.text = $"Players: {session.PlayerCount}/{ConnectionService.MaxPlayers}";
    }
}
