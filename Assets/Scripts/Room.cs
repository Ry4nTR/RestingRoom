using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Room � responsabilit� e flusso eventi:
/// - Contiene una lista di `Interaction` (punti di interazione) appartenenti alla stanza.
/// - Espone eventi statici:
///   - `OnRoomEntered(Room)` : emesso quando il Player entra nella room (usato da `RoomManager` per tracciare la stanza del player).
///   - `OnInteractionTriggeredByNPC(Room, Interaction)` : emesso quando un'`Interaction` viene toccata dall'NPC (utile per log, AI o UI globali).
/// - Viene notificata dalle istanze `Interaction` tramite `NotifyInteractionTouched` quando l'NPC attiva un trigger dentro la stanza.
/// 
/// Relazioni e chi si collega a cosa:
/// - `Interaction.OnTriggerEnter` -> chiama `Room.NotifyInteractionTouched` (la Room invia poi `OnInteractionTriggeredByNPC`).
/// - `Room.OnRoomEntered` -> sottoscritto da `RoomManager` per aggiornare `PlayerRoom`.
/// - `Room.OnInteractionTriggeredByNPC` -> pu� essere sottoscritto da sistemi globali (es. logging, analytics, AI manager).
/// </summary>
public class Room : MonoBehaviour
{
    // Evento emesso quando il player entra nella stanza (RoomManager si sottoscrive per aggiornare PlayerRoom)
    public static event Action<Room> OnRoomEntered = delegate { };

    // Evento emesso quando l'NPC tocca una Interaction in questa room
    public static event Action<Room, Interaction> OnInteractionTriggeredByNPC = delegate { };

    public enum HouseSection { Fixed = 0, Mutant1 = 1, Mutant2 = 2 }

    [SerializeField] private HouseSection _roomType;
    [SerializeField] private List<Interaction> _interactionList = new List<Interaction>();
    [SerializeField] private Transform WrongDestination;

    public Transform wrongdestination => WrongDestination;

    // Esposizione readonly della lista di interaction (RoomManager la usa per scegliere destinazioni)
    public IReadOnlyList<Interaction> InteractionList => _interactionList;
    public HouseSection RoomType => _roomType;

    private void Reset()
    {
        // Convenienza editor: popola automaticamente la lista con i componenti Interaction figli
        // Utile in fase di setup per non dover aggiungere manualmente ogni Interaction nel Inspector.
        _interaction_list_or_default();
    }

    // Metodo helper per mantenere Reset pulito (non cambia comportamento, solo leggibilit�)
    private void _interaction_list_or_default()
    {
        _interactionList = new List<Interaction>(GetComponentsInChildren<Interaction>(true));
    }

    private void OnTriggerEnter(Collider other)
    {
        // Rileva l'entrata del Player nella stanza.
        // Nota: il Player deve avere il tag "Player" in scena per attivare questo evento.
        if (other.gameObject.CompareTag("Player"))
        {
            // Emissione evento globale: chi ascolta (es. RoomManager) sapr� che il player � entrato in questa stanza.
            OnRoomEntered?.Invoke(this);
        }
    }

    /// <summary>
    /// Chiamato dalle istanze `Interaction` quando l'NCP tocca quel punto di interazione.
    /// - Log locale
    /// - Emissione evento statico `OnInteractionTriggeredByNPC` per sistemi esterni
    /// Nota: non si occupa di validare quale NPC o se l'interaction era la destinazione attesa � questo � delegato all'`NPC_Controller`.
    /// </summary>
    public void NotifyInteractionTouched(Interaction interaction)
    {
        Debug.Log($"Room '{name}' - interaction touched: {interaction.name}");

        // Emissione evento globale: altri sistemi possono reagire (es. WishManager, sistemi di punteggio, analytics).
        OnInteractionTriggeredByNPC?.Invoke(this, interaction);
    }
}