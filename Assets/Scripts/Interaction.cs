using UnityEngine;

/// <summary>
/// Interaction — responsabilità e flusso:
/// - Rappresenta un punto di interazione all'interno di una `Room`.
/// - Mantiene il testo del desiderio (`wishText`) che può essere mostrato dalla UI quando l'NPC riceve questa destinazione.
/// - Quando un collider entra nel suo trigger (tipicamente l'NPC), notifica:
///   1) La `Room` padre tramite `Room.NotifyInteractionTouched(this)`
///   2) L'`NPC_Controller` presente sul collider tramite `NotifyTouchedInteraction(this)`
///
/// Relazioni e chi si collega a cosa:
/// - `Interaction.OnTriggerEnter` -> chiama `Room.NotifyInteractionTouched` (la Room emetterà `Room.OnInteractionTriggeredByNPC`).
/// - `Interaction.OnTriggerEnter` -> chiama `NPC_Controller.NotifyTouchedInteraction` se il collider ha il componente `NPC_Controller`.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Interaction : MonoBehaviour
{
    [TextArea] public string wishText = "Desiderio NPC...";
    public bool isTrigger = true;

    private void Reset()
    {
        // Imposta il collider come trigger per default (comodo durante l'authoring)
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Quando qualcosa entra nel trigger verifichiamo se è l'NPC:
        // - check su tag "NPC" oppure
        // - check se il collider contiene un componente `NPC_Controller`.
        // In entrambi i casi notifichiamo la Room e l'NPC stesso.

        if (other.CompareTag("NPC") || other.GetComponent<NPC_Controller>() != null)
        {
            // Notifica la stanza (se presente) perché la Room è responsabile di emettere eventi globali legati alla stanza.
            Room parentRoom = GetComponentInParent<Room>();
            if (parentRoom != null)
            {
                parentRoom.NotifyInteractionTouched(this);
            }

            // Notifica direttamente l'NCP (se presente) così il controller può validare successo/fallimento.
            NPC_Controller npc = other.GetComponent<NPC_Controller>();
            if (npc != null)
            {
                npc.NotifyTouchedInteraction(this);
            }
        }
    }
}