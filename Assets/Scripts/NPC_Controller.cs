using UnityEngine;
using UnityEngine.AI;

public class NPC_Controller : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent;

    private void Update()
    {
        if(agent.hasPath)
        {

        }
    }
}
