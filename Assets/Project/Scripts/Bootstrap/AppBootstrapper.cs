using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AppBootstrapper : MonoBehaviour
{
    private const string MainMenuScene = "1MainMenu";

    private void Start()
    {
        SceneManager.LoadScene(MainMenuScene);
    }
}
