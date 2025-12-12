using UnityEngine;
using UnityEngine.AI;

public class DogChaseNavmesh : MonoBehaviour
{
    public string ballTag = "Ball";  // 追う対象のタグ
    private NavMeshAgent agent;
    private GameObject ball;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        // ボールを探す（毎フレームは重いので、後で最適化してもOK）
        if (!ball)
        {
            ball = GameObject.FindGameObjectWithTag(ballTag);
        }

        if (ball)
        {
            // NavMesh上の近い位置に投影して目的地に設定
            NavMeshHit hit;
            if (NavMesh.SamplePosition(ball.transform.position, out hit, 0.5f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }
}
