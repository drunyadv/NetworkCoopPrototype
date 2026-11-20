using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerListEntryView : MonoBehaviour
{
    [SerializeField] private TMP_Text playerName;
    [SerializeField] private TMP_Text hostIndicator;
    [SerializeField] private TMP_Text pingText;
    [SerializeField] private Button kickButton;

    public void SetPlayer(
        PlayerInfo player,
        bool isHost,
        Action<ulong> onKick = null)
    {
        playerName.text = player.Name.ToString();
        hostIndicator.text = isHost ? "HOST" : "CLIENT";
        pingText.text = $"{player.PingMs} ms";

        hostIndicator.color = isHost
            ? new Color32(244, 199, 91, 255)
            : new Color32(174, 189, 208, 255);

        kickButton.onClick.RemoveAllListeners();
        bool showKickButton = onKick != null && !isHost;
        kickButton.gameObject.SetActive(showKickButton);

        if (showKickButton)
        {
            ulong clientId = player.ClientId;
            kickButton.onClick.AddListener(() => onKick(clientId));
        }
    }
}
