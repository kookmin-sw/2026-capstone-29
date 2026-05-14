using System.Collections;
using Cinemachine;
using Mirror;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 필살기(Ultimate) 발동 게이트 + 연출 컴포넌트 (네트워크 동기화 지원).
///
/// [발동 조건]
///  1) R 키 (triggerKey)
///  2) 본인 제어 캐릭터 (AuthorityGuard.IsLocallyControlled)
///  3) 체력 비율 ≤ hpThreshold (기본 50%)
///  4) 매치당 1회. 부활(OnRespawn) 시 자동 해제 (resetOnRespawn)
///  5) 타겟 존재 필수 — 같은 "Player" 태그 중 자기 자신은 제외
///
///  [선택 조건] - 인스펙터에서 켤 수 있음
///   · requireAlive          : 사망 상태에서는 발동 불가
///   · blockDuringActions    : 차징/활시위 중 발동 불가
///
/// [네트워크 동기화]
///   · hasUsed 는 SyncVar — 서버에서 변경되며 클라에 자동 동기화
///   · 본인 클라 → CmdRequestUltimate → 서버 재검증 → RpcPlayUltimate (모든 클라에서 연출)
///   · 부활 시(OnRespawn) 서버에서 hasUsed=false 로 동기화
///   · 오프라인이면 SyncVar/Cmd/Rpc 없이 직접 실행
///
/// [카메라]
///   · 승리 연출(NetworkGameManger.SlowMotionVictorySequence) 패턴 차용:
///     anchor GameObject 생성 → CinemachineVirtualCamera.Follow/LookAt 갈아끼움.
///   · 모든 클라에서 각자의 로컬 vcam이 anchor 로 옮겨가고,
///     연출 종료 시 "직전에 자기 vcam이 보고있던 대상"으로 복귀
///     (=각 클라이언트의 자기 PlayerCameraRoot. 발동자 시점에 강제 묶이지 않음)
///
/// [슬로우모션]
///   · Time.timeScale 은 안 건드림. 발동자(+옵션으로 타겟) Animator.speed 만 낮춤.
///
/// [데미지]
///   · 별도 처리 없음. CharacterHitBox 가 처리.
///   · 필살기 애니메이션 클립에 AnimationEvent 로 EnableHitbox / DisableHitbox 추가하고,
///     CharacterHitBox.allowedStates 에 필살기 Animator state 이름을 등록하면 됨.
///   · 데미지 배율은 DamageAmplifier 가 자동 적용.
/// </summary>
public class UltimateActivator : NetworkBehaviour
{
    [Header("입력")]
    [Tooltip("필살기 발동 키 (기본 R)")]
    public KeyCode triggerKey = KeyCode.R;

    [Header("발동 조건 - 필수")]
    [Tooltip("체력 비율 임계치. CurrentHealth / startHealth 가 이 값 이하일 때만 발동")]
    [Range(0f, 1f)] public float hpThreshold = 0.5f;

    [Tooltip("매치당 1회. 부활 시 자동 해제됨(resetOnRespawn)")]
    public bool oneShotPerMatch = true;

    [Tooltip("OnRespawn 시 hasUsed 자동 해제")]
    public bool resetOnRespawn = true;

    [Tooltip("타겟(적)이 없으면 발동 불가")]
    public bool requireTarget = true;

    [Tooltip("적 자동검색 태그. 자기 자신은 제외됨")]
    public string enemyTag = "Player";

    [Tooltip("적 자동검색 최대 거리 (0 = 무제한)")]
    public float autoFindRange = 0f;

    [Header("발동 조건 - 선택")]
    [Tooltip("사망 상태에서는 발동 불가")]
    public bool requireAlive = true;

    [Tooltip("차징/활시위 등 다른 액션 중에는 발동 불가")]
    public bool blockDuringActions = false;

    [Header("연출 - 슬로우모션")]
    [Tooltip("슬로우 동안 본인 Animator.speed (0.15 = 매우 느림)")]
    [Range(0.05f, 1f)] public float slowMotionAnimSpeed = 0.15f;

    [Tooltip("슬로우/카메라 줌 유지 시간 (realtime 초)")]
    public float slowMotionDuration = 1.6f;

    [Tooltip("슬로우 종료 후, 정상속도로 필살기 모션이 재생될 추가 시간 (그 동안 본인+타겟 입력 차단, 카메라 anchor 유지)")]
    public float ultimateMotionDuration = 1.5f;

    [Tooltip("타겟의 Animator 도 같이 슬로우 적용")]
    public bool slowTargetAnim = true;

    [Header("연출 - 입력 차단")]
    [Tooltip("연출 동안 본인의 StarterAssetsInputs / PlayerInput 비활성화 (R 누른 시점부터 모션 끝까지)")]
    public bool blockSelfInput = true;

    [Tooltip("연출 동안 타겟의 StarterAssetsInputs / PlayerInput 비활성화 (이동/공격 입력 차단)")]
    public bool blockTargetInput = true;

    [Tooltip("타겟의 Rigidbody 속도(velocity)를 영점화해서 미끄러짐 방지")]
    public bool zeroTargetVelocity = true;

    [Header("연출 - 카메라 anchor 위치")]
    [Tooltip("나-적 중간점에서 옆으로 빠지는 거리 (둘 사이 거리에 distanceBackoff 곱한 값이 추가됨)")]
    public float lateralOffset = 2.5f;

    [Tooltip("anchor 높이")]
    public float heightOffset = 1.6f;

    [Tooltip("anchor 가 LookAt 할 중간점의 추가 높이")]
    public float lookAtHeightOffset = 1.4f;

    [Tooltip("거리 비례 백오프 계수")]
    public float distanceBackoff = 0.4f;

    [Header("연출 - 애니메이션 (선택)")]
    [Tooltip("필살기용 Animator 트리거 이름. CharacterHitBox.allowedStates 에 동일 state 이름 등록 필요")]
    public string ultimateAnimTrigger = "";

    [Header("연출 - 궁극기 무기 (선택)")]
    [Tooltip("궁극기 발동 시 장착할 전용 무기 프리팹.\n" +
             "필수 컴포넌트: NetworkIdentity, UnifiedWeaponMelee, UnifiedWeaponEquipHandler.\n" +
             "NetworkManager의 spawnPrefabs 리스트에 등록되어 있어야 클라이언트에서도 보임.\n" +
             "프리팹의 ItemStatus.availableTime은 (slowMotionDuration + ultimateMotionDuration)보다\n" +
             "충분히 크게 설정 권장 — 자체 lifeTimer가 ForceExpire 전에 만료되지 않도록.")]
    public GameObject ultimateWeaponPrefab;

    [Header("연출 - UI 컷인")]
    [Tooltip("궁극기 슬로우 시간 동안 표시할 캐릭터 데이터. portrait 이미지를 사용합니다.")]
    public CharacterData characterData;

    [Header("참조 (비워두면 자동 검색)")]
    public UnifiedCharacterModel selfModel;
    public Animator selfAnimator;

    // ────────── 네트워크 동기화 ──────────
    [SyncVar] private bool hasUsed = false;

    // ────────── 로컬 상태 ──────────
    private bool isPlaying = false;
    private GameObject ultimateAnchor;
    private CinemachineVirtualCamera cachedVcam;
    private Transform cachedVcamFollow;
    private Transform cachedVcamLookAt;

    // 본인 입력 차단 복원용 캐시
    private StarterAssetsInputs cachedSelfSAI;
    private bool cachedSelfSAIEnabled;
    private PlayerInput cachedSelfPI;
    private bool cachedSelfPIEnabled;

    // 타겟 입력 차단 복원용 캐시
    private StarterAssetsInputs cachedTargetSAI;
    private bool cachedTargetSAIEnabled;
    private PlayerInput cachedTargetPI;
    private bool cachedTargetPIEnabled;
    private Rigidbody cachedTargetRb;
    private bool cachedTargetRbWasKinematic;

    // 현재 활성화된 궁극기 무기 (서버/오프라인 권위에서만 ForceExpire 호출용으로 보관)
    private GameObject _currentUltimateWeapon;

    // ─────────────────────────────────────────────
    //  바인딩 / 라이프사이클
    // ─────────────────────────────────────────────
    private void Reset() { AutoBind(); }
    private void Awake() { AutoBind(); }

    private void AutoBind()
    {
        if (selfModel == null)
            selfModel = GetComponent<UnifiedCharacterModel>();

        if (selfAnimator == null)
            selfAnimator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if (selfModel != null)
            selfModel.OnRespawn += HandleRespawn;
    }

    private void OnDisable()
    {
        if (selfModel != null)
            selfModel.OnRespawn -= HandleRespawn;

        // 안전장치: 도중에 비활성화돼도 Animator/카메라/입력 원복
        if (isPlaying)
        {
            if (selfAnimator != null) selfAnimator.speed = 1f;
            RestoreSelfInput();
            RestoreTargetInput();
            RestoreCamera();

            // 궁극기 무기도 즉시 정리 (서버/오프라인 권위에서만)
            TryForceExpireUltimateWeapon();

            isPlaying = false;
        }
    }

    /// <summary>부활 시 1회 잠금 해제. SyncVar 변경은 서버에서만.</summary>
    private void HandleRespawn()
    {
        if (!resetOnRespawn) return;

        if (AuthorityGuard.IsOffline)
            hasUsed = false;
        else if (isServer)
            hasUsed = false;   // SyncVar → 클라이언트 자동 동기화
    }

    // ─────────────────────────────────────────────
    //  Update : 입력 + 게이트
    // ─────────────────────────────────────────────
    private void Update()
    {
        if (isPlaying) return;
        if (oneShotPerMatch && hasUsed) return;

        // 본인 제어가 아닌 경우 입력 무시
        if (!AuthorityGuard.IsLocallyControlled(gameObject)) return;
        if (!Input.GetKeyDown(triggerKey)) return;

        if (!CanActivate(out Transform target, out string reason))
        {
            Debug.Log($"[UltimateActivator] 발동 불가: {reason}");
            return;
        }

        if (AuthorityGuard.IsOffline)
        {
            // 오프라인: 즉시 실행 (무기도 로컬 스폰)
            hasUsed = true;
            GameObject weaponObj = SpawnUltimateWeaponLocal();
            StartCoroutine(PlayUltimateLocal(target, weaponObj));
        }
        else
        {
            // 네트워크: 서버 검증 후 모든 클라에 RPC
            NetworkIdentity targetId = target != null
                ? target.GetComponentInParent<NetworkIdentity>()
                : null;
            CmdRequestUltimate(targetId);
        }
    }

    // ─────────────────────────────────────────────
    //  네트워크 (Cmd / Rpc)
    // ─────────────────────────────────────────────
    [Command]
    private void CmdRequestUltimate(NetworkIdentity targetId)
    {
        // 서버 측 재검증 (치팅/지연 방지)
        if (oneShotPerMatch && hasUsed) return;
        if (selfModel == null) return;
        if (requireAlive && selfModel.IsDead) return;
        if (blockDuringActions && (selfModel.IsCharging || selfModel.IsBowDraw)) return;

        float startHp = selfModel.startHealth > 0f ? selfModel.startHealth : 1f;
        if (selfModel.CurrentHealth / startHp > hpThreshold) return;

        if (requireTarget)
        {
            if (targetId == null) return;
            if (targetId.gameObject == this.gameObject) return;   // 자기 자신 차단
        }

        hasUsed = true;                  // SyncVar

        // 궁극기 무기 서버 스폰 → SetUser 안에서 RpcSetUser 발사하여 모든 클라에 자동 장착
        GameObject weaponObj = SpawnUltimateWeaponServer();
        NetworkIdentity weaponId = weaponObj != null
            ? weaponObj.GetComponent<NetworkIdentity>()
            : null;

        RpcPlayUltimate(targetId, weaponId);
    }

    [ClientRpc]
    private void RpcPlayUltimate(NetworkIdentity targetId, NetworkIdentity weaponId)
    {
        Transform t = targetId != null ? targetId.transform : null;
        GameObject weapon = weaponId != null ? weaponId.gameObject : null;
        StartCoroutine(PlayUltimateLocal(t, weapon));
    }

    // ─────────────────────────────────────────────
    //  궁극기 무기 스폰 헬퍼
    // ─────────────────────────────────────────────
    /// <summary>오프라인 전용: 로컬 Instantiate + NetworkIdentity 비활성 + SetUser.</summary>
    private GameObject SpawnUltimateWeaponLocal()
    {
        if (ultimateWeaponPrefab == null) return null;

        GameObject weaponObj = Instantiate(ultimateWeaponPrefab, transform.position, Quaternion.identity);

        // 오프라인에선 Mirror가 관리 안 하므로 NetworkIdentity 비활성 (UnifiedSetItem.HardenOfflineObject 패턴)
        if (weaponObj.TryGetComponent(out NetworkIdentity nid))
            nid.enabled = false;
        if (!weaponObj.activeSelf) weaponObj.SetActive(true);

        IPlayerWeapon ipw = weaponObj.GetComponent<IPlayerWeapon>();
        if (ipw != null) ipw.SetUser(gameObject);

        _currentUltimateWeapon = weaponObj;
        return weaponObj;
    }

    /// <summary>서버 권위 전용: NetworkServer.Spawn 으로 모든 클라에 복제. SetUser → RpcSetUser 자동.</summary>
    [Server]
    private GameObject SpawnUltimateWeaponServer()
    {
        if (ultimateWeaponPrefab == null) return null;

        GameObject weaponObj = Instantiate(ultimateWeaponPrefab, transform.position, Quaternion.identity);

        NetworkIdentity ownerId = GetComponent<NetworkIdentity>();
        if (ownerId != null)
            NetworkServer.Spawn(weaponObj, ownerId.connectionToClient);
        else
            NetworkServer.Spawn(weaponObj);

        IPlayerWeapon ipw = weaponObj.GetComponent<IPlayerWeapon>();
        if (ipw != null) ipw.SetUser(gameObject);

        _currentUltimateWeapon = weaponObj;
        return weaponObj;
    }

    // ─────────────────────────────────────────────
    //  검사 / 검색
    // ─────────────────────────────────────────────
    private bool CanActivate(out Transform target, out string reason)
    {
        target = null; reason = string.Empty;

        if (selfModel == null) { reason = "UnifiedCharacterModel 없음"; return false; }
        if (requireAlive && selfModel.IsDead) { reason = "사망 상태"; return false; }
        if (blockDuringActions && (selfModel.IsCharging || selfModel.IsBowDraw))
        { reason = "다른 액션 중 (Charging/BowDraw)"; return false; }

        float startHp = selfModel.startHealth > 0f ? selfModel.startHealth : 1f;
        float ratio = selfModel.CurrentHealth / startHp;
        if (ratio > hpThreshold) { reason = $"체력 {ratio:P0} > 임계 {hpThreshold:P0}"; return false; }

        if (requireTarget)
        {
            target = FindNearestEnemy();
            if (target == null) { reason = $"타겟('{enemyTag}') 없음"; return false; }
        }

        return true;
    }

    private Transform FindNearestEnemy()
    {
        if (string.IsNullOrEmpty(enemyTag)) return null;

        GameObject[] candidates;
        try
        {
            candidates = GameObject.FindGameObjectsWithTag(enemyTag);
        }
        catch (UnityException)
        {
            Debug.LogWarning($"[UltimateActivator] '{enemyTag}' 태그가 등록되지 않았습니다.", this);
            return null;
        }

        if (candidates == null || candidates.Length == 0) return null;

        Transform nearest = null;
        float minSq = float.MaxValue;
        Vector3 myPos = transform.position;
        float rangeSq = autoFindRange > 0f ? autoFindRange * autoFindRange : float.MaxValue;

        foreach (GameObject go in candidates)
        {
            if (go == null) continue;
            // 자기 자신 / 자기 하위 노드(콜라이더가 자식에 있는 경우 등) 제외
            if (go == this.gameObject) continue;
            if (go.transform.IsChildOf(transform)) continue;

            float d = (go.transform.position - myPos).sqrMagnitude;
            if (d <= rangeSq && d < minSq)
            {
                minSq = d;
                nearest = go.transform;
            }
        }
        return nearest;
    }

    // ─────────────────────────────────────────────
    //  연출 (모든 클라에서 호출됨)
    // ─────────────────────────────────────────────
    private IEnumerator PlayUltimateLocal(Transform target, GameObject ultimateWeapon)
    {
        isPlaying = true;
        UltimateCutInUI.Instance?.Play(characterData != null ? characterData.portrait : null, slowMotionDuration);

        // 1) 자기 클라의 활성 vcam 캐싱
        cachedVcam = Object.FindAnyObjectByType<CinemachineVirtualCamera>();
        if (cachedVcam != null)
        {
            cachedVcamFollow = cachedVcam.Follow;
            cachedVcamLookAt = cachedVcam.LookAt;
        }

        // 2) anchor 위치 — 발동자 기준 right side, 둘 사이 거리에 비례 백오프
        Vector3 me = transform.position;
        Vector3 enemy = target != null ? target.position : (transform.position + transform.forward * 3f);
        Vector3 mid = (me + enemy) * 0.5f;

        Vector3 toEnemyFlat = new Vector3(enemy.x - me.x, 0f, enemy.z - me.z);
        if (toEnemyFlat.sqrMagnitude < 0.0001f) toEnemyFlat = transform.forward;
        toEnemyFlat.Normalize();

        Vector3 sideDir = Vector3.Cross(Vector3.up, toEnemyFlat).normalized;
        float pairDist = Vector3.Distance(me, enemy);
        Vector3 anchorPos = mid
                            + sideDir * (lateralOffset + pairDist * distanceBackoff)
                            + Vector3.up * heightOffset;

        if (ultimateAnchor != null) Destroy(ultimateAnchor);
        ultimateAnchor = new GameObject("UltimateCameraAnchor");
        ultimateAnchor.transform.position = anchorPos;
        ultimateAnchor.transform.LookAt(mid + Vector3.up * lookAtHeightOffset);

        if (cachedVcam != null)
        {
            cachedVcam.Follow = ultimateAnchor.transform;
            cachedVcam.LookAt = ultimateAnchor.transform;
        }

        // 3) 슬로우모션 (Animator.speed, Time.timeScale 미변경)
        if (selfAnimator != null) selfAnimator.speed = slowMotionAnimSpeed;

        Animator targetAnim = null;
        if (slowTargetAnim && target != null)
        {
            targetAnim = target.GetComponentInChildren<Animator>();
            if (targetAnim != null) targetAnim.speed = slowMotionAnimSpeed;
        }

        // 3-2) 본인 입력 차단 (이동/공격 등 - R 누른 시점부터 모션 끝까지)
        BlockSelfInput();

        // 3-3) 타겟 입력 차단 (이동/공격 등 모든 액션 입력)
        if (blockTargetInput && target != null)
        {
            cachedTargetSAI = target.GetComponentInChildren<StarterAssetsInputs>();
            if (cachedTargetSAI != null)
            {
                cachedTargetSAIEnabled = cachedTargetSAI.enabled;
                // 누르고 있던 키 잔류 방지
                cachedTargetSAI.move = Vector2.zero;
                cachedTargetSAI.look = Vector2.zero;
                cachedTargetSAI.jump = false;
                cachedTargetSAI.sprint = false;
                cachedTargetSAI.enabled = false;
            }

            cachedTargetPI = target.GetComponentInChildren<PlayerInput>();
            if (cachedTargetPI != null)
            {
                cachedTargetPIEnabled = cachedTargetPI.enabled;
                cachedTargetPI.enabled = false;
            }
        }

        // 3-4) 타겟 Rigidbody velocity 영점화 (관성 미끄러짐 방지)
        if (zeroTargetVelocity && target != null)
        {
            cachedTargetRb = target.GetComponentInChildren<Rigidbody>();
            if (cachedTargetRb != null)
            {
                cachedTargetRbWasKinematic = cachedTargetRb.isKinematic;
                cachedTargetRb.velocity = Vector3.zero;
                cachedTargetRb.angularVelocity = Vector3.zero;
            }
        }

        // 4) 애니메이터 트리거
        if (!string.IsNullOrEmpty(ultimateAnimTrigger) && selfAnimator != null)
        {
            selfAnimator.ResetTrigger(ultimateAnimTrigger);
            selfAnimator.SetTrigger(ultimateAnimTrigger);
        }

        // 5) Phase 1 — 슬로우 유지 (양쪽 슬로우 + 입력 차단)
        yield return new WaitForSecondsRealtime(slowMotionDuration);

        // 6) 슬로우 해제 (Animator 정상속도로 모션 진행)
        if (selfAnimator != null) selfAnimator.speed = 1f;
        if (targetAnim != null) targetAnim.speed = 1f;

        // 7) Phase 2 — 정상속도 모션 진행 (카메라 anchor 유지, 입력 차단 유지)
        yield return new WaitForSecondsRealtime(ultimateMotionDuration);

        // 7-2) 궁극기 무기 만료 — 서버/오프라인 권위에서만 호출
        //      (RpcUnequip 으로 모든 클라가 UnequipHandler → 기본 무기 복원)
        TryForceExpireUltimateWeapon();

        // 8) 모두 복원
        RestoreSelfInput();
        RestoreTargetInput();
        RestoreCamera();
        isPlaying = false;
    }

    /// <summary>
    /// 보관 중인 궁극기 무기에 ForceExpire 를 호출해 즉시 만료시킨다.
    /// 서버/오프라인 권위에서만 의미 있음. 다른 클라이언트에서는 _currentUltimateWeapon 가 비어 있음.
    /// </summary>
    private void TryForceExpireUltimateWeapon()
    {
        if (_currentUltimateWeapon == null) return;

        UnifiedWeaponMelee melee = _currentUltimateWeapon.GetComponent<UnifiedWeaponMelee>();
        if (melee != null) melee.ForceExpire();

        _currentUltimateWeapon = null;
    }

    /// <summary>본인의 StarterAssetsInputs / PlayerInput 비활성화 + 잔류 입력 클리어</summary>
    private void BlockSelfInput()
    {
        if (!blockSelfInput) return;

        cachedSelfSAI = GetComponentInChildren<StarterAssetsInputs>();
        if (cachedSelfSAI != null)
        {
            cachedSelfSAIEnabled = cachedSelfSAI.enabled;
            cachedSelfSAI.move = Vector2.zero;
            cachedSelfSAI.look = Vector2.zero;
            cachedSelfSAI.jump = false;
            cachedSelfSAI.sprint = false;
            cachedSelfSAI.enabled = false;
        }

        cachedSelfPI = GetComponentInChildren<PlayerInput>();
        if (cachedSelfPI != null)
        {
            cachedSelfPIEnabled = cachedSelfPI.enabled;
            cachedSelfPI.enabled = false;
        }
    }

    /// <summary>본인 Input 캐싱값으로 복원</summary>
    private void RestoreSelfInput()
    {
        if (cachedSelfSAI != null)
        {
            cachedSelfSAI.enabled = cachedSelfSAIEnabled;
            cachedSelfSAI = null;
        }
        if (cachedSelfPI != null)
        {
            cachedSelfPI.enabled = cachedSelfPIEnabled;
            cachedSelfPI = null;
        }
    }

    /// <summary>타겟 Input/Rigidbody 캐싱값으로 복원 (원래 Disable이었으면 그대로 둠)</summary>
    private void RestoreTargetInput()
    {
        if (cachedTargetSAI != null)
        {
            cachedTargetSAI.enabled = cachedTargetSAIEnabled;
            cachedTargetSAI = null;
        }
        if (cachedTargetPI != null)
        {
            cachedTargetPI.enabled = cachedTargetPIEnabled;
            cachedTargetPI = null;
        }
        if (cachedTargetRb != null)
        {
            // 영점화만 했을 뿐 isKinematic 변경은 안 했지만, 확장 대비 복원
            cachedTargetRb.isKinematic = cachedTargetRbWasKinematic;
            cachedTargetRb = null;
        }
    }

    /// <summary>각 클라이언트가 직전에 보고있던 vcam 대상으로 자연 복귀</summary>
    private void RestoreCamera()
    {
        if (cachedVcam != null)
        {
            cachedVcam.Follow = cachedVcamFollow;
            cachedVcam.LookAt = cachedVcamLookAt;
        }

        if (ultimateAnchor != null)
        {
            Destroy(ultimateAnchor);
            ultimateAnchor = null;
        }
    }

    // ─────────────────────────────────────────────
    //  외부 API
    // ─────────────────────────────────────────────
    /// <summary>외부에서 1회 잠금 해제 (서버에서만 의미 있음)</summary>
    public void ResetLock()
    {
        if (AuthorityGuard.IsOffline) hasUsed = false;
        else if (isServer) hasUsed = false;
    }

    public bool IsPlaying => isPlaying;
    public bool HasUsed => hasUsed;
}
