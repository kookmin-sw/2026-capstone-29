using Mirror;
using UnityEngine;

/// <summary>
/// 화살 오브젝트 통합판. <see cref="WeaponArrow"/>의 대체판.
///
/// - 온라인: 기존과 동일하게 <see cref="SyncVar"/>로 상태 동기화, 서버에서 수명·파괴.
/// - 오프라인: SyncVar 값이 불안정하므로 <see cref="_localOwner"/>와 평범한 bool로 로컬 관리.
///
/// 소유자 필터링 개선:
/// <see cref="CharacterHitBox.GetOwnerRoot"/>는 같은 GameObject에서만
/// <see cref="IWeaponHitBox"/>를 찾는다. 히트박스가 자식에 있을 경우를 대비해
/// Awake 단계에서 <see cref="WeaponOwnerRelay"/>를 자동 부착한다.
/// </summary>
public class UnifiedWeaponArrow : NetworkBehaviour, IWeaponHitBox
{
    [Header("아이템 정보")]
    [SerializeField] public ItemStatus itemStat;

    [Header("히트박스")]
    [SerializeField] private CharacterHitBox hitBox;

    // 온라인 동기화용
    [SyncVar] private GameObject owner;
    [SyncVar] private bool isNocked;
    [SyncVar] private bool isLaunched;
    [SyncVar] private Vector3 flyDirection;
    [SyncVar] private float flySpeed;

    // 오프라인 폴백 (SyncVar GameObject가 netId 역조회에 실패할 수 있음)
    private GameObject _localOwner;
    private bool _localNocked;
    private bool _localLaunched;
    private Vector3 _localFlyDirection;
    private float _localFlySpeed;

    private float lifeTimer;

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

        EnsureOwnerRelay();
    }

    /// <summary>
    /// 히트박스가 이 스크립트와 같은 GameObject가 아니면,
    /// 히트박스 GameObject에 <see cref="WeaponOwnerRelay"/>를 붙여
    /// <see cref="CharacterHitBox.GetOwnerRoot"/>가 소유자를 찾을 수 있도록 한다.
    /// </summary>
    private void EnsureOwnerRelay()
    {
        if (hitBox == null) return;
        if (hitBox.gameObject == gameObject) return; // 같은 오브젝트면 IWeaponHitBox가 직접 잡힘

        var relay = hitBox.GetComponent<WeaponOwnerRelay>();
        if (relay == null) relay = hitBox.gameObject.AddComponent<WeaponOwnerRelay>();
        relay.SetSource(this);
    }

    private void Update()
    {
        bool nocked   = IsNockedCurrent();
        bool launched = IsLaunchedCurrent();

        if (nocked) return;
        if (!launched) return;

        // 전방 이동
        Vector3 dir = AuthorityGuard.IsOffline ? _localFlyDirection : flyDirection;
        float   spd = AuthorityGuard.IsOffline ? _localFlySpeed     : flySpeed;
        transform.position += dir * spd * Time.deltaTime;

        // 수명 체크
        if (isServer || AuthorityGuard.IsOffline)
        {
            lifeTimer += Time.deltaTime;
            if (itemStat != null && lifeTimer >= itemStat.availableTime)
            {
                if (AuthorityGuard.IsOffline) Destroy(gameObject);
                else                          NetworkServer.Destroy(gameObject);
            }
        }
    }

    private bool IsNockedCurrent()   => AuthorityGuard.IsOffline ? _localNocked   : isNocked;
    private bool IsLaunchedCurrent() => AuthorityGuard.IsOffline ? _localLaunched : isLaunched;

    // ------------------------------------------------------------
    // 상태 전환
    // ------------------------------------------------------------
    public void SetNocked(bool nocked)
    {
        _localNocked   = nocked;
        _localLaunched = false;
        lifeTimer = 0f;

        if (!AuthorityGuard.IsOffline)
        {
            isNocked = nocked;
            isLaunched = false;
        }

        if (hitBox != null) hitBox.DisableHitbox();
    }

    public void Launch(Vector3 direction, float speed)
    {
        Vector3 dirN = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

        _localNocked   = false;
        _localLaunched = true;
        _localFlyDirection = dirN;
        _localFlySpeed = speed;
        lifeTimer = 0f;

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
        // 오프라인에선 SyncVar GameObject가 null로 보일 수 있으므로 로컬 필드 우선
        return _localOwner != null ? _localOwner : owner;
    }
}
