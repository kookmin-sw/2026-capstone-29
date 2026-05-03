using Mirror;
using UnityEngine;

/// <summary>
/// 폭탄 무기 본체.
/// 던지기 입력마다 폭탄 투사체(UnifiedBombProjectile)를 별도로 스폰하고 본체는 손에 남아 있는다.
/// throwCount 만큼 던지고 나면 무기 해제 및 교체
///
/// 온라인: 서버에서 throwCount 차감 후 투사체 스폰, 모든 클라이언트는 RPC/SyncVar로 시각 동기화.
/// 오프라인: 본인이 직접 차감/스폰/Destroy.
/// </summary>
public class UnifiedWeaponBomb : NetworkBehaviour, IPlayerWeapon
{
    [Header("아이템 정보")]
    [SerializeField] public ItemStatus itemStat;

    [Header("폭탄 설정")]
    [Tooltip("총 던질 수 있는 횟수. 0이 되면 무기 해제.")]
    [SerializeField] private int maxThrowCount = 3;

    [Tooltip("폭탄 투사체 프리팹. UnifiedBombProjectile 컴포넌트를 가져야 한다.")]
    [SerializeField] private GameObject bombProjectilePrefab;

    [Tooltip("던지기 시 본인 위치 기준 시작 오프셋 (forward 기준 z, up 기준 y).")]
    [SerializeField] private Vector3 throwStartOffset = new Vector3(0f, 1.5f, 0.3f);

    [Tooltip("폭탄 비행 시간(초). 포물선 비행 시간이며 이 시간 후 착탄.")]
    [SerializeField] private float throwDuration = 0.8f;

    [Tooltip("카메라가 완전히 아래(-90°)를 볼 때의 곡사 최저 높이. 거의 직선 던지기.")]
    [SerializeField] private float minArcHeight = 0.5f;

    [Tooltip("카메라가 완전히 위(+90°)를 볼 때의 곡사 최대 높이. 수직 lob에 가까움.")]
    [SerializeField] private float maxArcHeight = 6f;

    [Tooltip("카메라 피치를 무시하고 항상 이 값을 사용하려면 체크.")]
    [SerializeField] private bool useFixedArc = false;

    [Tooltip("useFixedArc가 true일 때 사용할 고정 곡사 높이.")]
    [SerializeField] private float fixedArcHeight = 3f;

    [Tooltip("폭탄이 날아갈 수평 거리.")]
    [SerializeField] private float throwDistance = 8f;

    [Header("폭발 설정")]
    [Tooltip("착탄 후 폭발까지 대기 시간(초).")]
    [SerializeField] private float fuseDuration = 1.5f;

    [Tooltip("폭발 반경.")]
    [SerializeField] private float explosionRadius = 4f;

    [Tooltip("폭발 데미지.")]
    [SerializeField] private float explosionDamage = 50f;

    [Tooltip("폭발 이펙트 프리팹 (NetworkIdentity 권장).")]
    [SerializeField] private GameObject explosionEffectPrefab;

    [Tooltip("폭발 이펙트 자동 제거 시간(초).")]
    [SerializeField] private float explosionLifetime = 2f;

    [Header("장착 위치")]
    [SerializeField] private WeaponSlot attachSlot = WeaponSlot.RightHand;

    [Tooltip("WeaponAttacher가 없을 때 사용할 본 경로 fallback.")]
    [SerializeField] private string boneFallbackPath = "";

    [Header("던지기 입력")]
    [SerializeField] private KeyCode throwKey = KeyCode.Mouse0;

    [SyncVar] private GameObject owner;
    [SyncVar] private int remainingThrows;

    private float lifeTimer;
    private bool isDepleted; // throwCount 소진 후 파괴 대기 중

    
    private void Awake()
    {
        // 오프라인이면 NetworkBehaviour의 OnStart 콜백이 호출되지 않으므로 여기서 초기화
        if (AuthorityGuard.IsOffline)
        {
            remainingThrows = maxThrowCount;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        remainingThrows = maxThrowCount;
    }
    //무기의 사용자 설정. 이 데이터 기반으로 오너 설정.
    public void SetUser(GameObject user)
    {
        owner = user;

        if (AuthorityGuard.IsOffline)
        {
            EquipHandler(user);
            return;
        }

        // 온라인: 서버 측 처리 + 모든 클라 RPC
        RpcSetUser(user);
    }

    [ClientRpc]
    private void RpcSetUser(GameObject user)
    {
        owner = user;
        EquipHandler(user);
    }

    //장착
    private void EquipHandler(GameObject user)
    {
        UnifiedWeaponEquipHandler handler = GetComponent<UnifiedWeaponEquipHandler>();
        if (handler == null) return;

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
            Debug.LogWarning($"[UnifiedWeaponBomb] WeaponAttacher가 슬롯 {attachSlot}에 본을 가지고 있지 않음 → fallback 시도.");
        }

        if (!string.IsNullOrEmpty(boneFallbackPath))
        {
            handler.Equip(user, boneFallbackPath);
            return;
        }

        Debug.LogWarning($"[UnifiedWeaponBomb] {name} 장착 실패: WeaponAttacher 없음 + boneFallbackPath 미설정.");
    }

    private void UnequipHandler()
    {
        UnifiedWeaponEquipHandler handler = GetComponent<UnifiedWeaponEquipHandler>();
        if (handler != null) handler.Unequip();
    }

    //라이프사이클 관리 및 던지기 신호 받음
    private void Update()
    {
        bool hasAuthority = AuthorityGuard.IsOffline || isServer;

        // 권위 측: 수명 관리
        if (hasAuthority && !isDepleted)
        {
            lifeTimer += Time.deltaTime;
            if (lifeTimer > itemStat.availableTime)
            {
                ExpireWeapon();
                return;
            }
        }

        // 던지기 입력: 온라인은 소유 클라이언트만, 오프라인은 본인
        bool canInput = AuthorityGuard.IsOffline || isOwned;
        if (canInput && !isDepleted && Input.GetKeyDown(throwKey))
        {
            RequestThrow();
        }
    }


    //무기던지기 - 근접무기처럼 던지기가 적용됨. 차이는 이 무기는 던지기가 기본 공격일 뿐,.
    public void ThrowWeapon()
    {
        if (isDepleted) return;

        bool canInput = AuthorityGuard.IsOffline || isOwned;
        if (!canInput) return;

        RequestThrow();
    }

    // 던져지는 방향 세부 설정
    // 입력 측(로컬 클라/오프라인)에서 카메라 피치를 읽어 arc 높이를 계산한 후 그 값을 Cmd로 서버에 전달. 서버는 받은 arc만큼 초기 속도를 역산한다.
    private void RequestThrow()
    {
        float arcHeight = ResolveArcHeight();

        if (AuthorityGuard.IsOffline)
        {
            ThrowOnAuthority(arcHeight);
            return;
        }

        CmdThrowBomb(arcHeight);
    }

    //카메라 피치를 통한 던질 때 고도 조절
    // 카메라 피치를 [-1, 1] 범위로 정규화 후 [minArcHeight, maxArcHeight]에 매핑.
    // useFixedArc가 true이거나 카메라가 없으면 fixedArcHeight 사용.
    private float ResolveArcHeight()
    {
        if (useFixedArc) return fixedArcHeight;

        Camera cam = Camera.main;
        if (cam == null) return fixedArcHeight;

        // forward.y: 완전 아래 -1, 수평 0, 완전 위 +1
        float t = Mathf.InverseLerp(-1f, 1f, cam.transform.forward.y);
        return Mathf.Lerp(minArcHeight, maxArcHeight, t);
    }

    [Command(requiresAuthority = true)]
    private void CmdThrowBomb(float arcHeight)
    {
        // 클라가 보내온 arc 값에 안전 범위 제한 (치트 방지 + 잘못된 입력 방어)
        arcHeight = Mathf.Clamp(arcHeight, minArcHeight, maxArcHeight);
        ThrowOnAuthority(arcHeight);
    }

    // 서버 혹은 로컬에서만 호출. 폭탄 투사체 스폰, throwCount 차감, 소진 시 무기 해제.
    private void ThrowOnAuthority(float arcHeight)
    {
        if (isDepleted) return;
        if (remainingThrows <= 0) return;

        // 발사 기준점은 항상 플레이어 루트 transform. Projectile의 것이다.
        Transform refTr;
        if (owner != null)
        {
            refTr = owner.transform.root;
        }
        else
        {
            if (!AuthorityGuard.IsOffline)
            {
                Debug.LogWarning("[UnifiedWeaponBomb] owner가 null. SetUser 호출 흐름 확인 필요. 던지기 취소.");
                return;
            }

            refTr = transform.root;
            if (refTr == transform)
            {
                Debug.LogWarning("[UnifiedWeaponBomb] transform.root가 자기 자신. 부착 상태 확인 필요. 던지기 취소.");
                return;
            }
        }

        Vector3 forward = refTr.forward;
        Vector3 startPos = refTr.position
                         + refTr.forward * throwStartOffset.z
                         + refTr.up * throwStartOffset.y
                         + refTr.right * throwStartOffset.x;

        // throwDistance/arcHeight/throwDuration으로부터 초기 속도 역산.
        // 수평: 거리 / 시간, 수직: 포물선 정점이 arcHeight가 되는 초기 v.
        // arcHeight는 카메라 피치로 결정된 값.
        Vector3 horizontal = forward * (throwDistance / Mathf.Max(0.01f, throwDuration));
        Vector3 vertical = Vector3.up * (arcHeight * 4f / Mathf.Max(0.01f, throwDuration));
        Vector3 initialVelocity = horizontal + vertical;

        SpawnBombProjectile(startPos, initialVelocity);

        remainingThrows--;

        if (remainingThrows <= 0)
        {
            DepletedAndDestroy();
        }
    }

    /// 폭탄 투사체 스폰. 권위 측에서 호출. 비행은 투사체가 자체 시뮬레이션.
    private void SpawnBombProjectile(Vector3 startPos, Vector3 initialVelocity)
    {
        if (bombProjectilePrefab == null)
        {
            Debug.LogWarning("[UnifiedWeaponBomb] bombProjectilePrefab이 설정되지 않음.");
            return;
        }

        GameObject bomb = Instantiate(bombProjectilePrefab, startPos, Quaternion.identity);

        // 스폰 및 NetworkIdentity 활성화 후 클라이언트로 스폰 메시지 전송.
        if (AuthorityGuard.IsOffline)
        {
            HardenOfflineObject(bomb);
        }
        else if (NetworkServer.active && bomb.GetComponent<NetworkIdentity>() != null)
        {
            NetworkServer.Spawn(bomb);
        }

        // SyncVar에 발사 정보 세팅. 모든 클라가 같은 수식으로 위치 계산.
        UnifiedBombProjectile projectile = bomb.GetComponent<UnifiedBombProjectile>();
        if (projectile != null)
        {
            projectile.Configure(
                initialVelocity: initialVelocity,
                fuseDuration: fuseDuration,
                radius: explosionRadius,
                damage: explosionDamage,
                explosionPrefab: explosionEffectPrefab,
                explosionLifetime: explosionLifetime,
                ownerObj: owner
            );
        }
    }

    // 무기 소진 / 수명 만료 처리
    private void DepletedAndDestroy()
    {
        isDepleted = true;
        NotifyUnequip();

        if (AuthorityGuard.IsOffline) Destroy(gameObject);
        else NetworkServer.Destroy(gameObject);
    }

    private void ExpireWeapon()
    {
        isDepleted = true;
        NotifyUnequip();

        if (AuthorityGuard.IsOffline) Destroy(gameObject);
        else NetworkServer.Destroy(gameObject);
    }

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
        UnequipHandler();
    }

    // 로컬 혹은 네트워크 환경인가
    private static void HardenOfflineObject(GameObject obj)
    {
        if (obj == null) return;
        if (!AuthorityGuard.IsOffline) return;

        if (obj.TryGetComponent(out NetworkIdentity nid))
            nid.enabled = false;

        if (!obj.activeSelf) obj.SetActive(true);
    }
}