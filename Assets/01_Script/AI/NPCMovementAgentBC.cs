using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// Phase 1-BC: Obstacle + Jump 통합 학습 Agent.
///
/// - Vector 관찰(9): 로컬 위치(x,z), 속도(x,y,z), 타겟 상대(x,z), 거리, grounded
/// - 추가 관찰(Prefab에 별도 부착): RayPerceptionSensorComponent3D 로 장애물/지형 감지
/// - 액션: 연속2 (x/z 이동), 이산1 [2] (점프: 0=안함, 1=점프)
/// - 보상: 접근(+), 도달(+10), 낙하(-5), 장애물 충돌(-0.1), 시간 페널티(-0.001/step)
///
/// Heuristic: WASD + Space(점프).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class NPCMovementAgentBC : Agent
{
    [Header("참조 — 비우면 자동 탐색")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform target;
    [SerializeField] private Transform arenaCenter;

    [Header("이동")]
    [Tooltip("NPC 최대 수평 이동 속도 (m/s).")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("아레나 절반 크기. ArenaBuilderBC와 일치해야 함.")]
    [SerializeField] private float arenaRadius = 10f;

    [Header("점프")]
    [Tooltip("점프 시 위쪽 impulse 세기.")]
    [SerializeField] private float jumpForce = 7f;

    [Tooltip("grounded 판정용 raycast 거리. 캡슐 센터(높이 1)에서 아래로 측정.")]
    [SerializeField] private float groundCheckDistance = 1.1f;

    [Tooltip("grounded 판정 시 대상 Layer. 기본값은 모든 Layer.")]
    [SerializeField] private LayerMask groundLayer = ~0;

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

    [Tooltip("장애물 충돌 시 한 번 적용되는 페널티 (음수).")]
    [SerializeField] private float obstacleHitPenalty = -0.1f;

    // 내부 상태
    private float _prevDistance;
    private bool _grounded;

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
            for (int i = 0; i < 9; i++) sensor.AddObservation(0f);
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
        float dist = Vector3.Distance(transform.position, target.position);

        sensor.AddObservation(localPos.x / arenaRadius);
        sensor.AddObservation(localPos.z / arenaRadius);
        sensor.AddObservation(localVel.x / moveSpeed);
        sensor.AddObservation(localVel.y / 10f); // 점프 속도 대응 (jumpForce~7 기준 여유 있게 10으로 정규화)
        sensor.AddObservation(localVel.z / moveSpeed);
        sensor.AddObservation(targetRel.x / (arenaRadius * 2f));
        sensor.AddObservation(targetRel.z / (arenaRadius * 2f));
        sensor.AddObservation(dist / (arenaRadius * 2f));
        sensor.AddObservation(_grounded ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (arenaCenter == null || target == null) return;

        // grounded 체크 (매 step 갱신)
        _grounded = Physics.Raycast(
            transform.position,
            Vector3.down,
            groundCheckDistance,
            groundLayer
        );

        float mx = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float mz = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        int jumpInput = actions.DiscreteActions[0]; // 0 or 1

        // 수평 이동 (아레나 로컬 기준)
        Vector3 moveDir = arenaCenter.TransformDirection(new Vector3(mx, 0f, mz));
        moveDir.y = 0f;

        Vector3 desiredVel = moveDir * moveSpeed;
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        desiredVel.y = rb.linearVelocity.y; // 중력/점프 성분 유지
        rb.linearVelocity = desiredVel;
#else
        desiredVel.y = rb.velocity.y;
        rb.velocity = desiredVel;
#endif

        // 점프 (grounded일 때만 유효)
        if (jumpInput == 1 && _grounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        // 이동 방향으로 서서히 회전
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 0.25f);
        }

        // ---- 보상 ----
        float distance = Vector3.Distance(transform.position, target.position);

        float delta = _prevDistance - distance;
        AddReward(delta * approachRewardScale);
        _prevDistance = distance;

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
        // WASD + Space
        var cont = actionsOut.ContinuousActions;
        cont[0] = Input.GetAxis("Horizontal");
        cont[1] = Input.GetAxis("Vertical");

        var disc = actionsOut.DiscreteActions;
        disc[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    private void OnCollisionEnter(Collision collision)
    {
        var go = collision.gameObject;

        // Floor는 제외 (바닥 접촉 = grounded 전환이므로 페널티 부과하면 안 됨)
        if (go.name.Contains("Floor")) return;

        // Target은 Trigger라 OnCollisionEnter에 안 오지만 안전을 위해 체크
        if (go.GetComponent<TrainingTarget>() != null) return;

        // 나머지는 장애물로 간주
        AddReward(obstacleHitPenalty);
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

        // 발밑 grounded raycast 시각화
        Gizmos.color = _grounded ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * groundCheckDistance);
    }
}
