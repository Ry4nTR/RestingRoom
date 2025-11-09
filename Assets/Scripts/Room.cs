// Room.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class Room : MonoBehaviour
{
    public enum RoomType { Fixed, Mutant1, Mutant2 }

    [Header("Room Settings")]
    public RoomType type;
    public Transform wrongDestinationPoint;

    [Header("Interactions")]
    [SerializeField] private List<Interaction> interactions = new List<Interaction>();

    // Events
    public static event Action<Room> OnPlayerEntered;
    public static event Action<Room, Interaction> OnInteractionTriggered;

    public IReadOnlyList<Interaction> AvailableInteractions => interactions.Where(i => i != null && i.IsActive).ToList();
    public bool IsActive => gameObject.activeInHierarchy;

    private void Awake()
    {
        // Auto-register interactions if empty
        if (interactions.Count == 0)
            interactions = new List<Interaction>(GetComponentsInChildren<Interaction>());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            OnPlayerEntered?.Invoke(this);
        }
    }

    public void NotifyInteractionTriggered(Interaction interaction)
    {
        OnInteractionTriggered?.Invoke(this, interaction);
    }

    // Editor convenience
    private void Reset()
    {
        interactions = new List<Interaction>(GetComponentsInChildren<Interaction>());
    }
}