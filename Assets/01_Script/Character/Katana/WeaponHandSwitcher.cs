using UnityEngine;

// 검 오브젝트에 부착하여 위치 참조를 변경시켜주는 스크립트.
// 공격 중일 때는 RightHand 소켓, 평소에는 LeftHand 소켓(검집 옆)으로 부모를 전환한다.
public class WeaponHandSwitcher : MonoBehaviour
{
    [Header("소속 캐릭터의 WeaponAttacher")]
    [Tooltip("비워두면 부모 계층에서 자동으로 찾는다.")]
    public WeaponAttacher attacher;

    [Header("슬롯 설정")]
    public WeaponSlot idleSlot = WeaponSlot.LeftHand;    // 평소: 왼손
    public WeaponSlot attackSlot = WeaponSlot.RightHand; // 공격 중: 오른손

    [Header("Idle (왼손) 오프셋")]
    public Vector3 idleLocalPosition = Vector3.zero;
    public Vector3 idleLocalEuler = Vector3.zero;

    [Header("Attack (오른손) 오프셋")]
    public Vector3 attackLocalPosition = Vector3.zero;
    public Vector3 attackLocalEuler = Vector3.zero;

    [Header("Animator State 자동 감지 (선택)")]
    [Tooltip("켜면 Animator의 현재 State가 attackStateTag와 같을 때 공격 중으로 간주.")]
    public bool useAnimatorStateDetect = false;
    public Animator animator;
    [Tooltip("공격 애니메이션 State에 달아둘 Tag 이름.")]
    public string[] attackStateTags = {"Attack", "Strong Attack"};
    [Tooltip("어떤 레이어를 검사할지. 보통 0(Base Layer).")]
    public int animatorLayer = 0;

    private bool isAttacking;
    private WeaponSlot currentSlot;
    private bool initialized;

    private void Awake()
    {
        if (attacher == null)
            attacher = GetComponentInParent<WeaponAttacher>();

        if (useAnimatorStateDetect && animator == null && attacher != null)
            animator = attacher.GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        // 시작 시점엔 idle 슬롯에 위치
        ApplySlot(idleSlot, force: true);
        initialized = true;
    }

    private void Update()
    {
        if (!useAnimatorStateDetect || animator == null) return;

        bool now = false;

        var current = animator.GetCurrentAnimatorStateInfo(animatorLayer);
        if (MatchesAnyAttackTag(current))
        {
            now = true;
        }
        else if (animator.IsInTransition(animatorLayer))
        {
            var next = animator.GetNextAnimatorStateInfo(animatorLayer);
            if (MatchesAnyAttackTag(next)) now = true;
        }

        if (now != isAttacking) SetAttacking(now);
    }

    public void SetAttacking(bool value)
    {
        if (initialized && isAttacking == value) return;
        isAttacking = value;
        ApplySlot(value ? attackSlot : idleSlot);
    }

    private bool MatchesAnyAttackTag(AnimatorStateInfo state)
    {
        if (attackStateTags == null) return false;
        for (int i = 0; i < attackStateTags.Length; i++)
        {
            var tag = attackStateTags[i];
            if (string.IsNullOrEmpty(tag)) continue;
            if (state.IsTag(tag)) return true;
        }
        return false;
    }

    private void ApplySlot(WeaponSlot slot, bool force = false)
    {
        if (!force && slot == currentSlot) return;
        if (attacher == null)
        {
            Debug.LogWarning("[SwordHandSwitcher] WeaponAttacher 참조 없음");
            return;
        }

        Transform socket = attacher.GetSocket(slot);
        if (socket == null)
        {
            Debug.LogWarning($"[SwordHandSwitcher] 슬롯 본을 찾을 수 없음: {slot}");
            return;
        }

        transform.SetParent(socket, worldPositionStays: false);

        bool isAttack = (slot == attackSlot);
        transform.localPosition = isAttack ? attackLocalPosition : idleLocalPosition;
        transform.localRotation = Quaternion.Euler(isAttack ? attackLocalEuler : idleLocalEuler);

        currentSlot = slot;
    }
}