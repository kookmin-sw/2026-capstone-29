using Mirror;
using UnityEngine;

/// <summary>
/// 근접 무기 본체.
/// - 온라인: 기존 <see cref="WeaponMelee"/>와 동일. 서버 권위로 수명·던지기 처리, RPC로 클라이언트 동기화.
/// - 오프라인: 서버/RPC 없이 본인이 모두 처리. SyncVar는 그냥 일반 필드처럼 동작.
///
/// SyncVar 자체는 오프라인에서 무해(단순 필드)이므로 그대로 둔다.
/// 분기는 (1) 권위 판정(`hasAuthority`), (2) 입력 권한(`isOwned` vs 오프라인은 본인),
/// (3) 동기화 채널(Cmd/Rpc vs 로컬 호출) 세 군데에서만 한다.
///
/// <see cref="UnifiedWeaponEquipHandler"/>와 호환:
/// 새 Handler는 Equip(GameObject, Transform) 또는 Equip(GameObject, string)을 요구하므로
/// 손 본을 결정해 넘겨준다. WeaponAttacher + WeaponSlot 우선, 실패 시 본 경로 fallback.
/// </summary>
public class UnifiedWeaponMelee : NetworkBehaviour, IPlayerWeapon, IWeaponHitBox
{
    [Header("아이템 정보")]
    [SerializeField] public ItemStatus itemStat;

    [Header("히트박스")]
    [Tooltip("근접 무기의 히트박스. 비워두면 자신에서 자동 탐색.")]
    [SerializeField] private CharacterHitBox weaponHitbox;

    [Header("속도 버프 (옵션)")]
    [Tooltip("같은 프리팹에 UnifiedAttackSpeed 가 있으면 장착/해제에 맞춰 자동 제어. 비워두면 자동 탐색.")]
    [SerializeField] private UnifiedAttackSpeed atkSpeed;

    [Header("장착 위치")]
    [Tooltip("WeaponAttacher 사용 시 부착할 슬롯.")]
    [SerializeField] private WeaponSlot attachSlot = WeaponSlot.RightHand;

    [Tooltip("WeaponAttacher가 없을 때 사용할 본 경로 fallback (예: \"Armature/Hips/.../RightHand\").")]
    [SerializeField] private string boneFallbackPath = "";

    [Header("던지기 입력")]
    [Tooltip("던지기 키 (오프라인 단일 플레이어 입력)")]
    [SerializeField] private KeyCode throwKey = KeyCode.E;

    [SyncVar] private GameObject owner;
    [SyncVar] private bool isThrown;
    [SyncVar] private Vector3 flyDirection;
    [SyncVar] private float flySpeed;
    [SyncVar] private float rollSpeed;

    private float lifeTimer;

    private void Awake()
    {
        if (weaponHitbox == null)
        {
            weaponHitbox = GetComponent<CharacterHitBox>();
        }

        if (weaponHitbox != null)
        {
            weaponHitbox.damage = itemStat.damage;
            weaponHitbox.DisableHitbox();
        }

        // 버프 컴포넌트 자동 탐색 (없으면 null)
        if (atkSpeed == null)
        {
            atkSpeed = GetComponent<UnifiedAttackSpeed>();
        }
        if (atkSpeed != null)
        {
            atkSpeed.duration = itemStat.availableTime;
        }
    }

    // -----------------------------
    // IPlayerWeapon — 무기 사용자 지정 (UnifiedSetItem이 호출)
    // -----------------------------
    public void SetUser(GameObject user)
    {
        owner = user;

        if (AuthorityGuard.IsOffline)
        {
            // 오프라인: 본인이 모든 것을 직접 처리
            if (weaponHitbox != null) weaponHitbox.EnableHitbox();
            if (atkSpeed != null) atkSpeed.ApplyTo(user);

            EquipHandler(user);
            return;
        }

        // 온라인: 서버 측 처리 + 모든 클라이언트 RPC
        if (weaponHitbox != null) weaponHitbox.EnableHitbox();
        if (atkSpeed != null) atkSpeed.ApplyTo(user);

        RpcSetUser(user);
    }

    [ClientRpc]
    private void RpcSetUser(GameObject user)
    {
        owner = user;

        // 클라이언트에서도 히트박스 활성화
        if (weaponHitbox != null) weaponHitbox.EnableHitbox();

        EquipHandler(user);
    }

    // -----------------------------
    // 장착 호출: WeaponAttacher + WeaponSlot 우선, 실패 시 본 경로 fallback
    // -----------------------------
    private void EquipHandler(GameObject user)
    {
        UnifiedWeaponEquipHandler handler = GetComponent<UnifiedWeaponEquipHandler>();
        if (handler == null) return;

        // 1) WeaponAttacher 우선 시도
        WeaponAttacher attacher = user.GetComponent<WeaponAttacher>();
        if (attacher == null) attacher = user.GetComponentInChildren<WeaponAttacher>();

        if (attacher != null)
        {
            Transform socket = attacher.GetSocket(attachSlot);
            if (socket != null)
            {
                handler.Equip(user, socket);
                return;
            }
            Debug.LogWarning($"[UnifiedWeaponMelee] WeaponAttacher가 슬롯 {attachSlot}에 본을 가지고 있지 않음 → fallback 시도.");
        }

        // 2) 본 경로 fallback
        if (!string.IsNullOrEmpty(boneFallbackPath))
        {
            handler.Equip(user, boneFallbackPath);
            return;
        }

        Debug.LogWarning($"[UnifiedWeaponMelee] {name} 장착 실패: WeaponAttacher 없음 + boneFallbackPath 미설정.");
    }

    private void UnequipHandler()
    {
        UnifiedWeaponEquipHandler handler = GetComponent<UnifiedWeaponEquipHandler>();
        if (handler != null) handler.Unequip();
    }

    // -----------------------------
    // IWeaponHitBox
    // -----------------------------
    public void SetOwner(GameObject user) { owner = user; }
    public GameObject GetOwner() { return owner; }

    // -----------------------------
    // Update : 비행 이동 + 수명 타이머 + 입력
    // -----------------------------
    private void Update()
    {
        // 비행 이동은 SyncVar로 동기화되므로 모든 클라이언트가 동일하게 계산
        if (isThrown)
        {
            transform.position += flyDirection * flySpeed * Time.deltaTime;
            transform.Rotate(Vector3.right, rollSpeed * Time.deltaTime, Space.Self);
        }

        // 권위(수명/Destroy): 온라인은 서버, 오프라인은 본인
        bool hasAuthority = AuthorityGuard.IsOffline || isServer;
        if (hasAuthority)
        {
            lifeTimer += Time.deltaTime;
            if (lifeTimer > itemStat.availableTime)
            {
                if (!isThrown)
                {
                    if (atkSpeed != null) atkSpeed.Remove();
                    NotifyUnequip();
                }

                if (AuthorityGuard.IsOffline) Destroy(gameObject);
                else NetworkServer.Destroy(gameObject);
                return;
            }
        }

        // 던지기 입력: 온라인은 소유 클라이언트만, 오프라인은 본인
        bool canInput = AuthorityGuard.IsOffline || isOwned;
        if (canInput && !isThrown && Input.GetKeyDown(throwKey))
        {
            Vector3 direction = owner != null ? owner.transform.forward : transform.forward;
            RequestThrow(direction);
        }
    }

    /// <summary>인터페이스 진입점. StarterAssetsInputs 등 외부에서 호출.</summary>
    public void ThrowWeapon()
    {
        if (isThrown) return;

        bool canInput = AuthorityGuard.IsOffline || isOwned;
        if (!canInput) return;

        Vector3 direction = owner != null ? owner.transform.forward : transform.forward;
        RequestThrow(direction);
    }

    // -----------------------------
    // 던지기 진입점 (오프라인 즉시 실행 / 온라인 Cmd)
    // -----------------------------
    private void RequestThrow(Vector3 direction)
    {
        if (AuthorityGuard.IsOffline)
        {
            ThrowLocal(direction);
            return;
        }

        CmdThrowWeapon(direction);
    }

    private void ThrowLocal(Vector3 direction)
    {
        if (isThrown) return;

        if (atkSpeed != null) atkSpeed.Remove();
        UnequipHandler();

        isThrown = true;
        flyDirection = direction.normalized;
        flySpeed = itemStat.speed;
        rollSpeed = itemStat.rollSpeed;
        lifeTimer = 0f;

        transform.rotation = Quaternion.LookRotation(flyDirection);

        // 오프라인은 RPC 대신 로컬에서 onThrown 효과 직접 적용
        OnThrownLocal(flyDirection);
    }

    [Command(requiresAuthority = true)]
    private void CmdThrowWeapon(Vector3 direction)
    {
        if (isThrown) return;

        if (atkSpeed != null) atkSpeed.Remove();
        UnequipHandler();

        // SyncVar 세팅 → 모든 클라이언트 자동 동기화
        isThrown = true;
        flyDirection = direction.normalized;
        flySpeed = itemStat.speed;
        rollSpeed = itemStat.rollSpeed;
        lifeTimer = 0f;

        transform.rotation = Quaternion.LookRotation(flyDirection);

        RpcOnThrown(flyDirection);
    }

    [ClientRpc]
    private void RpcOnThrown(Vector3 direction) => OnThrownLocal(direction);

    /// <summary>던진 직후 시각/히트박스 처리. 온라인은 RPC, 오프라인은 직접 호출.</summary>
    private void OnThrownLocal(Vector3 direction)
    {
        isThrown = true;

        UnequipHandler();

        if (weaponHitbox != null) weaponHitbox.EnableHitbox();

        transform.rotation = Quaternion.LookRotation(direction);
    }

    // -----------------------------
    // 수명 만료 시 장착 해제 알림
    // -----------------------------
    private void NotifyUnequip()
    {
        if (AuthorityGuard.IsOffline)
        {
            UnequipLocal();
            return;
        }

        if (NetworkServer.active) RpcUnequip();
    }

    [ClientRpc]
    private void RpcUnequip() => UnequipLocal();

    private void UnequipLocal()
    {
        if (weaponHitbox != null) weaponHitbox.DisableHitbox();
        UnequipHandler();
    }
}