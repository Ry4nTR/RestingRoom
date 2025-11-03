using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class RoomManager : MonoBehaviour
{
    public NavMeshAgent NPC_Agent;      // riferimento all'NCP da Inspector
    public Catalogue Catalogue;     // riferimento al Catalogue che pubblica l'evento
    public Room[] Rooms;            // lista di tutte le room
    public Room PlayerRoom;         // la room corrente del giocatore (assegnata esternamente)


    private void Update()
    {
        if(NPC_Agent.hasPath)
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

        do         {
            randomRoom = Rooms[Random.Range(0, Rooms.Length)]; ;
        } while (randomRoom != PlayerRoom);
        

        Interaction interaction = randomRoom.InteractionList [Random.Range(0, randomRoom.InteractionList.Count)];

        return interaction.transform.position;
    }




}