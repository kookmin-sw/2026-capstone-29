using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 테이저 건 무기 통합판. <see cref="WeaponTazorGun"/>의 대체판.
///
/// - 온라인: 기존과 동일하게 Cmd/Rpc로 서버 권한 동작.
/// - 오프라인: Cmd 없이 로컬에서 직접 총알 Instantiate·발사·수명 관리.
///
/// 차징 없음:
/// 활(<see cref="UnifiedWeaponBow"/>)과 달리 테이저 건은 즉발이다.
/// 좌클릭 → 즉시 발사. <see cref="ItemStatus.speed"/>로 고정 속도 발사.
///
/// 사용 횟수 제한:
/// <see cref="ItemStatus.useableTime"/>만큼 발사할 수 있으며, 횟수 소진 시 무기 파괴.
///
/// 장착 즉시 장전:
/// <see cref="OnStartAuthority"/>(온라인) 또는 <see cref="Start"/>(오프라인)에서
/// 첫 총알이 즉시 생성되어 총구 앞에 따라다닌다.
/// 한 발 발사하면 다음 발이 자동 장전되어 다시 총구 앞에 대기.
/// </summary>
public class UnifiedWeaponTazorGun : NetworkBehaviour, IPlayerWeapon
{
    [Header("아이템 정보")]
    [SerializeField] public ItemStatus itemStat;

    [Header("총알 설정")]
    [Tooltip("UnifiedTazorBullet이 붙은 총알 프리팹.")]
    [SerializeField] private GameObject bulletPrefab;

    [Tooltip("총알이 생성될 위치 (총구). 비워두면 자신의 Transform 사용.")]
    [SerializeField] private Transform nockPoint;

    [Header("장착 위치")]
    [Tooltip("WeaponAttacher 사용 시 부착할 슬롯.")]
    [SerializeField] private WeaponSlot attachSlot = WeaponSlot.RightHand;

    [Tooltip("WeaponAttacher가 슬롯 본을 못 가지고 있을 때 사용할 본 경로 fallback (예: \"Armature/Hips/.../RightHand\").")]
    [SerializeField] private string boneFallbackPath = "";

    [Header("추적 설정 (총알 스폰 오프셋)")]
    [SerializeField] public Vector3 followPositionOffset;
    [SerializeField] public Vector3 followRotationOffset;

    [Header("발사 보정")]
    [Tooltip("총 모델 추적 시 부모 forward 방향으로의 추가 오프셋.")]
    [SerializeField] private float forwardFollowDistance = 1.5f;

    [Header("히트박스")]
    [Tooltip("총 본체의 히트박스(근접 타격용). 비워두면 사용 안 함.")]
    [SerializeField] private CharacterHitBox weaponHitbox;

    [Header("Sound Settings")]
    public AudioSource audioSource;
    [Tooltip("발사 순간 재생되는 사운드 (랜덤 픽)")]
    public AudioClip[] shootSounds;
    [Range(0f, 1f)] public float shootVolume = 1f;

    [Header("수명 종료 지연")]
    [Tooltip("마지막 발사(usedChance == useableTime) 후 무기 파괴까지의 지연 시간(초). " +
             "발사 애니메이션과 마지막 총알 비행을 위해 여유를 두려면 1~2초 권장. " +
             "0이면 즉시 파괴. availableTime 자연 만료에는 적용되지 않음.")]
    [SerializeField] private float expireDelayAfterLastShot = 1.0f;

    [Header("에임 보정")]
    [SerializeField] private float aimYawOffset = 0f;

    // 현재 장전 중인 총알
    private UnifiedTazorBullet loadedBullet;
    [SyncVar] private GameObject loadedBulletObj;

    // 상태
    private float lifeTimer;
    [SyncVar] private int usedChance; // 사용한 횟수

    // 소유자 참조
    [SerializeField] public GameObject owner;

    // 다른 소켓의 무기 비활성화 목록 (해제 시 복원용)
    private readonly List<GameObject> _disabledWeapons = new List<GameObject>();

    // 첫 장전 완료 여부
    private bool _initialChargeStarted;

    // 횟수 소진으로 인한 지연 종료 진행 중 (중복 코루틴 방지)
    private bool _expireScheduled;

    private bool IsLocallyMine => AuthorityGuard.IsOffline || isOwned;
    private bool HasAuthorityRole => AuthorityGuard.IsOffline || isServer;

    // ------------------------------------------------------------
    // Unity
    // ------------------------------------------------------------
    private void Awake()
    {
        if (weaponHitbox != null && itemStat != null)
        {
            weaponHitbox.damage = itemStat.damage;
            weaponHitbox.EnableHitbox();
        }
    }

    private void Start()
    {
        // 오프라인 모드에선 OnStartAuthority가 호출되지 않으므로 Start에서 첫 장전 시도.
        if (AuthorityGuard.IsOffline && !_initialChargeStarted)
        {
            _initialChargeStarted = true;
            BeginChargeLocal();
        }
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        if (_initialChargeStarted) return;
        _initialChargeStarted = true;
        CmdBeginCharge();
    }

    // ------------------------------------------------------------
    // IPlayerWeapon
    // ------------------------------------------------------------
    public void SetUser(GameObject user)
    {
        owner = user;
        var model = user.GetComponent<ICharacterModel>();
        if (model != null) model.RequestSetHasGun(true);

        if (AuthorityGuard.IsOffline)
        {
            EquipLocal(user);
            return;
        }

        if (NetworkServer.active) RpcSetUser(user);
        else EquipLocal(user);
    }

    [ClientRpc]
    private void RpcSetUser(GameObject user)
    {
        owner = user;
        EquipLocal(user);
    }

    public void ThrowWeapon()
    {
        Debug.Log("[UnifiedWeaponTazorGun] 던지기 없음.");
    }

    // ------------------------------------------------------------
    // 장착/해제
    // ------------------------------------------------------------
    private void EquipLocal(GameObject user)
    {
        // UnifiedWeaponMelee와 동일한 본 탐색 로직.
        // attachSlot으로 명시 지정 → WeaponAttacher.GetSocket으로 본 조회 → 실패 시 boneFallbackPath.
        // (활처럼 attacher.addWeaponL을 직접 참조하지 않으므로 캐릭터에 따라 왼손에 박히는 문제 없음.)
        var handler = GetComponent<UnifiedWeaponEquipHandler>();
        if (handler != null)
        {
            // WeaponAttacher 우선 시도
            WeaponAttacher attacher = user.GetComponent<WeaponAttacher>();
            if (attacher == null) attacher = user.GetComponentInChildren<WeaponAttacher>();

            bool attached = false;
            if (attacher != null)
            {
                Transform socket = attacher.GetSocket(attachSlot);
                if (socket != null)
                {
                    handler.Equip(user, socket);
                    attached = true;
                }
                else
                {
                    Debug.LogWarning($"[UnifiedWeaponTazorGun] WeaponAttacher가 슬롯 {attachSlot}에 본을 가지고 있지 않음 → fallback 시도.");
                }
            }

            // 본 경로 fallback
            if (!attached)
            {
                if (!string.IsNullOrEmpty(boneFallbackPath))
                {
                    handler.Equip(user, boneFallbackPath);
                }
                else
                {
                    Debug.LogWarning($"[UnifiedWeaponTazorGun] {name} 장착 실패: WeaponAttacher 없음 + boneFallbackPath 미설정.");
                }
            }
        }
        else
        {
            // UnifiedWeaponEquipHandler가 없으면 레거시 핸들러로 폴백
            var legacy = GetComponent<WeaponEquipHandler>();
            if (legacy != null) legacy.Equip(user);
        }

        // 기본 무기 비활성화
        DisableOtherWeapons(user);

        // 에임 모드 ON
        var tpc = user.GetComponent<StarterAssets.UnifiedThirdPersonController>();
        if (tpc != null) tpc.SetAimMode(true, aimYawOffset);
    }

    private void DisableOtherWeapons(GameObject user)
    {
        _disabledWeapons.Clear();
        var attacher = user.GetComponent<WeaponAttacher>();
        if (attacher == null) return;

        TryDisable(attacher.addWeaponL);
        TryDisable(attacher.addWeaponR);
    }

    private void TryDisable(Transform t)
    {
        if (t == null) return;
        if (!t.gameObject.activeSelf) return;
        t.gameObject.SetActive(false);
        _disabledWeapons.Add(t.gameObject);
    }

    private void RestoreOtherWeapons()
    {
        foreach (var obj in _disabledWeapons)
            if (obj != null) obj.SetActive(true);
        _disabledWeapons.Clear();
    }

    private void UnequipLocal()
    {
        var unifiedHandler = GetComponent<UnifiedWeaponEquipHandler>();
        if (unifiedHandler != null)
        {
            unifiedHandler.Unequip();
        }
        else
        {
            var handler = GetComponent<WeaponEquipHandler>();
            if (handler != null) handler.Unequip();
        }

        RestoreOtherWeapons();

        // 에임 모드 OFF
        if (owner != null)
        {
            var tpc = owner.GetComponent<StarterAssets.UnifiedThirdPersonController>();
            if (tpc != null) tpc.SetAimMode(false);
        }
    }

    // ------------------------------------------------------------
    // Update: 추적/수명/입력
    // 총알 추적은 LateUpdate에서 (애니메이션 본 갱신 후의 nockPoint 위치를 읽기 위해)
    // ------------------------------------------------------------
    private void Update()
    {
        if (bulletPrefab == null) return;

        // 부모 위치/방향 추적 (총 본체) — 양측 클라이언트에서
        if (transform.parent != null)
        {
            transform.rotation = transform.parent.rotation;
            transform.position = transform.parent.position +
                                 transform.parent.forward * forwardFollowDistance;
        }

        // 수명/횟수 체크
        if (HasAuthorityRole)
        {
            TickLife();
            if (this == null) return;
        }

        if (!IsLocallyMine) return;
        HandleInput();
    }

    private void LateUpdate()
    {
        UpdateLoadedBulletFollow();
    }

    private void TickLife()
    {
        if (itemStat == null) return;
        lifeTimer += Time.deltaTime;

        // availableTime 자연 만료: 즉시 파괴 (기존 동작 유지)
        if (lifeTimer > itemStat.availableTime)
        {
            ExpireAndDestroy();
            return;
        }

        // 횟수 소진: 마지막 발사 애니메이션·총알 비행 시간을 위해 지연 후 파괴.
        // ShotLocal/CmdShot의 발사 후 분기에서 ScheduleExpire를 호출하므로 보통 여기는 안 탄다.
        // 외부에서 usedChance가 다른 경로로 증가했거나 발사 분기가 누락된 경우의 안전망.
        if (usedChance >= itemStat.useableTime && !_expireScheduled)
        {
            ScheduleExpire();
        }
    }

    /// <summary>
    /// 횟수 소진 시 호출. 즉시 파괴하지 않고 expireDelayAfterLastShot 후에 파괴한다.
    /// 발사 애니메이션 재생과 마지막 총알 비행을 위한 여유 시간 확보.
    /// 권한자(서버/오프라인)에서만 의미가 있고, 중복 호출은 _expireScheduled로 차단.
    /// </summary>
    private void ScheduleExpire()
    {
        if (_expireScheduled) return;
        _expireScheduled = true;

        if (expireDelayAfterLastShot <= 0f)
        {
            ExpireAndDestroy();
            return;
        }

        StartCoroutine(ExpireAfterDelayCoroutine(expireDelayAfterLastShot));
    }

    private System.Collections.IEnumerator ExpireAfterDelayCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (this == null) yield break;
        ExpireAndDestroy();
    }

    /// <summary>
    /// 장전된 총알이 총구(nockPoint)를 매 프레임 따라가도록 갱신.
    /// </summary>
    private void UpdateLoadedBulletFollow()
    {
        if (loadedBulletObj == null) return;

        Transform spawnPoint = nockPoint != null ? nockPoint : transform;
        loadedBulletObj.transform.position =
            spawnPoint.position + spawnPoint.TransformDirection(followPositionOffset);
        loadedBulletObj.transform.rotation =
            spawnPoint.rotation * Quaternion.Euler(followRotationOffset);
    }

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // 장전된 총알이 없으면 발사 불가
            if (loadedBulletObj == null) return;

            // 발사 방향: 소유자가 바라보는 방향.
            // 에임 모드에서 owner의 yaw가 카메라 yaw로 정렬되므로 화면 정중앙 시선과 거의 일치한다.
            // 카메라 raycast 방식보다 안정적 — 가까운 콜라이더에 맞아 방향이 튀는 문제 없음.
            Vector3 aimDir = owner != null ? owner.transform.forward : transform.forward;

            if (AuthorityGuard.IsOffline) ShotLocal(aimDir);
            else CmdShot(aimDir);
        }
    }

    // ------------------------------------------------------------
    // 수명 종료
    // ------------------------------------------------------------
    public void ForceExpire()
    {
        if (!HasAuthorityRole) return;
        ExpireAndDestroy();
    }

    private void ExpireAndDestroy()
    {
        // 모델에 총 해제 알림 (활의 ExpireAndDestroy 패턴과 동일)
        if (owner != null)
        {
            var model = owner.GetComponent<ICharacterModel>();
            if (model != null) model.RequestSetHasGun(false);
        }

        if (AuthorityGuard.IsOffline)
        {
            UnequipLocal();
            if (loadedBulletObj != null) Destroy(loadedBulletObj);
            Destroy(gameObject);
        }
        else
        {
            RpcUnequip();
            if (loadedBulletObj != null) NetworkServer.Destroy(loadedBulletObj);
            NetworkServer.Destroy(gameObject);
        }
    }

    [ClientRpc]
    private void RpcUnequip()
    {
        UnequipLocal();
    }

    // ------------------------------------------------------------
    // 오프라인 경로
    // ------------------------------------------------------------
    private void BeginChargeLocal()
    {
        if (usedChance >= (itemStat != null ? itemStat.useableTime : int.MaxValue)) return;
        if (loadedBulletObj != null) return; // 이미 장전된 총알이 있으면 무시

        Transform spawnPoint = nockPoint != null ? nockPoint : transform;
        GameObject bulletObj = Instantiate(
            bulletPrefab,
            spawnPoint.position + spawnPoint.TransformDirection(followPositionOffset),
            spawnPoint.rotation * Quaternion.Euler(followRotationOffset));

        HardenOfflineObject(bulletObj);

        loadedBulletObj = bulletObj;
        loadedBullet = bulletObj.GetComponent<UnifiedTazorBullet>();

        if (loadedBullet == null)
        {
            Debug.LogError("[UnifiedWeaponTazorGun] bulletPrefab에 UnifiedTazorBullet 컴포넌트가 없습니다.");
            Destroy(bulletObj);
            loadedBulletObj = null;
            return;
        }

        loadedBullet.SetNocked(true);
        loadedBullet.SetOwner(owner);
    }

    private void ShotLocal(Vector3 aimDirection)
    {
        if (loadedBullet == null) return;

        Vector3 direction = aimDirection.sqrMagnitude > 0.001f
            ? aimDirection.normalized
            : transform.forward;

        loadedBullet.Launch(direction);
        PlayShootSound();

        // 발사 애니메이션 트리거
        Debug.Log($"[UnifiedWeaponTazorGun] ShotLocal: owner={(owner != null ? owner.name : "NULL")}");
        if (owner != null)
        {
            var model = owner.GetComponent<ICharacterModel>();
            Debug.Log($"[UnifiedWeaponTazorGun] ShotLocal: model={(model != null ? model.GetType().Name : "NULL")}");
            if (model != null) model.RequestGunShoot();
        }

        loadedBullet = null;
        loadedBulletObj = null;
        usedChance++;

        // 횟수가 남았으면 다음 발 자동 장전, 소진됐으면 지연 후 파괴 스케줄
        if (itemStat != null && usedChance < itemStat.useableTime)
        {
            BeginChargeLocal();
        }
        else
        {
            ScheduleExpire();
        }
    }

    private void HardenOfflineObject(GameObject obj)
    {
        if (obj == null) return;
        if (obj.TryGetComponent(out NetworkIdentity nid))
            nid.enabled = false;
        if (!obj.activeSelf) obj.SetActive(true);
    }

    // ------------------------------------------------------------
    // 온라인 경로
    // ------------------------------------------------------------
    [Command(requiresAuthority = true)]
    private void CmdBeginCharge()
    {
        BeginChargeServer();
    }

    [Server]
    private void BeginChargeServer()
    {
        if (itemStat != null && usedChance >= itemStat.useableTime) return;
        if (loadedBulletObj != null) return;

        Transform spawnPoint = nockPoint != null ? nockPoint : transform;
        GameObject bulletObj = Instantiate(
            bulletPrefab,
            spawnPoint.position + spawnPoint.TransformDirection(followPositionOffset),
            spawnPoint.rotation * Quaternion.Euler(followRotationOffset));

        NetworkServer.Spawn(bulletObj);

        loadedBulletObj = bulletObj;
        loadedBullet = bulletObj.GetComponent<UnifiedTazorBullet>();

        if (loadedBullet == null)
        {
            Debug.LogError("[UnifiedWeaponTazorGun] bulletPrefab에 UnifiedTazorBullet 컴포넌트가 없습니다.");
            NetworkServer.Destroy(bulletObj);
            loadedBulletObj = null;
            return;
        }

        loadedBullet.SetNocked(true);
        loadedBullet.SetOwner(owner);
    }

    [Command(requiresAuthority = true)]
    private void CmdShot(Vector3 aimDirection)
    {
        if (loadedBullet == null)
        {
            Debug.LogWarning("[UnifiedWeaponTazorGun] CmdShot: loadedBullet이 null!");
            return;
        }

        Vector3 direction = aimDirection.sqrMagnitude > 0.001f
            ? aimDirection.normalized
            : transform.forward;

        loadedBullet.Launch(direction);

        // 발사 애니메이션 트리거 (서버 권한자 측에서 모델에 요청 → SyncVar로 모든 클라이언트 전파)
        Debug.Log($"[UnifiedWeaponTazorGun] CmdShot: owner={(owner != null ? owner.name : "NULL")}");
        if (owner != null)
        {
            var model = owner.GetComponent<ICharacterModel>();
            Debug.Log($"[UnifiedWeaponTazorGun] CmdShot: model={(model != null ? model.GetType().Name : "NULL")}");
            if (model != null) model.RequestGunShoot();
        }

        loadedBullet = null;
        loadedBulletObj = null;
        usedChance++;

        RpcOnShot();

        // 횟수가 남았으면 다음 발 자동 장전 (서버에서 즉시), 소진됐으면 지연 후 파괴 스케줄
        if (itemStat != null && usedChance < itemStat.useableTime)
        {
            BeginChargeServer();
        }
        else
        {
            ScheduleExpire();
        }
    }

    [ClientRpc]
    private void RpcOnShot()
    {
        PlayShootSound();
    }

    private void PlayShootSound()
    {
        if (audioSource == null) return;
        if (shootSounds == null || shootSounds.Length == 0) return;
        AudioClip clip = shootSounds[Random.Range(0, shootSounds.Length)];
        if (clip != null) audioSource.PlayOneShot(clip, shootVolume);
    }
}