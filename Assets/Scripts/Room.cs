using System;
using System.Collections.Generic;
using UnityEngine;

public class Room : MonoBehaviour
{
    public event Action OnRoomEntered = delegate { };

    public enum Type { Fixed = 0, Mutant1 = 1, Mutant2 = 2}

    [SerializeField] private Type _roomType;

    [SerializeField] private List<Interaction> _interactionList;

    public IReadOnlyList<Interaction> InteractionList => _interactionList;
    public Type RoomType => _roomType;
}
