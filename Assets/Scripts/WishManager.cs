using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gestisce la visualizzazione delle "wish bubble" quando `RoomManager` assegna
/// una interaction e coordina la sequenza dopo l'arrivo dell'NPC (stay -> lookAround -> think -> next).
/// </summary>
public class WishManager : MonoBehaviour
{
    [Header("References")]
    public RoomManager roomManager;
    public NPC_Controller npcController;
    public Canvas uiCanvasParent;

    [Header("UI Prefab")]
    public GameObject wishBubblePrefab;
    public Vector2 bubbleScreenPosition = new Vector2(-150, 200);

    [Header("Timings")]
    public float showDuration = 3.0f;           // durata del "task" sul punto
    public float lookAroundDuration = 1.5f;     // durata del roaming dopo il task
    public float wanderRadius = 2.0f;           // raggio per i piccoli spostamenti durante il lookAround
    public float thinkingDuration = 2.0f;       // breve riflessione prima della prossima wish
    public float rotationDuringThinkingSpeed = 90f;
    [Tooltip("Moltiplicatore che riduce il thinkingDelay ogni volta che l'NPC completa un wish (valore tra 0.5 e 1).")]
    [Range(0.5f, 1f)]
    public float thinkingDecayMultiplier = 0.95f;

    [Header("Behavior")]
    public bool forceNextWishAfterCompletion = true;
    public bool showDebugLogs = true;

    [Header("Initial Wish (Inspector)")]
    [Tooltip("Se abilitato, all'avvio verrà forzata la wish impostata qui sotto.")]
    public bool forceInitialWish = false;
    [Tooltip("Interaction da forzare come primo wish. Se non è assegnata, nessuna wish verrà forzata.")]
    public Interaction initialWishInteraction;
    [Tooltip("Opzionale: Room della interaction. Se non impostata verrà risolta da initialWishInteraction.GetComponentInParent<Room>().")]
    public Room initialWishRoom;

    // runtime
    private GameObject _currentBubble;
    private Coroutine _activeSequence;
    private int _wishCompletedCount = 0;

    // testo corrente della bubble (per evitare log duplicati quando si aggiorna)
    private string _currentBubbleText = null;

    // flag per tracciare che l'initial wish è stata forzata e non ancora completata (serve per comportamento post-fallimento)
    private bool _initialWishActive = false;

    private void OnEnable()
    {
        RoomManager.OnNpcNewDestination += HandleNewDestinationFromRoomManager;
        if (npcController != null) npcController.OnDestinationReached += HandleNpcDestinationReached;
    }

    private void OnDisable()
    {
        RoomManager.OnNpcNewDestination -= HandleNewDestinationFromRoomManager;
        if (npcController != null) npcController.OnDestinationReached -= HandleNpcDestinationReached;
    }

    // Auto-assegna un Canvas se non presente in Inspector
    private void Start()
    {
        if (uiCanvasParent == null && wishBubblePrefab != null)
        {
            Canvas c = FindFirstObjectByType<Canvas>();
            if (c != null)
            {
                uiCanvasParent = c;
            }
        }

        // Auto-assign npcController if missing and ensure subscription
        if (npcController == null)
        {
            npcController = FindObjectOfType<NPC_Controller>();
            if (npcController != null)
            {
                // avoid double subscription
                npcController.OnDestinationReached -= HandleNpcDestinationReached;
                npcController.OnDestinationReached += HandleNpcDestinationReached;
            }
            else
            {
                if (showDebugLogs) DebugLog("No NPC_Controller found in Start(); some behavior will be disabled.");
            }
        }
        else
        {
            // ensure subscription
            npcController.OnDestinationReached -= HandleNpcDestinationReached;
            npcController.OnDestinationReached += HandleNpcDestinationReached;
        }

        // Se richiesto, forza la wish iniziale impostata dall'Inspector
        TryForceInitialWish();
    }

    // Mostra la bubble quando RoomManager assegna una nuova destinazione
    private void HandleNewDestinationFromRoomManager(Interaction interaction, Room room)
    {
        if (interaction == null) return;
        ShowWishBubble(interaction.wishText);
    }

    // Riceve il risultato dell'arrivo dell'NPC e avvia la sequenza
    private void HandleNpcDestinationReached(bool success, Interaction touched)
    {
        if (success)
        {
            if (showDebugLogs) DebugLog($"Task started: {touched?.name}");
        }
        else
        {
            if (showDebugLogs) DebugLog($"Task failed / wrong interaction: {touched?.name}");
        }

        // Se era l'initial wish forzata, e fallisce, abilitiamo la ricerca casuale successiva
        if (_initialWishActive)
        {
            if (!success)
            {
                if (roomManager != null)
                {
                    roomManager.useRandomDestination = true;
                    if (showDebugLogs) DebugLog("Initial forced wish failed -> switching to random destinations for subsequent choices.");
                }
            }

            // Considieriamos l'initial wish consumata indipendentemente dal risultato
            _initialWishActive = false;
            forceInitialWish = false; // sicurezza ulteriore
        }

        // blocca RoomManager dall'assegnare nuove destinazioni durante la sequenza
        if (npcController != null) npcController.SetBusy(true);

        if (_activeSequence != null) StopCoroutine(_activeSequence);
        _activeSequence = StartCoroutine(Sequence_ArrivedAtWish(success, touched));
    }

    // Sequenza: mostra -> stay (task) -> lookAround (wander nella stanza) -> thinking -> chiudi -> opzionale next
    private IEnumerator Sequence_ArrivedAtWish(bool success, Interaction touched)
    {
        // stay: il NPC resta sul punto per mostrare la task
        float stayTimer = showDuration;
        while (stayTimer > 0f)
        {
            stayTimer -= Time.deltaTime;
            yield return null;
        }

        if (showDebugLogs) DebugLog("Task finished -> starting roaming in current room");

        // compute speed factor from NPC to scale durations (higher speed => shorter durations)
        float speedFactor =1f;
        if (npcController != null)
        {
            try { speedFactor = Mathf.Max(0.0001f, npcController.GetSpeedFactor()); } catch { speedFactor =1f; }
        }

        if (showDebugLogs) DebugLog($"Speed factor: {speedFactor:F2} (Agent speed {npcController?.Agent?.speed:F2} / base {(npcController != null ? npcController.GetSpeedFactor() * npcController.baseSpeed :0f)})");

        // scale lookAround and thinking durations inversely with speedFactor
        float scaledLookAround = (lookAroundDuration >0f) ? (lookAroundDuration / speedFactor) :0f;
        float scaledThinking = Mathf.Max(0.05f, thinkingDuration / speedFactor);

        if (showDebugLogs) DebugLog($"Scaled timings: lookAround={scaledLookAround:F2}s, thinking={scaledThinking:F2}s (orig lookAround={lookAroundDuration:F2}, thinking={thinkingDuration:F2})");

        // look around: piccoli spostamenti navmesh (wander) nella stanza corrente
        if (npcController != null && scaledLookAround > 0f)
        {
            if (showDebugLogs) DebugLog("NPC looking around...");
            // start wander for scaledLookAround within wanderRadius
            npcController.StartWander(scaledLookAround, wanderRadius);
            // attendi la durata del lookAround (il wander agisce in background)
            float timer = scaledLookAround;
            while (timer > 0f)
            {
                timer -= Time.deltaTime;
                yield return null;
            }
            // assicura stop wander e log fine roaming
            npcController.StopWander();
            if (showDebugLogs) DebugLog("NPC finished roaming in room");
        }

        // thinking: rotazione lenta come riflessione (azione visiva breve)
        if (showDebugLogs) DebugLog("NPC thinking...");
        float thinkTimer = scaledThinking;
        while (thinkTimer > 0f)
        {
            if (npcController != null && npcController.gameObject != null)
            {
                npcController.transform.Rotate(Vector3.up, rotationDuringThinkingSpeed * Time.deltaTime, Space.Self);
            }
            thinkTimer -= Time.deltaTime;
            yield return null;
        }

        HideWishBubble();

        _wishCompletedCount++;
        thinkingDuration *= thinkingDecayMultiplier;
        thinkingDuration = Mathf.Max(0.2f, thinkingDuration);

        if (showDebugLogs) DebugLog($"Task completed -> Idle (completed #{_wishCompletedCount})");

        // Fine della fase occupata: sblocca RoomManager per nuove assegnazioni
        if (npcController != null) npcController.SetBusy(false);

        // Se forziamo la prossima destinazione, scegliamo una Interaction in una ROOM diversa
        if (forceNextWishAfterCompletion && roomManager != null && npcController != null)
        {
            Room previousRoom = null;
            if (touched != null)
                previousRoom = touched.GetComponentInParent<Room>();

            Interaction chosen = null;
            Room chosenRoom = null;
            int attempts = 0;
            do
            {
                var pair = roomManager.GetRandomInteractionAndRoom();
                chosen = pair.Item1;
                chosenRoom = pair.Item2;
                attempts++;
                if (chosen == null || chosenRoom == null) break;
                if (previousRoom == null) break;
            } while (chosenRoom == previousRoom && attempts < 20);

            if (chosen != null && chosenRoom != null && chosenRoom != previousRoom)
            {
                if (showDebugLogs) DebugLog($"Forcing next wish: Interaction '{chosen.name}' in Room '{chosenRoom.name}'");
                // imposta destinazione e notifica l'evento in modo che la bubble venga mostrata
                npcController.SetDestination(chosen, chosenRoom);
                RoomManager.RaiseOnNpcNewDestination(chosen, chosenRoom);
            }
            else
            {
                if (showDebugLogs) DebugLog("Could not find a next interaction in a different room; skipping immediate force.");
            }
        }

        _activeSequence = null;
    }

    // Mostra o aggiorna la bubble con il testo
    private void ShowWishBubble(string text)
    {
        if (_currentBubble != null)
        {
            UpdateBubbleText(_currentBubble, text);
            return;
        }

        if (wishBubblePrefab == null)
        {
            return;
        }

        if (uiCanvasParent != null)
        {
            _currentBubble = Instantiate(wishBubblePrefab, uiCanvasParent.transform);
            RectTransform rt = _currentBubble.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = bubbleScreenPosition;
            }
        }
        else
        {
            _currentBubble = Instantiate(wishBubblePrefab);
            _currentBubble.transform.position = Vector3.zero;
        }

        UpdateBubbleText(_currentBubble, text);

        if (showDebugLogs) DebugLog($"Wish started: \"{text}\"");
    }

    // Aggiorna il testo cercando un `Text` o `TextMeshProUGUI`
    private void UpdateBubbleText(GameObject bubble, string text)
    {
        if (bubble == null) return;

        var txt = bubble.GetComponentInChildren<UnityEngine.UI.Text>();
        if (txt != null)
        {
            txt.text = text;
            _currentBubbleText = text;
            return;
        }

#if TMP_PRESENT
        var tmp = bubble.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = text;
            _currentBubbleText = text;
            return;
        }
#endif

        _currentBubbleText = text;
    }

    // Rimuove la bubble corrente
    private void HideWishBubble()
    {
        if (_currentBubble != null)
        {
            Destroy(_currentBubble);
            _currentBubble = null;
            _currentBubbleText = null;
        }
    }

    // Forza subito una nuova destinazione (utile per debug)
    public void ForceNextWishNow()
    {
        if (roomManager == null || npcController == null)
        {
            return;
        }

        var pair = room_manager_GetRandomInteractionAndRoom();
        if (pair.Item1 != null && pair.Item2 != null)
        {
            npcController.SetDestination(pair.Item1, pair.Item2);
            RoomManager.RaiseOnNpcNewDestination(pair.Item1, pair.Item2);
        }
    }

    // Prova a forzare la wish iniziale se abilitata dall'Inspector
    private void TryForceInitialWish()
    {
        if (!forceInitialWish) return;

        if (initialWishInteraction == null)
        {
            if (showDebugLogs) DebugLog("forceInitialWish abilitato ma initialWishInteraction non impostata; skipping.");
            return;
        }

        // risolvi la room se non impostata esplicitamente
        Room resolvedRoom = initialWishRoom ?? initialWishInteraction.GetComponentInParent<Room>();
        if (resolvedRoom == null)
        {
            if (showDebugLogs) DebugLog("Impossibile risolvere la Room per initialWishInteraction; skipping force.");
            return;
        }

        // assicurati di avere un NPC_Controller; prova a risolverlo se non assegnato
        if (npcController == null)
        {
            npcController = FindObjectOfType<NPC_Controller>();
            if (npcController == null)
            {
                if (showDebugLogs) DebugLog("Nessun NPC_Controller trovato per forzare la initial wish; skipping.");
                return;
            }
            else
            {
                // iscriviti all'evento se necessario
                npcController.OnDestinationReached += HandleNpcDestinationReached;
            }
        }

        // imposta destinazione e notifica subito l'evento per mostrare la bubble
        npcController.SetDestination(initialWishInteraction, resolvedRoom);
        RoomManager.RaiseOnNpcNewDestination(initialWishInteraction, resolvedRoom);

        if (showDebugLogs) DebugLog($"Initial forced wish: '{initialWishInteraction.name}' in Room '{resolvedRoom.name}'");

        // marca che l'initial wish è attiva e consumabile
        _initialWishActive = true;
        forceInitialWish = false; // disabilita per evitare ripetizioni
    }

    private void DebugLog(string msg)
    {
        Debug.Log($"[WishManager | {DateTime.Now:HH:mm:ss}] {msg}");
    }

    // small helper to avoid mangling call in ForceNextWishNow edit
    private (Interaction, Room) room_manager_GetRandomInteractionAndRoom()
    {
        if (roomManager == null) return (null, null);
        return roomManager.GetRandomInteractionAndRoom();
    }
}