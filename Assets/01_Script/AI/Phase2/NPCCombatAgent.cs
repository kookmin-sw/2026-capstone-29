using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// Phase 2-A: 검(Sword) 근접 전투 학습 Agent.
///
/// - 관찰(15):
///     self localPos(x, z), self vel(x, y, z), grounded,
///     enemy rel(x, y, z), enemy vel(x, z),
///     distance, self HP/max, enemy HP/max, attack cooldown 남은 비율
/// - 액션: Continuous 2 (이동 x/z), Discrete [2, 2] (점프, 공격)
/// - 공격: 쿨다운 0.5s, 전방 1.0m Sphere 0.8m 반경 Overlap 검사, 데미지 10
/// - 보상:
///     적에게 데미지  : +0.5 * 데미지
///     적 처치        : +10
///     피격           : -0.5 * 데미지
///     자신 사망      : -10
///     낙하           : -5
///     시간 페널티    : -0.001/step
/// - Heuristic: WASD + Space(점프) + F(공격)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class NPCCombatAgent : Agent
{
    [Header("참조 - 비우면 자동 탐색")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform arenaCenter;
    [SerializeField] private DummyEnemy enemy;

    [Header("이동")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float arenaRadius = 10f;

    [Header("점프")]
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float groundCheckDistance = 1.1f;
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("공격")]
    [Tooltip("공격 쿨다운 (초).")]
    [SerializeField] private float attackCooldown = 0.5f;

    [Tooltip("검 타격 판정 거리 (에이전트 전방).")]
    [SerializeField] private float attackRange = 1.0f;

    [Tooltip("검 타격 판정 구체 반경.")]
    [SerializeField] private float attackRadius = 0.8f;

    [Tooltip("한 번 타격 당 데미지.")]
    [SerializeField] private float attackDamage = 10f;

    [Tooltip("공격 판정 대상 Layer.")]
    [SerializeField] private LayerMask attackLayer = ~0;

    [Header("HP")]
    [SerializeField] private float maxHP = 50f;

    [Header("에피소드 종료")]
    [SerializeField] private float fallY = -3f;

    [Header("보상")]
    [SerializeField] private float killReward = 10f;
    [SerializeField] private float deathPenalty = -10f;
    [SerializeField] private float damageDealtScale = 0.5f;
    [SerializeField] private float damageTakenScale = -0.5f;
    [SerializeField] private float fallPenalty = -5f;
    [SerializeField] private float timePenalty = -0.001f;

    // 내부 상태
    private float _hp;
    private float _lastAttackTime;
    private bool _grounded;

    public float CurrentHP => _hp;
    public float MaxHP => maxHP;

    public override void Initialize()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (arenaCenter == null && transform.parent != null)
            arenaCenter = transform.parent;
        if (enemy == null && arenaCenter != null)
            enemy = arenaCenter.GetComponentInChildren<DummyEnemy>();

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public override void OnEpisodeBegin()
    {
        if (arenaCenter == null) return;

        // HP / 쿨다운 리셋
        _hp = maxHP;
        _lastAttackTime = -999f;

        // 에이전트 스폰 (가장자리 1m 여유)
        float r = arenaRadius - 1f;
        Vector3 spawn = arenaCenter.position + new Vector3(
            Random.Range(-r, r),
            1f,
            Random.Range(-r, r)
        );
        transform.position = spawn;
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        rb.linearVelocity = Vector3.zero;
#else
        rb.velocity = Vector3.zero;
#endif
        rb.angularVelocity = Vector3.zero;

        // 적 리스폰
        if (enemy != null) enemy.Respawn();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (arenaCenter == null || enemy == null)
        {
            for (int i = 0; i < 15; i++) sensor.AddObservation(0f);
            return;
        }

        Vector3 localPos = arenaCenter.InverseTransformPoint(transform.position);
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        Vector3 selfVel = rb.linearVelocity;
#else
        Vector3 selfVel = rb.velocity;
#endif
        Vector3 localVel = arenaCenter.InverseTransformDirection(selfVel);

        Vector3 enemyLocalPos = arenaCenter.InverseTransformPoint(enemy.transform.position);
        Vector3 enemyRel = enemyLocalPos - localPos;
        Vector3 enemyVel = arenaCenter.InverseTransformDirection(enemy.GetVelocity());
        float dist = Vector3.Distance(transform.position, enemy.transform.position);

        float cooldownRemaining = Mathf.Clamp01(
            (attackCooldown - (Time.time - _lastAttackTime)) / attackCooldown
        );

        // 1~6: self state
        sensor.AddObservation(localPos.x / arenaRadius);
        sensor.AddObservation(localPos.z / arenaRadius);
        sensor.AddObservation(localVel.x / moveSpeed);
        sensor.AddObservation(localVel.y / 10f);
        sensor.AddObservation(localVel.z / moveSpeed);
        sensor.AddObservation(_grounded ? 1f : 0f);

        // 7~11: enemy state
        sensor.AddObservation(enemyRel.x / (arenaRadius * 2f));
        sensor.AddObservation(enemyRel.y / (arenaRadius * 2f));
        sensor.AddObservation(enemyRel.z / (arenaRadius * 2f));
        sensor.AddObservation(enemyVel.x / moveSpeed);
        sensor.AddObservation(enemyVel.z / moveSpeed);

        // 12: distance
        sensor.AddObservation(dist / (arenaRadius * 2f));

        // 13~14: HP
        sensor.AddObservation(_hp / maxHP);
        sensor.AddObservation(enemy.CurrentHP / enemy.MaxHP);

        // 15: 공격 쿨다운
        sensor.AddObservation(cooldownRemaining);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (arenaCenter == null || enemy == null) return;

        _grounded = Physics.Raycast(
            transform.position, Vector3.down, groundCheckDistance, groundLayer
        );

        float mx = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float mz = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        int jumpInput = actions.DiscreteActions[0];
        int attackInput = actions.DiscreteActions[1];

        // 이동
        Vector3 moveDir = arenaCenter.TransformDirection(new Vector3(mx, 0f, mz));
        moveDir.y = 0f;

        Vector3 desiredVel = moveDir * moveSpeed;
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        desiredVel.y = rb.linearVelocity.y;
        rb.linearVelocity = desiredVel;
#else
        desiredVel.y = rb.velocity.y;
        rb.velocity = desiredVel;
#endif

        // 점프
        if (jumpInput == 1 && _grounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        // 회전 (이동 방향)
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 0.25f);
        }

        // 공격
        if (attackInput == 1 && (Time.time - _lastAttackTime) >= attackCooldown)
        {
            PerformAttack();
            _lastAttackTime = Time.time;
        }

        // 시간 페널티
        AddReward(timePenalty);

        // 낙하
        if (transform.position.y < fallY)
        {
            AddReward(fallPenalty);
            EndEpisode();
            return;
        }
    }

    private void PerformAttack()
    {
        Vector3 origin = transform.position + transform.forward * attackRange;
        Collider[] hits = Physics.OverlapSphere(origin, attackRadius, attackLayer);

        foreach (var col in hits)
        {
            // 자기 자신 제외
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

            var dummy = col.GetComponentInParent<DummyEnemy>();
            if (dummy != null && dummy == enemy)
            {
                float actualDamage = dummy.TakeDamage(attackDamage);
                AddReward(actualDamage * damageDealtScale);

                if (!dummy.IsAlive)
                {
                    AddReward(killReward);
                    EndEpisode();
                    return;
                }
            }
        }

#if UNITY_EDITOR
        Debug.DrawRay(transform.position, transform.forward * attackRange, Color.red, 0.1f);
#endif
    }

    /// <summary>
    /// 외부(예: 적의 공격)에서 호출. Phase 2-A 에서는 DummyEnemy가 공격 안 하므로 사용 안 됨.
    /// Phase 2-B/2-C 에서 스크립트 AI/Self-play 상대가 이 함수로 데미지 전달.
    /// </summary>
    public void TakeDamage(float damage)
    {
        float actual = Mathf.Min(damage, _hp);
        _hp -= actual;
        AddReward(actual * damageTakenScale);

        if (_hp <= 0f)
        {
            AddReward(deathPenalty);
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var cont = actionsOut.ContinuousActions;
        cont[0] = Input.GetAxis("Horizontal");
        cont[1] = Input.GetAxis("Vertical");

        var disc = actionsOut.DiscreteActions;
        disc[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
        disc[1] = Input.GetKey(KeyCode.F) ? 1 : 0;
    }

    private void OnDrawGizmosSelected()
    {
        // 공격 판정 범위 시각화
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(transform.position + transform.forward * attackRange, attackRadius);

        // 아레나 경계
        if (arenaCenter != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(arenaCenter.position,
                new Vector3(arenaRadius * 2f, 0.1f, arenaRadius * 2f));
        }

        // 적 라인
        if (enemy != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, enemy.transform.position);
        }

        // grounded ray
        Gizmos.color = _grounded ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * groundCheckDistance);
    }
}
