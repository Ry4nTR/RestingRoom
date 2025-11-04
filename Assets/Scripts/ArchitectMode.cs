using UnityEngine;

public class ArchitectMode : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    private bool status = false;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        SetMinimapStatus(false);
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Tab))
        {
            SetMinimapStatus(!status);
        }
    }

    private void SetMinimapStatus(bool newStatus)
    {
        canvasGroup.interactable = newStatus;
        canvasGroup.alpha = newStatus ? 1 : 0;
        canvasGroup.blocksRaycasts = newStatus;

        status = newStatus;
    }
}
