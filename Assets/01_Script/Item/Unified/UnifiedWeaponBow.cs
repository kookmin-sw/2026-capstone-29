using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 활 무기 통합판. <see cref="WeaponBow"/>의 대체판.
///
/// - 온라인: 기존과 동일하게 Cmd/Rpc로 서버 권한 동작.
/// - 오프라인: Cmd 없이 로컬에서 직접 화살 Instantiate·발사·수명 관리.
///
/// 손잡이 부착:
/// 프리팹에 <see cref="UnifiedWeaponEquipHandler"/>를 붙이고 무기 내부에
/// <see cref="WeaponGripPoint"/>를 배치하면, 손 본 원점에 GripPoint가
/// 정확히 오도록 수학적으로 정렬된다. → positionOffset 튜닝 불필요.
///
/// 기존 <see cref="WeaponEquipHandler"/>만 붙어 있어도 폴백 경로로 동작한다.
///
/// 캐릭터 모션이 검+방패용이라 <see cref="WeaponAttacher"/>가 손 본에 재부착해 놓은
/// 기존 무기(검·방패)가 활 장착 동안에는 보이지 않도록 비활성화하고,
/// 활 해제 시 자동 복원한다.
/// </summary>
public class UnifiedWeaponBow : NetworkBehaviour, IPlayerWeapon
{
    [Header("아이템 정보")]
    [SerializeField] public ItemStatus itemStat;

    [Header("화살 설정")]
    [Tooltip("UnifiedWeaponArrow가 붙은 화살 프리팹.")]
    [SerializeField] private GameObject arrow;

    [Tooltip("화살이 생성될 위치 (활의 시위 중앙). 비워두면 자신의 Transform 사용.")]
    [SerializeField] private Transform nockPoint;

    [Header("차징 설정")]
    [SerializeField] private float minLaunchSpeed = 5f;
    [SerializeField] private float maxLaunchSpeed = 40f;
    [SerializeField] private float maxChargeTime = 1.4f;

    [Header("장착 소켓 (폴백)")]
    [Tooltip("WeaponAttacher.addWeaponL을 찾지 못했을 때 사용할 본 경로.")]
    [SerializeField] private string bowSocketPath = "CombatGirls_Sword_Shield/root/add_weapon_l";

    [Header("추적 설정 (화살 스폰 오프셋)")]
    [SerializeField] public Vector3 followPositionOffset;
    [SerializeField] public Vector3 followRotationOffset;

    [Header("에임 레이캐스트")]
    [Tooltip("레이캐스트에서 제외할 레이어. Inspector에서 Player/Weapon 등 자기 자신 레이어를 꺼두세요.")]
    [SerializeField] private LayerMask aimLayerMask = ~0;

    [Tooltip("카메라에서 이 거리(m) 이내는 무시. 플레이어 몸통·활이 레이에 걸리는 것을 방지.")]
    [SerializeField] private float aimMinDistance = 2.0f;

    [Tooltip("레이캐스트 최대 거리 (m).")]
    [SerializeField] private float aimMaxDistance = 300f;

    [Header("활 애니메이션")]
    [SerializeField] private UnifiedBowAnimationController bowAnim;

    [Header("에임 보정")]
    [Tooltip("활 발사 애니메이션이 캐릭터 정면이 아닌 측면 스탠스일 때 몸 회전 보정각(도). " +
             "애니메이션이 캐릭터 오른쪽(+X)으로 발사하면 양수, 왼쪽이면 음수. " +
             "컨트롤러에 전달되어 target yaw = camera_yaw - 이 값으로 보정됨.")]
    [SerializeField] private float aimYawOffset = 0f;

    // 장전 중인 화살
    private UnifiedWeaponArrow loadedArrow;
    [SyncVar] private GameObject loadedArrowObj;

    // 차징 상태
    private float chargeTimer;
    private bool isCharging;
    private float lifeTimer;

    // 쿨타임
    private float recoveryTimer;
    private bool isRecovered = true;

    // 소유자 참조
    [SerializeField] public GameObject owner;

    // 활 장착 시 비활성화한 다른 소켓의 무기 목록 (해제 시 복원용)
    private readonly List<GameObject> _disabledWeapons = new List<GameObject>();

    public float ChargeRatio => isCharging ? Mathf.Clamp01(chargeTimer / maxChargeTime) : 0f;

    private bool IsLocallyMine   => AuthorityGuard.IsOffline || isOwned;
    private bool HasAuthorityRole => AuthorityGuard.IsOffline || isServer;

    // ------------------------------------------------------------
    // Unity
    // ------------------------------------------------------------
    private void Awake()
    {
        if (bowAnim == null) bowAnim = GetComponent<UnifiedBowAnimationController>();
        if (bowAnim == null) bowAnim = GetComponentInChildren<UnifiedBowAnimationController>();
    }

    // ------------------------------------------------------------
    // IPlayerWeapon
    // ------------------------------------------------------------
    public void SetUser(GameObject user)
    {
        owner = user;
        var model = user.GetComponent<ICharacterModel>();
        if (model != null) model.RequestSetHasBow(true);

        if (AuthorityGuard.IsOffline)
        {
            EquipLocal(user, bowSocketPath);
            return;
        }

        if (NetworkServer.active) RpcSetUser(user, bowSocketPath);
        else                      EquipLocal(user, bowSocketPath);
    }

    [ClientRpc]
    private void RpcSetUser(GameObject user, string socketPath)
    {
        owner = user;
        EquipLocal(user, socketPath);
    }

    public void ThrowWeapon()
    {
        Debug.Log("[UnifiedWeaponBow] 던지기 없음.");
    }

    // ------------------------------------------------------------
    // 장착/해제
    // ------------------------------------------------------------
    private void EquipLocal(GameObject user, string socketPath)
    {
        // WeaponAttacher는 기본 무기(Weapon_Sword / Weapon_Shiled)를 손 본으로 옮긴다.
        // 따라서 활을 붙일 대상은 addWeaponL 자신이 아니라 그 "부모 = 손 본".
        // Start 이후에 픽업이 일어나므로 재부모 처리가 끝나 있다고 가정한다.
        var attacher = user.GetComponent<WeaponAttacher>();
        Transform handBone = null;
        if (attacher != null && attacher.addWeaponL != null)
            handBone = attacher.addWeaponL.parent;

        // GripPoint 기반 새 Handler 우선, 없으면 기존 Handler 폴백
        var unifiedHandler = GetComponent<UnifiedWeaponEquipHandler>();
        if (unifiedHandler != null)
        {
            if (handBone != null)
                unifiedHandler.Equip(user, handBone);
            else if (attacher != null && !string.IsNullOrEmpty(attacher.leftHandBoneName))
                unifiedHandler.Equip(user, attacher.leftHandBoneName);
            else
                unifiedHandler.Equip(user, socketPath);
        }
        else
        {
            // 레거시 WeaponEquipHandler는 string 경로만 받는다.
            string path = (attacher != null && !string.IsNullOrEmpty(attacher.leftHandBoneName))
                ? attacher.leftHandBoneName
                : socketPath;
            var handler = GetComponent<WeaponEquipHandler>();
            if (handler != null) handler.Equip(user, path);
        }

        // 기본 무기(검·방패) 비활성화 — 모든 클라이언트에서 일관되게 적용
        DisableOtherWeapons(user);

        // 에임 모드 ON: 이동 입력 없을 때도 캐릭터 yaw를 카메라 yaw로 정렬
        // (활 애니메이션이 측면 스탠스인 경우 aimYawOffset으로 보정)
        var tpc = user.GetComponent<StarterAssets.UnifiedThirdPersonController>();
        if (tpc != null) tpc.SetAimMode(true, aimYawOffset);
    }

    /// <summary>
    /// <see cref="WeaponAttacher"/>가 직접 참조하는 기본 무기 GameObject
    /// (예: Weapon_Sword, Weapon_Shiled)만 정확히 비활성화하고 목록에 보관.
    /// 손 본의 자식 전체를 끄면 손가락 본 같은 게 꺼질 위험이 있어 피한다.
    /// </summary>
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
    // Update: 수명/쿨타임/장전화살 추적/입력
    // ------------------------------------------------------------
    private void Update()
    {
        if (arrow == null) return;

        if (HasAuthorityRole)
        {
            TickLifeAndRecovery();
            if (this == null) return;
        }

        UpdateNockedArrowFollow();

        if (!IsLocallyMine) return;
        HandleInput();
    }

    private void TickLifeAndRecovery()
    {
        if (itemStat == null) return;

        lifeTimer += Time.deltaTime;

        if (!isRecovered)
        {
            recoveryTimer += Time.deltaTime;
            if (recoveryTimer >= itemStat.RecoveryDelay) isRecovered = true;
        }

        if (lifeTimer > itemStat.availableTime)
        {
            if (owner != null)
            {
                var model = owner.GetComponent<ICharacterModel>();
                if (model != null) model.RequestSetHasBow(false);
            }

            if (AuthorityGuard.IsOffline)
            {
                UnequipLocal();
                if (loadedArrowObj != null) Destroy(loadedArrowObj);
                Destroy(gameObject);
            }
            else
            {
                RpcUnequip();
                if (loadedArrowObj != null) NetworkServer.Destroy(loadedArrowObj);
                NetworkServer.Destroy(gameObject);
            }
        }
    }

    private void UpdateNockedArrowFollow()
    {
        if (!isCharging) return;
        if (loadedArrowObj == null) return;

        Transform spawnPoint = nockPoint != null ? nockPoint : transform;
        loadedArrowObj.transform.position =
            spawnPoint.position + spawnPoint.TransformDirection(followPositionOffset);
        loadedArrowObj.transform.rotation =
            spawnPoint.rotation * Quaternion.Euler(followRotationOffset);
    }

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) && !isCharging && isRecovered)
        {
            isCharging = true;
            chargeTimer = 0f;

            if (AuthorityGuard.IsOffline) BeginChargeLocal();
            else                          CmdBeginCharge();

            if (owner != null)
            {
                var model = owner.GetComponent<ICharacterModel>();
                if (model != null) model.RequestSetBowDraw(true);
            }
        }

        if (isCharging && Input.GetMouseButton(0))
            chargeTimer += Time.deltaTime;

        if (Input.GetMouseButtonUp(0) && isCharging)
        {
            float ratio = Mathf.Clamp01(chargeTimer / maxChargeTime);
            isCharging = false;
            chargeTimer = 0f;

            if (AuthorityGuard.IsOffline) ReleaseArrowLocal(ratio);
            else                          CmdReleaseArrow(ratio, GetCameraAimDirection());

            if (owner != null)
            {
                var model = owner.GetComponent<ICharacterModel>();
                if (model != null) model.RequestBowRelease();
            }
        }
    }

    // ------------------------------------------------------------
    // 오프라인 경로
    // ------------------------------------------------------------
    private void BeginChargeLocal()
    {
        if (!isRecovered) return;

        Transform spawnPoint = nockPoint != null ? nockPoint : transform;
        GameObject arrowObj = Instantiate(
            arrow,
            spawnPoint.position + spawnPoint.TransformDirection(followPositionOffset),
            spawnPoint.rotation * Quaternion.Euler(followRotationOffset));

        HardenOfflineObject(arrowObj);

        loadedArrowObj = arrowObj;
        loadedArrow = arrowObj.GetComponent<UnifiedWeaponArrow>();

        if (loadedArrow == null)
        {
            Debug.LogError("[UnifiedWeaponBow] arrow 프리팹에 UnifiedWeaponArrow 컴포넌트가 없습니다.");
            Destroy(arrowObj);
            loadedArrowObj = null;
            return;
        }

        loadedArrow.SetNocked(true);
        loadedArrow.SetOwner(owner);

        isCharging = true;
        if (bowAnim != null) bowAnim.SetPull(true);
    }

    private void ReleaseArrowLocal(float chargeRatio)
    {
        if (loadedArrow == null)
        {
            CancelChargeLocal();
            return;
        }

        float launchSpeed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, chargeRatio);
        Vector3 direction = GetCameraAimDirection();

        loadedArrow.Launch(direction, launchSpeed);

        loadedArrow = null;
        loadedArrowObj = null;
        chargeTimer = 0f;
        isCharging = false;

        if (bowAnim != null) bowAnim.SetPull(false);

        isRecovered = false;
        recoveryTimer = 0f;
    }

    private void CancelChargeLocal()
    {
        if (loadedArrowObj != null) Destroy(loadedArrowObj);
        loadedArrow = null;
        loadedArrowObj = null;
        chargeTimer = 0f;
        isCharging = false;
        if (bowAnim != null) bowAnim.SetPull(false);
    }

    /// <summary>
    /// 화면 정중앙에서 레이캐스트로 에임 지점을 찾고,
    /// nockPoint에서 그 지점으로 향하는 방향을 반환.
    /// 카메라 없으면 nockPoint forward 폴백.
    /// </summary>
    private Vector3 GetCameraAimDirection()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Transform sp = nockPoint != null ? nockPoint : transform;
            return (sp.rotation * Quaternion.Euler(followRotationOffset)) * Vector3.forward;
        }

        Ray aimRay = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));

        // 카메라 바로 앞의 자기 자신 콜라이더를 피하기 위해 aimMinDistance 이후로 레이 시작
        Vector3 rayOrigin = aimRay.origin + aimRay.direction * aimMinDistance;
        float   rayDist   = Mathf.Max(1f, aimMaxDistance - aimMinDistance);

        Vector3 aimPoint;
        if (Physics.Raycast(rayOrigin, aimRay.direction, out RaycastHit hit, rayDist, aimLayerMask))
            aimPoint = hit.point;
        else
            aimPoint = aimRay.origin + aimRay.direction * aimMaxDistance;

        Transform spawnPoint = nockPoint != null ? nockPoint : transform;
        return (aimPoint - spawnPoint.position).normalized;
    }

    /// <summary>
    /// 오프라인 Instantiate된 오브젝트가 Mirror NetworkIdentity에 의해
    /// 비활성화되지 않도록 보강.
    /// </summary>
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
    [ClientRpc]
    private void RpcUnequip()
    {
        UnequipLocal();
    }

    [Command(requiresAuthority = true)]
    private void CmdBeginCharge()
    {
        if (!isRecovered) return;
        Transform spawnPoint = nockPoint != null ? nockPoint : transform;

        GameObject arrowObj = Instantiate(
            arrow,
            spawnPoint.position + spawnPoint.TransformDirection(followPositionOffset),
            spawnPoint.rotation * Quaternion.Euler(followRotationOffset));

        NetworkServer.Spawn(arrowObj);

        loadedArrowObj = arrowObj;
        loadedArrow = arrowObj.GetComponent<UnifiedWeaponArrow>();

        if (loadedArrow == null)
        {
            Debug.LogError("[UnifiedWeaponBow] arrow 프리팹에 UnifiedWeaponArrow 컴포넌트가 없습니다.");
            NetworkServer.Destroy(arrowObj);
            return;
        }

        loadedArrow.SetNocked(true);
        loadedArrow.SetOwner(owner);

        isCharging = true;

        if (bowAnim != null) bowAnim.SetPull(true);
        RpcOnBeginCharge();
    }

    [ClientRpc]
    private void RpcOnBeginCharge()
    {
        isCharging = true;
    }

    [Command(requiresAuthority = true)]
    private void CmdReleaseArrow(float chargeRatio, Vector3 aimDirection)
    {
        if (loadedArrow == null)
        {
            CancelChargeServer();
            return;
        }

        float launchSpeed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, chargeRatio);
        Vector3 direction = aimDirection.sqrMagnitude > 0.001f ? aimDirection.normalized : transform.forward;

        loadedArrow.Launch(direction, launchSpeed);

        loadedArrow = null;
        chargeTimer = 0f;
        isCharging = false;

        RpcOnRelease();
        if (bowAnim != null) bowAnim.SetPull(false);

        isRecovered = false;
        recoveryTimer = 0f;
    }

    [ClientRpc]
    private void RpcOnRelease()
    {
        isCharging = false;
    }

    private void CancelChargeServer()
    {
        if (loadedArrow != null) NetworkServer.Destroy(loadedArrow.gameObject);
        loadedArrow = null;
        chargeTimer = 0f;
        isCharging = false;
    }
}
