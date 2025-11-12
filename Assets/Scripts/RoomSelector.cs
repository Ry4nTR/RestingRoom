using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;
using System.Collections;

public class RoomSelector : MonoBehaviour
{
    // Popola manualmente questa lista dall'Inspector.
    [SerializeField] private List<Room> rooms = new List<Room>();

    // Assegna automaticamente il RoomManager (usiamo PlayerRoom per impedire il cambio quando il player è dentro una stanza).
    private RoomManager roomManager;

    private TMP_Dropdown selectorDropdown;

    // indice dell'ultima selezione valida (rimane se un tentativo di cambio viene bloccato)
    private int lastValidIndex = 0;

    private void Awake()
    {
        roomManager = FindFirstObjectByType<RoomManager>();
        selectorDropdown = GetComponent<TMP_Dropdown>();
        if (selectorDropdown == null)
        {
            Debug.LogError("RoomSelector: TMP_Dropdown non trovato sul GameObject.");
            return;
        }

        // registra listener
        selectorDropdown.onValueChanged.AddListener(ChangeRoom);
    }

    private void Start()
    {
        // Inizializziamo lastValidIndex con l'indice della stanza attiva, se presente, altrimenti con il valore attuale del dropdown (clampato).
        int activeIndex = rooms.FindIndex(r => r != null && r.gameObject.activeInHierarchy);
        if (activeIndex != -1)
        {
            lastValidIndex = activeIndex;
            // assicuriamoci che il dropdown rifletta lo stato corrente (senza ri-trigger)
            selectorDropdown.onValueChanged.RemoveListener(ChangeRoom);
            selectorDropdown.value = lastValidIndex;
            selectorDropdown.onValueChanged.AddListener(ChangeRoom);
        }
        else
        {
            // valore corrente del dropdown (clampato)
            int idx = Mathf.Clamp(selectorDropdown.value, 0, Mathf.Max(0, rooms.Count - 1));
            lastValidIndex = idx;
            selectorDropdown.onValueChanged.RemoveListener(ChangeRoom);
            selectorDropdown.value = lastValidIndex;
            selectorDropdown.onValueChanged.AddListener(ChangeRoom);
        }
    }

    private void Update()
    {
        selectorDropdown.interactable = !(rooms.Any(room => room == roomManager.PlayerRoom));
    }

    private void OnDestroy()
    {
        if (selectorDropdown != null)
            selectorDropdown.onValueChanged.RemoveAllListeners();
    }

    // Chiamato dal TMP_Dropdown (o manualmente). newValue è l'indice corrispondente nella lista 'rooms' (popolata dall'Inspector).
    public void ChangeRoom(int newValue)
    {
        if (rooms == null || rooms.Count == 0)
        {
            Debug.LogWarning("RoomSelector.ChangeRoom chiamato ma la lista 'rooms' è vuota o null. Popola la lista dall'Inspector.");
            // ripristina dropdown allo stato precedente e chiudi
            RestoreDropdownToLastValid();
            return;
        }

        if (newValue < 0 || newValue >= rooms.Count)
        {
            Debug.LogWarning($"RoomSelector.ChangeRoom: indice {newValue} fuori range (0..{rooms.Count - 1}). Operazione ignorata.");
            RestoreDropdownToLastValid();
            return;
        }

        // Se è stato assegnato il RoomManager e la PlayerRoom è esistente
        if (roomManager != null && roomManager.PlayerRoom != null)
        {
            // Se il player è dentro la stanza che si sta tentando di selezionare -> blocco e chiusura dropdown
            if (roomManager.PlayerRoom == rooms[newValue])
            {
                Debug.Log($"RoomSelector: selezione ignorata perché il Player è dentro la stanza '{rooms[newValue].name}' (index {newValue}). Dropdown chiuso.");
                RestoreDropdownToLastValid();
                HideDropdown();
                return;
            }
        }
        else if (roomManager == null)
        {
            Debug.LogWarning("RoomSelector: RoomManager non assegnato nell'Inspector. Il blocco basato su PlayerRoom non verrà applicato.");
        }

        // procedi al cambio effettivo
        Room oldRoom = rooms.FirstOrDefault(r => r != null && r.gameObject.activeInHierarchy);
        Room newRoom = rooms[newValue];

        StartChangeAnimation(newValue, oldRoom, newRoom);

        // aggiorna lastValidIndex se il cambio è avvenuto
        lastValidIndex = newValue;
    }

    // Comportamento base: attiva la stanza selezionata e disattiva tutte le altre immediatamente.
    // Logga ogni attivazione/disattivazione.
    private void StartChangeAnimation(int newValue, Room oldRoom, Room newRoom)
    {
        if (newRoom == null)
        {
            Debug.LogWarning("RoomSelector: la stanza selezionata è null. Controlla la lista 'rooms' nell'Inspector.");
            RestoreDropdownToLastValid();
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            var r = rooms[i];
            if (r == null) continue;

            bool shouldBeActive = (i == newValue);
            bool currentlyActive = r.gameObject.activeInHierarchy;

            if (shouldBeActive && !currentlyActive)
            {
                // Attivazione stanza selezionata
                r.gameObject.SetActive(true);
            }
            else if (!shouldBeActive && currentlyActive)
            {
                // Disattivazione delle altre stanze
                r.gameObject.SetActive(false);
            }
            // se shouldBeActive == currentlyActive non facciamo nulla
        }

        // chiudi il dropdown dopo la selezione (comportamento UX comune)
        HideDropdown();




        // Placeholder per animazioni future: se vorrai una transizione graduale, avvia qui una coroutine.
        // StartCoroutine(RotationCoroutine(oldRoom, newRoom));
    }

    IEnumerator RotationCoroutine(Room oldRoom, Room newRoom)
    {
        // Placeholder per animazioni future. Per ora non fa nulla.
        yield return null;
    }

    // Ripristina il valore del dropdown all'ultima selezione valida senza ri-triggerare il listener
    private void RestoreDropdownToLastValid()
    {
        if (selectorDropdown == null) return;

        selectorDropdown.onValueChanged.RemoveListener(ChangeRoom);
        selectorDropdown.value = Mathf.Clamp(lastValidIndex, 0, Mathf.Max(0, rooms.Count - 1));
        selectorDropdown.onValueChanged.AddListener(ChangeRoom);
    }

    // Nasconde il dropdown (TextMeshPro TMP_Dropdown API)
    private void HideDropdown()
    {
        if (selectorDropdown == null) return;

        selectorDropdown.Hide();
    }
}
