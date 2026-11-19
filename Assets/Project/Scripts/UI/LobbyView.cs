using UnityEngine;
using UnityEngine.UI;

public sealed class LobbyView : MonoBehaviour
{
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveButton;

    private ConnectionService connection;

    private void Start()
    {
        connection = ConnectionService.Instance;

        if (connection == null)
        {
            Debug.LogError("ConnectionService not found.");
            return;
        }

        // Only the host can start a match.
        startGameButton.gameObject.SetActive(connection.IsHost);

        startGameButton.onClick.AddListener(OnStartGameClicked);
        leaveButton.onClick.AddListener(OnLeaveClicked);
    }

    private void OnDestroy()
    {
        if (startGameButton != null)
            startGameButton.onClick.RemoveListener(OnStartGameClicked);

        if (leaveButton != null)
            leaveButton.onClick.RemoveListener(OnLeaveClicked);
    }

    private void OnStartGameClicked()
    {
        connection?.StartGame();
    }

    private void OnLeaveClicked()
    {
        connection?.Leave();
    }
}
