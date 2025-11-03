using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 1. Gestisce NPC trovando e assegnando destinazione causale.
/// 2. Accende e spegne prefab stanze in base a Action event da Catalogue
/// 3. Tiene traccia della stanza corrente del giocatore
/// 4. Manda evento a WishManger quando roomManager decide destinazione random per NPC
/// 5. Se random destination appartiene a una stanza disabilitata allora imposta come destinazione la stanza attiva (.
/// </summary>>
public class RoomManager : MonoBehaviour
{
    public NavMeshAgent NPC_Agent;      // riferimento all'NCP da Inspector
    public Catalogue Catalogue;     // riferimento al Catalogue che pubblica l'evento
    public Room[] Rooms;            // lista di tutte le room
    public Room PlayerRoom;         // la room corrente del giocatore (assegnata esternamente)

    private void Start()
    {
        Room.OnRoomEntered += HandleRoomEntered;
    }

    private void OnDestroy()
    {
        Room.OnRoomEntered -= HandleRoomEntered;
    }

    private void HandleRoomEntered(Room room)
    {
        PlayerRoom = room;
    }

    private void Update()
    {

        if (NPC_Agent.hasPath)
        {
            Debug.Log("Agent has path");
            return;
        }  
        else
        {
            Debug.Log("Agent HAS NOT path");
            Vector3 destination = GetRandomDestination();
            NPC_Agent.SetDestination(destination);
        }


    }


    public Vector3 GetRandomDestination()
    {
        Room randomRoom;

        do
        {
            randomRoom = Rooms[Random.Range(0, Rooms.Length)]; ;
        } while (randomRoom == PlayerRoom);
        
        if (randomRoom.InteractionList.Count == 0)
        {
            return GetRandomDestination();
        }
        Interaction interaction = randomRoom.InteractionList [Random.Range(0, randomRoom.InteractionList.Count)];

        return interaction.transform.position;
    }




}