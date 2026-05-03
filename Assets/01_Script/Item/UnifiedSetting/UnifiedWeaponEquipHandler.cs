using Mirror;
using UnityEngine;

/// <summary>
/// GripPoint 기반 장착 Handler. <see cref="WeaponEquipHandler"/>의 대체판.
///
/// 기존 Handler는 소켓 위치 + positionOffset/rotationOffset 으로 부착했으나
/// 이 Handler는 무기 프리팹 자체의 <see cref="WeaponGripPoint"/>가
/// 손 본 위치에 오도록 무기 루트를 역변환하여 부착한다.
/// → 무기별/캐릭터별 오프셋 튜닝 불필요.
///
/// 오프라인(NetworkIdentity 비활성) → <see cref="Transform.SetParent"/>로 부착,
/// 애니메이션은 유니티가 자동으로 따라감.
///
/// 온라인(NetworkIdentity 활성) → Mirror가 Transform을 관리하므로 SetParent 대신
/// LateUpdate 매 프레임 GripPoint가 손 본 위치/회전을 유지하도록 월드 트랜스폼 갱신.
///
/// 기본 무기 숨김:
/// <see cref="WeaponAttacher"/>가 있을 때 양손(LeftHand/RightHand) 슬롯의
/// 기본 무기 오브젝트(addWeaponR/addWeaponL)를 Equip 시 비활성, Unequip 시 복원.
/// WeaponAttacher의 슬롯은 enum 두 개(RightHand/LeftHand)뿐이므로 두 슬롯만 검사하면 충분.
/// </summary>
public class UnifiedWeaponEquipHandler : MonoBehaviour
{
    [Header("잡는 점")]
    [Tooltip("비워두면 자식에서 WeaponGripPoint를 자동 탐색.")]
    [SerializeField] private Transform gripPoint;

    [Header("히트박스")]
    [Tooltip("이 무기의 히트박스. 비워두면 자식에서 자동 탐색.")]
    [SerializeField] private CharacterHitBox weaponHitbox;

    [Header("부착 미세조정 — 베이스(Idle 포함 항상 적용)")]
    [Tooltip("GripPoint를 손 본 원점에 정렬한 뒤, 손 본 로컬 공간에서 추가로 밀어내는 위치 오프셋.")]
    [SerializeField] private Vector3 attachPositionOffset = Vector3.zero;

    [Tooltip("GripPoint를 손 본 원점에 정렬한 뒤, 손 본 로컬 공간에서 추가로 돌리는 회전 오프셋(Euler). 활이 손 안에서 비스듬하면 Y축으로 조정.")]
    [SerializeField] private Vector3 attachRotationOffset = Vector3.zero;

    [Header("부착 미세조정 — Charge 추가 델타 (베이스 위에 더해짐)")]
    [Tooltip("Charge 상태에서 추가로 적용되는 위치 델타 (followTarget 로컬).")]
    [SerializeField] private Vector3 chargePositionDelta = Vector3.zero;

    [Tooltip("Charge 상태에서 추가로 적용되는 회전 델타 (followTarget 로컬, Euler).")]
    [SerializeField] private Vector3 chargeRotationDelta = Vector3.zero;

    [Tooltip("Idle ↔ Charge 전환 시 보간 시간(초). 0이면 즉시 전환.")]
    [SerializeField] private float chargeBlendDuration = 0.1f;

    [Header("기본 무기 숨김")]
    [Tooltip("장착 중 양손(R/L) 슬롯의 기본 무기를 비활성화할지 (WeaponAttacher 필요).")]
    [SerializeField] private bool hideDefaultOnEquip = true;

    // 추적 대상 본/소켓
    [SerializeField] private Transform followTarget;

    /// <summary>현재 부착된 손 본/소켓. 외부에서 조회용.</summary>
    public Transform FollowTarget => followTarget;

    // 백업
    private UnifiedCharacterView cachedUnifiedView;
    private CharacterView cachedView;
    private CharacterHitBox originalRightHitbox;
    private bool isEquipped;

    // 부착 방식
    private bool _useSetParent;
    private Transform _originalParent;

    // GripPoint의 "무기 루트 로컬" 기준 정적 오프셋 (Equip 시 캐싱)
    private Matrix4x4 _gripInWeaponLocal;
    private bool _gripCached;

    // Charge 상태 및 보간 블렌드값 (0=Idle, 1=Charge)
    private bool _isCharging;
    private float _chargeBlend;

    // 숨긴 기본 무기 핸들 (양손) — Unequip 시 복원
    private GameObject _hiddenLeftDefault;
    private GameObject _hiddenRightDefault;

    /// <summary>
    /// Charge 상태 설정. UnifiedBowAnimationController.ApplyPull에서 호출됨.
    /// 오프/온라인 양쪽에서 동일 경로를 타므로 별도 동기화 불필요.
    /// </summary>
    public void SetCharging(bool charging)
    {
        _isCharging = charging;
    }

    private void UpdateChargeBlend()
    {
        float target = _isCharging ? 1f : 0f;
        if (chargeBlendDuration > 0f)
            _chargeBlend = Mathf.MoveTowards(_chargeBlend, target, Time.deltaTime / chargeBlendDuration);
        else
            _chargeBlend = target;
    }

    // 현재 블렌드 기반 최종 오프셋 계산
    private void GetCurrentOffsets(out Vector3 pos, out Quaternion rot)
    {
        pos = attachPositionOffset + chargePositionDelta * _chargeBlend;
        Vector3 eul = attachRotationOffset + chargeRotationDelta * _chargeBlend;
        rot = Quaternion.Euler(eul);
    }

    // ------------------------------------------------------------
    // Unity
    // ------------------------------------------------------------
    private void Awake()
    {
        if (gripPoint == null)
        {
            var gp = GetComponentInChildren<WeaponGripPoint>(true);
            if (gp != null) gripPoint = gp.transform;
        }

        if (gripPoint == null)
            Debug.LogWarning($"[UnifiedWeaponEquipHandler] {name}에 WeaponGripPoint를 찾지 못했습니다. 소켓 원점에 정렬됩니다.");
    }

    // ------------------------------------------------------------
    // 장착
    // ------------------------------------------------------------

    /// <summary>Transform을 직접 지정해 장착. WeaponAttacher 이후에도 안전.</summary>
    public void Equip(GameObject owner, Transform handBone)
    {
        if (owner == null || handBone == null) return;
        followTarget = handBone;
        EquipInternal(owner);
    }

    /// <summary>경로 문자열로 본을 찾아 장착. 실패 시 경로 마지막 이름으로 재귀 탐색.</summary>
    public void Equip(GameObject owner, string bonePath)
    {
        if (owner == null) return;

        followTarget = owner.transform.Find(bonePath);
        if (followTarget == null)
        {
            string boneName = bonePath.Contains("/")
                ? bonePath.Substring(bonePath.LastIndexOf('/') + 1)
                : bonePath;
            followTarget = FindRecursive(owner.transform, boneName);
        }

        if (followTarget == null)
        {
            Debug.LogWarning($"[UnifiedWeaponEquipHandler] 본을 찾을 수 없습니다: {bonePath}");
            return;
        }

        EquipInternal(owner);
    }

    private void EquipInternal(GameObject owner)
    {
        // CharacterView 히트박스 교체
        cachedView = owner.GetComponent<CharacterView>();
        if (cachedView != null)
        {
            originalRightHitbox = cachedView.rightHandHitbox;
            if (weaponHitbox == null)
                weaponHitbox = GetComponentInChildren<CharacterHitBox>();
            if (weaponHitbox != null)
            {
                cachedView.rightHandHitbox = weaponHitbox;
                weaponHitbox.DisableHitbox();
            }
        }

        else
        {
            // CharacterView가 null이 나온다면 UnifiedCharacterView 시도
            cachedUnifiedView = owner.GetComponent<UnifiedCharacterView>();
            if (cachedUnifiedView != null)
            {
                originalRightHitbox = cachedUnifiedView.rightHandHitbox;
                if (weaponHitbox == null)
                    weaponHitbox = GetComponentInChildren<CharacterHitBox>();
                if (weaponHitbox != null)
                {
                    cachedUnifiedView.rightHandHitbox = weaponHitbox;
                    weaponHitbox.DisableHitbox();
                }
            }
        }

        // 기본 무기 숨김 (양손) — WeaponAttacher가 있을 때만
        if (hideDefaultOnEquip)
        {
            HideDefaultWeapons(owner);
        }

        // GripPoint 로컬 오프셋 캐싱 (무기 루트 기준)
        if (gripPoint != null)
        {
            _gripInWeaponLocal = transform.worldToLocalMatrix * gripPoint.localToWorldMatrix;
            _gripCached = true;
        }
        else
        {
            _gripInWeaponLocal = Matrix4x4.identity;
            _gripCached = false;
        }

        // 부착 방식 결정: NetworkIdentity 비활성이면 SetParent 사용
        bool hasActiveNetId = TryGetComponent(out NetworkIdentity nid) && nid.enabled;
        _useSetParent = !hasActiveNetId;

        if (_useSetParent)
        {
            _originalParent = transform.parent;
            transform.SetParent(followTarget, worldPositionStays: false);
            ApplyGripAlignmentLocal();
        }
        // 온라인이면 LateUpdate에서 월드 기준 추적

        isEquipped = true;
    }

    /// <summary>
    /// WeaponAttacher를 찾아 양손(LeftHand/RightHand) 슬롯의 기본 무기를 비활성화.
    /// WeaponAttacher의 GetDefaultWeapon은 addWeaponR/addWeaponL.gameObject를 반환하므로
    /// 슬롯에 기본 무기 Transform이 할당된 슬롯만 끄게 된다.
    /// </summary>
    private void HideDefaultWeapons(GameObject owner)
    {
        WeaponAttacher attacher = owner.GetComponent<WeaponAttacher>();
        if (attacher == null) attacher = owner.GetComponentInChildren<WeaponAttacher>();
        if (attacher == null) return;

        _hiddenRightDefault = TryHideSlot(attacher, WeaponSlot.RightHand);
        _hiddenLeftDefault = TryHideSlot(attacher, WeaponSlot.LeftHand);
    }

    private GameObject TryHideSlot(WeaponAttacher attacher, WeaponSlot slot)
    {
        GameObject defaultObj = attacher.GetDefaultWeapon(slot);
        if (defaultObj == null) return null;

        // 자기 자신(또는 자기 자신의 부모/조상)이 default로 잡힌 경우 끄지 않는다.
        // 새 무기 자체를 끄면 즉시 사라져 장착 자체가 무의미해진다.
        if (defaultObj == gameObject) return null;
        if (transform.IsChildOf(defaultObj.transform)) return null;

        // 이미 비활성이면 우리가 켜버리는 사고를 막기 위해 복원 대상에서 제외.
        if (!defaultObj.activeSelf) return null;

        defaultObj.SetActive(false);
        return defaultObj;
    }

    /// <summary>
    /// 현재 followTarget의 자식으로 있다는 가정 하에
    /// GripPoint가 followTarget 원점에 오도록 localPosition/localRotation을 설정.
    /// attachPositionOffset/attachRotationOffset + Charge 블렌드된 델타가 추가 적용된다.
    /// </summary>
    private void ApplyGripAlignmentLocal()
    {
        Matrix4x4 inv = _gripCached ? _gripInWeaponLocal.inverse : Matrix4x4.identity;

        Vector3 basePos = inv.GetColumn(3);
        Quaternion baseRot = _gripCached ? inv.rotation : Quaternion.identity;

        GetCurrentOffsets(out Vector3 posOff, out Quaternion offsetRot);
        transform.localPosition = basePos + posOff;
        transform.localRotation = offsetRot * baseRot;
    }

    // ------------------------------------------------------------
    // 해제
    // ------------------------------------------------------------
    public void Unequip()
    {
        if (!isEquipped) return;

        if (cachedView != null && originalRightHitbox != null)
            cachedView.rightHandHitbox = originalRightHitbox;
        else if (cachedUnifiedView != null && originalRightHitbox != null)
            cachedUnifiedView.rightHandHitbox = originalRightHitbox;

        // 양손 기본 무기 복원 (한쪽만 비어있어도 안전)
        if (_hiddenRightDefault != null)
        {
            _hiddenRightDefault.SetActive(true);
            _hiddenRightDefault = null;
        }
        if (_hiddenLeftDefault != null)
        {
            _hiddenLeftDefault.SetActive(true);
            _hiddenLeftDefault = null;
        }

        if (_useSetParent && transform != null)
            transform.SetParent(_originalParent, worldPositionStays: true);

        followTarget = null;
        cachedView = null;
        originalRightHitbox = null;
        _originalParent = null;
        _useSetParent = false;
        _gripCached = false;
        isEquipped = false;
        cachedUnifiedView = null;
    }

    // ------------------------------------------------------------
    // 온라인 추적 (SetParent 미사용 시)
    // ------------------------------------------------------------
    private void LateUpdate()
    {
        // Charge 블렌드 프레임별 갱신 (아래 두 경로 모두 여기 계산값을 읽음)
        UpdateChargeBlend();

        if (_useSetParent)
        {
            // SetParent 경로에서도 Play 중 오프셋 튜닝이 실시간 반영되도록 매 프레임 재적용.
            if (followTarget != null) ApplyGripAlignmentLocal();
            return;
        }

        if (followTarget == null) return;

        // followTarget 로컬에서의 최종 위치/회전 (Charge 블렌드 오프셋 포함)
        Matrix4x4 inv = _gripCached ? _gripInWeaponLocal.inverse : Matrix4x4.identity;
        Vector3 basePos = inv.GetColumn(3);
        Quaternion baseRot = _gripCached ? inv.rotation : Quaternion.identity;

        GetCurrentOffsets(out Vector3 posOff, out Quaternion offsetRot);
        Vector3 localPos = basePos + posOff;
        Quaternion localRot = offsetRot * baseRot;

        // followTarget 월드 기준으로 변환
        Vector3 worldPos = followTarget.TransformPoint(localPos);
        Quaternion worldRot = followTarget.rotation * localRot;

        transform.SetPositionAndRotation(worldPos, worldRot);
    }

    // ------------------------------------------------------------
    // 유틸
    // ------------------------------------------------------------
    private static Vector3 ExtractPosition(Matrix4x4 m)
    {
        Vector4 c = m.GetColumn(3);
        return new Vector3(c.x, c.y, c.z);
    }

    private static Transform FindRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void OnDestroy()
    {
        if (isEquipped) Unequip();
    }
}