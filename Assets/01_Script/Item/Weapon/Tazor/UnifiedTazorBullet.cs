using Mirror;
using UnityEngine;

/// <summary>
/// 테이저 총알 통합판. <see cref="TazorBullet"/>의 대체판.
///
/// - 온라인: 기존과 동일하게 <see cref="SyncVar"/>로 상태 동기화, 서버에서 수명·파괴.
/// - 오프라인: SyncVar 값이 불안정하므로 <see cref="_localOwner"/>와 평범한 bool로 로컬 관리.
///
/// 적중 시 효과:
/// 1. 데미지는 <see cref="CharacterHitBox"/>가 자동 처리.
/// 2. 피격 대상의 <see cref="ICharacterModel.RequestApplyStun"/>를 호출:
///    - 모델이 SyncVar로 스턴 상태 동기화
///    - <see cref="UnifiedCharacterView"/>가 이벤트를 받아 애니메이터 SS_Stun Bool 토글
///    - <see cref="StarterAssets.StarterAssetsInputs"/>.enabled = false 로 입력 차단
///    - 인스펙터의 전격 VFX 프리팹이 피격 대상에 자식으로 부착 (오프셋 적용)
///    - 이미 스턴 중이면 지속시간 갱신 + VFX 재스폰
///
/// 소유자 필터링:
/// <see cref="UnifiedWeaponArrow"/>와 동일하게 <see cref="WeaponOwnerRelay"/>를 자동 부착해
/// 히트박스가 자식에 있어도 소유자 역추적이 가능하도록 한다.
/// </summary>
public class UnifiedTazorBullet : NetworkBehaviour, IWeaponHitBox
{
    [Header("아이템 정보")]
    [SerializeField] public ItemStatus itemStat;

    [Header("히트박스")]
    [SerializeField] private CharacterHitBox hitBox;

    [Header("스턴 설정")]
    [Tooltip("적중 시 부여할 스턴 지속시간(초).")]
    [SerializeField] private float stunDuration = 2f;

    [Header("전격 VFX")]
    [Tooltip("스턴 동안 피격 대상에 부착될 전격 이펙트 프리팹.")]
    [SerializeField] private GameObject stunVfxPrefab;

    [Tooltip("피격 대상 기준 VFX 로컬 위치 오프셋.")]
    [SerializeField] private Vector3 stunVfxPositionOffset = Vector3.zero;

    [Tooltip("피격 대상 기준 VFX 로컬 회전 오프셋(Euler).")]
    [SerializeField] private Vector3 stunVfxRotationOffset = Vector3.zero;

    // 온라인 동기화용
    [SyncVar] private GameObject owner;
    [SyncVar] private bool isNocked;      // 발사 준비 상태
    [SyncVar] private bool isLaunched;    // 발사된 상태
    [SyncVar] private Vector3 flyDirection;
    [SyncVar] private float flySpeed;

    // 오프라인 폴백
    private GameObject _localOwner;
    private bool _localNocked;
    private bool _localLaunched;
    private Vector3 _localFlyDirection;
    private float _localFlySpeed;

    private float lifeTimer;
    private bool _hasHit; // 중복 적중 방지 (한 발의 총알이 여러 대상 맞는 것 방지)

    // ------------------------------------------------------------
    // Unity
    // ------------------------------------------------------------
    private void Awake()
    {
        if (hitBox == null)
            hitBox = GetComponent<CharacterHitBox>();
        if (hitBox == null)
            hitBox = GetComponentInChildren<CharacterHitBox>();

        if (hitBox != null && itemStat != null)
            hitBox.damage = itemStat.damage;

        if (itemStat != null)
        {
            flySpeed = itemStat.speed;
            _localFlySpeed = itemStat.speed;
        }

        EnsureOwnerRelay();

        // 클라이언트 측 RPC가 VFX 프리팹을 이름으로 역참조할 수 있도록 사전 등록.
        // 모든 클라이언트가 동일한 프리팹을 로컬 Instantiate하기 위해 필요.
        if (stunVfxPrefab != null)
            StunVfxRegistry.Register(stunVfxPrefab);
    }

    private void EnsureOwnerRelay()
    {
        if (hitBox == null) return;
        if (hitBox.gameObject == gameObject) return;

        var relay = hitBox.GetComponent<WeaponOwnerRelay>();
        if (relay == null) relay = hitBox.gameObject.AddComponent<WeaponOwnerRelay>();
        relay.SetSource(this);
    }

    private void Update()
    {
        bool nocked = IsNockedCurrent();
        bool launched = IsLaunchedCurrent();

        if (nocked) return;
        if (!launched) return;

        Vector3 dir = AuthorityGuard.IsOffline ? _localFlyDirection : flyDirection;
        float spd = AuthorityGuard.IsOffline ? _localFlySpeed : flySpeed;
        transform.position += dir * spd * Time.deltaTime;

        if (isServer || AuthorityGuard.IsOffline)
        {
            lifeTimer += Time.deltaTime;
            if (itemStat != null && lifeTimer >= itemStat.availableTime)
            {
                if (AuthorityGuard.IsOffline) Destroy(gameObject);
                else NetworkServer.Destroy(gameObject);
            }
        }
    }

    private bool IsNockedCurrent() => AuthorityGuard.IsOffline ? _localNocked : isNocked;
    private bool IsLaunchedCurrent() => AuthorityGuard.IsOffline ? _localLaunched : isLaunched;

    // ------------------------------------------------------------
    // 적중 처리
    // ------------------------------------------------------------
    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;
        if (!IsLaunchedCurrent()) return;

        // 자기 자신(소유자)에게는 적용 안 함
        GameObject ownerGo = GetOwner();
        if (ownerGo != null)
        {
            Transform otherRoot = other.transform.root;
            if (otherRoot == ownerGo.transform.root) return;
        }

        // 권한 검사 — 스턴 적용은 권한자가 결정.
        // 모델 내부에서도 다시 한 번 분기하지만, 클라이언트 측에서 OnTriggerEnter가
        // 중복 호출되는 것을 피하기 위해 여기서도 막아둠.
        bool hasAuthority = AuthorityGuard.IsOffline || isServer;
        if (!hasAuthority) return;

        // 피격 대상에서 ICharacterModel 탐색. 캐릭터 루트에 붙어있다고 가정.
        var targetModel = other.GetComponentInParent<ICharacterModel>();
        if (targetModel == null) return;

        _hasHit = true;

        targetModel.RequestApplyStun(
            stunDuration,
            stunVfxPrefab,
            stunVfxPositionOffset,
            stunVfxRotationOffset);

        if (!AuthorityGuard.IsOffline)
            RpcOnHit();

        // 적중 후 자기 자신 파괴 (관통이 필요하면 이 부분 제거)
        if (AuthorityGuard.IsOffline) Destroy(gameObject);
        else NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    private void RpcOnHit()
    {
        _hasHit = true;
        // 클라이언트 측 추가 피드백(사운드 등)이 있으면 여기서
    }

    // ------------------------------------------------------------
    // 상태 전환
    // ------------------------------------------------------------
    public void SetNocked(bool nocked)
    {
        _localNocked = nocked;
        _localLaunched = false;
        lifeTimer = 0f;
        _hasHit = false;

        if (!AuthorityGuard.IsOffline)
        {
            isNocked = nocked;
            isLaunched = false;
        }

        if (hitBox != null) hitBox.DisableHitbox();
    }

    /// <summary>호환용: itemStat.speed로 발사.</summary>
    public void Launch(Vector3 direction)
    {
        float speed = itemStat != null ? itemStat.speed : _localFlySpeed;
        Launch(direction, speed);
    }

    public void Launch(Vector3 direction, float speed)
    {
        Vector3 dirN = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

        _localNocked = false;
        _localLaunched = true;
        _localFlyDirection = dirN;
        _localFlySpeed = speed;
        lifeTimer = 0f;
        _hasHit = false;

        if (!AuthorityGuard.IsOffline)
        {
            isNocked = false;
            isLaunched = true;
            flyDirection = dirN;
            flySpeed = speed;
        }

        transform.rotation = Quaternion.LookRotation(dirN);

        if (hitBox != null)
        {
            hitBox.EnableHitbox();
            if (!AuthorityGuard.IsOffline && isServer) RpcEnableHitbox();
        }
    }

    [ClientRpc]
    private void RpcEnableHitbox()
    {
        if (hitBox != null) hitBox.EnableHitbox();
    }

    // ------------------------------------------------------------
    // IWeaponHitBox
    // ------------------------------------------------------------
    public void SetOwner(GameObject user)
    {
        _localOwner = user;
        if (!AuthorityGuard.IsOffline) owner = user;
    }

    public GameObject GetOwner()
    {
        return _localOwner != null ? _localOwner : owner;
    }
}