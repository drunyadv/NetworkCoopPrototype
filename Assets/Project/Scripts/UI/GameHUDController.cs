
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class GameHUDController : MonoBehaviour
{
    [Header("Player List")]
    [SerializeField] private GameObject playerListPanel;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private PlayerListEntryView playerEntryPrefab;
    [SerializeField] private TMP_Text playerCountText;

    [Header("Pause")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button leaveButton;

    [Header("HUD")]
    [SerializeField] private GameObject crosshair;
    [SerializeField] private TMP_Text connectionStatus;

    private NetworkSessionState session;
    private bool paused;

    private IEnumerator Start()
    {
        pausePanel.SetActive(false);
        playerListPanel.SetActive(false);

        resumeButton.onClick.AddListener(Resume);
        leaveButton.onClick.AddListener(Leave);

        ConnectionService connection = ConnectionService.Instance;

        if (connection != null && connectionStatus != null)
            connectionStatus.text = connection.IsHost ? "HOST" : "CONNECTED";

        while (NetworkSessionState.Instance == null)
            yield return null;

        session = NetworkSessionState.Instance;
        session.PlayersChanged += RefreshPlayerList;

        RefreshPlayerList();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame)
            SetPaused(!paused);

        bool showPlayers = !paused && keyboard.tabKey.isPressed;

        if (playerListPanel.activeSelf != showPlayers)
            playerListPanel.SetActive(showPlayers);
    }

    private void SetPaused(bool value)
    {
        paused = value;

        pausePanel.SetActive(paused);
        playerListPanel.SetActive(false);

        if (crosshair != null)
            crosshair.SetActive(!paused);

        Cursor.lockState = paused
            ? CursorLockMode.None
            : CursorLockMode.Locked;

        Cursor.visible = paused;
    }

    private void RefreshPlayerList()
    {
        if (session == null)
            return;

        for (int i = playerListContent.childCount - 1; i >= 0; i--)
            Destroy(playerListContent.GetChild(i).gameObject);

        for (int i = 0; i < session.PlayerCount; i++)
        {
            PlayerInfo player = session.GetPlayer(i);

            PlayerListEntryView entry = Instantiate(
                playerEntryPrefab,
                playerListContent
            );

            entry.SetPlayer(
                player,
                player.ClientId == 0
            );
        }

        if (playerCountText != null)
            playerCountText.text = $"Players: {session.PlayerCount}/4";
    }

    private void Resume()
    {
        SetPaused(false);
    }

    private void Leave()
    {
        ConnectionService.Instance?.Leave();
    }

    private void OnDestroy()
    {
        if (session != null)
            session.PlayersChanged -= RefreshPlayerList;

        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(Resume);

        if (leaveButton != null)
            leaveButton.onClick.RemoveListener(Leave);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
