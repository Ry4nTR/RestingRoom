using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NPC_Controller � responsabilit� e flusso eventi:
/// - Mantiene riferimento al `NavMeshAgent` e gestisce la navigazione dell'NCP.
/// - Riceve destinazione come `Interaction` + `Room` tramite `SetDestination(...)` (tipicamente chiamato da `RoomManager`).
/// - Quando l'NCP entra nel trigger dell'`Interaction`, `Interaction` chiama `NotifyTouchedInteraction` su questo controller:
///     -> qui si confronta con il target atteso e si emette `OnDestinationReached(success, touchedInteraction)`.
/// - `RoomManager` (o altri sistemi) possono sottoscrivere `OnDestinationReached` per aggiornare lo stato di gioco, UI, ecc.
/// 
/// Note implementative:
/// - Nel `Update` viene controllato l'avvicinamento usando `remainingDistance` ma il successo definitivo � determinato dal trigger dell'Interaction.
/// - Dopo la notifica (`OnDestinationReached`) il target viene resettato e il NavMeshAgent interrompe il percorso.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NPC_Controller : MonoBehaviour
{
    public NavMeshAgent Agent { get; private set; }

    // Evento locale: (successo, interaction effettivamente toccata)
    public event Action<bool, Interaction> OnDestinationReached = delegate { };

    private Interaction _currentTargetInteraction;
    private Room _currentTargetRoom;
    private bool _hasActiveTarget = false;

    // evita notifiche ripetute quando l'agent � "near" il target
    private bool _arrivalNotified = false;

    // indica che l'NPC � occupato (task + roam + thinking). RoomManager controller� questa flag.
    public bool IsBusy { get; private set; } = false;

    // wander
    private Coroutine _wanderRoutine;

    void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
    }


    void Update()
    {
        if (!_hasActiveTarget) return;

        // Controllo se l'agent � arrivato vicino alla destinazione.
        // Se siamo vicino e non abbiamo ancora notificato l'arrivo, invochiamo il comportamento di "tocco"
        // (utile quando il trigger fisico dell'Interaction non scatta).
        if (!Agent.pathPending)
        {
            if (Agent.remainingDistance <= Agent.stoppingDistance)
            {
                if (!Agent.hasPath || Agent.velocity.sqrMagnitude == 0f)
                {
                    if (!_arrivalNotified)
                    {
                        _arrivalNotified = true;
                        // fallback: chiamiamo NotifyTouchedInteraction senza log ripetuti
                        if (_currentTargetInteraction != null)
                        {
                            NotifyTouchedInteraction(_currentTargetInteraction);
                        }
                        else
                        {
                            // Nessun interaction registrata: reset sicurezza
                            _hasActiveTarget = false;
                            Agent.ResetPath();
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Imposta il flag busy (usato da WishManager mentre l'NPC esegue task/roam/think).
    /// </summary>
    public void SetBusy(bool busy)
    {
        IsBusy = busy;
        if (!busy)
        {
            // quando smettiamo di essere busy interrompiamo eventuale wander residuo
            StopWander();
        }
    }

    /// <summary>
    /// Imposta la destinazione dell'NPC: memorizza target e chiama `Agent.SetDestination`.
    /// - Chiamato da `RoomManager` o altro sistema di high-level.
    /// </summary>
    public void SetDestination(Interaction interaction, Room targetRoom)
    {
        if (interaction == null)
        {
            Debug.LogWarning("NPC_Controller.SetDestination called with null interaction");
            return;
        }

        // interrompe eventuale wander
        StopWander();

        _currentTargetInteraction = interaction;
        _currentTargetRoom = targetRoom;
        _hasActiveTarget = true;
        _arrivalNotified = false; // reset notify flag per il nuovo target

        // impostiamo IsBusy a false: la destinazione implica che l'NPC stia navigando verso il target
        IsBusy = false;

        // Inizia la navigazione verso la posizione dell'interaction
        Agent.SetDestination(interaction.transform.position);
        // la UI/WishManager mostrer� la wish
    }

    /// <summary>
    /// Avvia un semplice wander usando il NavMesh: piccoli target casuali entro `radius` per `duration` secondi.
    /// Il wander viene interrotto automaticamente se viene impostata una nuova destinazione tramite `SetDestination`.
    /// </summary>
    public void StartWander(float duration, float radius)
    {
        if (_wanderRoutine != null) StopCoroutine(_wanderRoutine);
        _wanderRoutine = StartCoroutine(WanderRoutine(duration, radius));
    }

    /// <summary>
    /// Ferma il wander corrente (se attivo) e resetta il path dell'agente.
    /// </summary>
    public void StopWander()
    {
        if (_wanderRoutine != null)
        {
            StopCoroutine(_wanderRoutine);
            _wanderRoutine = null;
        }
        if (Agent != null && Agent.hasPath)
        {
            Agent.ResetPath();
        }
    }

    private IEnumerator WanderRoutine(float duration, float radius)
    {
        float endTime = Time.time + Mathf.Max(0.01f, duration);
        while (Time.time < endTime)
        {
            // scegli un punto casuale intorno all'NPC
            Vector3 randomDir = UnityEngine.Random.insideUnitSphere * radius;
            randomDir.y = 0f;
            Vector3 samplePoint = transform.position + randomDir;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(samplePoint, out hit, radius, NavMesh.AllAreas))
            {
                Agent.SetDestination(hit.position);

                // attendi che arrivi o timeout breve
                float waitTimeout = 3.0f;
                while (!Agent.pathPending && Agent.remainingDistance > Agent.stoppingDistance && waitTimeout > 0f)
                {
                    waitTimeout -= Time.deltaTime;
                    yield return null;
                }

                // piccola pausa prima di scegliere il prossimo punto
                yield return new WaitForSeconds(0.15f);
            }
            else
            {
                // non trovato punto valido, aspetta un frame
                yield return null;
            }
        }

        // terminato wander
        _wanderRoutine = null;
        if (Agent != null && Agent.hasPath)
            Agent.ResetPath();
    }

    /// <summary>
    /// Chiamato da `Interaction` quando l'NCP entra nel suo trigger.
    /// Confronta con il target atteso e notifica il risultato tramite `OnDestinationReached`.
    /// Dopo la notifica resetta lo stato di destinazione.
    /// </summary>
    public void NotifyTouchedInteraction(Interaction touched)
    {
        bool success = (_hasActiveTarget && touched == _currentTargetInteraction);

        // Notifica gli ascoltatori (RoomManager / WishManager / altri)
        OnDestinationReached?.Invoke(success, touched);

        // Reset target: l'NCP smette di navigare e sar� pronto per una nuova destinazione
        _hasActiveTarget = false;
        _currentTargetInteraction = null;
        _currentTargetRoom = null;
        _arrivalNotified = false;
        Agent.ResetPath();
    }
}
