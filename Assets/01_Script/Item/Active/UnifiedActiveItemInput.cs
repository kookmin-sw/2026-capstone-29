using UnityEngine;
using Mirror;

/// <summary>
/// 액티브 아이템 사용 + 아이템 픽업 입력 컴포넌트.
/// - 온라인: isLocalPlayer만 입력 처리.
/// - 오프라인: 본인이 무조건 입력 처리.
///
/// UnifiedItemManager 우선, 없으면 legacy ItemManager fallback.
/// StarterAssetsInputs 연동 이후 제거 예정 (원본 주석 그대로 유지).
///
/// 추가: 픽업 입력(E 기본) 시 UnifiedItemPickUp 의 가장 가까운 후보를 획득.
/// </summary>
public class UnifiedActiveItemInput : NetworkBehaviour
{
    [Header("입력 키 (임시)")]
    [SerializeField] private KeyCode useActiveKey = KeyCode.Q;
    [SerializeField] private KeyCode pickUpKey = KeyCode.E;

    private UnifiedItemManager unifiedManager;
    private ItemManager legacyManager;
    private UnifiedItemPickUp pickUp;

    private void Awake()
    {
        unifiedManager = GetComponent<UnifiedItemManager>();
        if (unifiedManager == null) legacyManager = GetComponent<ItemManager>();

        pickUp = GetComponent<UnifiedItemPickUp>();
    }

    private void Update()
    {
        // 입력 권한: 오프라인은 본인, 온라인은 로컬 플레이어만
        if (!AuthorityGuard.IsLocallyControlled(gameObject)) return;

        // 액티브 아이템 사용
        if (Input.GetKeyDown(useActiveKey))
        {
            if (unifiedManager != null)
            {
                unifiedManager.RequestUseActive();
            }
            else if (legacyManager != null)
            {
                legacyManager.RequestUseActive();
            }
        }

        // 아이템 픽업
        if (Input.GetKeyDown(pickUpKey))
        {
            if (pickUp != null)
            {
                pickUp.TryPickupNearest();
            }
        }
    }
}