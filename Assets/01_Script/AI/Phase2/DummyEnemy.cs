using UnityEngine;

/// <summary>
/// Phase 2-A 학습용 더미 적.
///
/// - HP 시스템 (TakeDamage로 차감, 0이 되면 IsAlive=false, 오브젝트는 비활성화)
/// - 랜덤 walk: 3초마다 방향 변경, 아레나 가장자리 부딪히면 즉시 반사
/// - NPCCombatAgent가 arenaCenter 아래 GetComponentInChildren<DummyEnemy>()로 자동 탐색
/// - Phase 2-B에서는 공격 기능 추가 예정 (지금은 안전하게 표적 역할만)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class DummyEnemy : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private float maxHP = 50f;

    [Header("이동")]
    [Tooltip("랜덤 walk 속도 (m/s). 0이면 정지.")]
    [SerializeField] private float moveSpeed = 1.5f;

    [Tooltip("방향 변경 주기 (초).")]
    [SerializeField] private float directionChangeInterval = 3f;

    [Header("아레나")]
    [Tooltip("ArenaBuilderCombat가 자동 세팅. arenaCenter 기준 반경 바깥으로 못 나감.")]
    [SerializeField] private Transform arenaCenter;

    [SerializeField] private float arenaRadius = 10f;

    private Rigidbody _rb;
    private float _currentHP;
    private Vector3 _moveDir;
    private float _nextDirChangeTime;

    public float CurrentHP => _currentHP;
    public float MaxHP => maxHP;
    public bool IsAlive => _currentHP > 0f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        if (arenaCenter == null && transform.parent != null) arenaCenter = transform.parent;

        _currentHP = maxHP;
        PickNewDirection();
    }

    private void FixedUpdate()
    {
        if (!IsAlive)
        {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
            _rb.linearVelocity = Vector3.zero;
#else
            _rb.velocity = Vector3.zero;
#endif
            return;
        }

        // 방향 전환
        if (Time.time >= _nextDirChangeTime)
            PickNewDirection();

        // 아레나 경계 반사
        if (arenaCenter != null)
        {
            Vector3 local = arenaCenter.InverseTransformPoint(transform.position);
            float r = arenaRadius - 0.5f;
            if (Mathf.Abs(local.x) > r) _moveDir.x = -Mathf.Sign(local.x) * Mathf.Abs(_moveDir.x);
            if (Mathf.Abs(local.z) > r) _moveDir.z = -Mathf.Sign(local.z) * Mathf.Abs(_moveDir.z);
        }

        Vector3 desired = _moveDir * moveSpeed;
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        desired.y = _rb.linearVelocity.y;
        _rb.linearVelocity = desired;
#else
        desired.y = _rb.velocity.y;
        _rb.velocity = desired;
#endif
    }

    private void PickNewDirection()
    {
        Vector2 d2 = Random.insideUnitCircle.normalized;
        _moveDir = new Vector3(d2.x, 0f, d2.y);
        _nextDirChangeTime = Time.time + directionChangeInterval;
    }

    public Vector3 GetVelocity()
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        return _rb != null ? _rb.linearVelocity : Vector3.zero;
#else
        return _rb != null ? _rb.velocity : Vector3.zero;
#endif
    }

    /// <summary>
    /// 데미지를 받고, 실제로 차감된 양을 반환 (HP가 넘치는 데미지는 clamp).
    /// </summary>
    public float TakeDamage(float damage)
    {
        if (!IsAlive) return 0f;
        float actual = Mathf.Min(damage, _currentHP);
        _currentHP -= actual;

        if (_currentHP <= 0f)
        {
            _currentHP = 0f;
            gameObject.SetActive(false); // 에피소드 끝에서 Respawn으로 재활성화
        }
        return actual;
    }

    /// <summary>
    /// 에피소드 시작 시 NPCCombatAgent.OnEpisodeBegin()에서 호출.
    /// HP 원복, 랜덤 위치 스폰, 이동 방향 재설정.
    /// </summary>
    public void Respawn()
    {
        _currentHP = maxHP;
        gameObject.SetActive(true);

#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        _rb.linearVelocity = Vector3.zero;
#else
        _rb.velocity = Vector3.zero;
#endif
        _rb.angularVelocity = Vector3.zero;

        if (arenaCenter != null)
        {
            // 에이전트와 겹치지 않도록 중앙에서 최소 4m 떨어지게
            Vector3 pos = Vector3.zero;
            int attempts = 0;
            do
            {
                float r = arenaRadius - 1f;
                pos = new Vector3(
                    Random.Range(-r, r),
                    0.5f,
                    Random.Range(-r, r)
                );
                attempts++;
            } while (pos.magnitude < 4f && attempts < 20);

            transform.position = arenaCenter.TransformPoint(pos);
        }

        PickNewDirection();
    }

    public void Configure(Transform center, float radius)
    {
        arenaCenter = center;
        arenaRadius = radius;
    }
}
