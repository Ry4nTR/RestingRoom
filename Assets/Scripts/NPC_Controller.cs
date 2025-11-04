using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NPC_Controller è responsabilità e flusso eventi:
/// - Mantiene riferimento al `NavMeshAgent` e gestisce la navigazione dell'NCP.
/// - Riceve destinazione come `Interaction` + `Room` tramite `SetDestination(...)` (tipicamente chiamato da `RoomManager`).
/// - Quando l'NCP entra nel trigger dell'`Interaction`, `Interaction` chiama `NotifyTouchedInteraction` su questo controller:
///     -> qui si confronta con il target atteso e si emette `OnDestinationReached(success, touchedInteraction)`.
/// - `RoomManager` (o altri sistemi) possono sottoscrivere `OnDestinationReached` per aggiornare lo stato di gioco, UI, ecc.
/// 
/// Note implementative:
/// - Nel `Update` viene controllato l'avvicinamento usando `remainingDistance` ma il successo definitivo è determinato dal trigger dell'Interaction.
/// - Dopo la notifica (`OnDestinationReached`) il target viene resettato e il NavMeshAgent interrompe il percorso.
/// - Se la destinazione appartiene a una Room non attiva e la Room è di tipo "mutante" (cioè diversa da Fixed),
///   l'NPC viene inviato al marker `wrongdestination` specifico di quella stanza: questo rappresenta un arrivo "sbagliato"
///   e deve essere gestito come fallimento dai consumer dell'evento `OnDestinationReached`.
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

    // evita notifiche ripetute quando l'agent è "near" il target
    private bool _arrivalNotified = false;

    // indica che l'NPC è occupato (task + roam + thinking). RoomManager controllerà questa flag.
    public bool IsBusy { get; private set; } = false;

    // indica che stiamo aspettando che l'agent raggiunga il marker 'wrongdestination'
    // (usato quando la Room di destinazione è disattivata e la Room è mutante)
    private bool _expectingWrongDestination = false;

    // Contatori e parametri per la velocità crescente
    [Header("Speed progression")]
    [Tooltip("Velocità base usata come riferimento (se <=0 verrà presa da Agent.speed in Awake)")]
    public float baseSpeed =0f;
    [Tooltip("Incremento di velocità per ogni destinazione corretta (additivo)")]
    public float speedIncreasePerSuccess =0.5f;
    [Tooltip("Velocità massima che l'agente può raggiungere")]
    public float maxSpeed =8f;

    // Moltiplicatore applicato alla penalità di velocità quando l'NPC sbaglia
    [Tooltip("Moltiplicatore dell'aumento di velocità in caso di errore rispetto all'incremento per successo (es.2 = il doppio)")]
    public float failureSpeedMultiplier =2.5f;

    private int _successfulArrivals =0;
    private int _failedArrivals =0;

    // wander
    private Coroutine _wanderRoutine;

    void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        if (Agent == null)
        {
            Debug.LogError("NPC_Controller: NavMeshAgent missing");
            return;
        }

        // Se non è stata impostata una baseSpeed via Inspector, prendila dall'agent
        if (baseSpeed <=0f)
        {
            baseSpeed = Agent.speed;
        }

        // Assicuriamoci che l'agente parta con la baseSpeed corretta
        Agent.speed = Mathf.Clamp(baseSpeed,0f, maxSpeed);
    }


    void Update()
    {
        if (!_hasActiveTarget) return;

        // Controllo se l'agent è arrivato vicino alla destinazione.
        // Se siamo vicino e non abbiamo ancora notificato l'arrivo, invochiamo il comportamento di "tocco"
        // (utile quando il trigger fisico dell'Interaction non scatta).
        if (!Agent.pathPending)
        {
            if (Agent.remainingDistance <= Agent.stoppingDistance)
            {
                if (!Agent.hasPath || Agent.velocity.sqrMagnitude ==0f)
                {
                    if (!_arrivalNotified)
                    {
                        _arrivalNotified = true;

                        // Se eravamo stati indirizzati al marker 'wrongdestination' perché la Room non è attiva
                        // e la Room è di tipo mutante, consideriamo l'arrivo come un fallimento esplicito.
                        if (_expectingWrongDestination)
                        {
                            // Applichiamo l'aumento di velocità per fallimento
                            ApplyFailureSpeedIncrease();

                            // Notifica fallimento senza interaction (consumer decide come applicare penalità)
                            OnDestinationReached?.Invoke(false, null);

                            // Reset stato interno
                            _expectingWrongDestination = false;
                            _hasActiveTarget = false;
                            _currentTargetInteraction = null;
                            _currentTargetRoom = null;
                            _arrivalNotified = false;
                            Agent.ResetPath();
                        }
                        else if (_currentTargetInteraction != null)
                        {
                            // Fallback: chiamiamo NotifyTouchedInteraction senza dipendere dal trigger fisico.
                            NotifyTouchedInteraction(_currentTargetInteraction);
                        }
                        else
                        {
                            // Nessun interaction registrata: reset sicurezza
                            _hasActiveTarget = false;
                            Agent.ResetPath();
                            _arrivalNotified = false;
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
    ///
    /// Comportamento speciale:
    /// - Se la Room target è disattivata (gameObject inactive) O l'Interaction non è attiva in gerarchia, E la Room è di tipo mutante (HouseSection != Fixed),
    ///   l'NPC viene inviato al marker `Room.wrongdestination` specifico di quella stanza. Questo consente di
    ///   avere un wrong destination diverso stanza per stanza (es. per ogni variante mutante).
    /// - In tutti gli altri casi l'NPC viene mandato verso l'Interaction richiesta.
    /// - Se il marker `wrongdestination` non è assegnato o è null, si effettua fallback verso la posizione dell'Interaction.
    /// </summary>
    public void SetDestination(Interaction interaction, Room targetRoom)
    {
        if (interaction == null)
        {
            Debug.LogWarning("NPC_Controller.SetDestination called with null interaction");
            return;
        }

        if (targetRoom == null)
        {
            Debug.LogWarning("NPC_Controller.SetDestination called with null targetRoom");
            return;
        }

        // interrompe eventuale wander
        StopWander();

        // memorizza target logico (usato per validazione quando l'Interaction viene effettivamente toccata)
        _currentTargetInteraction = interaction;
        _currentTargetRoom = targetRoom;
        _hasActiveTarget = true;
        _arrivalNotified = false;
        _expectingWrongDestination = false;

        // Determiniamo se dobbiamo forzare il wrongdestination:
        // - la stanza deve essere inattiva nella gerarchia O l'interaction non esistere/essere inattiva nella gerarchia,
        // - e la stanza deve essere di tipo "mutante" (ossia diversa da Fixed).
        bool roomActive = targetRoom.gameObject != null && targetRoom.gameObject.activeInHierarchy;
        bool interactionActive = interaction.gameObject != null && interaction.gameObject.activeInHierarchy;

        if ((!roomActive || !interactionActive) && targetRoom.RoomType != Room.HouseSection.Fixed)
        {
            if (targetRoom.wrongdestination != null)
            {
                // Indichiamo che stiamo aspettando il marker sbagliato e rimuoviamo l'interaction logica
                // per evitare che il fallback nel Update chiami NotifyTouchedInteraction con successo.
                _expectingWrongDestination = true;
                _currentTargetInteraction = null; // non vogliamo validare la interaction perché la stanza/interaction non è disponibile
                Agent.SetDestination(targetRoom.wrongdestination.position);
                if (!roomActive && interactionActive)
                {
                    Debug.Log($"NPC_Controller: Room '{targetRoom.name}' inattiva -> inviato a wrongdestination.");
                }
                else if (!interactionActive)
                {
                    Debug.Log($"NPC_Controller: Interaction '{interaction.name}' non attiva -> inviato a wrongdestination della Room '{targetRoom.name}'.");
                }
            }
            else
            {
                // Se manca il marker wrongdestination facciamo fallback all'interaction per non lasciare l'agente inattivo.
                Debug.LogWarning($"NPC_Controller: Room '{targetRoom.name}' è mutante ma manca 'wrongdestination'. Fallback alla Interaction position.");
                _expectingWrongDestination = false;
                Agent.SetDestination(interaction.transform.position);
            }

            // Impostiamo IsBusy a false: l'NPC sta navigando verso il marker di errore o fallback
            IsBusy = false;
            return;
        }

        // comportamento normale: vai verso l'interaction della room attiva
        _expectingWrongDestination = false;
        Agent.SetDestination(interaction.transform.position);

        // impostiamo IsBusy a false: la destinazione implica che l'NPC stia navigando verso il target
        IsBusy = false;
        // la UI/WishManager mostrerà la wish
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
            randomDir.y =0f;
            Vector3 samplePoint = transform.position + randomDir;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(samplePoint, out hit, radius, NavMesh.AllAreas))
            {
                Agent.SetDestination(hit.position);

                // attendi che arrivi o timeout breve
                float waitTimeout =3.0f;
                while (!Agent.pathPending && Agent.remainingDistance > Agent.stoppingDistance && waitTimeout >0f)
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

        // Se è un successo, incrementiamo il contatore e aggiornato la velocità dell'agente
        if (success)
        {
            _successfulArrivals++;
            UpdateAgentSpeedBySuccess();
        }
        else
        {
            // in caso di fallimento applica aumento di velocità maggiore
            _failedArrivals++;
            ApplyFailureSpeedIncrease();
        }

        // Notifica gli ascoltatori (RoomManager / WishManager / altri)
        OnDestinationReached?.Invoke(success, touched);

        // Reset target: l'NCP smette di navigare e sarà pronto per una nuova destinazione
        _hasActiveTarget = false;
        _currentTargetInteraction = null;
        _currentTargetRoom = null;
        _arrivalNotified = false;
        _expectingWrongDestination = false;
        Agent.ResetPath();
    }

    private void UpdateAgentSpeedBySuccess()
    {
        if (Agent == null) return;
        float newSpeed = Mathf.Min(maxSpeed, baseSpeed + _successfulArrivals * speedIncreasePerSuccess);
        Agent.speed = newSpeed;
        // opzionale: potremmo anche aggiornare acceleration se necessario
        Debug.Log($"NPC_Controller: success count={_successfulArrivals}, Agent.speed set to {Agent.speed:F2}");
    }

    private void ApplyFailureSpeedIncrease()
    {
        if (Agent == null) return;
        float delta = speedIncreasePerSuccess * failureSpeedMultiplier;
        float newSpeed = Mathf.Min(maxSpeed, Agent.speed + delta);
        Agent.speed = newSpeed;
        Debug.Log($"NPC_Controller: failure count={_failedArrivals}, applied failure speed increase {delta:F2}, Agent.speed now {Agent.speed:F2}");
    }

    // Ritorna il fattore velocità relativo alla base (es.1.0 = baseSpeed,2.0 = doppia velocità)
    public float GetSpeedFactor()
    {
        if (Agent == null || baseSpeed <=0f) return 1f;
        return Agent.speed / Mathf.Max(0.0001f, baseSpeed);
    }
}