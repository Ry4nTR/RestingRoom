// WishManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WishManager : MonoBehaviour
{
    [Header("UI References")]
    public Canvas uiCanvas;
    public GameObject wishBubblePrefab;
    public Vector2 bubbleScreenPosition = new Vector2(-150, 200);

    [Header("Timing Settings")]
    public float thinkingDuration = 2f;
    public float thinkingDecayMultiplier = 0.95f;
    public float minThinkingDuration = 0.2f;

    private GameObject currentBubble;
    private RoomManager roomManager;
    private int completedWishes;

    private void Start()
    {
        roomManager = FindAnyObjectByType<RoomManager>();
        if (roomManager == null)
        {
            Debug.LogError("WishManager: RoomManager not found in scene!");
        }

        RoomManager.OnNewWishAssigned += HandleNewWish;
        RoomManager.OnWishCompleted += HandleWishCompleted;
    }

    private void OnDestroy()
    {
        RoomManager.OnNewWishAssigned -= HandleNewWish;
        RoomManager.OnWishCompleted -= HandleWishCompleted;
    }

    private void HandleNewWish(Interaction interaction, Room room)
    {
        ShowWishBubble(interaction.wishText);
    }

    private void HandleWishCompleted(bool success, Interaction interaction)
    {
        completedWishes++;

        // Reduce thinking time for next wish
        thinkingDuration *= thinkingDecayMultiplier;
        thinkingDuration = Mathf.Max(minThinkingDuration, thinkingDuration);

        HideWishBubble();
        Debug.Log($"WishManager: Wish completed. Total: {completedWishes}. New thinking duration: {thinkingDuration}");
    }

    private void ShowWishBubble(string wishText)
    {
        if (currentBubble != null)
            Destroy(currentBubble);

        if (wishBubblePrefab != null && uiCanvas != null)
        {
            currentBubble = Instantiate(wishBubblePrefab, uiCanvas.transform);
            RectTransform rt = currentBubble.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = bubbleScreenPosition;

            // Set text
            var textComponent = currentBubble.GetComponentInChildren<TMP_Text>();
            if (textComponent != null)
                textComponent.text = wishText;
        }
    }

    private void HideWishBubble()
    {
        if (currentBubble != null)
        {
            Destroy(currentBubble);
            currentBubble = null;
            Debug.Log("WishManager: Hid wish bubble.");
        }
    }

    // API for forcing specific wishes
    public void ForceWish(Interaction interaction, Room room)
    {
        if (roomManager != null)
            roomManager.ForceWish(interaction, room);
        else
            Debug.LogError("WishManager: RoomManager reference is null. Cannot force wish.");
    }
}