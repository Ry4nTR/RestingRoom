using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ha la lista di Interaction all'interno della stanza
/// Viene identificato con un enum Slot { Mutante1, Mutante2, Fisso …}
/// Notifica RoomManager quando il giocatore entra nella stanza
/// </summary>
public class Room : MonoBehaviour
{
    public static event Action<Room> OnRoomEntered = delegate { };

    public enum Type { Fixed = 0, Mutant1 = 1, Mutant2 = 2}

    [SerializeField] private Type _roomType;

    [SerializeField] private List<Interaction> _interactionList;

    public IReadOnlyList<Interaction> InteractionList => _interactionList;
    public Type RoomType => _roomType;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            OnRoomEntered?.Invoke(this);
        }
    }
}
