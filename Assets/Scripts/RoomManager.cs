// RoomManager.cs
using UnityEngine;
using System;
using System.Linq;

public class RoomManager : MonoBehaviour
{
    [Header("References")]
    public NPC_Controller npcController;
    public WishManager wishManager;

    [Header("Rooms")]
    public Room[] allRooms;

    // Runtime state
    public Room PlayerRoom { get; private set; }
    public Room CurrentTargetRoom { get; private set; }
    public Interaction CurrentTargetInteraction { get; private set; }
    public Room LastVisitedRoom { get; private set; } // NEW: Track last visited room

    // Events
    public static event Action<Interaction, Room> OnNewWishAssigned;
    public static event Action<bool, Interaction> OnWishCompleted;

    private void Start()
    {
        Room.OnPlayerEntered += HandlePlayerEnteredRoom;

        if (npcController != null)
        {
            npcController.OnDestinationReached += HandleDestinationReached;
        }

        // Start the first wish after a brief delay to let everything initialize
        Invoke(nameof(StartFirstWish), 1f);
    }

    private void StartFirstWish()
    {
        Debug.Log("[RoomManager] Starting first wish...");
        AssignNewWish();
    }

    private void OnDestroy()
    {
        Room.OnPlayerEntered -= HandlePlayerEnteredRoom;

        if (npcController != null)
            npcController.OnDestinationReached -= HandleDestinationReached;
    }

    private void HandlePlayerEnteredRoom(Room room)
    {
        PlayerRoom = room;
        Debug.Log($"[RoomManager] Player entered room: {room.name}");
    }

    private void HandleDestinationReached(bool success, Interaction interaction)
    {
        // Update last visited room when NPC completes a wish
        if (CurrentTargetRoom != null)
        {
            LastVisitedRoom = CurrentTargetRoom;
            Debug.Log($"[RoomManager] Marking {CurrentTargetRoom.name} as last visited room");
        }

        OnWishCompleted?.Invoke(success, interaction);

        // Wait a moment before assigning new wish
        Invoke(nameof(AssignNewWish), 0.5f);
    }

    public void AssignNewWish()
    {
        if (npcController.IsBusy)
        {
            Debug.Log("[RoomManager] NPC is busy, delaying wish assignment");
            Invoke(nameof(AssignNewWish), 1f);
            return;
        }

        var (interaction, room) = GetRandomWish();

        if (interaction != null && room != null)
        {
            CurrentTargetInteraction = interaction;
            CurrentTargetRoom = room;

            //Debug.Log($"[RoomManager] 🎯 New Wish Assigned: '{interaction.wishText}' in Room: {room.name}");

            npcController.SetDestination(interaction, room);
            OnNewWishAssigned?.Invoke(interaction, room);
        }
        else
        {
            Debug.LogWarning("[RoomManager] Failed to find valid wish - will retry in 2 seconds");
            Invoke(nameof(AssignNewWish), 2f);
        }
    }

    private (Interaction, Room) GetRandomWish()
    {
        if (allRooms == null || allRooms.Length == 0)
        {
            return (null, null);
        }

        // Filter valid rooms (not player's current room, not last visited room, has interactions)
        int validRoomCount = 0;
        foreach (var room in allRooms)
        {
            if (room != null &&
                room != PlayerRoom &&
                room != LastVisitedRoom && // NEW: Exclude last visited room
                room.AvailableInteractions.Count > 0)
                validRoomCount++;
        }

        Debug.Log($"[RoomManager] Found {validRoomCount} valid rooms for wish assignment (excluding player room and last visited room: {LastVisitedRoom?.name ?? "none"})");

        if (validRoomCount == 0)
        {
            // If no valid rooms found, allow last visited room but not player room
            Debug.LogWarning("[RoomManager] No rooms available excluding last visited - relaxing constraints");
            foreach (var room in allRooms)
            {
                if (room != null &&
                    room != PlayerRoom &&
                    room.AvailableInteractions.Count > 0)
                    validRoomCount++;
            }

            if (validRoomCount == 0)
            {
                Debug.LogWarning("[RoomManager] No valid rooms available for wish assignment even with relaxed constraints");
                return (null, null);
            }
        }

        Room selectedRoom;
        int attempts = 0;
        do
        {
            selectedRoom = allRooms[UnityEngine.Random.Range(0, allRooms.Length)];
            attempts++;

            if (attempts > 50) // Increased safety break
            {
                Debug.LogError("[RoomManager] Could not find valid room after 50 attempts");
                return (null, null);
            }
        }
        while (selectedRoom == null ||
               selectedRoom == PlayerRoom ||
               selectedRoom == LastVisitedRoom || // NEW: Exclude last visited
               selectedRoom.AvailableInteractions.Count == 0);

        Interaction selectedInteraction = selectedRoom.AvailableInteractions[
            UnityEngine.Random.Range(0, selectedRoom.AvailableInteractions.Count)];

        //Debug.Log($"[RoomManager] Selected room: {selectedRoom.name} with {selectedRoom.AvailableInteractions.Count} available interactions");

        return (selectedInteraction, selectedRoom);
    }

    public void ForceWish(Interaction interaction, Room room)
    {
        if (interaction == null || room == null)
        {
            return;
        }

        CurrentTargetInteraction = interaction;
        CurrentTargetRoom = room;

        Debug.Log($"[RoomManager] 🔥 Forced Wish: '{interaction.wishText}' in Room: {room.name}");

        npcController.SetDestination(interaction, room);
        OnNewWishAssigned?.Invoke(interaction, room);
    }
}