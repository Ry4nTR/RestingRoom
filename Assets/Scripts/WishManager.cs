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

    // runtime
    private GameObject _currentBubble;
    private Coroutine _activeSequence;
    private int _wishCompletedCount = 0;

    // testo corrente della bubble (per evitare log duplicati quando si aggiorna)
    private string _currentBubbleText = null;

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

        // look around: piccoli spostamenti navmesh (wander) nella stanza corrente
        if (npcController != null && lookAroundDuration > 0f)
        {
            if (showDebugLogs) DebugLog("NPC looking around...");
            // start wander for lookAroundDuration within wanderRadius
            npcController.StartWander(lookAroundDuration, wanderRadius);
            // attendi la durata del lookAround (il wander agisce in background)
            float timer = lookAroundDuration;
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
        float thinkTimer = thinkingDuration;
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

        var pair = roomManager.GetRandomInteractionAndRoom();
        if (pair.Item1 != null && pair.Item2 != null)
        {
            npcController.SetDestination(pair.Item1, pair.Item2);
            RoomManager.RaiseOnNpcNewDestination(pair.Item1, pair.Item2);
        }
    }

    private void DebugLog(string msg)
    {
        Debug.Log($"[WishManager | {DateTime.Now:HH:mm:ss}] {msg}");
    }
}