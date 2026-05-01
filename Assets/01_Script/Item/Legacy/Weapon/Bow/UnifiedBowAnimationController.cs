using Mirror;
using UnityEngine;

/// <summary>
/// 활 애니메이션 컨트롤러 통합판. <see cref="BowAnimationController"/>의 대체판.
///
/// - 온라인: 서버에서 <see cref="SetPull"/> 호출 시 <see cref="ClientRpc"/>로 전파.
/// - 오프라인: Cmd/Rpc 없이 로컬 Animator만 조작.
///
/// <see cref="UnifiedWeaponBow"/>가 <see cref="SetPull"/>을 호출한다.
/// </summary>
public class UnifiedBowAnimationController : NetworkBehaviour
{
    [Header("참조")]
    [Tooltip("활 오브젝트의 Animator. 비워두면 자동으로 찾음.")]
    [SerializeField] private Animator bowAnimator;

    [Header("Charge 오프셋 트리거")]
    [Tooltip("부착 핸들러의 Charge 오프셋이 적용될 Hold 상태 이름. " +
             "이 Animator 상태에 있을 때만 UnifiedWeaponEquipHandler.SetCharging(true) 호출. " +
             "비워두면 Pull 시작과 동시에 적용(구 동작).")]
    [SerializeField] private string holdStateName = "Pull_Loop";

    [Tooltip("holdStateName 검사에 사용할 Animator 레이어 인덱스. 보통 0(Base Layer).")]
    [SerializeField] private int holdStateLayer = 0;

    private static readonly int PullHash = Animator.StringToHash("Pull");

    private bool isPulling;
    public bool IsPulling => isPulling;

    // 같은 오브젝트의 장착 핸들러 (Charge 상태 공유용)
    private UnifiedWeaponEquipHandler _equipHandler;

    private void Awake()
    {
        if (bowAnimator == null) bowAnimator = GetComponent<Animator>();
        if (bowAnimator == null) bowAnimator = GetComponentInChildren<Animator>();

        if (bowAnimator == null)
            Debug.LogWarning($"[UnifiedBowAnimationController] {gameObject.name} Animator를 찾지 못했습니다.");

        _equipHandler = GetComponent<UnifiedWeaponEquipHandler>();
    }

    /// <summary>
    /// 활 당김 상태 설정. 오프라인이면 로컬만, 온라인이면 모든 클라이언트에 전파.
    /// </summary>
    public void SetPull(bool pulling)
    {
        if (AuthorityGuard.IsOffline)
        {
            ApplyPull(pulling);
            return;
        }

        if (NetworkServer.active)
        {
            ApplyPull(pulling);
            RpcSetPull(pulling);
        }
        else
        {
            // 클라이언트에서 직접 호출한 경우 로컬만 적용 (이론상 사용되지 않음)
            ApplyPull(pulling);
        }
    }

    [ClientRpc]
    private void RpcSetPull(bool pulling)
    {
        ApplyPull(pulling);
    }

    private void ApplyPull(bool pulling)
    {
        isPulling = pulling;
        if (bowAnimator != null)
            bowAnimator.SetBool(PullHash, pulling);

        // SetCharging은 Update에서 Hold 상태 진입 여부와 함께 판단해 호출.
        // 단, release(false)는 즉시 반영해야 자연스럽게 Idle로 복귀.
        if (!pulling && _equipHandler != null)
            _equipHandler.SetCharging(false);
    }

    private void Update()
    {
        if (_equipHandler == null || bowAnimator == null) return;
        if (!isPulling) return; // release 직후엔 ApplyPull(false)에서 이미 처리됨

        bool inHoldState;
        if (string.IsNullOrEmpty(holdStateName))
        {
            // 빈 값이면 즉시 적용(Pull 시작과 동시) — 구 동작과 동일
            inHoldState = true;
        }
        else
        {
            // 전이 중이면 GetCurrentAnimatorStateInfo는 이전 상태를 가리킴 →
            // 다음 상태도 같이 검사해 Hold로 전이 시작 시점에 활성화되도록.
            var cur = bowAnimator.GetCurrentAnimatorStateInfo(holdStateLayer);
            bool curIsHold = cur.IsName(holdStateName);

            bool nextIsHold = false;
            if (bowAnimator.IsInTransition(holdStateLayer))
            {
                var nxt = bowAnimator.GetNextAnimatorStateInfo(holdStateLayer);
                nextIsHold = nxt.IsName(holdStateName);
            }

            inHoldState = curIsHold || nextIsHold;
        }

        _equipHandler.SetCharging(inHoldState);
    }
}
