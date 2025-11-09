// Interaction.cs
using UnityEngine;

public class Interaction : MonoBehaviour
{
    [Header("Wish Settings")]
    [TextArea] public string wishText = "I want to do something...";

    public bool IsActive => gameObject.activeInHierarchy && isActiveAndEnabled;

    private void OnTriggerEnter(Collider other)
    {
        // Notify room
        Room room = GetComponentInParent<Room>();
        if (room != null)
            room.NotifyInteractionTriggered(this);

        // Notify NPC controller
        NPC_Controller npc = other.GetComponent<NPC_Controller>();
        if (npc != null)
            npc.NotifyInteractionReached(this);
    }
}