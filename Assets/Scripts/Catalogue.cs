using System;
using UnityEngine;

public class Catalogue : MonoBehaviour
{
    public static event Action<Room> OnCatalogueUpdated;

    void UpdateCatalogue(Room currentRoom)
    {
        OnCatalogueUpdated?.Invoke(currentRoom);
    }
}
