using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// RoomManager — responsabilità e flusso eventi:
/// - Tiene la lista delle `Room` presenti nella scena e può attivarle/disattivarle in base alle preferenze del `Catalogue`.
/// - Assegna destinazioni casuali all'NPC scegliendo una `Interaction` all'interno di una `Room` valida.
/// - Tiene traccia della `PlayerRoom` ascoltando l'evento statico `Room.OnRoomEntered` (la Room in cui è entrato il player).
/// - Emana l'evento statico `OnNpcNewDestination` quando seleziona una nuova `Interaction`/Room per l'NPC.
/// </summary>
public class RoomManager : MonoBehaviour
{
    // Riferimenti di scena (configurati in Inspector)
    public NavMeshAgent NPC_Agent;
    public NPC_Controller NPC_Controller;
    public Room[] Rooms; // lista di tutte le room disponibili nella scena

    // Stato runtime
    [NonSerialized] public Room PlayerRoom; // la room corrente del player (aggiornata da Room.OnRoomEntered)

    // Evento pubblico: notifica a sistemi esterni (WishManager / UI) quale Interaction/Room è stata scelta per l'NPC.
    // Firma: (Interaction scelta, Room contenente l'interaction)
    public static event Action<Interaction, Room> OnNpcNewDestination = delegate { };

    // Public helper: permette ad altri oggetti di richiedere l'emissione dell'evento senza invocare l'evento direttamente.
    // Gli eventi in C# possono essere invocati solo dall'interno della classe che li dichiara, quindi esponiamo questo metodo.
    public static void RaiseOnNpcNewDestination(Interaction interaction, Room room)
    {
        OnNpcNewDestination?.Invoke(interaction, room);
    }

    private void Start()
    {
        Room.OnRoomEntered += HandleRoomEntered;

        if (NPC_Controller != null)
        {
            NPC_Controller.OnDestinationReached += HandleNpcDestinationResult;
        }
    }

    private void OnDestroy()
    {
        Room.OnRoomEntered -= HandleRoomEntered;
        if (NPC_Controller != null)
            NPC_Controller.OnDestinationReached -= HandleNpcDestinationResult;
    }

    private void HandleRoomEntered(Room room)
    {
        PlayerRoom = room;
    }

    // log minimizzati: la UI/WishManager si occupa di mostrare quello che serve al giocatore
    private void HandleNpcDestinationResult(bool success, Interaction touched)
    {
        // comportamento di gioco qui (nessun log ripetuto)
    }

    private void Update()
    {
        // Se non abbiamo un NPC_Controller useremo direttamente il NavMeshAgent (legacy / fallback).
        if (NPC_Controller == null)
        {
            if (NPC_Agent == null) return;
            if (NPC_Agent.hasPath) return;

            Vector3 dest = GetRandomDestination();
            NPC_Agent.SetDestination(dest);
            return;
        }

        // Evita di assegnare se l'agent sta navigando o è occupato (task/roam/think)
        if (NPC_Controller.Agent != null && (NPC_Controller.Agent.hasPath || NPC_Controller.IsBusy))
            return;

        // Se non ha percorso e non è busy, selezioniamo una Interaction e Room valide e assegniamo come destinazione
        (Interaction interaction, Room room) = GetRandomInteractionAndRoom();
        if (interaction != null && room != null)
        {
            NPC_Controller.SetDestination(interaction, room);
            // Emit evento per UI / WishManager: mostrare il testo desiderio, aggiornare indicatori ecc.
            OnNpcNewDestination?.Invoke(interaction, room);
        }
    }

    [SerializeField] public bool useRandomDestination = false;
    [SerializeField] public int tempRoomIndex = 0;
    public (Interaction, Room) GetRandomInteractionAndRoom()
    {
        if (Rooms == null || Rooms.Length == 0) return (null, null);

        Room randomRoom = null;
        if (useRandomDestination)
        {
            int attempts = 0;
            do
            {
                randomRoom = Rooms[UnityEngine.Random.Range(0, Rooms.Length)];
                attempts++;
                if (attempts > 50) break; // sicurezza: esci se non trovi nulla di valido
            }
            // NOTE: rimosso il controllo su randomRoom.gameObject.activeInHierarchy per permettere
            // che anche le Room non attive nella gerarchia possano essere selezionate.
            // Manteniamo comunque l'esclusione della PlayerRoom e delle stanze senza interaction.
            while ((randomRoom == PlayerRoom) || randomRoom.InteractionList.Count == 0);
        }
        else
        {
            randomRoom = Rooms[tempRoomIndex];
        }

        if (randomRoom == null || randomRoom.InteractionList.Count == 0) return (null, null);

        Interaction chosen = randomRoom.InteractionList[UnityEngine.Random.Range(0, randomRoom.InteractionList.Count)];
        return (chosen, randomRoom);
    }

    public Vector3 GetRandomDestination()
    {
        var pair = GetRandomInteractionAndRoom();
        return (pair.Item1 != null) ? pair.Item1.transform.position : transform.position;
    }

    public void ApplyCataloguePreference(Room.HouseSection preferredType)
    {
        foreach (var r in Rooms)
        {
            if (r == null) continue;
            bool shouldBeActive = (r.RoomType == preferredType);
            r.gameObject.SetActive(shouldBeActive);
        }

        // log rimossi per ridurre rumore
    }
}