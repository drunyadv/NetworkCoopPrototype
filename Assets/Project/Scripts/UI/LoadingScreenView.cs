using TMPro;
using UnityEngine;

public sealed class LoadingScreenView : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text statusText;

    public bool IsVisible => panel.activeSelf;

    private void Awake()
    {
        panel.SetActive(false);
    }

    public void Show(string message)
    {
        statusText.text = message;
        panel.SetActive(true);
    }

    public void SetProgress(int loaded, int total)
    {
        statusText.text = $"Loading players: {loaded}/{total}";
    }

    public void Hide()
    {
        panel.SetActive(false);
    }
}
