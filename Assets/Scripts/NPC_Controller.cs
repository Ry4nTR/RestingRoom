// NPC_Controller.cs
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class NPC_Controller : MonoBehaviour
{
    [Header("Navigation")]
    public NavMeshAgent agent;
    public float baseSpeed = 3.5f;
    public float maxSpeed = 8f;

    [Header("Speed Progression")]
    public float speedIncreasePerSuccess = 0.5f;
    public float failureSpeedMultiplier = 2.5f;

    [Header("Behavior Timings")]
    public float interactDuration = 2f;
    public float wanderDuration = 3f;
    public float thinkDuration = 2f;
    public float frustrationDuration = 3f; // Time for 720 rotation at wrong destination

    [Header("Wander Settings")]
    public float wanderRadius = 2f;

    // Events
    public event Action<bool, Interaction> OnDestinationReached;

    // Runtime state
    public bool IsBusy { get; private set; }
    public Interaction CurrentTarget { get; private set; }
    public Room CurrentTargetRoom { get; private set; }
    public Room CurrentRoom { get; private set; }

    private int successfulTrips;
    private int failedTrips;
    private Coroutine currentBehaviorRoutine;
    private bool isGoingToWrongDestination;
    private bool hasReachedDestination;
    private bool hasValidatedRoom; // NEW: Track if room validation happened

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        agent.speed = baseSpeed;
    }

    private void Update()
    {
        // Check if we've reached our destination (for wrong destinations that don't have triggers)
        if (!IsBusy && !hasReachedDestination && agent.hasPath && !agent.pathPending)
        {
            if (agent.remainingDistance <= agent.stoppingDistance)
            {
                if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
                {
                    hasReachedDestination = true;

                    if (isGoingToWrongDestination)
                    {
                        // Reached wrong destination - do frustration sequence
                        Debug.Log($"[NPC] Reached wrong destination - starting frustration behavior");
                        StartCoroutine(FrustrationAtWrongDestination());
                    }
                    else if (CurrentTarget != null && !hasValidatedRoom)
                    {
                        // If we reached target but never validated room, it means we never entered the room
                        // This can happen if the room is deactivated while NPC is en route
                        Debug.Log($"[NPC] Reached target but never entered room - room might be deactivated");
                        bool success = IsInteractionValid(CurrentTarget, CurrentTargetRoom);
                        StartBehaviorSequence(success, CurrentTarget);
                    }
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if NPC entered a room
        Room room = other.GetComponent<Room>();
        if (room != null)
        {
            CurrentRoom = room;
            hasValidatedRoom = true; // NEW: Mark that we've validated a room
            Debug.Log($"[NPC] Entered room: {room.name}");

            // If this is our target room, check if the interaction is available
            if (CurrentTargetRoom != null && room == CurrentTargetRoom && !isGoingToWrongDestination)
            {
                bool interactionValid = IsInteractionValid(CurrentTarget, CurrentTargetRoom);

                Debug.Log($"[NPC] Room validation - Interaction available: {interactionValid}");

                if (!interactionValid)
                {
                    // Interaction not available - go to wrong destination
                    Debug.Log($"[NPC] Target interaction not available in room - going to wrong destination");
                    GoToWrongDestination();
                }
                // If interaction is valid, we wait for the interaction trigger to fire
            }
        }
    }

    private bool IsInteractionValid(Interaction interaction, Room room)
    {
        // Check if room is active and interaction exists and is active
        return room != null && room.IsActive &&
               interaction != null && interaction.IsActive &&
               room.AvailableInteractions.Contains(interaction);
    }

    private void GoToWrongDestination()
    {
        if (CurrentTargetRoom == null || CurrentTargetRoom.wrongDestinationPoint == null)
        {
            Debug.LogWarning($"[NPC] No wrong destination available - treating as failure");
            StartBehaviorSequence(false, CurrentTarget);
            return;
        }

        isGoingToWrongDestination = true;
        hasReachedDestination = false; // Reset this so Update can detect when we reach wrong destination
        agent.SetDestination(CurrentTargetRoom.wrongDestinationPoint.position);
        Debug.Log($"[NPC] Redirecting to wrong destination in {CurrentTargetRoom.name} at position: {CurrentTargetRoom.wrongDestinationPoint.position}");
    }

    private IEnumerator FrustrationAtWrongDestination()
    {
        IsBusy = true;
        agent.ResetPath();

        Debug.Log($"[NPC] Starting frustration behavior at wrong destination");

        // Update speed for failure
        failedTrips++;
        float speedIncrease = speedIncreasePerSuccess * failureSpeedMultiplier;
        agent.speed = Mathf.Min(maxSpeed, agent.speed + speedIncrease);
        Debug.Log($"[NPC] FAILURE! Speed: {agent.speed}");

        // Do the 720 rotation frustration behavior
        Debug.Log($"[NPC] Showing frustration for {frustrationDuration}s");
        yield return StartCoroutine(FrustrationBehavior(frustrationDuration));

        // Thinking phase after frustration
        Debug.Log($"[NPC] Thinking for {thinkDuration}s");
        yield return StartCoroutine(ThinkingBehavior(thinkDuration));

        // Notify completion
        Debug.Log($"[NPC] Frustration sequence complete");
        OnDestinationReached?.Invoke(false, null);

        // Clean up
        CurrentTarget = null;
        CurrentTargetRoom = null;
        IsBusy = false;
        isGoingToWrongDestination = false;
        hasReachedDestination = false;
        hasValidatedRoom = false;
        currentBehaviorRoutine = null;
    }

    public void SetDestination(Interaction interaction, Room room)
    {
        if (IsBusy)
        {
            Debug.LogWarning("[NPC] Cannot set destination - NPC is busy");
            return;
        }

        StopAllBehaviors();

        CurrentTarget = interaction;
        CurrentTargetRoom = room;
        isGoingToWrongDestination = false;
        hasReachedDestination = false;
        hasValidatedRoom = false; // NEW: Reset validation flag

        Debug.Log($"[NPC] New destination: {interaction.name} in {room.name}");

        // Always start by going to the interaction first
        // We'll check if it's valid when we enter the room
        agent.SetDestination(interaction.transform.position);
    }

    public void NotifyInteractionReached(Interaction interaction)
    {
        if (IsBusy || hasReachedDestination)
        {
            Debug.LogWarning($"[NPC] Ignoring interaction - already busy or reached destination");
            return;
        }

        hasReachedDestination = true;
        bool success = (interaction == CurrentTarget && IsInteractionValid(interaction, CurrentTargetRoom));

        Debug.Log($"[NPC] Reached interaction: {interaction.name} - Success: {success}");

        StartBehaviorSequence(success, interaction);
    }

    private void StartBehaviorSequence(bool success, Interaction interaction)
    {
        if (currentBehaviorRoutine != null)
        {
            Debug.Log("[NPC] Stopping previous behavior routine");
            StopCoroutine(currentBehaviorRoutine);
        }

        currentBehaviorRoutine = StartCoroutine(BehaviorSequence(success, interaction));
    }

    private IEnumerator BehaviorSequence(bool success, Interaction interaction)
    {
        IsBusy = true;
        agent.ResetPath();

        Debug.Log($"[NPC] Starting behavior sequence - Success: {success}");

        if (success)
        {
            successfulTrips++;
            agent.speed = Mathf.Min(maxSpeed, baseSpeed + successfulTrips * speedIncreasePerSuccess);
            Debug.Log($"[NPC] SUCCESS! Speed: {agent.speed}");

            // SUCCESS SEQUENCE: Interact -> Wander -> Think
            yield return new WaitForSeconds(interactDuration);

            yield return StartCoroutine(WanderBehavior(wanderDuration));

            yield return StartCoroutine(ThinkingBehavior(thinkDuration));
        }
        else
        {
            // This should only happen if we failed without going to wrong destination
            failedTrips++;
            float speedIncrease = speedIncreasePerSuccess * failureSpeedMultiplier;
            agent.speed = Mathf.Min(maxSpeed, agent.speed + speedIncrease);
            Debug.Log($"[NPC] FAILURE! Speed: {agent.speed}");

            // Direct failure sequence (without wrong destination)
            yield return StartCoroutine(ThinkingBehavior(thinkDuration));

            yield return StartCoroutine(WanderBehavior(wanderDuration));
        }

        // Notify completion
        Debug.Log($"[NPC] Behavior sequence complete");
        OnDestinationReached?.Invoke(success, interaction);

        // Clean up
        CurrentTarget = null;
        CurrentTargetRoom = null;
        IsBusy = false;
        isGoingToWrongDestination = false;
        hasReachedDestination = false;
        hasValidatedRoom = false;
        currentBehaviorRoutine = null;

        Debug.Log("[NPC] Ready for next wish!");
    }

    private IEnumerator FrustrationBehavior(float duration)
    {
        // 720 degrees rotation = 2 full rotations
        float totalRotation = 720f;
        float rotationSpeed = totalRotation / duration;
        float timer = 0f;

        Debug.Log($"[NPC] Starting 720-degree frustration rotation");

        while (timer < duration)
        {
            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
            timer += Time.deltaTime;
            yield return null;
        }

        Debug.Log($"[NPC] Finished frustration rotation");
    }

    private IEnumerator WanderBehavior(float duration)
    {
        float endTime = Time.time + duration;
        int wanderPointCount = 0;

        Debug.Log($"[NPC] Starting wander behavior for {duration}s");

        while (Time.time < endTime && IsBusy)
        {
            Vector3 randomPoint = transform.position + UnityEngine.Random.insideUnitSphere * wanderRadius;
            randomPoint.y = transform.position.y;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, wanderRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                wanderPointCount++;

                // Wait until reached or timeout
                float waitTime = 0f;
                while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
                {
                    waitTime += Time.deltaTime;
                    if (waitTime > 2f) break;
                    yield return null;
                }
            }

            yield return new WaitForSeconds(0.3f);
        }

        agent.ResetPath();
        Debug.Log($"[NPC] Finished wander behavior after {wanderPointCount} points");
    }

    private IEnumerator ThinkingBehavior(float duration)
    {
        float rotationSpeed = 90f;
        float timer = 0f;

        Debug.Log($"[NPC] Starting thinking behavior for {duration}s");

        while (timer < duration)
        {
            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
            timer += Time.deltaTime;
            yield return null;
        }

        Debug.Log($"[NPC] Finished thinking behavior");
    }

    private void StopAllBehaviors()
    {
        if (currentBehaviorRoutine != null)
        {
            StopCoroutine(currentBehaviorRoutine);
            currentBehaviorRoutine = null;
            Debug.Log("[NPC] Stopped all behaviors");
        }

        if (agent != null)
        {
            agent.ResetPath();
        }

        IsBusy = false;
        isGoingToWrongDestination = false;
        hasReachedDestination = false;
        hasValidatedRoom = false;
    }

    public float GetSpeedFactor()
    {
        return agent.speed / baseSpeed;
    }
}