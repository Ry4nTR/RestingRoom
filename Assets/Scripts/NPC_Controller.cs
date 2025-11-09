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

    private int successfulTrips;
    private int failedTrips;
    private Coroutine currentBehaviorRoutine;
    private bool isGoingToWrongDestination;
    private bool hasReachedDestination;
    private Room currentRoom; // Track which room NPC is currently in

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
                        Debug.Log($"[NPC] Reached wrong destination");
                        StartBehaviorSequence(false, null);
                    }
                    else if (CurrentTarget != null)
                    {
                        // Reached target position - check if interaction is valid
                        bool success = IsInteractionValid(CurrentTarget, CurrentTargetRoom);
                        Debug.Log($"[NPC] Reached target position - Success: {success}");
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
            currentRoom = room;
            Debug.Log($"[NPC] Entered room: {room.name}");

            // If this is our target room, check if the interaction is available
            if (CurrentTargetRoom != null && room == CurrentTargetRoom && !isGoingToWrongDestination)
            {
                bool interactionValid = IsInteractionValid(CurrentTarget, CurrentTargetRoom);

                if (!interactionValid)
                {
                    // Interaction not available - go to wrong destination
                    Debug.Log($"[NPC] Target interaction not available in room - going to wrong destination");
                    GoToWrongDestination();
                }
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
            Debug.LogWarning($"[NPC] No wrong destination available - cannot redirect");
            StartBehaviorSequence(false, CurrentTarget);
            return;
        }

        isGoingToWrongDestination = true;
        hasReachedDestination = false;
        agent.SetDestination(CurrentTargetRoom.wrongDestinationPoint.position);
        Debug.Log($"[NPC] Redirecting to wrong destination in {CurrentTargetRoom.name}");
    }

    public void SetDestination(Interaction interaction, Room room)
    {
        if (IsBusy)
        {
            return;
        }

        StopAllBehaviors();

        CurrentTarget = interaction;
        CurrentTargetRoom = room;
        isGoingToWrongDestination = false;
        hasReachedDestination = false;

        Debug.Log($"[NPC] New destination: {interaction.name} in {room.name}");

        // Always start by going to the interaction first
        // We'll check if it's valid when we enter the room
        agent.SetDestination(interaction.transform.position);
    }

    public void NotifyInteractionReached(Interaction interaction)
    {
        if (IsBusy || hasReachedDestination)
        {
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
            StopCoroutine(currentBehaviorRoutine);

        currentBehaviorRoutine = StartCoroutine(BehaviorSequence(success, interaction));
    }

    private IEnumerator BehaviorSequence(bool success, Interaction interaction)
    {
        IsBusy = true;
        agent.ResetPath();

        Debug.Log($"[NPC] Starting behavior sequence - Success: {success}");

        // Update speed based on result
        if (success)
        {
            successfulTrips++;
            agent.speed = Mathf.Min(maxSpeed, baseSpeed + successfulTrips * speedIncreasePerSuccess);
            Debug.Log($"[NPC] SUCCESS! Speed: {agent.speed}");

            // SUCCESS SEQUENCE: Interact -> Wander -> Think
            //Debug.Log($"[NPC] Interacting for {interactDuration}s");
            yield return new WaitForSeconds(interactDuration);

            //Debug.Log($"[NPC] Wandering for {wanderDuration}s");
            yield return StartCoroutine(WanderBehavior(wanderDuration));

            //Debug.Log($"[NPC] Thinking for {thinkDuration}s");
            yield return StartCoroutine(ThinkingBehavior(thinkDuration));
        }
        else
        {
            failedTrips++;
            float speedIncrease = speedIncreasePerSuccess * failureSpeedMultiplier;
            agent.speed = Mathf.Min(maxSpeed, agent.speed + speedIncrease);
            Debug.Log($"[NPC] FAILURE! Speed: {agent.speed}");

            // FAILURE SEQUENCE: Frustration (720 rotation) -> Think
            Debug.Log($"[NPC] Showing frustration for {frustrationDuration}s");
            yield return StartCoroutine(FrustrationBehavior(frustrationDuration));

            Debug.Log($"[NPC] Thinking for {thinkDuration}s");
            yield return StartCoroutine(ThinkingBehavior(thinkDuration));
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
        currentBehaviorRoutine = null;
    }

    private IEnumerator FrustrationBehavior(float duration)
    {
        // 720 degrees rotation = 2 full rotations
        float totalRotation = 720f;
        float rotationSpeed = totalRotation / duration;
        float timer = 0f;

        while (timer < duration)
        {
            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
            timer += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator WanderBehavior(float duration)
    {
        float endTime = Time.time + duration;

        while (Time.time < endTime && IsBusy)
        {
            Vector3 randomPoint = transform.position + UnityEngine.Random.insideUnitSphere * wanderRadius;
            randomPoint.y = transform.position.y;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, wanderRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);

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
    }

    private IEnumerator ThinkingBehavior(float duration)
    {
        float rotationSpeed = 90f;
        float timer = 0f;

        while (timer < duration)
        {
            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
            timer += Time.deltaTime;
            yield return null;
        }
    }

    private void StopAllBehaviors()
    {
        if (currentBehaviorRoutine != null)
        {
            StopCoroutine(currentBehaviorRoutine);
            currentBehaviorRoutine = null;
        }

        agent.ResetPath();
        IsBusy = false;
        isGoingToWrongDestination = false;
        hasReachedDestination = false;
    }

    public float GetSpeedFactor()
    {
        return agent.speed / baseSpeed;
    }
}