
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public sealed class LobbyView : MonoBehaviour
{
    [Header("Player List")]
    [SerializeField] private Transform playerListContent;
    [SerializeField] private PlayerListEntryView playerEntryPrefab;
    [SerializeField] private TMP_Text playerCountText;

    [Header("Buttons")]
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveButton;

    private ConnectionService connection;
    private NetworkSessionState session;

    private IEnumerator Start()
    {
        connection = ConnectionService.Instance;

        if (connection == null)
        {
            Debug.LogError("ConnectionService not found.");
            yield break;
        }

        startGameButton.gameObject.SetActive(connection.IsHost);

        startGameButton.onClick.AddListener(OnStartGameClicked);
        leaveButton.onClick.AddListener(OnLeaveClicked);

        while (NetworkSessionState.Instance == null)
            yield return null;

        session = NetworkSessionState.Instance;
        session.PlayersChanged += RefreshPlayerList;

        RefreshPlayerList();
    }

    private void RefreshPlayerList()
    {
        for (int i = playerListContent.childCount - 1; i >= 0; i--)
            Destroy(playerListContent.GetChild(i).gameObject);

        for (int i = 0; i < session.PlayerCount; i++)
        {
            PlayerInfo player = session.GetPlayer(i);

            PlayerListEntryView entry = Instantiate(
                playerEntryPrefab,
                playerListContent
            );

            bool isHost = player.ClientId == NetworkManager.ServerClientId;
            
            entry.SetPlayer(
                player,
                isHost,
                connection != null && connection.IsHost,
                clientId => connection.KickPlayer(clientId)
            );

        }

        playerCountText.text = $"Players: {session.PlayerCount}/4";
    }

    private void OnStartGameClicked()
    {
        connection?.StartGame();
    }

    private void OnLeaveClicked()
    {
        connection?.Leave();
    }

    private void OnDestroy()
    {
        if (session != null)
            session.PlayersChanged -= RefreshPlayerList;

        if (startGameButton != null)
            startGameButton.onClick.RemoveListener(OnStartGameClicked);

        if (leaveButton != null)
            leaveButton.onClick.RemoveListener(OnLeaveClicked);
    }
}