using TMPro;
using UnityEngine;

public sealed class PlayerListEntryView : MonoBehaviour
{
    [SerializeField] private TMP_Text playerName;
    [SerializeField] private TMP_Text hostIndicator;

    public void SetPlayer(PlayerInfo player, bool isHost)
    {
        playerName.text = player.Name.ToString();
        hostIndicator.text = isHost ? "HOST" : "CLIENT";

        hostIndicator.color = isHost
            ? new Color32(244, 199, 91, 255)
            : new Color32(174, 189, 208, 255);
    }
}