using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// Phase 1-A: 이동 학습용 Agent.
/// - 관찰(7): 로컬 위치(x,z), 로컬 속도(x,z), 타겟 상대 위치(x,z), 전방 지면 여부(1)
/// - 액션(연속 2): x/z 이동 입력 (-1..1)
/// - 보상: 타겟 접근(+), 도달(+10), 낙하(-5), 시간 페널티(-0.001/step)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class NPCMovementAgent : Agent
{
    [Header("참조 — 비우면 자동 탐색")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform target;
    [SerializeField] private Transform arenaCenter;

    [Header("이동")]
    [Tooltip("NPC 최대 이동 속도 (m/s).")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("아레나 절반 크기 (정사각형 반지름). ArenaBuilder와 일치해야 함.")]
    [SerializeField] private float arenaRadius = 10f;

    [Header("에피소드 종료")]
    [Tooltip("타겟과 이 거리 이하면 도달로 판정.")]
    [SerializeField] private float reachDistance = 1.5f;

    [Tooltip("이 Y 좌표 아래로 내려가면 낙하 판정 후 에피소드 종료.")]
    [SerializeField] private float fallY = -3f;

    [Header("보상")]
    [SerializeField] private float reachReward = 10f;
    [SerializeField] private float fallPenalty = -5f;

    [Tooltip("타겟과의 거리 감소량(m)에 곱할 보상 계수.")]
    [SerializeField] private float approachRewardScale = 0.5f;

    [Tooltip("매 step 마다 적용되는 시간 페널티 (음수).")]
    [SerializeField] private float timePenalty = -0.001f;

    // 내부 상태
    private float _prevDistance;
    private Vector3 _arenaLocalStart;

    public override void Initialize()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (arenaCenter == null && transform.parent != null)
            arenaCenter = transform.parent;
        if (target == null && arenaCenter != null)
        {
            var tt = arenaCenter.GetComponentInChildren<TrainingTarget>();
            if (tt != null) target = tt.transform;
        }

        // 물리 권장 설정
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public override void OnEpisodeBegin()
    {
        if (arenaCenter == null) return;

        // 에이전트 랜덤 위치 (가장자리 안쪽 1m 여유)
        float r = arenaRadius - 1f;
        Vector3 spawn = arenaCenter.position + new Vector3(
            Random.Range(-r, r),
            1f,
            Random.Range(-r, r)
        );
        transform.position = spawn;
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        // 속도 리셋
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        rb.linearVelocity = Vector3.zero;
#else
        rb.velocity = Vector3.zero;
#endif
        rb.angularVelocity = Vector3.zero;

        // 타겟 리스폰
        if (target != null)
        {
            var tt = target.GetComponent<TrainingTarget>();
            if (tt != null) tt.Respawn();
        }

        _prevDistance = target != null
            ? Vector3.Distance(transform.position, target.position)
            : 0f;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (arenaCenter == null || target == null)
        {
            // 안전장치: 관찰 차원 유지
            for (int i = 0; i < 7; i++) sensor.AddObservation(0f);
            return;
        }

        Vector3 localPos = arenaCenter.InverseTransformPoint(transform.position);
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        Vector3 vel = rb.linearVelocity;
#else
        Vector3 vel = rb.velocity;
#endif
        Vector3 localVel = arenaCenter.InverseTransformDirection(vel);
        Vector3 localTarget = arenaCenter.InverseTransformPoint(target.position);
        Vector3 targetRel = localTarget - localPos;

        // 정규화 후 관찰 추가
        sensor.AddObservation(localPos.x / arenaRadius);
        sensor.AddObservation(localPos.z / arenaRadius);
        sensor.AddObservation(localVel.x / moveSpeed);
        sensor.AddObservation(localVel.z / moveSpeed);
        sensor.AddObservation(targetRel.x / (arenaRadius * 2f));
        sensor.AddObservation(targetRel.z / (arenaRadius * 2f));

        // 전방 1m 지점 바닥 있음 여부 (낙하 방지 학습 힌트)
        Vector3 probe = transform.position + transform.forward * 1f + Vector3.up * 0.5f;
        bool groundAhead = Physics.Raycast(probe, Vector3.down, 2f);
        sensor.AddObservation(groundAhead ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (arenaCenter == null || target == null) return;

        float mx = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float mz = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        // 아레나 로컬 기준 이동 (수평만)
        Vector3 moveDir = arenaCenter.TransformDirection(new Vector3(mx, 0f, mz));
        moveDir.y = 0f;

        Vector3 desiredVel = moveDir * moveSpeed;
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        desiredVel.y = rb.linearVelocity.y; // 중력 성분 유지
        rb.linearVelocity = desiredVel;
#else
        desiredVel.y = rb.velocity.y;
        rb.velocity = desiredVel;
#endif

        // 이동 방향으로 서서히 회전
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 0.25f);
        }

        // ---- 보상 ----
        float distance = Vector3.Distance(transform.position, target.position);

        // 접근 보상: 이전 step 대비 거리 감소분 * scale
        float delta = _prevDistance - distance;
        AddReward(delta * approachRewardScale);
        _prevDistance = distance;

        // 시간 페널티
        AddReward(timePenalty);

        // 도달
        if (distance < reachDistance)
        {
            AddReward(reachReward);
            EndEpisode();
            return;
        }

        // 낙하
        if (transform.position.y < fallY)
        {
            AddReward(fallPenalty);
            EndEpisode();
            return;
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // WASD로 수동 테스트
        var cont = actionsOut.ContinuousActions;
        cont[0] = Input.GetAxis("Horizontal");
        cont[1] = Input.GetAxis("Vertical");
    }

    private void OnDrawGizmosSelected()
    {
        if (arenaCenter == null) return;
        Gizmos.color = Color.cyan;
        Vector3 c = arenaCenter.position;
        float s = arenaRadius;
        Gizmos.DrawWireCube(c, new Vector3(s * 2f, 0.1f, s * 2f));

        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target.position);
            Gizmos.DrawWireSphere(target.position, reachDistance);
        }
    }
}
