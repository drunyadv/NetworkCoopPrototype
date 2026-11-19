
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MainMenuView : MonoBehaviour
{
    [Header("Input Fields")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_InputField addressInput;
    [SerializeField] private TMP_InputField portInput;

    [Header("Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;

    [Header("Status")]
    [SerializeField] private TMP_Text statusText;

    private ConnectionService connection;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        connection = ConnectionService.Instance;

        if (connection == null)
        {
            Debug.LogError("ConnectionService was not initialized.");
            return;
        }

        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);

        connection.StatusChanged += UpdateStatus;
        UpdateStatus(connection.LastMessage);
    }

    private void OnDestroy()
    {
        if (connection != null)
            connection.StatusChanged -= UpdateStatus;

        if (hostButton != null)
            hostButton.onClick.RemoveListener(OnHostClicked);

        if (joinButton != null)
            joinButton.onClick.RemoveListener(OnJoinClicked);
    }

    private void OnHostClicked()
    {
        if (!TryGetPort(out ushort port))
            return;

        connection.StartHost(playerNameInput.text, port);
    }

    private void OnJoinClicked()
    {
        if (!TryGetPort(out ushort port))
            return;

        string address = addressInput.text.Trim();

        if (string.IsNullOrWhiteSpace(address))
        {
            UpdateStatus("Enter a valid address");
            return;
        }

        connection.Join(playerNameInput.text, address, port);
    }

    private bool TryGetPort(out ushort port)
    {
        if (!ushort.TryParse(portInput.text, out port) ||
            port == 0)
        {
            UpdateStatus("Invalid port");
            return false;
        }

        return true;
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
