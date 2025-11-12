using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PleasureManager : MonoBehaviour
{
    [Header("References")]
    public NPC_Controller npcController;
    public Slider pleasureBar; // assegnare lo Slider UI dall'Inspector

    [Header("Pleasure Settings")]
    public float maxPleasure = 100f;
    [Tooltip("Valore iniziale della barra.")]
    public float startPleasure = 60f;

    [Header("Penalty (on mistake/wrong destination)")]
    [Tooltip("Penalità base applicata al primo errore.")]
    public float basePenalty = 10f;
    [Tooltip("Fattore moltiplicativo della penalità per ogni errore successivo (>=1).")]
    public float penaltyGrowthFactor = 1.3f;

    [Header("Reward (on correct arrival)")]
    [Tooltip("Ricompensa base per arrivo corretto.")]
    public float baseReward = 8f;
    [Tooltip("Opzionale: sconto del conteggio errori dopo un successo (riduce penalità future).")]
    public int reduceMistakeCountOnSuccess = 1;

    // Evento emesso quando la barra arriva a zero (invia una sola notifica finché non viene ripristinata)
    public static event Action OnPleasureDepleted = delegate { };

    // Runtime
    private float _currentPleasure;
    private int _mistakeCount = 0;

    // evita di emettere l'evento ripetutamente finché non viene ripristinata la barra
    private bool _depletedRaised = false;

    private void Start()
    {
        Time.timeScale = 1;
        // Auto-assign NPC_Controller se non impostato in inspector (comodità)
        npcController = FindFirstObjectByType<NPC_Controller>();

        // inizializza barra
        _currentPleasure = Mathf.Clamp(startPleasure, 0f, maxPleasure);
        ApplyToUI();

        // controlla se inizialmente è già a zero
        CheckAndRaiseDepleted();

        // iscrizione all'evento (se disponibile)
        if (npcController != null)
        {
            npcController.OnDestinationReached += HandleNpcDestinationReached;
        }
        else
        {
            Debug.LogWarning("[PleasureManager] Nessun NPC_Controller assegnato / trovato in scena.");
        }
    }

    private void OnDestroy()
    {
        if (npcController != null)
            npcController.OnDestinationReached -= HandleNpcDestinationReached;
    }

    private void HandleNpcDestinationReached(bool success, Interaction touched)
    {
        if (success)
        {
            ApplyReward();
        }
        else
        {
            ApplyPenalty();
        }
    }

    private void ApplyPenalty()
    {
        // penalty = basePenalty * (penaltyGrowthFactor ^ mistakeCount)
        float penalty = basePenalty * Mathf.Pow(penaltyGrowthFactor, Mathf.Max(0, _mistakeCount));
        _mistakeCount++;
        _currentPleasure = Mathf.Clamp(_currentPleasure - penalty, 0f, maxPleasure);
        ApplyToUI();
        Debug.Log($"[PleasureManager] Mistake #{_mistakeCount}: -{penalty:F1} pleasure -> {_currentPleasure:F1}/{maxPleasure}");

        CheckAndRaiseDepleted();
    }

    private void ApplyReward()
    {
        _currentPleasure = Mathf.Clamp(_currentPleasure + baseReward, 0f, maxPleasure);

        // Riduce il conteggio errori per attenuare penalità future (opzionale)
        if (reduceMistakeCountOnSuccess > 0)
        {
            _mistakeCount = Mathf.Max(0, _mistakeCount - reduceMistakeCountOnSuccess);
        }

        ApplyToUI();
        //Debug.Log($"[PleasureManager] Success: +{baseReward:F1} pleasure -> {_currentPleasure:F1}/{maxPleasure} (mistakes={_mistakeCount})");

        // se la barra è stata precedentemente a zero e ora risale, permetti future notifiche di depletion
        if (_currentPleasure > 0f && _depletedRaised)
        {
            _depletedRaised = false;
            Debug.Log("[PleasureManager] Pleasure restored above 0 -> depletion event reset.");
        }
    }

    private void ApplyToUI()
    {
        if (pleasureBar != null)
        {
            pleasureBar.maxValue = maxPleasure;
            pleasureBar.value = _currentPleasure;
        }
    }

    // Verifica lo stato della barra e emette evento se arriva a zero (una volta fino al ripristino)
    private void CheckAndRaiseDepleted()
    {
        if (_currentPleasure <= 0f && !_depletedRaised)
        {
            _depletedRaised = true;
            Debug.Log("[PleasureManager] Pleasure depleted -> emitting OnPleasureDepleted");
            OnPleasureDepleted?.Invoke();
        }
    }

    // API pubblica utile per debug / test
    public void ForcePenaltyOnce()
    {
        ApplyPenalty();
    }

    public void ForceRewardOnce()
    {
        ApplyReward();
    }

    public float CurrentPleasure => _currentPleasure;
    public int MistakeCount => _mistakeCount;
    public bool IsDepleted => _depletedRaised;
}