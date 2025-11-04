using UnityEngine;

/// <summary>
/// Handle finish panel of the game
/// </summary>

public class FinishPanel : MonoBehaviour
{
    private CanvasGroup finishPanel;

    private void Awake()
    {
        finishPanel = GetComponent<CanvasGroup>();
        PleasureManager.OnPleasureDepleted += StartGameOver;
    }

    private void Start()
    {
        Time.timeScale = 1;
        finishPanel.SetCanvasGroup(false);
    }

    private void OnDestroy()
    {
        PleasureManager.OnPleasureDepleted -= StartGameOver;
    }

    private void StartGameOver()
    {
        Time.timeScale = 0;
        finishPanel.SetCanvasGroup(true);
    }
}
