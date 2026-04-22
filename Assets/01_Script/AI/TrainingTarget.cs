using UnityEngine;

/// <summary>
/// 학습용 타겟. 에피소드 시작 시 랜덤 위치로 이동.
/// 에이전트와 너무 가깝지 않게 재시도 로직 포함.
/// </summary>
public class TrainingTarget : MonoBehaviour
{
    [Header("참조 — 비우면 자동 탐색")]
    [SerializeField] private Transform arenaCenter;
    [SerializeField] private Transform agent;

    [Header("스폰 범위")]
    [Tooltip("아레나 중심으로부터 타겟이 스폰될 최대 반경.")]
    [SerializeField] private float spawnRadius = 8f;

    [Tooltip("에이전트와 최소한 이 거리는 떨어진 위치에 스폰.")]
    [SerializeField] private float minDistFromAgent = 3f;

    [Tooltip("타겟 Y 좌표 (아레나 중심 기준).")]
    [SerializeField] private float spawnY = 0.5f;

    private void Awake()
    {
        if (arenaCenter == null && transform.parent != null)
            arenaCenter = transform.parent;

        if (agent == null && arenaCenter != null)
        {
            var a = arenaCenter.GetComponentInChildren<NPCMovementAgent>();
            if (a != null) agent = a.transform;
        }
    }

    public void Respawn()
    {
        if (arenaCenter == null) return;

        for (int i = 0; i < 10; i++)
        {
            Vector3 pos = arenaCenter.position + new Vector3(
                Random.Range(-spawnRadius, spawnRadius),
                spawnY,
                Random.Range(-spawnRadius, spawnRadius)
            );

            if (agent == null || Vector3.Distance(pos, agent.position) >= minDistFromAgent)
            {
                transform.position = pos;
                return;
            }
        }

        // 10회 내 조건 만족 못하면 마지막 시도 위치 사용
        transform.position = arenaCenter.position + new Vector3(spawnRadius, spawnY, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        if (arenaCenter == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);
        Gizmos.DrawWireSphere(arenaCenter.position, spawnRadius);
    }
}
